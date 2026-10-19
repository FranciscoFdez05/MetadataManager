using System.Globalization;
using System.Runtime.InteropServices;
using MetadataManager.Resources;
using MetadataManager.Services;

namespace MetadataManager
{
    public partial class MainForm
    {
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
    }
}
