using System;
using System.Globalization;
using System.IO;
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
    /// Carga y guarda las preferencias en %APPDATA%\MetadataManager\settings.json.
    /// Un archivo corrupto o inaccesible nunca impide arrancar: se usan los valores por defecto.
    /// </summary>
    public static class SettingsService
    {
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        public static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MetadataManager",
            "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new AppSettings();

                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                string? directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // No poder guardar preferencias no debe interrumpir el trabajo del usuario.
            }
        }
    }
}
