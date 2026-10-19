using System.Collections;
using System.Diagnostics;
using System.Globalization;
using MetadataManager.Models;
using MetadataManager.Resources;
using MetadataManager.Services;
using MetadataManager.UI;

namespace MetadataManager
{
    public partial class MainForm
    {
        private void OnFilesDragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void OnFilesDragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths) _ = AddPathsAsync(paths);
        }

        private void OnAddFilesRequested(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = Strings.DialogSelectFiles,
                Filter = Strings.FilterAllFiles,
                InitialDirectory = _settings.LastFolder ?? string.Empty
            };

            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            _settings.LastFolder = Path.GetDirectoryName(dialog.FileName);
            _ = AddPathsAsync(dialog.FileNames);
        }

        private void OnAddFolderRequested(object? sender, EventArgs e)
        {
            string? folder = AskForFolder();
            if (folder is not null) _ = AddPathsAsync(new[] { folder });
        }

        /// <summary>Añade el contenido de una carpeta, incluidas sus subcarpetas.</summary>
        private async void OnAddFolderFilesRequested(object? sender, EventArgs e)
        {
            string? folder = AskForFolder();
            if (folder is null) return;

            // Recorrer un árbol grande lleva su tiempo: se hace fuera del hilo de la interfaz.
            List<string> files;
            string previousStatus = statusLabel.Text ?? string.Empty;
            UseWaitCursor = true;
            SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusReading, folder));

            try
            {
                files = await Task.Run(() => FileScanner.EnumerateFiles(folder)).ConfigureAwait(true);
            }
            finally
            {
                UseWaitCursor = false;
            }

            if (files.Count == 0)
            {
                SetStatus(previousStatus);
                MessageBox.Show(this, Strings.MsgFolderEmpty, Strings.TitleAddFolder,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirmation = MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, Strings.MsgFolderScan, files.Count),
                Strings.TitleAddFolder, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirmation == DialogResult.Yes) await AddPathsAsync(files).ConfigureAwait(true);
            else SetStatus(previousStatus);
        }

        private string? AskForFolder()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = Strings.DialogSelectFolder,
                UseDescriptionForTitle = true,
                SelectedPath = _settings.LastFolder ?? string.Empty
            };

            if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(dialog.SelectedPath)) return null;

            _settings.LastFolder = dialog.SelectedPath;
            return dialog.SelectedPath;
        }

        /// <summary>
        /// Añade rutas evitando duplicados; los errores individuales no detienen el resto.
        /// Las consultas al disco se hacen en segundo plano y la lista se rellena de una vez.
        /// </summary>
        private async Task AddPathsAsync(IEnumerable<string> paths)
        {
            var snapshot = paths.ToList();
            ScanResult scan;

            try
            {
                scan = await Task.Run(() => FileScanner.Resolve(snapshot)).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ShowError(Strings.ErrorRead, ex);
                return;
            }

            // Los duplicados con la lista se filtran aquí, en el hilo de la interfaz: entre tanto
            // ha podido completarse otra carga.
            var existing = new HashSet<string>(
                listViewFiles.Items.Cast<ListViewItem>().Select(item => (item.Tag as FileEntry)?.Path ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            var items = scan.Entries
                .Where(entry => existing.Add(entry.Path))
                .Select(CreateListItem)
                .ToArray();

            if (items.Length > 0)
            {
                listViewFiles.BeginUpdate();

                try
                {
                    listViewFiles.Items.AddRange(items);
                }
                finally
                {
                    listViewFiles.EndUpdate();
                }

                if (listViewFiles.SelectedItems.Count == 0)
                {
                    items[0].Selected = true;
                    listViewFiles.Focus();
                }
            }

            UpdateCommandStates();

            string message = string.Format(CultureInfo.CurrentCulture, Strings.StatusAdded, items.Length, listViewFiles.Items.Count);
            if (scan.Skipped > 0) message += string.Format(CultureInfo.CurrentCulture, Strings.StatusSkipped, scan.Skipped);
            SetStatus(message);
        }

        private static ListViewItem CreateListItem(FileEntry entry)
        {
            var item = new ListViewItem(entry.DisplayName) { Tag = entry };
            item.SubItems.Add(FileTypes.Describe(entry.Path));
            item.SubItems.Add(FileTypes.FormatCompactSize(entry.Length));
            item.ToolTipText = entry.Path;
            return item;
        }

        /// <summary>Vuelve a leer el elemento del disco tras modificarlo (renombrado, limpieza...).</summary>
        private static void RefreshListItem(ListViewItem item)
        {
            if (item.Tag is not FileEntry entry) return;

            entry.Refresh();
            item.Text = entry.DisplayName;
            item.SubItems[1].Text = FileTypes.Describe(entry.Path);
            item.SubItems[2].Text = FileTypes.FormatCompactSize(entry.Length);
            item.ToolTipText = entry.Path;
        }

        /// <summary>
        /// Reparte todo el ancho visible de la lista entre las tres columnas. El total nunca
        /// supera el ancho del control: sin barra horizontal, ninguna columna puede quedar fuera.
        /// </summary>
        private void FitFileColumns()
        {
            if (_fittingFileColumns) return;

            int available = listViewFiles.ClientSize.Width;
            if (available <= 0) return;

            ApplyFileColumnWidths(FileColumnLayout.Distribute(available));
        }

        private void OnFilesResize(object? sender, EventArgs e) => FitFileColumns();

        /// <summary>
        /// Las cabeceras de la lista no se arrastran: los anchos los decide <see cref="FitFileColumns"/>
        /// con el espacio disponible, así que un ajuste manual solo podría dejar columnas escondidas.
        /// </summary>
        private void OnFilesColumnWidthChanging(object? sender, ColumnWidthChangingEventArgs e)
        {
            if (_fittingFileColumns) return;

            e.NewWidth = listViewFiles.Columns[e.ColumnIndex].Width;
            e.Cancel = true;
        }

        /// <param name="widths">Anchos calculados por <see cref="FileColumnLayout"/>: nombre, tipo y tamaño.</param>
        private void ApplyFileColumnWidths(int[] widths)
        {
            if (_fittingFileColumns) return;

            _fittingFileColumns = true;
            listViewFiles.BeginUpdate();

            try
            {
                columnName.Width = widths[0];
                columnType.Width = widths[1];
                columnSize.Width = widths[2];
            }
            finally
            {
                listViewFiles.EndUpdate();
                ScrollFileListToStart();
                _fittingFileColumns = false;
            }
        }

        /// <summary>
        /// Devuelve la lista al extremo izquierdo: si alguna vez quedó desplazada,
        /// las primeras columnas seguirían escondidas aunque ya vuelvan a caber.
        /// </summary>
        private void ScrollFileListToStart()
        {
            if (!listViewFiles.IsHandleCreated) return;

            // LVM_SCROLL recorta solo el desplazamiento sobrante, así que basta con pasarse.
            SendMessage(listViewFiles.Handle, LvmScroll, new IntPtr(-listViewFiles.Width * 4), IntPtr.Zero);
        }

        private void OnFilesColumnClick(object? sender, ColumnClickEventArgs e)
        {
            if (e.Column == _sortColumn)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _sortColumn = e.Column;
                _sortAscending = true;
            }

            listViewFiles.ListViewItemSorter = new FileItemComparer(_sortColumn, _sortAscending);
            listViewFiles.Sort();
        }

        private void OnFilesKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete) return;

            OnRemoveRequested(sender, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnRemoveRequested(object? sender, EventArgs e)
        {
            var selected = SelectedItems;

            if (selected.Count == 0)
            {
                SetStatus(Strings.StatusNothingSelected);
                return;
            }

            listViewFiles.BeginUpdate();
            foreach (var item in selected) listViewFiles.Items.Remove(item);
            listViewFiles.EndUpdate();

            if (listViewFiles.SelectedItems.Count == 0) ClearMetadata();

            UpdateCommandStates();
            SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusRemoved, selected.Count));
        }

        private void OnClearListRequested(object? sender, EventArgs e)
        {
            if (listViewFiles.Items.Count == 0) return;

            listViewFiles.Items.Clear();
            ClearMetadata();
            UpdateCommandStates();
            SetStatus(Strings.StatusListCleared);
        }

        private void OnFileDoubleClick(object? sender, EventArgs e) => OnOpenFileRequested(sender, e);

        private void OnOpenFileRequested(object? sender, EventArgs e)
        {
            var entry = SelectedFile;

            if (entry is null || !entry.Exists)
            {
                SetStatus(Strings.StatusUnavailable);
                return;
            }

            StartProcess(entry.Path);
        }

        private void OnOpenContainingFolderRequested(object? sender, EventArgs e)
        {
            var entry = SelectedFile;
            if (entry is null || !entry.Exists) return;

            if (entry.IsDirectory)
            {
                StartProcess(entry.Path);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{entry.Path}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowError(Strings.ErrorOpenFolder, ex);
            }
        }

        private void StartProcess(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowError(Strings.ErrorOpen, ex);
            }
        }

        /// <summary>
        /// Ordena la lista por la columna pulsada. El tamaño se compara numéricamente con el
        /// valor guardado en <see cref="FileEntry"/>, sin consultar el disco en cada comparación.
        /// </summary>
        private sealed class FileItemComparer : IComparer
        {
            private readonly int _column;
            private readonly int _direction;

            public FileItemComparer(int column, bool ascending)
            {
                _column = column;
                _direction = ascending ? 1 : -1;
            }

            public int Compare(object? x, object? y)
            {
                if (x is not ListViewItem left || y is not ListViewItem right) return 0;

                if (_column == 2)
                {
                    long leftSize = (left.Tag as FileEntry)?.Length ?? -1;
                    long rightSize = (right.Tag as FileEntry)?.Length ?? -1;
                    return leftSize.CompareTo(rightSize) * _direction;
                }

                string leftText = left.SubItems.Count > _column ? left.SubItems[_column].Text : string.Empty;
                string rightText = right.SubItems.Count > _column ? right.SubItems[_column].Text : string.Empty;

                return string.Compare(leftText, rightText, StringComparison.CurrentCultureIgnoreCase) * _direction;
            }
        }
    }
}
