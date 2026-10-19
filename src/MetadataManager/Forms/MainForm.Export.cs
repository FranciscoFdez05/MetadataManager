using System.Globalization;
using MetadataManager.Models;
using MetadataManager.Resources;
using MetadataManager.Services;

namespace MetadataManager
{
    public partial class MainForm
    {
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

            _operationCancellation?.Dispose();
            _operationCancellation = new CancellationTokenSource();
            CancellationToken token = _operationCancellation.Token;

            SetBusy(true, files.Count);
            SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusExporting, 0, files.Count));

            // Progress<T> devuelve cada aviso al hilo de la interfaz.
            var progress = new Progress<int>(done =>
            {
                // Los avisos llegan desde varios hilos y pueden adelantarse unos a otros.
                if (!_isBusy || done <= statusProgress.Value) return;

                statusProgress.Value = Math.Min(done, statusProgress.Maximum);
                SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusExporting, done, files.Count));
            });

            try
            {
                // La lectura se reparte entre varios hilos y la escritura del informe tampoco bloquea la ventana.
                var format = MetadataExporter.FormatFromExtension(destination);

                await Task.Run(() =>
                {
                    var entries = MetadataService.ReadMany(files, progress, token);
                    var report = files.Select((path, index) => (path, entries[index])).ToList();
                    MetadataExporter.ExportBatch(destination, report, format);
                }, token).ConfigureAwait(true);

                SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusExported, destination));
            }
            catch (OperationCanceledException)
            {
                SetStatus(Strings.StatusExportCancelled);
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
    }
}
