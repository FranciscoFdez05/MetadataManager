using System;

namespace MetadataManager.Models
{
    /// <summary>
    /// Indica si una propiedad puede escribirse de vuelta en el archivo y de qué forma.
    /// </summary>
    public enum MetadataEditKind
    {
        /// <summary>Solo lectura: el valor se muestra pero no puede modificarse.</summary>
        ReadOnly,

        /// <summary>Renombra el archivo o directorio.</summary>
        FileName,

        /// <summary>Fecha de creación del sistema de archivos.</summary>
        CreationTime,

        /// <summary>Fecha de última modificación del sistema de archivos.</summary>
        LastWriteTime,

        /// <summary>Fecha de último acceso del sistema de archivos.</summary>
        LastAccessTime,

        /// <summary>Atributos del sistema de archivos.</summary>
        Attributes,

        /// <summary>Marca de solo lectura, editable como Sí/No.</summary>
        ReadOnlyFlag,

        /// <summary>Ruta completa: renombra y, si cambia de carpeta, mueve el elemento.</summary>
        FullPath,

        /// <summary>Etiqueta incrustada (EXIF, IPTC...) que se escribe con ExifTool.</summary>
        ExifTag
    }

    /// <summary>
    /// Una propiedad de metadatos lista para mostrarse en la rejilla.
    /// </summary>
    public sealed class MetadataEntry
    {
        public MetadataEntry(
            string category,
            string name,
            string? value,
            MetadataEditKind editKind = MetadataEditKind.ReadOnly,
            string? editTarget = null)
        {
            Category = category ?? string.Empty;
            Name = name ?? string.Empty;
            Value = value ?? string.Empty;
            EditKind = editKind;
            EditTarget = editTarget;
        }

        /// <summary>Grupo lógico al que pertenece la propiedad (Archivo, Resumen, Exif IFD0...).</summary>
        public string Category { get; }

        /// <summary>Nombre de la propiedad dentro de su categoría.</summary>
        public string Name { get; }

        /// <summary>Valor actual, nunca nulo.</summary>
        public string Value { get; }

        public MetadataEditKind EditKind { get; }

        /// <summary>Nombre de la etiqueta de ExifTool a la que corresponde, si es editable.</summary>
        public string? EditTarget { get; }

        public bool IsEditable => EditKind != MetadataEditKind.ReadOnly;

        /// <summary>Clave única mostrada en la primera columna de la rejilla.</summary>
        public string DisplayName => Category.Length == 0 ? Name : $"{Category} - {Name}";

        public MetadataEntry WithValue(string? value) => new(Category, Name, value, EditKind, EditTarget);

        public override string ToString() => $"{DisplayName} = {Value}";
    }
}
