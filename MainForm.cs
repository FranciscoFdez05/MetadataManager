using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MetadataManager.Models;
using MetadataManager.Resources;
using MetadataManager.Services;

namespace MetadataManager
{
    /// <summary>
    /// Ventana principal: coordina la lista de archivos, la rejilla de metadatos
    /// y las operaciones de lectura, edición, limpieza y exportación.
    /// </summary>
    public partial class MainForm : Form
    {
        private const string CoordinatesProperty = "Coordenadas";
        private const string HashProperty = "SHA-256";
        private const int PreviewMaxSize = 480;

        private static readonly Color EditableCellColor = Color.FromArgb(255, 251, 230);
        private static readonly Color CategoryBackColor = Color.FromArgb(238, 242, 248);
        private static readonly Color CategoryForeColor = Color.FromArgb(30, 58, 110);

        private readonly List<MetadataEntry> _entries = new();
        private readonly HashSet<string> _collapsedCategories = new(StringComparer.OrdinalIgnoreCase);
        private readonly AppSettings _settings;
        private readonly string[] _initialPaths;

        private CancellationTokenSource? _loadCancellation;
        private CancellationTokenSource? _cleanCancellation;
        private Image? _preview;
        private string? _previewText;

        /// <summary>Archivo cuya vista previa espera la autorización del usuario.</summary>
        private string? _blockedPreviewPath;

        /// <summary>Ejecutables que el usuario ya ha autorizado durante esta sesión.</summary>
        private readonly HashSet<string> _allowedPreviews = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Evita que los eventos de la rejilla reaccionen mientras la repintamos.</summary>
        private bool _suspendGridEvents;

        private bool _isBusy;
        private string? _editingOriginalValue;
        private int _sortColumn = -1;
        private bool _sortAscending = true;

        public MainForm() : this(new AppSettings(), Array.Empty<string>())
        {
        }

        /// <param name="settings">Preferencias cargadas del disco.</param>
        /// <param name="initialPaths">Rutas recibidas por línea de comandos.</param>
        public MainForm(AppSettings settings, string[] initialPaths)
        {
            InitializeComponent();

            _settings = settings ?? new AppSettings();
            _initialPaths = initialPaths ?? Array.Empty<string>();

            ApplyLocalization();
            ApplyGlyphs();
            LoadWindowIcon();

            Text = $"MetadataManager {ApplicationVersion}";
            listViewFiles.MultiSelect = true;
            pictureThumbnail.Visible = _settings.ShowThumbnail;

            UpdateExifToolStatus();
            UpdateCommandStates();
            SetStatus(Strings.StatusWelcome);
        }

        private static string ApplicationVersion =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        private FileEntry? SelectedFile =>
            listViewFiles.SelectedItems.Count > 0 ? listViewFiles.SelectedItems[0].Tag as FileEntry : null;

        private IReadOnlyList<ListViewItem> SelectedItems =>
            listViewFiles.SelectedItems.Cast<ListViewItem>().ToList();

        #region Arranque y cierre

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (_settings.WindowMaximized)
            {
                WindowState = FormWindowState.Maximized;
            }
            else if (_settings.WindowWidth >= 800 && _settings.WindowHeight >= 460)
            {
                // Se guarda el área de cliente: así la ventana no encoge un poco en cada arranque.
                ClientSize = new Size(_settings.WindowWidth, _settings.WindowHeight);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            RestoreSplitterDistance();

            if (_initialPaths.Length > 0) AddPaths(_initialPaths);
        }

