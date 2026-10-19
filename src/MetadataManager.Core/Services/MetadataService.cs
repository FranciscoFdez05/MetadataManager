using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataManager.Models;
using MetadataManager.Resources;
using Directory = System.IO.Directory;

namespace MetadataManager.Services
{
    /// <summary>
    /// Lectura de metadatos. No depende de la interfaz: devuelve datos planos que la UI pinta.
    /// </summary>
    public static class MetadataService
    {
        private static string CategoryFile => CoreStrings.CategoryFile;
        private static string CategorySummary => CoreStrings.CategorySummary;

        /// <summary>Tamaño de bloque para el cálculo de la huella.</summary>
        private const int HashBufferSize = 1024 * 1024;

        /// <summary>
        /// Lee todos los metadatos disponibles de un fichero o directorio.
        /// Nunca lanza: los errores parciales se devuelven como entradas de la categoría "Error".
        /// </summary>
        /// <param name="allowTagEditing">
        /// Marca como editables las etiquetas incrustadas conocidas. Solo debe activarse
        /// cuando ExifTool esté disponible, porque es quien las escribe.
        /// </param>
        public static IReadOnlyList<MetadataEntry> Read(string path, CancellationToken cancellationToken = default, bool allowTagEditing = false)
        {
            if (Directory.Exists(path)) return ReadDirectory(path);
            if (File.Exists(path)) return ReadFile(path, cancellationToken, allowTagEditing);

            return new[] { new MetadataEntry(CoreStrings.CategoryStatus, CoreStrings.PropertyAvailability, CoreStrings.ValuePathUnavailable) };
        }

        /// <summary>
        /// Lee varios elementos en paralelo y devuelve los resultados en el mismo orden recibido.
        /// </summary>
        /// <param name="progress">Recibe el número de elementos terminados tras cada lectura.</param>
        public static IReadOnlyList<MetadataEntry>[] ReadMany(
            IReadOnlyList<string> paths,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(paths);

            var results = new IReadOnlyList<MetadataEntry>[paths.Count];
            int completed = 0;

            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            Parallel.For(0, paths.Count, options, index =>
            {
                results[index] = Read(paths[index], cancellationToken);
                progress?.Report(Interlocked.Increment(ref completed));
            });

            return results;
        }

