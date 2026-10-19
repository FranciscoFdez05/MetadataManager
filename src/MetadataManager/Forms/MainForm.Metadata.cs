using System.Globalization;
using MetadataManager.Models;
using MetadataManager.Resources;
using MetadataManager.Services;

namespace MetadataManager
{
    public partial class MainForm
    {
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

                if (File.Exists(path)) _entries.Add(new MetadataEntry(Strings.CategoryIntegrity, HashProperty, "...", key: MetadataKeys.Sha256));

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

            int index = _entries.FindIndex(item => item.Key == MetadataKeys.Sha256);
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

                // Las filas se construyen fuera de la rejilla y se añaden de una vez: añadirlas
                // una a una dispara un repintado y un recálculo de diseño por cada fila.
                var rows = new List<DataGridViewRow>(_entries.Count + 16);

                foreach (var group in _entries.GroupBy(entry => entry.Category, StringComparer.Ordinal))
                {
                    var visible = filter.Length == 0
                        ? group.ToList()
                        : group.Where(entry => Matches(entry, filter)).ToList();

                    if (visible.Count == 0) continue;

                    bool collapsed = _collapsedCategories.Contains(group.Key);
                    rows.Add(CreateCategoryRow(group.Key, visible.Count, collapsed));

                    shown += visible.Count;
                    if (collapsed) continue;

                    foreach (var entry in visible) rows.Add(CreateEntryRow(entry));
                }

                dataGridViewMetadata.Rows.AddRange(rows.ToArray());

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

        private DataGridViewRow CreateCategoryRow(string category, int count, bool collapsed)
        {
            string marker = collapsed ? "▸" : "▾";

            var row = new DataGridViewRow();
            row.CreateCells(dataGridViewMetadata, $"{marker}  {category}  ({count})", string.Empty);
            row.Tag = new CategoryRow(category);
            row.ReadOnly = true;
            row.DefaultCellStyle = _categoryStyle;
            return row;
        }

        private DataGridViewRow CreateEntryRow(MetadataEntry entry)
        {
            var row = new DataGridViewRow();
            row.CreateCells(dataGridViewMetadata, entry.Name, entry.Value);
            row.Tag = entry;
            row.ReadOnly = !entry.IsEditable;
            row.Cells[0].ToolTipText = entry.DisplayName;

            if (entry.IsEditable)
            {
                row.Cells[1].Style = _editableStyle;
                row.Cells[1].ToolTipText = Strings.TipEditable;
            }

            return row;
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

        /// <summary>
        /// El filtro se aplica cuando se deja de escribir: repintar la rejilla en cada
        /// pulsación se nota con archivos que tienen cientos de propiedades.
        /// </summary>
        private void OnFilterChanged(object? sender, EventArgs e)
        {
            _filterTimer.Stop();
            _filterTimer.Start();
        }

        private void OnFilterTimerTick(object? sender, EventArgs e)
        {
            _filterTimer.Stop();
            RenderEntries();
        }

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
    }
}