        private void RestoreSplitterDistance()
        {
            try
            {
                int maximum = splitContainer.Width - splitContainer.Panel2MinSize - splitContainer.SplitterWidth;
                int minimum = splitContainer.Panel1MinSize;

                splitContainer.SplitterDistance = Math.Clamp(_settings.SplitterDistance, minimum, Math.Max(minimum, maximum));
            }
            catch (ArgumentException)
            {
                // La ventana es demasiado estrecha para ese divisor: se deja el valor por defecto.
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isBusy &&
                MessageBox.Show(this, Strings.MsgBusyOnExit, Strings.TitleError,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _loadCancellation?.Cancel();
            _cleanCancellation?.Cancel();
            SaveSettings();

            base.OnFormClosing(e);
        }

        private void SaveSettings()
        {
            _settings.WindowMaximized = WindowState == FormWindowState.Maximized;

            if (WindowState == FormWindowState.Normal)
            {
                _settings.WindowWidth = ClientSize.Width;
                _settings.WindowHeight = ClientSize.Height;
            }

            _settings.SplitterDistance = splitContainer.SplitterDistance;
            SettingsService.Save(_settings);
        }

        private void LoadWindowIcon()
        {
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException)
            {
                // Sin icono asociado se usa el predeterminado de Windows Forms.
            }
        }

        private void ApplyGlyphs()
        {
            buttonAddFiles.Image = Glyphs.AddFile;
            buttonAddFolder.Image = Glyphs.AddFolder;
            buttonRemove.Image = Glyphs.Remove;
            buttonClean.Image = Glyphs.Clean;
            buttonExport.Image = Glyphs.Save;
            buttonExifTool.Image = Glyphs.Connect;
            buttonOptions.Image = Glyphs.Options;
        }

        /// <summary>Vuelca los textos del idioma activo sobre los controles del diseñador.</summary>
        private void ApplyLocalization()
        {
            menuFile.Text = Strings.MenuFile;
            menuAddFiles.Text = Strings.MenuAddFiles;
            menuAddFolder.Text = Strings.MenuAddFolder;
            menuAddFolderFiles.Text = Strings.MenuAddFolderFiles;
            menuExport.Text = Strings.MenuExport;
            menuExportBatch.Text = Strings.MenuExportBatch;
            menuExit.Text = Strings.MenuExit;
            menuTools.Text = Strings.MenuTools;
            menuClean.Text = Strings.MenuClean;
            menuRemove.Text = Strings.MenuRemove;
            menuClearList.Text = Strings.MenuClearList;
            menuDetectExifTool.Text = Strings.MenuDetectExifTool;
            menuConnectExifTool.Text = Strings.MenuConnectExifTool;
            menuOptions.Text = Strings.MenuOptions;
            menuHelp.Text = Strings.MenuHelp;
            menuAbout.Text = Strings.MenuAbout;

            buttonAddFiles.Text = Strings.ToolAddFiles;
            buttonAddFiles.ToolTipText = Strings.TipAddFiles;
            buttonAddFolder.Text = Strings.ToolAddFolder;
            buttonRemove.Text = Strings.ToolRemove;
            buttonRemove.ToolTipText = Strings.TipRemove;
            buttonClean.Text = Strings.ToolClean;
            buttonClean.ToolTipText = Strings.TipClean;
            buttonExport.Text = Strings.ToolExport;
            buttonExport.ToolTipText = Strings.TipExport;
            buttonExifTool.Text = Strings.ToolExifTool;
            buttonExifTool.ToolTipText = Strings.TipExifTool;
            buttonOptions.Text = Strings.ToolOptions;
            labelFilter.Text = Strings.LabelFilter;
            textBoxFilter.ToolTipText = Strings.TipFilter;

            columnName.Text = Strings.ColumnName;
            columnType.Text = Strings.ColumnType;
            columnSize.Text = Strings.ColumnSize;
            columnProperty.HeaderText = Strings.ColumnProperty;
            columnValue.HeaderText = Strings.ColumnValue;

            listMenuOpen.Text = Strings.ContextOpen;
            listMenuOpenFolder.Text = Strings.ContextOpenFolder;
            listMenuClean.Text = Strings.MenuClean;
            listMenuRemove.Text = Strings.MenuRemove;
            gridMenuCopy.Text = Strings.ContextCopyValue;
            gridMenuEdit.Text = Strings.ContextEditValue;
            gridMenuMaps.Text = Strings.ContextMaps;
            gridMenuExpandAll.Text = Strings.ContextExpandAll;
            gridMenuCollapseAll.Text = Strings.ContextCollapseAll;
        }

        #endregion

        #region Lista de archivos

        private void OnFilesDragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void OnFilesDragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths) AddPaths(paths);
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
            AddPaths(dialog.FileNames);
        }

        private void OnAddFolderRequested(object? sender, EventArgs e)
        {
            string? folder = AskForFolder();
            if (folder is not null) AddPaths(new[] { folder });
        }

        /// <summary>Añade el contenido de una carpeta, incluidas sus subcarpetas.</summary>
        private void OnAddFolderFilesRequested(object? sender, EventArgs e)
        {
            string? folder = AskForFolder();
            if (folder is null) return;

            var files = EnumerateFilesSafe(folder).ToList();

            if (files.Count == 0)
            {
                MessageBox.Show(this, Strings.MsgFolderEmpty, Strings.TitleAddFolder,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirmation = MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, Strings.MsgFolderScan, files.Count),
                Strings.TitleAddFolder, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirmation == DialogResult.Yes) AddPaths(files);
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

        /// <summary>Recorre el árbol de carpetas saltándose las que no se pueden leer.</summary>
        private static IEnumerable<string> EnumerateFilesSafe(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                string current = pending.Pop();
                string[] files;

                try
                {
                    files = Directory.GetFiles(current);

                    foreach (string directory in Directory.GetDirectories(current)) pending.Push(directory);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    continue;
                }

                foreach (string file in files) yield return file;
            }
        }

