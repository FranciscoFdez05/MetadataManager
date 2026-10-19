using System.Globalization;
using MetadataManager.Models;
using MetadataManager.Resources;
using MetadataManager.Services;

namespace MetadataManager
{
    public partial class MainForm
    {
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

            _operationCancellation?.Dispose();
            _operationCancellation = new CancellationTokenSource();
            CancellationToken token = _operationCancellation.Token;

            var results = new CleanResult?[targets.Count];
            int completed = 0;

            SetBusy(true, targets.Count);
            SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusCleaning,
                (targets[0].Tag as FileEntry)?.DisplayName, 1, targets.Count));

            // Varios archivos a la vez: cada uno espera sobre todo al disco o a su proceso de ExifTool.
            using var throttle = new SemaphoreSlim(CleanParallelism);

            async Task CleanOneAsync(ListViewItem item, int index)
            {
                if (item.Tag is not FileEntry file) return;

                await throttle.WaitAsync(token).ConfigureAwait(true);

                try
                {
                    // Task.Run: la preparación de la copia y la lectura de la orientación también tocan el disco.
                    string path = file.Path;
                    results[index] = await Task.Run(() => MetadataCleaner.CleanAsync(path, options, token), token).ConfigureAwait(true);
                }
                finally
                {
                    throttle.Release();
                }

                // De vuelta en el hilo de la interfaz: el contador no necesita sincronización.
                RefreshListItem(item);
                completed++;
                statusProgress.Value = Math.Min(completed, statusProgress.Maximum);
                SetStatus(string.Format(CultureInfo.CurrentCulture, Strings.StatusCleaning, file.DisplayName, completed, targets.Count));
            }

            try
            {
                await Task.WhenAll(targets.Select(CleanOneAsync)).ConfigureAwait(true);
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

            var finished = results.OfType<CleanResult>().ToList();

            // En modo copia el archivo limpio es nuevo: se añade a la lista al terminar.
            var created = finished
                .Where(result => result.Success && !string.Equals(result.OutputPath, result.Path, StringComparison.OrdinalIgnoreCase))
                .Select(result => result.OutputPath)
                .ToList();

            if (created.Count > 0) await AddPathsAsync(created).ConfigureAwait(true);

            ReportCleanResults(finished);
            await LoadSelectedMetadataAsync().ConfigureAwait(true);

            // La recarga escribe su propio estado: se recuerda al final que la limpieza no se completó.
            if (token.IsCancellationRequested) SetStatus(Strings.StatusCleanCancelled);
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
    }
}