        private static IReadOnlyList<MetadataEntry> ReadDirectory(string path)
        {
            var entries = new List<MetadataEntry>();

            try
            {
                var info = new DirectoryInfo(path);
                entries.Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyName, info.Name, MetadataEditKind.FileName));
                entries.Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyPath, info.FullName, MetadataEditKind.FullPath));
                entries.Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyType, CoreStrings.TypeFolder));
                entries.Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyCreated, FormatDate(info.CreationTime), MetadataEditKind.CreationTime));
                entries.Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyModified, FormatDate(info.LastWriteTime), MetadataEditKind.LastWriteTime));
                entries.Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyAccessed, FormatDate(info.LastAccessTime), MetadataEditKind.LastAccessTime));
                entries.Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyAttributes, info.Attributes.ToString(), MetadataEditKind.Attributes));

                try
                {
                    var (files, directories) = CountChildren(path);
                    entries.Add(new MetadataEntry(CoreStrings.CategoryContents, CoreStrings.PropertyFiles, files.ToString(CultureInfo.CurrentCulture)));
                    entries.Add(new MetadataEntry(CoreStrings.CategoryContents, CoreStrings.PropertySubfolders, directories.ToString(CultureInfo.CurrentCulture)));
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    entries.Add(new MetadataEntry(CoreStrings.CategoryContents, CoreStrings.PropertyWarning, Format(CoreStrings.ValueContentsInaccessible, ex.Message)));
                }
            }
            catch (Exception ex)
            {
                entries.Add(new MetadataEntry(CoreStrings.CategoryError, CoreStrings.PropertyFolderRead, ex.Message));
            }

            return entries;
        }

        private static IReadOnlyList<MetadataEntry> ReadFile(string path, CancellationToken cancellationToken, bool allowTagEditing)
        {
            var entries = new List<MetadataEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(MetadataEntry entry)
            {
                if (seen.Add(entry.DisplayName)) entries.Add(entry);
            }

            try
            {
                var info = new FileInfo(path);
                Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyName, info.Name, MetadataEditKind.FileName));
                Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyPath, info.FullName, MetadataEditKind.FullPath));
                Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyType, FileTypes.Describe(path)));
                Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyExtension, info.Extension));
                Add(new MetadataEntry(CategoryFile, CoreStrings.PropertySize, FileTypes.FormatSize(info.Length)));
                Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyCreated, FormatDate(info.CreationTime), MetadataEditKind.CreationTime));
                Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyModified, FormatDate(info.LastWriteTime), MetadataEditKind.LastWriteTime));
                Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyAccessed, FormatDate(info.LastAccessTime), MetadataEditKind.LastAccessTime));
                Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyAttributes, info.Attributes.ToString(), MetadataEditKind.Attributes));
                Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyReadOnly, info.IsReadOnly ? CoreStrings.ValueYes : CoreStrings.ValueNo, MetadataEditKind.ReadOnlyFlag));

                string? detected = FileTypes.DetectByContent(path);
                if (detected is not null)
                {
                    Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyActualType, detected));

                    if (!FileTypes.ExtensionMatches(path, detected))
                    {
                        Add(new MetadataEntry(CategoryFile, CoreStrings.PropertyWarning,
                            Format(CoreStrings.ValueExtensionMismatch, info.Extension, detected)));
                    }
                }
            }
            catch (Exception ex)
            {
                Add(new MetadataEntry(CoreStrings.CategoryError, CoreStrings.PropertyFileInfo, ex.Message));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (FileTypes.IsImage(path))
            {
                foreach (var entry in ReadImageDimensions(path)) Add(entry);
            }

            cancellationToken.ThrowIfCancellationRequested();
            ReadEmbeddedMetadata(path, Add, allowTagEditing && ExifWritableTags.SupportsWriting(path));

            return entries;
        }

        private static IEnumerable<MetadataEntry> ReadImageDimensions(string path)
        {
            Size size;
            string pixelFormat;

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
                size = image.Size;
                pixelFormat = image.PixelFormat.ToString();
            }
            catch (Exception)
            {
                // Formatos que GDI+ no entiende (WebP, HEIC...) siguen siendo válidos: los cubre MetadataExtractor.
                yield break;
            }

            yield return new MetadataEntry(CategorySummary, CoreStrings.PropertyResolution, $"{size.Width} x {size.Height} px");
            yield return new MetadataEntry(CategorySummary, CoreStrings.PropertyPixelFormat, pixelFormat);
        }

        private static void ReadEmbeddedMetadata(string path, Action<MetadataEntry> add, bool allowTagEditing)
        {
            IReadOnlyList<MetadataExtractor.Directory> directories;

            try
            {
                directories = ImageMetadataReader.ReadMetadata(path);
            }
            catch (ImageProcessingException)
            {
                // El formato no lleva metadatos que sepamos leer (por ejemplo un PDF), pero ExifTool
                // sí puede escribir en él: se ofrecen igualmente los campos de edición rápida.
                add(new MetadataEntry(CategorySummary, CoreStrings.PropertyEmbedded, CoreStrings.ValueNoEmbeddedMetadata));

                if (allowTagEditing)
                {
                    foreach (var entry in QuickEditFields.Build(Array.Empty<MetadataExtractor.Directory>())) add(entry);
                }

                return;
            }
            catch (Exception ex)
            {
                add(new MetadataEntry(CoreStrings.CategoryError, CoreStrings.PropertyEmbedded, ex.Message));
                return;
            }

            if (allowTagEditing)
            {
                foreach (var entry in QuickEditFields.Build(directories)) add(entry);
            }

            foreach (var entry in BuildSummary(directories)) add(entry);

            foreach (var directory in directories)
            {
                foreach (var tag in directory.Tags)
                {
                    string? exifToolTag = allowTagEditing ? ResolveWritableTag(directory, tag) : null;

                    add(exifToolTag is not null
                        ? new MetadataEntry(directory.Name, tag.Name, tag.Description, MetadataEditKind.ExifTag, exifToolTag)
                        : new MetadataEntry(directory.Name, tag.Name, tag.Description));
                }

                foreach (string error in directory.Errors)
                {
                    add(new MetadataEntry(directory.Name, CoreStrings.PropertyReadError, error));
                }
            }
        }

        /// <summary>
        /// Decide si una etiqueta puede editarse: primero la lista curada y, si no está,
        /// solo cuando el valor guardado es el mismo texto que se muestra.
        /// </summary>
        private static string? ResolveWritableTag(MetadataExtractor.Directory directory, MetadataExtractor.Tag tag)
        {
            if (ExifWritableTags.TryGetTag(directory.Name, tag.Name, out string curated)) return curated;

            object? raw = directory.GetObject(tag.Type);

            return ExifWritableTags.TryGetFreeTextTag(directory.Name, tag.Name, raw, tag.Description, out string generic)
                ? generic
                : null;
        }

        private static IEnumerable<MetadataEntry> BuildSummary(IReadOnlyList<MetadataExtractor.Directory> directories)
        {
            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (ifd0 is not null)
            {
                string? make = ifd0.GetDescription(ExifDirectoryBase.TagMake);
                if (!string.IsNullOrWhiteSpace(make))
                    yield return new MetadataEntry(CategorySummary, CoreStrings.PropertyCameraMake, make);

                string? model = ifd0.GetDescription(ExifDirectoryBase.TagModel);
                if (!string.IsNullOrWhiteSpace(model))
                    yield return new MetadataEntry(CategorySummary, CoreStrings.PropertyCameraModel, model);
            }

            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (subIfd is not null)
            {
                if (subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime original))
                    yield return new MetadataEntry(CategorySummary, CoreStrings.PropertyCaptureDate, FormatDate(original));

                string? iso = subIfd.GetDescription(ExifDirectoryBase.TagIsoEquivalent);
                if (!string.IsNullOrWhiteSpace(iso)) yield return new MetadataEntry(CategorySummary, "ISO", iso);

                string? exposure = subIfd.GetDescription(ExifDirectoryBase.TagExposureTime);
                if (!string.IsNullOrWhiteSpace(exposure)) yield return new MetadataEntry(CategorySummary, CoreStrings.PropertyExposure, exposure);

                string? aperture = subIfd.GetDescription(ExifDirectoryBase.TagFNumber);
                if (!string.IsNullOrWhiteSpace(aperture)) yield return new MetadataEntry(CategorySummary, CoreStrings.PropertyAperture, aperture);
            }

            var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
            if (gps is not null && gps.TryGetGeoLocation(out GeoLocation location) && !location.IsZero)
            {
                yield return new MetadataEntry(
                    CategorySummary,
                    CoreStrings.PropertyCoordinates,
                    string.Format(CultureInfo.InvariantCulture, "{0:0.######}, {1:0.######}", location.Latitude, location.Longitude),
                    key: MetadataKeys.Coordinates);
            }
        }

        /// <summary>
        /// Cuenta ficheros y subcarpetas en una sola pasada y sin crear un objeto por entrada.
        /// </summary>
        private static (int Files, int Directories) CountChildren(string path)
        {
            int files = 0;
            int directories = 0;

            var enumerable = new FileSystemEnumerable<bool>(
                path,
                (ref FileSystemEntry entry) => entry.IsDirectory,
                new EnumerationOptions { AttributesToSkip = 0, IgnoreInaccessible = false });

            foreach (bool isDirectory in enumerable)
            {
                if (isDirectory) directories++;
                else files++;
            }

            return (files, directories);
        }

        /// <summary>
        /// Calcula el SHA-256 de un fichero por bloques, cancelable y sin bloquear a otros lectores.
        /// </summary>
        public static string ComputeSha256(string path, CancellationToken cancellationToken = default)
        {
            // Sin búfer propio del FileStream (bufferSize 0): se lee directamente en bloques grandes.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 0, FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            byte[] buffer = ArrayPool<byte>.Shared.Rent(HashBufferSize);

            try
            {
                int read;

                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    hash.AppendData(buffer, 0, read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        private static string Format(string format, params object?[] arguments) =>
            string.Format(CultureInfo.CurrentCulture, format, arguments);

        /// <summary>Formato de fecha estable e inequívoco para mostrar y para volver a parsear.</summary>
        public static string FormatDate(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