        /// <summary>Añade rutas evitando duplicados; los errores individuales no detienen el resto.</summary>
        private void AddPaths(IEnumerable<string> paths)
        {
            var existing = new HashSet<string>(
                listViewFiles.Items.Cast<ListViewItem>()
                    .Select(item => (item.Tag as FileEntry)?.Path ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            int skipped = 0;
            int added = 0;

            listViewFiles.BeginUpdate();

            try
            {
                foreach (string raw in paths)
                {
                    string path;

                    try
                    {
                        path = Path.GetFullPath(raw);
                    }
                    catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                    {
                        skipped++;
                        continue;
                    }

                    if (!File.Exists(path) && !Directory.Exists(path))
                    {
                        skipped++;
                        continue;
                    }

                    if (!existing.Add(path)) continue;

                    listViewFiles.Items.Add(CreateListItem(new FileEntry(path)));
                    added++;
                }
            }
            finally
            {
                listViewFiles.EndUpdate();
            }

            if (added > 0 && listViewFiles.SelectedItems.Count == 0)
            {
                listViewFiles.Items[listViewFiles.Items.Count - added].Selected = true;
                listViewFiles.Focus();
            }

            UpdateCommandStates();

            string message = string.Format(CultureInfo.CurrentCulture, Strings.StatusAdded, added, listViewFiles.Items.Count);
            if (skipped > 0) message += string.Format(CultureInfo.CurrentCulture, Strings.StatusSkipped, skipped);
            SetStatus(message);
        }

        private static ListViewItem CreateListItem(FileEntry entry)
        {
            var item = new ListViewItem(entry.DisplayName) { Tag = entry };
            item.SubItems.Add(FileTypes.Describe(entry.Path));
            item.SubItems.Add(FileTypes.FormatCompactSize(GetLength(entry)));
            item.ToolTipText = entry.Path;
            return item;
        }

        private static long GetLength(FileEntry entry)
        {
            try
            {
                return entry.IsFile ? new FileInfo(entry.Path).Length : -1;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return -1;
            }
        }

        private void RefreshListItem(ListViewItem item)
        {
            if (item.Tag is not FileEntry entry) return;

            item.Text = entry.DisplayName;
            item.SubItems[1].Text = FileTypes.Describe(entry.Path);
            item.SubItems[2].Text = FileTypes.FormatCompactSize(GetLength(entry));
            item.ToolTipText = entry.Path;
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

        /// <summary>Ordena la lista por la columna pulsada; el tamaño se compara numéricamente.</summary>
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
                    long leftSize = left.Tag is FileEntry a ? GetLength(a) : -1;
                    long rightSize = right.Tag is FileEntry b ? GetLength(b) : -1;
                    return leftSize.CompareTo(rightSize) * _direction;
                }

                string leftText = left.SubItems.Count > _column ? left.SubItems[_column].Text : string.Empty;
                string rightText = right.SubItems.Count > _column ? right.SubItems[_column].Text : string.Empty;

                return string.Compare(leftText, rightText, StringComparison.CurrentCultureIgnoreCase) * _direction;
            }
        }

        #endregion

        #region Lectura de metadatos

        private async void OnFileSelectionChanged(object? sender, EventArgs e)
        {
            UpdateCommandStates();
            await LoadSelectedMetadataAsync().ConfigureAwait(true);
        }

