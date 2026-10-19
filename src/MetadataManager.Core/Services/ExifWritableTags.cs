using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MetadataManager.Services
{
    /// <summary>
    /// Correspondencia entre las etiquetas que muestra MetadataExtractor y los nombres que
    /// entiende ExifTool al escribir. Solo se ofrecen etiquetas de texto o fecha: los valores
    /// que MetadataExtractor decora al mostrarlos ("1/125 sec", "f/2.8") no se pueden devolver
    /// tal cual y quedan de solo lectura.
    /// </summary>
    public static class ExifWritableTags
    {
        /// <summary>Etiqueta compuesta que ExifTool acepta como "latitud, longitud".</summary>
        public const string GpsPositionTag = "GPSPosition";

        private static readonly Dictionary<string, string> ExifTags = new(StringComparer.OrdinalIgnoreCase)
        {
            // Equipo
            ["Make"] = "Make",
            ["Model"] = "Model",
            ["Unique Camera Model"] = "UniqueCameraModel",
            ["Camera Owner Name"] = "OwnerName",
            ["Owner Name"] = "OwnerName",
            ["Body Serial Number"] = "SerialNumber",
            ["Camera Serial Number"] = "CameraSerialNumber",
            ["Lens Make"] = "LensMake",
            ["Lens Model"] = "LensModel",
            ["Lens Serial Number"] = "LensSerialNumber",

            // Autoría y descripción
            ["Artist"] = "Artist",
            ["Copyright"] = "Copyright",
            ["Image Description"] = "ImageDescription",
            ["Document Name"] = "DocumentName",
            ["Page Name"] = "PageName",
            ["User Comment"] = "UserComment",
            ["Image History"] = "ImageHistory",
            ["Image Unique ID"] = "ImageUniqueID",
            ["Original Raw File Name"] = "OriginalRawFileName",
            ["Software"] = "Software",
            ["Host Computer"] = "HostComputer",
            ["Rating"] = "Rating",
            ["Rating Percent"] = "RatingPercent",

            // Etiquetas de Windows (las que muestra el Explorador)
            ["Windows XP Title"] = "XPTitle",
            ["Windows XP Author"] = "XPAuthor",
            ["Windows XP Comment"] = "XPComment",
            ["Windows XP Keywords"] = "XPKeywords",
            ["Windows XP Subject"] = "XPSubject",

            // Fechas
            ["Date/Time"] = "ModifyDate",
            ["Date/Time Original"] = "DateTimeOriginal",
            ["Date/Time Digitized"] = "CreateDate",
            ["Sub-Sec Time"] = "SubSecTime",
            ["Sub-Sec Time Original"] = "SubSecTimeOriginal",
            ["Sub-Sec Time Digitized"] = "SubSecTimeDigitized",
            ["Offset Time"] = "OffsetTime",
            ["Offset Time Original"] = "OffsetTimeOriginal",
            ["Offset Time Digitized"] = "OffsetTimeDigitized",

            // Numéricas simples que se muestran sin decorar
            ["ISO Speed Ratings"] = "ISO",
            ["Image Number"] = "ImageNumber",

            // GPS de texto
            ["GPS Date Stamp"] = "GPSDateStamp",
            ["GPS Processing Method"] = "GPSProcessingMethod",
            ["GPS Area Information"] = "GPSAreaInformation",
            ["GPS Map Datum"] = "GPSMapDatum",
            ["GPS Satellites"] = "GPSSatellites"
        };

        private static readonly Dictionary<string, string> IptcTags = new(StringComparer.OrdinalIgnoreCase)
        {
            ["By-line"] = "By-line",
            ["By-line Title"] = "By-lineTitle",
            ["Caption/Abstract"] = "Caption-Abstract",
            ["Headline"] = "Headline",
            ["Keywords"] = "Keywords",
            ["Object Name"] = "ObjectName",
            ["Category"] = "Category",
            ["Supplemental Category(s)"] = "SupplementalCategories",
            ["City"] = "City",
            ["Sub-location"] = "Sub-location",
            ["Province/State"] = "Province-State",
            ["Country/Primary Location Name"] = "Country-PrimaryLocationName",
            ["Country/Primary Location Code"] = "Country-PrimaryLocationCode",
            ["Copyright Notice"] = "CopyrightNotice",
            ["Credit"] = "Credit",
            ["Source"] = "Source",
            ["Contact"] = "Contact",
            ["Special Instructions"] = "SpecialInstructions",
            ["Writer/Editor"] = "Writer-Editor",
            ["Original Transmission Reference"] = "OriginalTransmissionReference",
            ["Urgency"] = "Urgency",
            ["Date Created"] = "DateCreated",
            ["Time Created"] = "TimeCreated"
        };

        /// <summary>Nombres de etiqueta que nunca deben ofrecerse aunque sean texto.</summary>
        private static readonly HashSet<string> Blocked = new(StringComparer.OrdinalIgnoreCase)
        {
            "Orientation",          // se muestra descrita ("Top, left side")
            "Exif Version",
            "Flashpix Version",
            "Interoperability Index",
            "Interoperability Version",
            "Components Configuration",
            "File Source",
            "Scene Type",
            "Thumbnail Offset",
            "Thumbnail Length",
            "Padding"
        };

        /// <summary>
        /// Devuelve el nombre de la etiqueta en ExifTool si la propiedad está en la lista curada.
        /// </summary>
        public static bool TryGetTag(string directoryName, string tagName, out string exifToolTag)
        {
            exifToolTag = string.Empty;

            if (string.IsNullOrEmpty(directoryName) || string.IsNullOrEmpty(tagName)) return false;
            if (Blocked.Contains(tagName)) return false;

            if (directoryName.StartsWith("Exif", StringComparison.OrdinalIgnoreCase) ||
                directoryName.StartsWith("GPS", StringComparison.OrdinalIgnoreCase))
            {
                return ExifTags.TryGetValue(tagName, out exifToolTag!);
            }

            if (directoryName.StartsWith("IPTC", StringComparison.OrdinalIgnoreCase))
                return IptcTags.TryGetValue(tagName, out exifToolTag!);

            if (directoryName.StartsWith("XMP", StringComparison.OrdinalIgnoreCase))
                return TryGetXmpTag(tagName, out exifToolTag);

            return false;
        }

        /// <summary>
        /// Traduce una propiedad XMP tal como la nombra MetadataExtractor ("dc:title")
        /// al nombre de ExifTool ("XMP-dc:Title"). Las propiedades de lista con índice
        /// ("dc:subject[1]") se dejan fuera porque no se escriben elemento a elemento.
        /// </summary>
        private static bool TryGetXmpTag(string tagName, out string exifToolTag)
        {
            exifToolTag = string.Empty;

            if (tagName.Contains('[') || tagName.Contains('/')) return false;

            string[] parts = tagName.Split(':');
            if (parts.Length != 2) return false;

            string prefix = parts[0].Trim();
            string property = parts[1].Trim();

            if (prefix.Length == 0 || property.Length == 0) return false;
            if (!prefix.All(char.IsLetterOrDigit) || !property.All(char.IsLetterOrDigit)) return false;

            exifToolTag = $"XMP-{prefix}:{char.ToUpperInvariant(property[0])}{property[1..]}";
            return true;
        }

        /// <summary>
        /// Último recurso para etiquetas EXIF de texto que no están en la lista curada: solo se
        /// ofrecen si el valor mostrado coincide con el texto guardado (es decir, MetadataExtractor
        /// no lo ha reformateado) y si el nombre se traduce a un identificador plausible.
        /// </summary>
        public static bool TryGetFreeTextTag(string directoryName, string tagName, object? rawValue, string? description, out string exifToolTag)
        {
            exifToolTag = string.Empty;

            if (rawValue is not string text || description is null) return false;
            if (Blocked.Contains(tagName)) return false;
            if (!directoryName.StartsWith("Exif", StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.Equals(text.Trim(), description.Trim(), StringComparison.Ordinal)) return false;

            string candidate = BuildIdentifier(tagName);
            if (candidate.Length < 3) return false;

            exifToolTag = candidate;
            return true;
        }

        /// <summary>Convierte "Date/Time Original" en "DateTimeOriginal".</summary>
        private static string BuildIdentifier(string tagName)
        {
            var builder = new StringBuilder(tagName.Length);

            foreach (char c in tagName)
            {
                if (char.IsLetterOrDigit(c)) builder.Append(c);
            }

            return builder.ToString();
        }

        /// <summary>Formatos en los que ExifTool puede escribir etiquetas con garantías.</summary>
        public static bool SupportsWriting(string path)
        {
            string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();

            return extension is ".jpg" or ".jpeg" or ".jpe" or ".png" or ".tif" or ".tiff"
                or ".webp" or ".heic" or ".heif" or ".pdf" or ".dng" or ".cr2" or ".nef" or ".arw"
                or ".gif" or ".psd" or ".mp4" or ".mov" or ".avi" or ".mp3" or ".flac" or ".m4a";
        }
    }
}
