using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MetadataManager.Services
{
    /// <summary>Qué se hace con el archivo original al limpiarlo.</summary>
    public enum CleanOutputMode
    {
        /// <summary>Se sobrescribe el original.</summary>
        Overwrite,

        /// <summary>Se guarda una copia del original con extensión .bak y luego se sobrescribe.</summary>
        Backup,

        /// <summary>El original no se toca: se limpia una copia nueva junto a él.</summary>
        Copy
    }

    /// <summary>Preferencias persistentes de la aplicación.</summary>
    public sealed class AppSettings
    {
        /// <summary>"auto", "es" o "en".</summary>
        public string Language { get; set; } = "auto";

        /// <summary>Fecha que se aplica a los archivos limpiados, en formato yyyy-MM-dd HH:mm:ss.</summary>
        public string NormalizationDate { get; set; } = "2000-01-01 00:00:00";

        public CleanOutputMode OutputMode { get; set; } = CleanOutputMode.Backup;

        /// <summary>Reinserta la orientación EXIF tras limpiar para que la imagen no se vea girada.</summary>
        public bool PreserveOrientation { get; set; } = true;

        /// <summary>Normaliza también las fechas del sistema de archivos.</summary>
        public bool ResetFileDates { get; set; } = true;

        public bool UseExifTool { get; set; } = true;

        /// <summary>Ruta elegida a mano para exiftool.exe; null deja la detección automática.</summary>
        public string? ExifToolPath { get; set; }

        public bool ShowThumbnail { get; set; } = true;

        public int WindowWidth { get; set; } = 1100;

        public int WindowHeight { get; set; } = 650;

        public bool WindowMaximized { get; set; }

        public int SplitterDistance { get; set; } = 340;

        public string? LastFolder { get; set; }

        [JsonIgnore]
        public DateTime StandardDate =>
            DateTime.TryParseExact(NormalizationDate, "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)
                ? parsed
                : new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local);

        public CleanOptions ToCleanOptions() => new()
        {
            StandardDate = StandardDate,
            PreserveOrientation = PreserveOrientation,
            ResetFileDates = ResetFileDates,
            UseExifTool = UseExifTool,
            OutputMode = OutputMode
        };

        public AppSettings Clone() => (AppSettings)MemberwiseClone();
    }

    /// <summary>
    /// Lee y escribe las preferencias en formato INI: secciones entre corchetes y
    /// pares <c>clave=valor</c>. Las claves se buscan sin distinguir mayusculas y las
    /// que falten o no se entiendan conservan su valor por defecto.
    /// </summary>
    public static class SettingsIni
    {
        private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

        /// <summary>Vuelca las preferencias al texto que se escribe en el archivo .ini.</summary>
        public static string Write(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var text = new StringBuilder();

            text.AppendLine("; Preferencias de MetadataManager.");
            text.AppendLine("; Se reescribe al cerrar la aplicacion: los comentarios anadidos a mano se pierden.");
            text.AppendLine();

            text.AppendLine("[General]");
            text.AppendLine($"Language={settings.Language}");
            text.AppendLine($"ShowThumbnail={Bool(settings.ShowThumbnail)}");
            text.AppendLine($"LastFolder={settings.LastFolder}");
            text.AppendLine();

            text.AppendLine("[Limpieza]");
            text.AppendLine($"NormalizationDate={settings.NormalizationDate}");
            text.AppendLine($"OutputMode={settings.OutputMode}");
            text.AppendLine($"PreserveOrientation={Bool(settings.PreserveOrientation)}");
            text.AppendLine($"ResetFileDates={Bool(settings.ResetFileDates)}");
            text.AppendLine();

            text.AppendLine("[ExifTool]");
            text.AppendLine($"UseExifTool={Bool(settings.UseExifTool)}");
            text.AppendLine($"ExifToolPath={settings.ExifToolPath}");
            text.AppendLine();

            text.AppendLine("[Ventana]");
            text.AppendLine($"Width={settings.WindowWidth.ToString(CultureInfo.InvariantCulture)}");
            text.AppendLine($"Height={settings.WindowHeight.ToString(CultureInfo.InvariantCulture)}");
            text.AppendLine($"Maximized={Bool(settings.WindowMaximized)}");
            text.AppendLine($"SplitterDistance={settings.SplitterDistance.ToString(CultureInfo.InvariantCulture)}");

            return text.ToString();
        }

        /// <summary>Reconstruye las preferencias a partir del contenido de un archivo .ini.</summary>
        public static AppSettings Read(string content)
        {
            var settings = new AppSettings();
            var values = ParseEntries(content);

            if (values.TryGetValue("Language", out string? language) && !string.IsNullOrWhiteSpace(language))
            {
                settings.Language = language;
            }

            settings.ShowThumbnail = ReadBool(values, "ShowThumbnail", settings.ShowThumbnail);
            settings.LastFolder = ReadPath(values, "LastFolder");

            if (values.TryGetValue("NormalizationDate", out string? date)
                && DateTime.TryParseExact(date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                settings.NormalizationDate = date!;
            }

            if (values.TryGetValue("OutputMode", out string? mode)
                && Enum.TryParse(mode, ignoreCase: true, out CleanOutputMode parsedMode))
            {
                settings.OutputMode = parsedMode;
            }

            settings.PreserveOrientation = ReadBool(values, "PreserveOrientation", settings.PreserveOrientation);
            settings.ResetFileDates = ReadBool(values, "ResetFileDates", settings.ResetFileDates);
            settings.UseExifTool = ReadBool(values, "UseExifTool", settings.UseExifTool);
            settings.ExifToolPath = ReadPath(values, "ExifToolPath");

            settings.WindowWidth = ReadInt(values, "Width", settings.WindowWidth);
            settings.WindowHeight = ReadInt(values, "Height", settings.WindowHeight);
            settings.WindowMaximized = ReadBool(values, "Maximized", settings.WindowMaximized);
            settings.SplitterDistance = ReadInt(values, "SplitterDistance", settings.SplitterDistance);

            return settings;
        }

        /// <summary>
        /// Recorre las lineas quedandose con los pares clave=valor. Las secciones solo
        /// documentan el archivo: las claves son unicas, asi que no hacen falta para leerlas.
        /// </summary>
        private static Dictionary<string, string> ParseEntries(string content)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(content)) return values;

            foreach (string raw in content.Split('\n'))
            {
                string line = raw.Trim();

                if (line.Length == 0 || line[0] is ';' or '#' or '[') continue;

                int separator = line.IndexOf('=');
                if (separator <= 0) continue;

                // El valor puede contener '=' (rutas UNC, parametros): solo parte por el primero.
                values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }

            return values;
        }

        private static string Bool(bool value) => value ? "true" : "false";

        private static bool ReadBool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
            values.TryGetValue(key, out string? text) && bool.TryParse(text, out bool parsed) ? parsed : fallback;

        private static int ReadInt(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
            values.TryGetValue(key, out string? text)
            && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallback;

        /// <summary>Una ruta vacia en el .ini equivale a "sin valor".</summary>
        private static string? ReadPath(IReadOnlyDictionary<string, string> values, string key) =>
            values.TryGetValue(key, out string? text) && !string.IsNullOrWhiteSpace(text) ? text : null;
    }

    /// <summary>
    /// Carga y guarda las preferencias en %APPDATA%\MetadataManager\settings.ini.
    /// Un archivo corrupto o inaccesible nunca impide arrancar: se usan los valores por defecto.
    /// </summary>
    public static class SettingsService
    {
        public static string FilePath => Path.Combine(SettingsFolder, "settings.ini");

        /// <summary>Preferencias de versiones anteriores a la 1.4.0, que usaban JSON.</summary>
        public static string LegacyFilePath => Path.Combine(SettingsFolder, "settings.json");

        private static string SettingsFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MetadataManager");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath)) return SettingsIni.Read(File.ReadAllText(FilePath));

                var migrated = LoadLegacy();
                if (migrated is null) return new AppSettings();

                // Se conserva el settings.json por si el usuario vuelve a una version anterior.
                Save(migrated);
                return migrated;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(SettingsFolder);
                File.WriteAllText(FilePath, SettingsIni.Write(settings), Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // No poder guardar preferencias no debe interrumpir el trabajo del usuario.
            }
        }

        /// <summary>Lee el settings.json de versiones anteriores para no perder las preferencias.</summary>
        private static AppSettings? LoadLegacy()
        {
            if (!File.Exists(LegacyFilePath)) return null;

            try
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(LegacyFilePath));
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
