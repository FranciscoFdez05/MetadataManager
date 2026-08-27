using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace MetadataManager.Services
{
    /// <summary>Alcance conseguido al limpiar un fichero.</summary>
    public enum CleanScope
    {
        /// <summary>Se eliminaron todos los metadatos conocidos.</summary>
        Complete,

        /// <summary>Se eliminó lo que la aplicación sabe reescribir; puede quedar información residual.</summary>
        Partial,

        /// <summary>Solo se pudieron ajustar las fechas del sistema de ficheros.</summary>
        TimestampsOnly,

        /// <summary>La operación falló.</summary>
        Failed
    }

    /// <summary>Resultado de limpiar un fichero.</summary>
    /// <param name="Path">Archivo de partida.</param>
    /// <param name="OutputPath">Archivo limpio resultante (distinto del original en modo copia).</param>
    /// <param name="BackupPath">Copia de seguridad creada, si procede.</param>
    public sealed record CleanResult(
        string Path,
        string OutputPath,
        string? BackupPath,
        CleanScope Scope,
        string Message)
    {
        public bool Success => Scope != CleanScope.Failed;
    }

    /// <summary>
    /// Borrado de metadatos. Usa ExifTool cuando está disponible y, si no,
    /// aplica estrategias propias según el formato.
    /// </summary>
    public static class MetadataCleaner
    {
        /// <summary>Fecha neutra por defecto para los archivos limpiados.</summary>
        public static readonly DateTime StandardDate = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Local);

        public static async Task<CleanResult> CleanAsync(string path, CleanOptions options, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(path))
                return new CleanResult(path, path, null, CleanScope.Failed, "El archivo no existe.");

            string target;
            string? backup;

            try
            {
                (target, backup) = PrepareTarget(path, options);
            }
            catch (Exception ex)
            {
                return new CleanResult(path, path, null, CleanScope.Failed, "No se pudo preparar la copia: " + ex.Message);
            }

            CleanResult result = await CleanInPlaceAsync(target, options, cancellationToken).ConfigureAwait(false);
            result = result with { Path = path, OutputPath = target, BackupPath = backup };

            if (result.Scope == CleanScope.Failed && options.OutputMode == CleanOutputMode.Copy && target != path)
            {
                TryDelete(target);
                result = result with { OutputPath = path };
            }

            return result;
        }

        /// <summary>Compatibilidad: limpia con las opciones por defecto.</summary>
        public static Task<CleanResult> CleanAsync(string path, bool preferExifTool, CancellationToken cancellationToken = default) =>
            CleanAsync(path, new CleanOptions { UseExifTool = preferExifTool }, cancellationToken);

        /// <summary>
        /// Aplica el modo de salida elegido y devuelve el archivo sobre el que hay que trabajar
        /// junto a la copia de seguridad creada, si la hay.
        /// </summary>
        private static (string Target, string? Backup) PrepareTarget(string path, CleanOptions options)
        {
            switch (options.OutputMode)
            {
                case CleanOutputMode.Backup:
                    string backup = GetAvailablePath(path + ".bak");
                    File.Copy(path, backup);
                    return (path, backup);

                case CleanOutputMode.Copy:
                    string directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? Path.GetTempPath();
                    string name = Path.GetFileNameWithoutExtension(path) + "_limpio" + Path.GetExtension(path);
                    string copy = GetAvailablePath(Path.Combine(directory, name));
                    File.Copy(path, copy);
                    return (copy, null);

                default:
                    return (path, null);
            }
        }

        /// <summary>Añade un sufijo numérico hasta encontrar un nombre libre.</summary>
        private static string GetAvailablePath(string desired)
        {
            if (!File.Exists(desired)) return desired;

            string directory = Path.GetDirectoryName(desired) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(desired);
            string extension = Path.GetExtension(desired);

            for (int index = 2; index < 1000; index++)
            {
                string candidate = Path.Combine(directory, $"{name} ({index}){extension}");
                if (!File.Exists(candidate)) return candidate;
            }

            return Path.Combine(directory, $"{name} ({Guid.NewGuid():N}){extension}");
        }

        private static async Task<CleanResult> CleanInPlaceAsync(string path, CleanOptions options, CancellationToken cancellationToken)
        {
            // Reescribir el archivo cambia sus fechas: se guardan para poder devolverlas
            // cuando el usuario no quiere normalizarlas.
            var originalTimes = ReadFileTimes(path);
            CleanResult result;

            if (options.UseExifTool && ExifTool.Locate() is string exifTool)
            {
                int orientation = options.PreserveOrientation ? ReadOrientation(path) : 1;

                var stripped = await ExifTool
                    .StripAllAsync(exifTool, path, options.StandardDate, orientation, cancellationToken)
                    .ConfigureAwait(false);

                result = stripped.Success
                    ? Result(path, CleanScope.Complete, "Metadatos eliminados con ExifTool.")
                    : await FallbackAsync(path, options, stripped.Message, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                result = await CleanWithoutExifToolAsync(path, options, cancellationToken).ConfigureAwait(false);
            }

            ApplyFileTimes(path, options, originalTimes);
            return result;
        }

        /// <summary>Cuando ExifTool falla se reintenta con los medios propios de la aplicación.</summary>
        private static async Task<CleanResult> FallbackAsync(string path, CleanOptions options, string reason, CancellationToken cancellationToken)
        {
            var fallback = await CleanWithoutExifToolAsync(path, options, cancellationToken).ConfigureAwait(false);
            return fallback with { Message = $"ExifTool falló ({reason}). {fallback.Message}" };
        }

        private static (DateTime Creation, DateTime LastWrite, DateTime LastAccess) ReadFileTimes(string path)
        {
            try
            {
                return (File.GetCreationTime(path), File.GetLastWriteTime(path), File.GetLastAccessTime(path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return (DateTime.Now, DateTime.Now, DateTime.Now);
            }
        }

        private static void ApplyFileTimes(string path, CleanOptions options, (DateTime Creation, DateTime LastWrite, DateTime LastAccess) original)
        {
            if (!File.Exists(path)) return;

            if (options.ResetFileDates) MetadataEditor.SetFileTimes(path, options.StandardDate);
            else MetadataEditor.SetFileTimes(path, original.Creation, original.LastWrite, original.LastAccess);
        }

        private static Task<CleanResult> CleanWithoutExifToolAsync(string path, CleanOptions options, CancellationToken cancellationToken) =>
            Task.Run(() => CleanWithoutExifTool(path, options, cancellationToken), cancellationToken);

        private static CleanResult CleanWithoutExifTool(string path, CleanOptions options, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string extension = Path.GetExtension(path).ToLowerInvariant();

                CleanResult result;

                if (FileTypes.IsImage(path))
                {
                    result = CleanImage(path, extension, options);
                }
                else if (FileTypes.IsOpenXml(path))
                {
                    OpenXmlCleaner.Clean(path, options.StandardDate);
                    result = Result(path, CleanScope.Complete, "Propiedades del documento eliminadas.");
                }
                else if (extension == ".pdf")
                {
                    result = CleanPdf(path, options);
                }
                else
                {
                    result = Result(path, CleanScope.TimestampsOnly,
                        $"Sin soporte interno para {extension}: solo se ajustaron las fechas. Instala ExifTool para eliminar sus metadatos.");
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Result(path, CleanScope.Failed, ex.Message);
            }
        }

        private static CleanResult CleanPdf(string path, CleanOptions options)
        {
            try
            {
                PdfCleaner.Clean(path, options.StandardDate);
                return Result(path, CleanScope.Complete, "Propiedades del PDF y bloque XMP eliminados.");
            }
            catch (Exception ex)
            {
                return Result(path, CleanScope.TimestampsOnly,
                    "El PDF no se pudo reescribir (" + ex.Message + "): solo se ajustaron las fechas.");
            }
        }

        private static CleanResult CleanImage(string path, string extension, CleanOptions options)
        {
            byte[] original = File.ReadAllBytes(path);
            int orientation = options.PreserveOrientation ? ReadOrientation(path) : 1;
            byte[]? stripped = LosslessImageStripper.Strip(original, orientation);

            if (stripped is not null)
            {
                SafeFileWriter.ReplaceContents(path, stripped);

                string message = orientation > 1
                    ? "Metadatos eliminados sin recomprimir la imagen (se conservó la orientación)."
                    : "Metadatos eliminados sin recomprimir la imagen.";

                return Result(path, CleanScope.Complete, message);
            }

            if (!TryReencode(path, extension, original, orientation))
            {
                return Result(path, CleanScope.TimestampsOnly,
                    "El formato de imagen no se pudo reescribir: solo se ajustaron las fechas.");
            }

            return Result(path, CleanScope.Partial,
                "Imagen regenerada sin metadatos (se ha recomprimido, puede perder calidad o animación).");
        }

        /// <summary>Lee la orientación EXIF (1-8); devuelve 1 si no hay o no se puede leer.</summary>
        internal static int ReadOrientation(string path)
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(path);
                var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();

                if (ifd0 is not null && ifd0.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientation) &&
                    orientation is >= 1 and <= 8)
                {
                    return orientation;
                }
            }
            catch (Exception)
            {
                // Sin EXIF legible se asume orientación normal.
            }

            return 1;
        }

        /// <summary>
        /// Redibuja la imagen en un mapa de bits nuevo, lo que descarta cualquier metadato.
        /// Se usa solo con formatos que no admiten borrado directo (BMP, GIF, TIFF...).
        /// </summary>
        private static bool TryReencode(string path, string extension, byte[] original, int orientation)
        {
            var format = GetImageFormat(extension);
            if (format is null) return false;

            try
            {
                using var input = new MemoryStream(original, writable: false);
                using var image = Image.FromStream(input);

                // Los formatos indexados no admiten un Graphics: se normalizan a 32 bits.
                var pixelFormat = IsIndexed(image.PixelFormat) ? PixelFormat.Format32bppArgb : image.PixelFormat;
                if (pixelFormat == PixelFormat.Format16bppGrayScale) pixelFormat = PixelFormat.Format32bppArgb;

                using var clean = new Bitmap(image.Width, image.Height, pixelFormat);
                clean.SetResolution(image.HorizontalResolution, image.VerticalResolution);

                using (var graphics = Graphics.FromImage(clean))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height));
                }

                // Al recomprimir se pierde el EXIF: la rotación se aplica directamente a los píxeles.
                var rotation = GetRotation(orientation);
                if (rotation != RotateFlipType.RotateNoneFlipNone) clean.RotateFlip(rotation);

                using var output = new MemoryStream();
                clean.Save(output, format);
                SafeFileWriter.ReplaceContents(path, output.ToArray());
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException or IOException)
            {
                return false;
            }
        }

        /// <summary>Traduce el valor EXIF de orientación a la transformación equivalente.</summary>
        internal static RotateFlipType GetRotation(int orientation) => orientation switch
        {
            2 => RotateFlipType.RotateNoneFlipX,
            3 => RotateFlipType.Rotate180FlipNone,
            4 => RotateFlipType.Rotate180FlipX,
            5 => RotateFlipType.Rotate90FlipX,
            6 => RotateFlipType.Rotate90FlipNone,
            7 => RotateFlipType.Rotate270FlipX,
            8 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone
        };

        private static CleanResult Result(string path, CleanScope scope, string message) =>
            new(path, path, null, scope, message);

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Se deja el archivo: informar del fallo principal es más útil.
            }
        }

        private static bool IsIndexed(PixelFormat format) =>
            (format & PixelFormat.Indexed) != 0;

        private static ImageFormat? GetImageFormat(string extension) => extension switch
        {
            ".jpg" or ".jpeg" or ".jpe" => ImageFormat.Jpeg,
            ".png" => ImageFormat.Png,
            ".bmp" => ImageFormat.Bmp,
            ".gif" => ImageFormat.Gif,
            ".tif" or ".tiff" => ImageFormat.Tiff,
            _ => null
        };
    }
}
