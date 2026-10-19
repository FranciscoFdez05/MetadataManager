using System.Diagnostics;
using System.Globalization;
using MetadataManager.Models;
using MetadataManager.Resources;
using MetadataManager.Services;

namespace MetadataManager
{
    public partial class MainForm
    {
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
            string.Equals(entry.Key, MetadataKeys.Coordinates, StringComparison.Ordinal);

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
    }
}