        private async Task LoadSelectedMetadataAsync()
        {
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = null;

            var entry = SelectedFile;

            if (entry is null)
            {
                ClearMetadata();
                return;
            }

            var cancellation = new CancellationTokenSource();
            _loadCancellation = cancellation;
            CancellationToken token = cancellation.Token;

            string path = entry.Path;
            SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusReading, entry.DisplayName));

            try
            {
                bool allowTagEditing = _settings.UseExifTool && ExifTool.IsAvailable;

                var result = await Task
                    .Run(() => MetadataService.Read(path, token, allowTagEditing), token)
                    .ConfigureAwait(true);

                if (token.IsCancellationRequested) return;

                _entries.Clear();
                _entries.AddRange(result);

                if (File.Exists(path)) _entries.Add(new MetadataEntry("Integridad", HashProperty, "..."));

                RenderEntries();
                SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusRead, _entries.Count, entry.DisplayName));

                await UpdatePreviewAsync(path, token).ConfigureAwait(true);
                if (File.Exists(path)) await AppendHashAsync(path, token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                // Se seleccionó otro archivo antes de terminar: no hay nada que informar.
            }
            catch (Exception ex)
            {
                ShowError(Strings.ErrorRead, ex);
                SetStatus(Strings.StatusReadError);
            }
        }

        private async Task AppendHashAsync(string path, CancellationToken token)
        {
            string value;

            try
            {
                value = await Task.Run(() => MetadataService.ComputeSha256(path, token), token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                value = ex.Message;
            }

            if (token.IsCancellationRequested) return;

            int index = _entries.FindIndex(item => item.Name == HashProperty);
            if (index < 0) return;

            _entries[index] = _entries[index].WithValue(value);
            UpdateRowValue(_entries[index].DisplayName, value);
        }

        private void ClearMetadata()
        {
            _entries.Clear();
            RenderEntries();
            ClearPreview();
            SetStatus(Strings.StatusNoSelection);
        }

        /// <summary>Vuelca <see cref="_entries"/> en la rejilla, agrupado por categoría y filtrado.</summary>
        private void RenderEntries()
        {
            _suspendGridEvents = true;

            try
            {
                dataGridViewMetadata.CancelEdit();
                dataGridViewMetadata.CurrentCell = null;
                dataGridViewMetadata.Rows.Clear();

                string filter = textBoxFilter.Text.Trim();
                int shown = 0;

                foreach (var group in _entries.GroupBy(entry => entry.Category, StringComparer.Ordinal))
                {
                    var visible = filter.Length == 0
                        ? group.ToList()
                        : group.Where(entry => Matches(entry, filter)).ToList();

                    if (visible.Count == 0) continue;

                    bool collapsed = _collapsedCategories.Contains(group.Key);
                    AddCategoryRow(group.Key, visible.Count, collapsed);

                    shown += visible.Count;
                    if (collapsed) continue;

                    foreach (var entry in visible) AddEntryRow(entry);
                }

                if (filter.Length > 0)
                {
                    SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusFiltered, shown, _entries.Count, filter));
                }

                dataGridViewMetadata.ClearSelection();
            }
            finally
            {
                _suspendGridEvents = false;
            }
        }

        private void AddCategoryRow(string category, int count, bool collapsed)
        {
            string marker = collapsed ? "▸" : "▾";
            int index = dataGridViewMetadata.Rows.Add($"{marker}  {category}  ({count})", string.Empty);

            var row = dataGridViewMetadata.Rows[index];
            row.Tag = new CategoryRow(category);
            row.ReadOnly = true;
            row.DefaultCellStyle.BackColor = CategoryBackColor;
            row.DefaultCellStyle.ForeColor = CategoryForeColor;
            row.DefaultCellStyle.Font = new Font(dataGridViewMetadata.Font, FontStyle.Bold);
            row.DefaultCellStyle.SelectionBackColor = CategoryBackColor;
            row.DefaultCellStyle.SelectionForeColor = CategoryForeColor;
        }

        private void AddEntryRow(MetadataEntry entry)
        {
            int index = dataGridViewMetadata.Rows.Add(entry.Name, entry.Value);

            var row = dataGridViewMetadata.Rows[index];
            row.Tag = entry;
            row.ReadOnly = !entry.IsEditable;
            row.Cells[0].ToolTipText = entry.DisplayName;

            if (!entry.IsEditable) return;

            row.Cells[1].Style.BackColor = EditableCellColor;
            row.Cells[1].ToolTipText = Strings.TipEditable;
        }

        private static bool Matches(MetadataEntry entry, string filter) =>
            entry.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
            entry.Value.Contains(filter, StringComparison.CurrentCultureIgnoreCase);

        private void UpdateRowValue(string displayName, string value)
        {
            foreach (DataGridViewRow row in dataGridViewMetadata.Rows)
            {
                if (row.Tag is not MetadataEntry entry || entry.DisplayName != displayName) continue;

                _suspendGridEvents = true;
                row.Cells[1].Value = value;
                row.Tag = entry.WithValue(value);
                _suspendGridEvents = false;
                return;
            }
        }

        private void OnFilterChanged(object? sender, EventArgs e) => RenderEntries();

        private void OnExpandAllRequested(object? sender, EventArgs e)
        {
            _collapsedCategories.Clear();
            RenderEntries();
        }

        private void OnCollapseAllRequested(object? sender, EventArgs e)
        {
            foreach (string category in _entries.Select(entry => entry.Category).Distinct(StringComparer.Ordinal))
            {
                _collapsedCategories.Add(category);
            }

            RenderEntries();
        }

        /// <summary>Marca una fila de encabezado de categoría.</summary>
        private sealed record CategoryRow(string Category);

        #endregion

        #region Vista previa

        /// <summary>Tipografía de paso fijo para las vistas previas de texto.</summary>
        private static readonly Font PreviewTextFont = new(FontFamily.GenericMonospace, 8.25f);

        /// <summary>
        /// Genera la vista previa del archivo seleccionado: imagen, texto, miniatura del shell
        /// o icono del tipo. Los ejecutables quedan a la espera de que el usuario los autorice.
        /// </summary>
        private async Task UpdatePreviewAsync(string path, CancellationToken token)
        {
            if (!_settings.ShowThumbnail)
            {
                ClearPreview();
                return;
            }

            bool allowExecutable = _allowedPreviews.Contains(path);

            try
            {
                var result = await Task
                    .Run(() => PreviewService.Create(path, PreviewMaxSize, allowExecutable, token), token)
                    .ConfigureAwait(true);

                if (token.IsCancellationRequested)
                {
                    result.Dispose();
                    return;
                }

                SetPreview(result, path);
            }
            catch (OperationCanceledException)
            {
                // Se seleccionó otro archivo antes de terminar.
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExternalException)
            {
                ClearPreview();
            }
        }

        /// <summary>Pide permiso para previsualizar un ejecutable y, si se concede, la genera.</summary>
        private async void OnPreviewClick(object? sender, EventArgs e)
        {
            string? path = _blockedPreviewPath;
            if (path is null) return;

            var answer = MessageBox.Show(
                this,
                string.Format(CultureInfo.CurrentCulture, Strings.PreviewConfirmMessage, Path.GetFileName(path)),
                Strings.PreviewConfirmTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes) return;

            _allowedPreviews.Add(path);

            // Entre la pregunta y la respuesta el usuario ha podido cambiar de archivo.
            if (!string.Equals(SelectedFile?.Path, path, StringComparison.OrdinalIgnoreCase)) return;

            await UpdatePreviewAsync(path, _loadCancellation?.Token ?? CancellationToken.None).ConfigureAwait(true);
        }

        private void ClearPreview() => SetPreview(PreviewResult.None, null);

        private void SetPreview(PreviewResult result, string? path)
        {
            pictureThumbnail.Image = result.Image;
            _preview?.Dispose();
            _preview = result.Image;
            _previewText = result.Text;
            _blockedPreviewPath = result.Kind == PreviewKind.Blocked ? path : null;

            // Los iconos del shell son pequeños: ampliarlos los deja borrosos, así que solo se centran.
            pictureThumbnail.SizeMode = result.Image is not null &&
                result.Image.Width <= pictureThumbnail.ClientSize.Width &&
                result.Image.Height <= pictureThumbnail.ClientSize.Height
                    ? PictureBoxSizeMode.CenterImage
                    : PictureBoxSizeMode.Zoom;

            pictureThumbnail.Cursor = _blockedPreviewPath is null ? Cursors.Default : Cursors.Hand;
            pictureThumbnail.Invalidate();
        }

        /// <summary>Dibuja el texto de la vista previa cuando no hay imagen que mostrar.</summary>
        private void OnPreviewPaint(object? sender, PaintEventArgs e)
        {
            if (pictureThumbnail.Image is not null) return;

            var bounds = Rectangle.Inflate(pictureThumbnail.ClientRectangle, -6, -6);

            if (!string.IsNullOrEmpty(_previewText))
            {
                TextRenderer.DrawText(e.Graphics, _previewText, PreviewTextFont, bounds, SystemColors.WindowText,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
                return;
            }

            string message = _blockedPreviewPath is null ? Strings.PreviewUnavailable : Strings.PreviewBlocked;

            TextRenderer.DrawText(e.Graphics, message, Font, bounds, SystemColors.GrayText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }

        #endregion

        #region Edición de metadatos

        private void OnMetadataCellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 1) return;

            var row = dataGridViewMetadata.Rows[e.RowIndex];

            if (row.Tag is not MetadataEntry entry || !entry.IsEditable)
            {
                e.Cancel = true;
                return;
            }

            _editingOriginalValue = Convert.ToString(row.Cells[1].Value) ?? string.Empty;
        }

        private void OnMetadataCellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
        {
            if (_suspendGridEvents || e.RowIndex < 0 || e.ColumnIndex != 1) return;

            var row = dataGridViewMetadata.Rows[e.RowIndex];
            if (row.Tag is not MetadataEntry entry || !entry.IsEditable) return;

            string value = Convert.ToString(e.FormattedValue) ?? string.Empty;
            string? error = MetadataEditor.Validate(entry.EditKind, value, entry.EditTarget);

            row.ErrorText = error ?? string.Empty;
            if (error is not null) e.Cancel = true;
        }

        private void OnMetadataCellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (_suspendGridEvents || e.RowIndex < 0 || e.ColumnIndex != 1) return;

            var row = dataGridViewMetadata.Rows[e.RowIndex];
            if (row.Tag is not MetadataEntry entry || !entry.IsEditable) return;

            string newValue = Convert.ToString(row.Cells[1].Value) ?? string.Empty;
            string originalValue = _editingOriginalValue ?? entry.Value;
            _editingOriginalValue = null;

            if (string.Equals(newValue, originalValue, StringComparison.Ordinal)) return;

            var item = listViewFiles.SelectedItems.Count > 0 ? listViewFiles.SelectedItems[0] : null;

            if (item?.Tag is not FileEntry file)
            {
                RevertCell(row, originalValue);
                return;
            }

            if (entry.EditKind == MetadataEditKind.ExifTag)
            {
                _ = ApplyTagEditAsync(row, entry, file, newValue, originalValue);
                return;
            }

            try
            {
                file.Path = MetadataEditor.Apply(file.Path, entry.EditKind, newValue);
                RefreshListItem(item);
                SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusUpdated, entry.DisplayName));

                // La recarga se aplaza: modificar la rejilla dentro de CellEndEdit provoca una llamada reentrante.
                BeginInvoke(new Action(() => _ = LoadSelectedMetadataAsync()));
            }
            catch (Exception ex)
            {
                RevertCell(row, originalValue);
                ShowError(Strings.ErrorApply, ex);
            }
        }

        /// <summary>Escribe una etiqueta incrustada con ExifTool y recarga la vista.</summary>
        private async Task ApplyTagEditAsync(DataGridViewRow row, MetadataEntry entry, FileEntry file, string newValue, string originalValue)
        {
            SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusApplying, entry.DisplayName));

            try
            {
                await MetadataEditor.ApplyTagAsync(file.Path, entry.EditTarget ?? entry.Name, newValue).ConfigureAwait(true);
                SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusUpdated, entry.DisplayName));
                await LoadSelectedMetadataAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                RevertCell(row, originalValue);
                ShowError(Strings.ErrorApply, ex);
            }
        }

        private void RevertCell(DataGridViewRow row, string originalValue)
        {
            _suspendGridEvents = true;
            row.Cells[1].Value = originalValue;
            row.ErrorText = string.Empty;
            _suspendGridEvents = false;
        }

        private void OnMetadataDataError(object? sender, DataGridViewDataErrorEventArgs e)
        {
            // La rejilla no debe lanzar excepciones por un valor mal formateado.
            e.ThrowException = false;
            Debug.WriteLine("DataGridView error: " + e.Exception?.Message);
        }

        private void OnMetadataCellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (_suspendGridEvents || e.RowIndex < 0) return;
            if (dataGridViewMetadata.Rows[e.RowIndex].Tag is not CategoryRow header) return;

            if (!_collapsedCategories.Add(header.Category)) _collapsedCategories.Remove(header.Category);

            RenderEntries();
        }

        private void OnMetadataCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = dataGridViewMetadata.Rows[e.RowIndex];
            if (row.Tag is not MetadataEntry entry) return;

            // Editar tiene prioridad; el mapa sigue disponible en el menú contextual.
            if (entry.IsEditable && e.ColumnIndex == 1)
            {
                dataGridViewMetadata.BeginEdit(true);
                return;
            }

            if (IsCoordinates(entry)) OpenInMaps(entry.Value);
        }

        /// <summary>El menú contextual debe actuar sobre la fila pulsada, no sobre la que tuviera el foco.</summary>
        private void OnMetadataCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 0) return;

            try
            {
                dataGridViewMetadata.CurrentCell = dataGridViewMetadata.Rows[e.RowIndex].Cells[e.ColumnIndex];
            }
            catch (InvalidOperationException)
            {
                // La rejilla está en edición y no permite mover la celda actual.
            }
        }

        private void OnGridContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var entry = CurrentEntry;

            gridMenuCopy.Enabled = entry is not null;
            gridMenuEdit.Enabled = entry?.IsEditable == true;
            gridMenuMaps.Visible = entry is not null && IsCoordinates(entry) && entry.Value.Length > 0;

            bool hasRows = dataGridViewMetadata.Rows.Count > 0;
            gridMenuExpandAll.Enabled = hasRows;
            gridMenuCollapseAll.Enabled = hasRows;
        }

        private MetadataEntry? CurrentEntry => dataGridViewMetadata.CurrentRow?.Tag as MetadataEntry;

        /// <summary>Una fila de coordenadas puede venir del resumen o de la edición rápida.</summary>
        private static bool IsCoordinates(MetadataEntry entry) =>
            string.Equals(entry.Name, CoordinatesProperty, StringComparison.Ordinal);

        private void OnCopyValueRequested(object? sender, EventArgs e)
        {
            var entry = CurrentEntry;
            if (entry is null) return;

            try
            {
                if (entry.Value.Length > 0) Clipboard.SetText(entry.Value);
                SetStatus(Strings.StatusCopied);
            }
            catch (Exception ex)
            {
                ShowError(Strings.ErrorCopy, ex);
            }
        }

        private void OnEditValueRequested(object? sender, EventArgs e)
        {
            if (CurrentEntry?.IsEditable != true) return;

            dataGridViewMetadata.CurrentCell = dataGridViewMetadata.CurrentRow?.Cells[1];
            dataGridViewMetadata.BeginEdit(true);
        }

        private void OnOpenMapsRequested(object? sender, EventArgs e)
        {
            var entry = CurrentEntry;
            if (entry is not null) OpenInMaps(entry.Value);
        }

        private void OpenInMaps(string coordinates)
        {
            string[] parts = coordinates.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length < 2 ||
                !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double latitude) ||
                !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double longitude))
            {
                SetStatus(Strings.StatusBadCoordinates);
                return;
            }

            StartProcess(string.Format(
                CultureInfo.InvariantCulture,
                "https://www.google.com/maps/search/?api=1&query={0},{1}",
                latitude,
                longitude));
        }

        #endregion

        #region Limpieza de metadatos

        private async void OnCleanRequested(object? sender, EventArgs e)
        {
            if (_isBusy) return;

            var targets = SelectedItems.Where(item => (item.Tag as FileEntry)?.IsFile == true).ToList();

            if (targets.Count == 0)
            {
                MessageBox.Show(this, Strings.MsgSelectFileToClean, Strings.TitleClean,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var options = _settings.ToCleanOptions();

            string engine = options.UseExifTool && ExifTool.IsAvailable
                ? Strings.MsgCleanExifTool
                : Strings.MsgCleanNoExifTool;

            string mode = options.OutputMode switch
            {
                CleanOutputMode.Backup => Strings.MsgModeBackup,
                CleanOutputMode.Copy => Strings.MsgModeCopy,
                _ => Strings.MsgModeOverwrite
            };

            var confirmation = MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, Strings.MsgCleanConfirm, targets.Count, engine, mode),
                Strings.TitleClean, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

            if (confirmation != DialogResult.Yes) return;

            _cleanCancellation?.Dispose();
            _cleanCancellation = new CancellationTokenSource();
            CancellationToken token = _cleanCancellation.Token;

            var results = new List<CleanResult>(targets.Count);
            var created = new List<string>();

            SetBusy(true, targets.Count);

            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    if (token.IsCancellationRequested) break;

                    var item = targets[i];
                    if (item.Tag is not FileEntry file) continue;

                    SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusCleaning, file.DisplayName, i + 1, targets.Count));
                    statusProgress.Value = i;

                    var result = await MetadataCleaner.CleanAsync(file.Path, options, token).ConfigureAwait(true);
                    results.Add(result);
                    RefreshListItem(item);

                    // En modo copia el archivo limpio es nuevo: se añade a la lista al terminar.
                    if (result.Success && !string.Equals(result.OutputPath, file.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        created.Add(result.OutputPath);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus(Strings.StatusCleanCancelled);
            }
            catch (Exception ex)
            {
                ShowError(Strings.ErrorClean, ex);
            }
            finally
            {
                SetBusy(false, 0);
            }

            if (created.Count > 0) AddPaths(created);

            ReportCleanResults(results);
            await LoadSelectedMetadataAsync().ConfigureAwait(true);
        }

        private void ReportCleanResults(IReadOnlyList<CleanResult> results)
        {
            if (results.Count == 0) return;

            int complete = results.Count(r => r.Scope == CleanScope.Complete);
            int partial = results.Count(r => r.Scope is CleanScope.Partial or CleanScope.TimestampsOnly);
            int failed = results.Count(r => r.Scope == CleanScope.Failed);

            string summary = string.Format(CultureInfo.CurrentCulture, Strings.CleanSummary, complete, partial, failed);
            SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusCleanDone, summary));

            var detail = new System.Text.StringBuilder(summary);
            detail.AppendLine().AppendLine();

            foreach (var result in results.Take(12))
            {
                detail.AppendLine($"• {Path.GetFileName(result.Path)}: {result.Message}");
            }

            if (results.Count > 12)
            {
                detail.AppendLine(string.Format(CultureInfo.CurrentCulture, Strings.CleanMore, results.Count - 12));
            }

            MessageBox.Show(this, detail.ToString(), Strings.TitleCleanResult,
                MessageBoxButtons.OK,
                failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private void OnDetectExifToolRequested(object? sender, EventArgs e)
        {
            ExifTool.ResetCache();
            UpdateExifToolStatus();

            string? path = ExifTool.Locate();

            if (path is not null)
            {
                MessageBox.Show(this,
                    string.Format(CultureInfo.CurrentCulture, Strings.MsgExifToolFound, path),
                    Strings.TitleExifTool, MessageBoxButtons.OK, MessageBoxIcon.Information);

                _ = LoadSelectedMetadataAsync();
                return;
            }

            // Sin herramienta: se ofrece la descarga, y luego podrá conectarse a mano.
            var answer = MessageBox.Show(this, Strings.MsgExifToolDownload, Strings.TitleExifTool,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (answer == DialogResult.Yes) StartProcess("https://exiftool.org/");
        }

        /// <summary>
        /// Permite señalar a mano el ejecutable de ExifTool cuando no está en el PATH.
        /// La ruta se valida ejecutándolo y se recuerda entre sesiones.
        /// </summary>
        private void OnConnectExifToolRequested(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = Strings.DialogSelectExifTool,
                Filter = Strings.FilterExifTool,
                CheckFileExists = true,
                InitialDirectory = GetExifToolFolder()
            };

            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            if (!ExifTool.TryConnect(dialog.FileName, out string detail))
            {
                MessageBox.Show(this,
                    string.Format(CultureInfo.CurrentCulture, Strings.MsgExifToolInvalid, detail),
                    Strings.TitleExifTool, MessageBoxButtons.OK, MessageBoxIcon.Error);

                UpdateExifToolStatus();
                return;
            }

            _settings.ExifToolPath = dialog.FileName;
            SettingsService.Save(_settings);
            UpdateExifToolStatus();

            MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, Strings.MsgExifToolConnected, detail, dialog.FileName),
                Strings.TitleExifTool, MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Con ExifTool disponible aparecen etiquetas editables: se recarga la vista.
            _ = LoadSelectedMetadataAsync();
        }

        private string GetExifToolFolder()
        {
            string? current = _settings.ExifToolPath ?? ExifTool.Locate();

            try
            {
                if (current is not null && File.Exists(current)) return Path.GetDirectoryName(current) ?? string.Empty;
            }
            catch (ArgumentException)
            {
                // Ruta inservible: se abre el diálogo en la carpeta por defecto.
            }

            return _settings.LastFolder ?? string.Empty;
        }

        #endregion

        #region Exportación

        private void OnExportRequested(object? sender, EventArgs e)
        {
            var file = SelectedFile;

            if (file is null || _entries.Count == 0)
            {
                MessageBox.Show(this, Strings.MsgNothingToExport, Strings.DialogSaveMetadata,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string suggested = Path.GetFileNameWithoutExtension(file.DisplayName) + Strings.ExportSuffix + ".csv";
            string? destination = AskForExportPath(suggested);
            if (destination is null) return;

            try
            {
                MetadataExporter.Export(destination, file.Path, _entries, MetadataExporter.FormatFromExtension(destination));
                SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusExported, destination));
            }
            catch (Exception ex)
            {
                ShowError(Strings.ErrorExport, ex);
            }
        }

        private async void OnExportBatchRequested(object? sender, EventArgs e)
        {
            if (_isBusy) return;

            var files = listViewFiles.Items.Cast<ListViewItem>()
                .Select(item => item.Tag as FileEntry)
                .Where(entry => entry is not null && entry.Exists)
                .Select(entry => entry!.Path)
                .ToList();

            if (files.Count == 0)
            {
                MessageBox.Show(this, Strings.MsgEmptyList, Strings.DialogSaveMetadata,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string? destination = AskForExportPath(Strings.ExportBatchName + ".csv");
            if (destination is null) return;

            var report = new List<(string Path, IReadOnlyList<MetadataEntry> Entries)>(files.Count);

            _cleanCancellation?.Dispose();
            _cleanCancellation = new CancellationTokenSource();
            CancellationToken token = _cleanCancellation.Token;

            SetBusy(true, files.Count);

            try
            {
                for (int i = 0; i < files.Count; i++)
                {
                    if (token.IsCancellationRequested) break;

                    string path = files[i];
                    SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusExporting, i + 1, files.Count));
                    statusProgress.Value = i;

                    var entries = await Task.Run(() => MetadataService.Read(path, token), token).ConfigureAwait(true);
                    report.Add((path, entries));
                }

                MetadataExporter.ExportBatch(destination, report, MetadataExporter.FormatFromExtension(destination));
                SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusExported, destination));
            }
            catch (OperationCanceledException)
            {
                SetStatus(Strings.StatusCleanCancelled);
            }
            catch (Exception ex)
            {
                ShowError(Strings.ErrorExport, ex);
            }
            finally
            {
                SetBusy(false, 0);
            }
        }

        private string? AskForExportPath(string suggestedName)
        {
            using var dialog = new SaveFileDialog
            {
                Title = Strings.DialogSaveMetadata,
                FileName = suggestedName,
                Filter = Strings.FilterExport,
                AddExtension = true,
                OverwritePrompt = true,
                InitialDirectory = _settings.LastFolder ?? string.Empty
            };

            if (dialog.ShowDialog(this) != DialogResult.OK) return null;

            _settings.LastFolder = Path.GetDirectoryName(dialog.FileName);
            return dialog.FileName;
        }

        #endregion

        #region Preferencias y estado de la interfaz

        private void OnOptionsRequested(object? sender, EventArgs e)
        {
            using var dialog = new OptionsForm(_settings);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var updated = dialog.Settings;

            _settings.Language = updated.Language;
            _settings.NormalizationDate = updated.NormalizationDate;
            _settings.OutputMode = updated.OutputMode;
            _settings.PreserveOrientation = updated.PreserveOrientation;
            _settings.ResetFileDates = updated.ResetFileDates;
            _settings.UseExifTool = updated.UseExifTool;
            _settings.ShowThumbnail = updated.ShowThumbnail;

            SettingsService.Save(_settings);

            pictureThumbnail.Visible = _settings.ShowThumbnail;
            if (!_settings.ShowThumbnail) ClearPreview();

            _ = LoadSelectedMetadataAsync();
        }

        private void UpdateCommandStates()
        {
            bool hasSelection = listViewFiles.SelectedItems.Count > 0;
            bool hasFileSelection = SelectedItems.Any(item => (item.Tag as FileEntry)?.IsFile == true);
            bool hasItems = listViewFiles.Items.Count > 0;

            buttonRemove.Enabled = hasSelection;
            menuRemove.Enabled = hasSelection;
            listMenuRemove.Enabled = hasSelection;

            buttonClean.Enabled = hasFileSelection && !_isBusy;
            menuClean.Enabled = buttonClean.Enabled;
            listMenuClean.Enabled = buttonClean.Enabled;

            buttonExport.Enabled = hasSelection;
            menuExport.Enabled = hasSelection;
            menuExportBatch.Enabled = hasItems && !_isBusy;
            menuClearList.Enabled = hasItems;
        }

        private void UpdateExifToolStatus()
        {
            string? path = ExifTool.Locate();
            bool available = path is not null;

            statusExifTool.Text = available && ExifTool.Version is string version
                ? string.Format(CultureInfo.CurrentCulture, Strings.StatusExifToolVersion, version)
                : available ? Strings.StatusExifToolFound : Strings.StatusExifToolMissing;

            statusExifTool.ForeColor = available ? Color.FromArgb(0, 110, 40) : Color.FromArgb(160, 90, 0);

            statusExifTool.ToolTipText = available
                ? $"{Strings.TipExifToolFound}\n{path}"
                : $"{Strings.TipExifToolMissing}\n{Strings.TipExifToolConnect}";
        }

        private void SetBusy(bool busy, int total)
        {
            _isBusy = busy;
            statusProgress.Visible = busy;
            statusProgress.Value = 0;
            statusProgress.Maximum = Math.Max(total, 1);

            toolStrip.Enabled = !busy;
            menuStrip.Enabled = !busy;
            listViewFiles.Enabled = !busy;
            dataGridViewMetadata.Enabled = !busy;

            UseWaitCursor = busy;
            UpdateCommandStates();
        }

        private void SetStatus(string message) => statusLabel.Text = message;

        private void ShowError(string title, Exception exception)
        {
            MessageBox.Show(this, $"{title}:\n\n{exception.Message}", Strings.TitleError,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void OnAboutRequested(object? sender, EventArgs e)
        {
            MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, Strings.AboutText, ApplicationVersion),
                Strings.TitleAbout, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnExitRequested(object? sender, EventArgs e) => Close();

        #endregion
    }
}
