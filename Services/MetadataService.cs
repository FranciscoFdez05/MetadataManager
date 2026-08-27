using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataManager.Models;
using Directory = System.IO.Directory;

namespace MetadataManager.Services
{
    /// <summary>
    /// Lectura de metadatos. No depende de la interfaz: devuelve datos planos que la UI pinta.
    /// </summary>
    public static class MetadataService
    {
        private const string CategoryFile = "Archivo";
        private const string CategorySummary = "Resumen";

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

            return new[] { new MetadataEntry("Estado", "Disponibilidad", "La ruta ya no existe o no es accesible") };
        }

        private static IReadOnlyList<MetadataEntry> ReadDirectory(string path)
        {
            var entries = new List<MetadataEntry>();

            try
            {
                var info = new DirectoryInfo(path);
                entries.Add(new MetadataEntry(CategoryFile, "Nombre", info.Name, MetadataEditKind.FileName));
                entries.Add(new MetadataEntry(CategoryFile, "Ruta", info.FullName, MetadataEditKind.FullPath));
                entries.Add(new MetadataEntry(CategoryFile, "Tipo", "Carpeta"));
                entries.Add(new MetadataEntry(CategoryFile, "Fecha de creación", FormatDate(info.CreationTime), MetadataEditKind.CreationTime));
                entries.Add(new MetadataEntry(CategoryFile, "Fecha de modificación", FormatDate(info.LastWriteTime), MetadataEditKind.LastWriteTime));
                entries.Add(new MetadataEntry(CategoryFile, "Último acceso", FormatDate(info.LastAccessTime), MetadataEditKind.LastAccessTime));
                entries.Add(new MetadataEntry(CategoryFile, "Atributos", info.Attributes.ToString(), MetadataEditKind.Attributes));

                try
                {
                    int files = info.EnumerateFiles().Count();
                    int directories = info.EnumerateDirectories().Count();
                    entries.Add(new MetadataEntry("Contenido", "Ficheros", files.ToString(CultureInfo.CurrentCulture)));
                    entries.Add(new MetadataEntry("Contenido", "Subcarpetas", directories.ToString(CultureInfo.CurrentCulture)));
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    entries.Add(new MetadataEntry("Contenido", "Aviso", "Contenido no accesible: " + ex.Message));
                }
            }
            catch (Exception ex)
            {
                entries.Add(new MetadataEntry("Error", "Lectura del directorio", ex.Message));
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
                Add(new MetadataEntry(CategoryFile, "Nombre", info.Name, MetadataEditKind.FileName));
                Add(new MetadataEntry(CategoryFile, "Ruta", info.FullName, MetadataEditKind.FullPath));
                Add(new MetadataEntry(CategoryFile, "Tipo", FileTypes.Describe(path)));
                Add(new MetadataEntry(CategoryFile, "Extensión", info.Extension));
                Add(new MetadataEntry(CategoryFile, "Tamaño", FileTypes.FormatSize(info.Length)));
                Add(new MetadataEntry(CategoryFile, "Fecha de creación", FormatDate(info.CreationTime), MetadataEditKind.CreationTime));
                Add(new MetadataEntry(CategoryFile, "Fecha de modificación", FormatDate(info.LastWriteTime), MetadataEditKind.LastWriteTime));
                Add(new MetadataEntry(CategoryFile, "Último acceso", FormatDate(info.LastAccessTime), MetadataEditKind.LastAccessTime));
                Add(new MetadataEntry(CategoryFile, "Atributos", info.Attributes.ToString(), MetadataEditKind.Attributes));
                Add(new MetadataEntry(CategoryFile, "Solo lectura", info.IsReadOnly ? "Sí" : "No", MetadataEditKind.ReadOnlyFlag));

                string? detected = FileTypes.DetectByContent(path);
                if (detected is not null)
                {
                    Add(new MetadataEntry(CategoryFile, "Tipo real (contenido)", detected));

                    if (!FileTypes.ExtensionMatches(path, detected))
                    {
                        Add(new MetadataEntry(CategoryFile, "Aviso",
                            $"La extensión {info.Extension} no corresponde al contenido detectado ({detected})."));
                    }
                }
            }
            catch (Exception ex)
            {
                Add(new MetadataEntry("Error", "Información del fichero", ex.Message));
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

            yield return new MetadataEntry(CategorySummary, "Resolución", $"{size.Width} x {size.Height} px");
            yield return new MetadataEntry(CategorySummary, "Formato de píxel", pixelFormat);
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
                add(new MetadataEntry(CategorySummary, "Metadatos incrustados", "El formato no contiene metadatos legibles"));

                if (allowTagEditing)
                {
                    foreach (var entry in QuickEditFields.Build(Array.Empty<MetadataExtractor.Directory>())) add(entry);
                }

                return;
            }
            catch (Exception ex)
            {
                add(new MetadataEntry("Error", "Metadatos incrustados", ex.Message));
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
                    add(new MetadataEntry(directory.Name, "Error de lectura", error));
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
                    yield return new MetadataEntry(CategorySummary, "Cámara (marca)", make);

                string? model = ifd0.GetDescription(ExifDirectoryBase.TagModel);
                if (!string.IsNullOrWhiteSpace(model))
                    yield return new MetadataEntry(CategorySummary, "Cámara (modelo)", model);
            }

            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (subIfd is not null)
            {
                if (subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime original))
                    yield return new MetadataEntry(CategorySummary, "Fecha de captura", FormatDate(original));

                string? iso = subIfd.GetDescription(ExifDirectoryBase.TagIsoEquivalent);
                if (!string.IsNullOrWhiteSpace(iso)) yield return new MetadataEntry(CategorySummary, "ISO", iso);

                string? exposure = subIfd.GetDescription(ExifDirectoryBase.TagExposureTime);
                if (!string.IsNullOrWhiteSpace(exposure)) yield return new MetadataEntry(CategorySummary, "Tiempo de exposición", exposure);

                string? aperture = subIfd.GetDescription(ExifDirectoryBase.TagFNumber);
                if (!string.IsNullOrWhiteSpace(aperture)) yield return new MetadataEntry(CategorySummary, "Apertura", aperture);
            }

            var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
            if (gps is not null && gps.TryGetGeoLocation(out GeoLocation location) && !location.IsZero)
            {
                yield return new MetadataEntry(
                    CategorySummary,
                    "Coordenadas",
                    string.Format(CultureInfo.InvariantCulture, "{0:0.######}, {1:0.######}", location.Latitude, location.Longitude));
            }
        }

        /// <summary>
        /// Calcula el SHA-256 de un fichero por bloques, cancelable y sin bloquear a otros lectores.
        /// </summary>
        public static string ComputeSha256(string path, CancellationToken cancellationToken = default)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128 * 1024, FileOptions.SequentialScan);
            using var sha = SHA256.Create();

            byte[] buffer = new byte[128 * 1024];
            int read;

            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sha.TransformBlock(buffer, 0, read, null, 0);
            }

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha.Hash ?? Array.Empty<byte>()).ToLowerInvariant();
        }

        /// <summary>Formato de fecha estable e inequívoco para mostrar y para volver a parsear.</summary>
        public static string FormatDate(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
