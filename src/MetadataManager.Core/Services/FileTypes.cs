using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MetadataManager.Resources;

namespace MetadataManager.Services
{
    /// <summary>
    /// Clasificación de ficheros por extensión y utilidades de formato asociadas.
    /// </summary>
    public static class FileTypes
    {
        public static readonly IReadOnlySet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".jpe", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".heic", ".heif"
        };

        public static readonly IReadOnlySet<string> OpenXmlExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".docx", ".docm", ".xlsx", ".xlsm", ".pptx", ".pptm"
        };

        /// <summary>
        /// Extensiones que Windows puede ejecutar. Previsualizarlas hace que el shell
        /// abra el archivo con un controlador externo, así que se pide confirmación antes.
        /// </summary>
        public static readonly IReadOnlySet<string> ExecutableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".com", ".scr", ".sys", ".ocx", ".cpl", ".drv", ".efi", ".msi", ".msp", ".msix", ".appx",
            ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".hta", ".jar",
            ".lnk", ".pif", ".reg", ".msc", ".inf", ".scf", ".url", ".gadget"
        };

        /// <summary>Descripción de cada extensión conocida, resuelta en el idioma activo.</summary>
        private static readonly Dictionary<string, Func<string>> KnownTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = () => CoreStrings.TypeJpegImage,
            [".jpeg"] = () => CoreStrings.TypeJpegImage,
            [".jpe"] = () => CoreStrings.TypeJpegImage,
            [".png"] = () => CoreStrings.TypePngImage,
            [".gif"] = () => CoreStrings.TypeGifImage,
            [".bmp"] = () => CoreStrings.TypeBmpImage,
            [".tif"] = () => CoreStrings.TypeTiffImage,
            [".tiff"] = () => CoreStrings.TypeTiffImage,
            [".webp"] = () => CoreStrings.TypeWebpImage,
            [".heic"] = () => CoreStrings.TypeHeicImage,
            [".heif"] = () => CoreStrings.TypeHeifImage,
            [".pdf"] = () => "PDF",
            [".doc"] = () => "Word",
            [".docx"] = () => "Word",
            [".docm"] = () => "Word",
            [".xls"] = () => "Excel",
            [".xlsx"] = () => "Excel",
            [".xlsm"] = () => "Excel",
            [".ppt"] = () => "PowerPoint",
            [".pptx"] = () => "PowerPoint",
            [".pptm"] = () => "PowerPoint",
            [".txt"] = () => CoreStrings.TypeText,
            [".csv"] = () => "CSV",
            [".json"] = () => "JSON",
            [".xml"] = () => "XML",
            [".zip"] = () => CoreStrings.TypeArchive,
            [".rar"] = () => CoreStrings.TypeArchive,
            [".7z"] = () => CoreStrings.TypeArchive,
            [".mp3"] = () => CoreStrings.TypeMp3Audio,
            [".wav"] = () => CoreStrings.TypeWavAudio,
            [".flac"] = () => CoreStrings.TypeFlacAudio,
            [".mp4"] = () => CoreStrings.TypeMp4Video,
            [".mov"] = () => CoreStrings.TypeQuickTimeVideo,
            [".avi"] = () => CoreStrings.TypeAviVideo,
            [".mkv"] = () => CoreStrings.TypeMatroskaVideo,
            [".exe"] = () => CoreStrings.TypeExecutable,
            [".dll"] = () => CoreStrings.TypeDll,
            [".msi"] = () => CoreStrings.TypeMsi,
            [".bat"] = () => CoreStrings.TypeBatchScript,
            [".cmd"] = () => CoreStrings.TypeBatchScript,
            [".ps1"] = () => CoreStrings.TypePowerShellScript,
            [".lnk"] = () => CoreStrings.TypeShortcut
        };

        public static bool IsImage(string path) => ImageExtensions.Contains(Path.GetExtension(path));

        public static bool IsOpenXml(string path) => OpenXmlExtensions.Contains(Path.GetExtension(path));

        /// <summary>
        /// Ejecutable o script, ya sea por la extensión o por la cabecera del archivo.
        /// La comprobación del contenido detecta también los ejecutables disfrazados de otra cosa.
        /// </summary>
        public static bool IsExecutable(string path)
        {
            if (ExecutableExtensions.Contains(Path.GetExtension(path))) return true;

            try
            {
                if (!File.Exists(path)) return false;

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                byte[] header = new byte[2];
                if (stream.Read(header, 0, header.Length) < 2) return false;

                return header[0] == 0x4D && header[1] == 0x5A;   // MZ
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Si no se puede leer, se trata como ejecutable: es la opción prudente.
                return true;
            }
        }

        /// <summary>Descripción legible del tipo de un fichero o directorio.</summary>
        public static string Describe(string path)
        {
            if (Directory.Exists(path)) return CoreStrings.TypeFolder;

            string extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension)) return CoreStrings.TypeNoExtension;
            if (KnownTypes.TryGetValue(extension, out var description)) return description();

            return extension.TrimStart('.').ToUpperInvariant();
        }

        /// <summary>
        /// Identifica el formato leyendo la cabecera del archivo, sin fiarse de la extensión,
        /// y devuelve su nombre en el idioma activo. Devuelve null si no se reconoce el contenido.
        /// </summary>
        public static string? DetectByContent(string path) =>
            DetectFormat(path) is string format ? FormatName(format) : null;

        /// <summary>
        /// Identificador estable (independiente del idioma) del formato detectado por contenido,
        /// o null si no se reconoce. Ver <see cref="Formats"/>.
        /// </summary>
        internal static string? DetectFormat(string path)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                byte[] header = new byte[16];
                int read = stream.Read(header, 0, header.Length);
                if (read < 4) return null;

                string? signature = MatchSignature(header, read, stream);
                if (signature is not null) return signature;

                stream.Position = 0;
                var detected = MetadataExtractor.Util.FileTypeDetector.DetectFileType(stream);

                return detected == MetadataExtractor.Util.FileType.Unknown ? null : detected.ToString();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Comprueba si la extensión del archivo es coherente con el formato detectado por contenido.
        /// Los formatos que no sabemos correlacionar se dan por buenos para no generar avisos falsos.
        /// </summary>
        public static bool ExtensionMatches(string path, string detectedType)
        {
            string extension = Path.GetExtension(path);
            if (extension.Length == 0) return true;

            string? format = ExpectedExtensions.ContainsKey(detectedType)
                ? detectedType
                : ExpectedExtensions.Keys.FirstOrDefault(key => FormatName(key) == detectedType);

            return format is null || ExpectedExtensions[format].Contains(extension);
        }

        /// <summary>Identificadores de los formatos que la aplicación reconoce por su firma.</summary>
        internal static class Formats
        {
            public const string Executable = "Executable";
            public const string Pdf = "PDF";
            public const string Ole2 = "OLE2";
            public const string Rar = "RAR";
            public const string SevenZip = "7-Zip";
            public const string Mp3 = "Mp3";
            public const string GZip = "GZip";
            public const string Zip = "Zip";
            public const string Word = "Word (OOXML)";
            public const string Excel = "Excel (OOXML)";
            public const string PowerPoint = "PowerPoint (OOXML)";
            public const string OpenDocument = "OpenDocument";
        }

        /// <summary>
        /// Nombre visible de un formato. Los de MetadataExtractor (Jpeg, Png...) y las siglas se
        /// muestran tal cual; los descriptivos se traducen.
        /// </summary>
        internal static string FormatName(string format) => format switch
        {
            Formats.Executable => CoreStrings.TypeExecutable,
            Formats.Ole2 => CoreStrings.TypeOle2Document,
            Formats.Mp3 => CoreStrings.TypeMp3Audio,
            Formats.Zip => CoreStrings.TypeZipArchive,
            Formats.OpenDocument => CoreStrings.TypeOpenDocumentOrJar,
            _ => format
        };

        /// <summary>Extensiones coherentes con cada formato detectado por contenido.</summary>
        private static readonly Dictionary<string, HashSet<string>> ExpectedExtensions = new(StringComparer.Ordinal)
        {
            [Formats.Pdf] = Set(".pdf"),
            [Formats.Executable] = Set(".exe", ".dll", ".com", ".scr", ".sys", ".ocx", ".cpl", ".drv", ".efi", ".mun", ".msstyles", ".ax", ".node", ".tsp"),
            ["Jpeg"] = Set(".jpg", ".jpeg", ".jpe"),
            ["Png"] = Set(".png"),
            ["Gif"] = Set(".gif"),
            ["Bmp"] = Set(".bmp"),
            ["Tiff"] = Set(".tif", ".tiff", ".dng", ".nef", ".arw", ".cr2"),
            ["WebP"] = Set(".webp"),
            ["Heif"] = Set(".heic", ".heif", ".avif"),
            [Formats.Word] = Set(".docx", ".docm", ".dotx"),
            [Formats.Excel] = Set(".xlsx", ".xlsm", ".xltx"),
            [Formats.PowerPoint] = Set(".pptx", ".pptm", ".potx"),
            [Formats.Ole2] = Set(".doc", ".xls", ".ppt", ".msg"),
            [Formats.Zip] = Set(".zip", ".jar", ".apk", ".epub"),
            [Formats.Rar] = Set(".rar"),
            [Formats.SevenZip] = Set(".7z"),
            [Formats.Mp3] = Set(".mp3")
        };

        private static HashSet<string> Set(params string[] extensions) => new(extensions, StringComparer.OrdinalIgnoreCase);

        /// <summary>Firmas de formatos que MetadataExtractor no clasifica (documentos y contenedores).</summary>
        private static string? MatchSignature(byte[] header, int length, FileStream stream)
        {
            bool Starts(params byte[] expected)
            {
                if (length < expected.Length) return false;
                for (int i = 0; i < expected.Length; i++)
                {
                    if (header[i] != expected[i]) return false;
                }

                return true;
            }

            if (Starts(0x4D, 0x5A)) return Formats.Executable;                        // MZ
            if (Starts(0x25, 0x50, 0x44, 0x46)) return Formats.Pdf;                                  // %PDF
            if (Starts(0xD0, 0xCF, 0x11, 0xE0)) return Formats.Ole2;
            if (Starts(0x52, 0x61, 0x72, 0x21)) return Formats.Rar;
            if (Starts(0x37, 0x7A, 0xBC, 0xAF)) return Formats.SevenZip;
            if (Starts(0x49, 0x44, 0x33)) return Formats.Mp3;
            if (Starts(0x1F, 0x8B)) return Formats.GZip;
            if (Starts(0x50, 0x4B, 0x03, 0x04)) return DescribeZip(stream);

            return null;
        }

        /// <summary>Distingue un ZIP normal de un documento OOXML mirando sus entradas.</summary>
        private static string DescribeZip(FileStream stream)
        {
            try
            {
                stream.Position = 0;
                using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true);

                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase)) return Formats.Word;
                    if (entry.FullName.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)) return Formats.Excel;
                    if (entry.FullName.StartsWith("ppt/", StringComparison.OrdinalIgnoreCase)) return Formats.PowerPoint;
                    if (entry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase)) return Formats.OpenDocument;
                }

                return Formats.Zip;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                return Formats.Zip;
            }
        }

        /// <summary>Tamaño legible acompañado del valor exacto en bytes.</summary>
        public static string FormatSize(long bytes)
        {
            if (bytes < 0) return string.Empty;
            if (bytes < 1024) return string.Format(CultureInfo.CurrentCulture, "{0} bytes", bytes);

            return string.Format(CultureInfo.CurrentCulture, "{0} ({1:N0} bytes)", FormatCompactSize(bytes), bytes);
        }

        /// <summary>Tamaño abreviado, pensado para una columna estrecha.</summary>
        public static string FormatCompactSize(long bytes)
        {
            if (bytes < 0) return string.Empty;
            if (bytes < 1024) return string.Format(CultureInfo.CurrentCulture, "{0} B", bytes);

            string[] units = { "KB", "MB", "GB", "TB", "PB" };
            double value = bytes;
            int unit = -1;

            do
            {
                value /= 1024;
                unit++;
            }
            while (value >= 1024 && unit < units.Length - 1);

            return string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", value, units[unit]);
        }
    }
}
