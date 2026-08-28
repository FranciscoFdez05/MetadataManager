using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;

namespace MetadataManager.Services
{
    /// <summary>Qué se ha podido generar para la vista previa de un archivo.</summary>
    public enum PreviewKind
    {
        /// <summary>No hay nada que mostrar.</summary>
        None,

        /// <summary>Miniatura del contenido o icono del tipo de archivo.</summary>
        Image,

        /// <summary>Primeras líneas de un archivo de texto.</summary>
        Text,

        /// <summary>Ejecutable o script: hace falta que el usuario lo autorice.</summary>
        Blocked
    }

    /// <summary>Resultado de <see cref="PreviewService.Create"/>.</summary>
    public sealed class PreviewResult : IDisposable
    {
        public static readonly PreviewResult None = new(PreviewKind.None, null, null);

        public static readonly PreviewResult Blocked = new(PreviewKind.Blocked, null, null);

        private PreviewResult(PreviewKind kind, Image? image, string? text)
        {
            Kind = kind;
            Image = image;
            Text = text;
        }

        public PreviewKind Kind { get; }

        public Image? Image { get; }

        public string? Text { get; }

        public static PreviewResult FromImage(Image image) => new(PreviewKind.Image, image, null);

        public static PreviewResult FromText(string text) => new(PreviewKind.Text, null, text);

        public void Dispose() => Image?.Dispose();
    }

    /// <summary>
    /// Vista previa de cualquier archivo: imagen propia cuando GDI+ puede abrirla,
    /// miniatura del shell de Windows para el resto de formatos, texto para los archivos
    /// de texto plano e icono del tipo como último recurso.
    /// Los ejecutables y scripts quedan bloqueados hasta que el usuario los autoriza,
    /// porque generar su miniatura implica que Windows abra el archivo con un controlador externo.
    /// </summary>
    public static class PreviewService
    {
        private const int MaxTextBytes = 8192;
        private const int MaxTextLines = 60;
        private const int MaxTextLineLength = 200;

        /// <summary>Indica si el archivo necesita confirmación explícita antes de previsualizarlo.</summary>
        public static bool RequiresConfirmation(string path) => FileTypes.IsExecutable(path);

        /// <param name="path">Archivo o carpeta a previsualizar.</param>
        /// <param name="maxSize">Lado máximo de la miniatura en píxeles.</param>
        /// <param name="allowExecutable">El usuario ya ha autorizado este archivo.</param>
        public static PreviewResult Create(string path, int maxSize, bool allowExecutable, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            bool isDirectory = Directory.Exists(path);
            if (!isDirectory && !File.Exists(path)) return PreviewResult.None;

            if (isDirectory)
            {
                var icon = ShellThumbnail.TryGetIcon(path, maxSize);
                return icon is null ? PreviewResult.None : PreviewResult.FromImage(icon);
            }

            if (!allowExecutable && RequiresConfirmation(path)) return PreviewResult.Blocked;

            if (FileTypes.IsImage(path))
            {
                Image? image = TryDecodeImage(path, maxSize);
                if (image is not null) return PreviewResult.FromImage(image);
            }

            token.ThrowIfCancellationRequested();

            // Solo se intenta el texto cuando el contenido no corresponde a un formato binario conocido.
            if (FileTypes.DetectByContent(path) is null)
            {
                string? text = TryReadText(path);
                if (text is not null) return PreviewResult.FromText(text);
            }

            token.ThrowIfCancellationRequested();

            Bitmap? thumbnail = ShellThumbnail.TryGetThumbnail(path, maxSize) ?? ShellThumbnail.TryGetIcon(path, maxSize);

            return thumbnail is null ? PreviewResult.None : PreviewResult.FromImage(thumbnail);
        }

        /// <summary>
        /// Genera una miniatura ya rotada según la orientación EXIF, para no mostrarla tumbada.
        /// Devuelve null si GDI+ no reconoce el formato (HEIC, WebP antiguos, archivos dañados).
        /// </summary>
        internal static Image? TryDecodeImage(string path, int maxSize)
        {
            try
            {
                Bitmap thumbnail;

                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var source = Image.FromStream(stream))
                {
                    double scale = Math.Min(1.0, (double)maxSize / Math.Max(source.Width, source.Height));
                    int width = Math.Max(1, (int)(source.Width * scale));
                    int height = Math.Max(1, (int)(source.Height * scale));

                    thumbnail = new Bitmap(width, height);

                    using var graphics = Graphics.FromImage(thumbnail);
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(source, 0, 0, width, height);
                }

                var rotation = MetadataCleaner.GetRotation(MetadataCleaner.ReadOrientation(path));
                if (rotation != RotateFlipType.RotateNoneFlipNone) thumbnail.RotateFlip(rotation);

                return thumbnail;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException)
            {
                return null;
            }
        }

        /// <summary>
        /// Primeras líneas del archivo si su contenido parece texto plano.
        /// Devuelve null cuando encuentra bytes que descartan el texto.
        /// </summary>
        internal static string? TryReadText(string path)
        {
            byte[] buffer;
            int read;

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                buffer = new byte[MaxTextBytes];
                read = stream.Read(buffer, 0, buffer.Length);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return null;
            }

            if (read == 0) return null;

            Encoding? encoding = DetectEncoding(buffer, read, out int offset);
            if (encoding is null) return null;

            string summary = Summarize(encoding.GetString(buffer, offset, read - offset));

            return summary.Length == 0 ? null : summary;
        }

        /// <summary>
        /// Deduce la codificación por la marca de orden de bytes o descartando bytes de control.
        /// Null significa que el contenido no es texto.
        /// </summary>
        private static Encoding? DetectEncoding(byte[] buffer, int length, out int offset)
        {
            offset = 0;

            if (length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            {
                offset = 3;
                return Encoding.UTF8;
            }

            if (length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
            {
                offset = 2;
                return Encoding.Unicode;
            }

            if (length >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
            {
                offset = 2;
                return Encoding.BigEndianUnicode;
            }

            int control = 0;

            for (int index = 0; index < length; index++)
            {
                byte value = buffer[index];

                if (value == 0) return null;
                if (value < 0x20 && value is not ((byte)'\t' or (byte)'\n' or (byte)'\r' or 0x0C)) control++;
            }

            // Un poco de ruido es tolerable, pero un binario acumula muchos bytes de control.
            return control * 100 / length > 2 ? null : new UTF8Encoding(false, false);
        }

        /// <summary>Recorta el texto a lo que cabe razonablemente en el panel de vista previa.</summary>
        private static string Summarize(string text)
        {
            var result = new StringBuilder();
            int lines = 0;

            foreach (string line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                if (lines == MaxTextLines)
                {
                    result.Append('…');
                    break;
                }

                string trimmed = line.Replace("\t", "    ");
                if (trimmed.Length > MaxTextLineLength) trimmed = trimmed[..MaxTextLineLength] + "…";

                if (lines > 0) result.Append('\n');
                result.Append(trimmed);
                lines++;
            }

            return result.ToString().TrimEnd();
        }
    }
}
