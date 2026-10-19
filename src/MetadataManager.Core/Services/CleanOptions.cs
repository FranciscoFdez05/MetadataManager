using System;

namespace MetadataManager.Services
{
    /// <summary>Cómo debe comportarse una operación de limpieza.</summary>
    public sealed class CleanOptions
    {
        /// <summary>Fecha neutra que se escribe en los metadatos y, opcionalmente, en el sistema de archivos.</summary>
        public DateTime StandardDate { get; set; } = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Local);

        /// <summary>
        /// Reinserta la etiqueta EXIF de orientación después de limpiar. Sin ella, las fotos
        /// tomadas en vertical se muestran giradas en los visores.
        /// </summary>
        public bool PreserveOrientation { get; set; } = true;

        /// <summary>Normaliza las fechas de creación, modificación y acceso del archivo.</summary>
        public bool ResetFileDates { get; set; } = true;

        /// <summary>Usa ExifTool cuando esté disponible.</summary>
        public bool UseExifTool { get; set; } = true;

        /// <summary>Qué se hace con el archivo original.</summary>
        public CleanOutputMode OutputMode { get; set; } = CleanOutputMode.Overwrite;

        public static CleanOptions Default => new();
    }
}
