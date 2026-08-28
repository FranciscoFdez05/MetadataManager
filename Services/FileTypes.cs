using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

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

        private static readonly Dictionary<string, string> KnownTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "Imagen JPEG",
            [".jpeg"] = "Imagen JPEG",
            [".jpe"] = "Imagen JPEG",
            [".png"] = "Imagen PNG",
            [".gif"] = "Imagen GIF",
            [".bmp"] = "Imagen BMP",
            [".tif"] = "Imagen TIFF",
            [".tiff"] = "Imagen TIFF",
            [".webp"] = "Imagen WebP",
            [".heic"] = "Imagen HEIC",
            [".heif"] = "Imagen HEIF",
            [".pdf"] = "PDF",
            [".doc"] = "Word",
            [".docx"] = "Word",
            [".docm"] = "Word",
            [".xls"] = "Excel",
            [".xlsx"] = "Excel",
            [".xlsm"] = "Excel",
            [".ppt"] = "PowerPoint",
            [".pptx"] = "PowerPoint",
            [".pptm"] = "PowerPoint",
            [".txt"] = "Texto",
            [".csv"] = "CSV",
            [".json"] = "JSON",
            [".xml"] = "XML",
            [".zip"] = "Archivo comprimido",
            [".rar"] = "Archivo comprimido",
            [".7z"] = "Archivo comprimido",
            [".mp3"] = "Audio MP3",
            [".wav"] = "Audio WAV",
            [".flac"] = "Audio FLAC",
            [".mp4"] = "Vídeo MP4",
            [".mov"] = "Vídeo QuickTime",
            [".avi"] = "Vídeo AVI",
            [".mkv"] = "Vídeo Matroska",
            [".exe"] = "Ejecutable de Windows",
            [".dll"] = "Biblioteca DLL",
            [".msi"] = "Instalador MSI",
            [".bat"] = "Script por lotes",
            [".cmd"] = "Script por lotes",
            [".ps1"] = "Script de PowerShell",
            [".lnk"] = "Acceso directo"
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
            if (Directory.Exists(path)) return "Carpeta";

            string extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension)) return "Sin extensión";
            if (KnownTypes.TryGetValue(extension, out string? description)) return description;

            return extension.TrimStart('.').ToUpperInvariant();
        }

        /// <summary>
        /// Identifica el formato leyendo la cabecera del archivo, sin fiarse de la extensión.
        /// Devuelve null si no se reconoce el contenido.
        /// </summary>
        public static string? DetectByContent(string path)
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
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension.Length == 0) return true;

            string[]? expected = detectedType switch
            {
                "PDF" => new[] { ".pdf" },
                "Ejecutable de Windows" => new[] { ".exe", ".dll", ".com", ".scr", ".sys", ".ocx", ".cpl", ".drv", ".efi", ".mun", ".msstyles", ".ax", ".node", ".tsp" },
                "Jpeg" => new[] { ".jpg", ".jpeg", ".jpe" },
                "Png" => new[] { ".png" },
                "Gif" => new[] { ".gif" },
                "Bmp" => new[] { ".bmp" },
                "Tiff" => new[] { ".tif", ".tiff", ".dng", ".nef", ".arw", ".cr2" },
                "WebP" => new[] { ".webp" },
                "Heif" => new[] { ".heic", ".heif", ".avif" },
                "Word (OOXML)" => new[] { ".docx", ".docm", ".dotx" },
                "Excel (OOXML)" => new[] { ".xlsx", ".xlsm", ".xltx" },
                "PowerPoint (OOXML)" => new[] { ".pptx", ".pptm", ".potx" },
                "Documento OLE2 (Office 97-2003)" => new[] { ".doc", ".xls", ".ppt", ".msg" },
                "Archivo comprimido ZIP" => new[] { ".zip", ".jar", ".apk", ".epub" },
                "RAR" => new[] { ".rar" },
                "7-Zip" => new[] { ".7z" },
                "Audio MP3" => new[] { ".mp3" },
                _ => null
            };

            return expected is null || expected.Contains(extension);
        }

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

            if (Starts(0x4D, 0x5A)) return "Ejecutable de Windows";                        // MZ
            if (Starts(0x25, 0x50, 0x44, 0x46)) return "PDF";                                  // %PDF
            if (Starts(0xD0, 0xCF, 0x11, 0xE0)) return "Documento OLE2 (Office 97-2003)";
            if (Starts(0x52, 0x61, 0x72, 0x21)) return "RAR";
            if (Starts(0x37, 0x7A, 0xBC, 0xAF)) return "7-Zip";
            if (Starts(0x49, 0x44, 0x33)) return "Audio MP3";
            if (Starts(0x1F, 0x8B)) return "GZip";
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
                    if (entry.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase)) return "Word (OOXML)";
                    if (entry.FullName.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)) return "Excel (OOXML)";
                    if (entry.FullName.StartsWith("ppt/", StringComparison.OrdinalIgnoreCase)) return "PowerPoint (OOXML)";
                    if (entry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase)) return "Documento OpenDocument o JAR";
                }

                return "Archivo comprimido ZIP";
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                return "Archivo comprimido ZIP";
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
