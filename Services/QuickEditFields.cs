using System;
using System.Collections.Generic;
using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Iptc;
using MetadataExtractor.Formats.Xmp;
using MetadataManager.Models;

namespace MetadataManager.Services
{
    /// <summary>
    /// Campos de uso habitual que se ofrecen siempre para editar cuando ExifTool está
    /// disponible, incluso si el archivo todavía no los tiene. Así se pueden **añadir**
    /// metadatos, no solo modificar los existentes.
    /// </summary>
    public static class QuickEditFields
    {
        public const string Category = "Edición rápida";

        /// <summary>
        /// Un campo editable: el nombre que ve el usuario, la etiqueta con la que ExifTool lo
        /// escribe y las etiquetas de las que se lee el valor actual, por orden de preferencia.
        /// </summary>
        private sealed record Field(string Name, string Tag, string[] Sources);

        private static readonly Field[] Fields =
        {
            new("Título", "XMP:Title",
                new[] { "dc:title", "Object Name", "Windows XP Title" }),

            new("Autor", "XMP:Creator",
                new[] { "dc:creator", "Artist", "By-line", "Windows XP Author" }),

            new("Descripción", "XMP:Description",
                new[] { "dc:description", "Image Description", "Caption/Abstract" }),

            new("Copyright", "XMP:Rights",
                new[] { "dc:rights", "Copyright", "Copyright Notice" }),

            new("Palabras clave", "XMP:Subject",
                new[] { "dc:subject", "Keywords", "Windows XP Keywords" }),

            new("Comentario", "UserComment",
                new[] { "User Comment", "Windows XP Comment" }),

            new("Fecha de captura", "AllDates",
                new[] { "Date/Time Original", "Date/Time Digitized", "Date/Time" }),

            new("Coordenadas", ExifWritableTags.GpsPositionTag, Array.Empty<string>())
        };

        /// <summary>
        /// Construye las filas de edición rápida a partir de los metadatos ya leídos.
        /// </summary>
        public static IEnumerable<MetadataEntry> Build(IReadOnlyList<MetadataExtractor.Directory> directories)
        {
            string? coordinates = ReadCoordinates(directories);

            foreach (var field in Fields)
            {
                string value = field.Tag == ExifWritableTags.GpsPositionTag
                    ? coordinates ?? string.Empty
                    : ReadFirst(directories, field.Sources) ?? string.Empty;

                yield return new MetadataEntry(Category, field.Name, value, MetadataEditKind.ExifTag, field.Tag);
            }
        }

        /// <summary>Primer valor no vacío entre las etiquetas indicadas, mirando todos los directorios.</summary>
        private static string? ReadFirst(IReadOnlyList<MetadataExtractor.Directory> directories, string[] names)
        {
            foreach (string name in names)
            {
                foreach (var directory in directories)
                {
                    if (!IsEditableSource(directory)) continue;

                    var tag = directory.Tags.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (tag?.Description is { Length: > 0 } value) return value;
                }
            }

            return null;
        }

        /// <summary>
        /// Solo se leen los contenedores de los que ExifTool también sabe escribir; así el valor
        /// mostrado y el que se guarda hablan del mismo sitio.
        /// </summary>
        private static bool IsEditableSource(MetadataExtractor.Directory directory) =>
            directory is ExifDirectoryBase or IptcDirectory or XmpDirectory;

        private static string? ReadCoordinates(IReadOnlyList<MetadataExtractor.Directory> directories)
        {
            var gps = directories.OfType<GpsDirectory>().FirstOrDefault();

            if (gps is null || !gps.TryGetGeoLocation(out GeoLocation location) || location.IsZero) return null;

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:0.######}, {1:0.######}",
                location.Latitude,
                location.Longitude);
        }
    }
}
