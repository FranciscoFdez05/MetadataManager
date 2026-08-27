using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MetadataManager.Models;
using Directory = System.IO.Directory;

namespace MetadataManager.Services
{
    /// <summary>
    /// Escribe de vuelta en el sistema de ficheros las propiedades editables.
    /// Lanza excepciones descriptivas: quien llama decide cómo informar al usuario.
    /// </summary>
    public static class MetadataEditor
    {
        /// <summary>
        /// Aplica un cambio y devuelve la ruta resultante (cambia si se renombró el elemento).
        /// </summary>
        public static string Apply(string path, MetadataEditKind kind, string newValue)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Ruta no válida.", nameof(path));

            bool isFile = File.Exists(path);
            bool isDirectory = Directory.Exists(path);

            if (!isFile && !isDirectory)
                throw new FileNotFoundException("El elemento ya no existe en el disco.", path);

            return kind switch
            {
                MetadataEditKind.FileName => Rename(path, newValue, isFile),
                MetadataEditKind.FullPath => MoveTo(path, newValue, isFile),
                MetadataEditKind.CreationTime => SetDate(path, newValue, isFile, DateKind.Creation),
                MetadataEditKind.LastWriteTime => SetDate(path, newValue, isFile, DateKind.LastWrite),
                MetadataEditKind.LastAccessTime => SetDate(path, newValue, isFile, DateKind.LastAccess),
                MetadataEditKind.Attributes => SetAttributes(path, newValue),
                MetadataEditKind.ReadOnlyFlag => SetReadOnly(path, newValue),
                MetadataEditKind.ExifTag => throw new NotSupportedException("Las etiquetas incrustadas se escriben con ApplyTagAsync."),
                _ => throw new NotSupportedException("Esta propiedad es de solo lectura.")
            };
        }

        /// <summary>
        /// Escribe una etiqueta incrustada mediante ExifTool. Un valor vacío la elimina.
        /// </summary>
        public static async Task ApplyTagAsync(string path, string tag, string value, CancellationToken cancellationToken = default)
        {
            string? executable = ExifTool.Locate();
            if (executable is null)
                throw new InvalidOperationException("ExifTool no está disponible: no se pueden escribir etiquetas incrustadas.");

            var result = await ExifTool.WriteTagAsync(executable, path, tag, value, cancellationToken).ConfigureAwait(false);
            if (!result.Success) throw new InvalidOperationException(result.Message);
        }

        /// <summary>Valida un valor antes de aceptarlo en la rejilla. Devuelve null si es válido.</summary>
        public static string? Validate(MetadataEditKind kind, string newValue, string? editTarget = null)
        {
            if (kind == MetadataEditKind.ExifTag)
            {
                return editTarget == ExifWritableTags.GpsPositionTag && !IsValidCoordinatePair(newValue)
                    ? "Usa el formato «latitud, longitud» (por ejemplo 40.416775, -3.703790) o deja el campo vacío para borrarlas."
                    : null;
            }

            switch (kind)
            {
                case MetadataEditKind.FileName:
                    if (string.IsNullOrWhiteSpace(newValue))
                        return "El nombre no puede estar vacío.";
                    if (newValue.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                        return "El nombre contiene caracteres no permitidos.";
                    return null;

                case MetadataEditKind.FullPath:
                    if (string.IsNullOrWhiteSpace(newValue))
                        return "La ruta no puede estar vacía.";
                    if (Path.GetFileName(newValue.Trim()).Length == 0)
                        return "La ruta debe terminar en un nombre de archivo.";
                    if (!IsRootedAndValid(newValue))
                        return "Escribe una ruta completa válida, por ejemplo C:\\fotos\\imagen.jpg.";
                    return null;

                case MetadataEditKind.CreationTime:
                case MetadataEditKind.LastWriteTime:
                case MetadataEditKind.LastAccessTime:
                    return TryParseDate(newValue, out _)
                        ? null
                        : "Fecha no válida. Usa el formato yyyy-MM-dd HH:mm:ss.";

                case MetadataEditKind.Attributes:
                    return Enum.TryParse<FileAttributes>(newValue, ignoreCase: true, out _)
                        ? null
                        : "Atributos no válidos. Ejemplo: Archive, ReadOnly, Hidden.";

                case MetadataEditKind.ReadOnlyFlag:
                    return TryParseBoolean(newValue, out _)
                        ? null
                        : "Escribe Sí o No.";

                default:
                    return null;
            }
        }

        /// <summary>Acepta un par de coordenadas decimales, o una cadena vacía para eliminarlas.</summary>
        private static bool IsValidCoordinatePair(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;

            string[] parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return parts.Length == 2 &&
                   double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double latitude) &&
                   double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double longitude) &&
                   latitude is >= -90 and <= 90 &&
                   longitude is >= -180 and <= 180;
        }

        private static string Rename(string path, string newName, bool isFile)
        {
            string? error = Validate(MetadataEditKind.FileName, newName);
            if (error is not null) throw new ArgumentException(error);

            newName = newName.Trim();

            string? parent = isFile ? Path.GetDirectoryName(path) : Directory.GetParent(path)?.FullName;
            if (string.IsNullOrEmpty(parent))
                throw new InvalidOperationException("No se puede determinar la carpeta contenedora.");

            string destination = Path.Combine(parent, newName);
            if (string.Equals(destination, path, StringComparison.OrdinalIgnoreCase))
                return path;

            if (File.Exists(destination) || Directory.Exists(destination))
                throw new IOException("Ya existe un elemento con ese nombre en la carpeta.");

            if (isFile) File.Move(path, destination);
            else Directory.Move(path, destination);

            return destination;
        }

        /// <summary>Cambia la ruta completa: renombra y, si hace falta, mueve a otra carpeta.</summary>
        private static string MoveTo(string path, string newPath, bool isFile)
        {
            string? error = Validate(MetadataEditKind.FullPath, newPath);
            if (error is not null) throw new ArgumentException(error);

            string destination = Path.GetFullPath(newPath.Trim());

            if (string.Equals(destination, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                return path;

            string? parent = Path.GetDirectoryName(destination);
            if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
                throw new DirectoryNotFoundException("La carpeta de destino no existe: " + parent);

            if (Path.GetFileName(destination).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("El nombre contiene caracteres no permitidos.");

            if (File.Exists(destination) || Directory.Exists(destination))
                throw new IOException("Ya existe un elemento en la ruta de destino.");

            if (isFile) File.Move(path, destination);
            else Directory.Move(path, destination);

            return destination;
        }

        private enum DateKind
        {
            Creation,
            LastWrite,
            LastAccess
        }

        private static string SetDate(string path, string value, bool isFile, DateKind kind)
        {
            if (!TryParseDate(value, out DateTime date))
                throw new ArgumentException("Fecha no válida. Usa el formato yyyy-MM-dd HH:mm:ss.");

            if (isFile)
            {
                switch (kind)
                {
                    case DateKind.Creation: File.SetCreationTime(path, date); break;
                    case DateKind.LastWrite: File.SetLastWriteTime(path, date); break;
                    default: File.SetLastAccessTime(path, date); break;
                }
            }
            else
            {
                switch (kind)
                {
                    case DateKind.Creation: Directory.SetCreationTime(path, date); break;
                    case DateKind.LastWrite: Directory.SetLastWriteTime(path, date); break;
                    default: Directory.SetLastAccessTime(path, date); break;
                }
            }

            return path;
        }

        /// <summary>Activa o desactiva la marca de solo lectura sin tocar el resto de atributos.</summary>
        private static string SetReadOnly(string path, string value)
        {
            if (!TryParseBoolean(value, out bool readOnly))
                throw new ArgumentException("Escribe Sí o No.");

            var attributes = File.GetAttributes(path);

            File.SetAttributes(path, readOnly
                ? attributes | FileAttributes.ReadOnly
                : attributes & ~FileAttributes.ReadOnly);

            return path;
        }

        /// <summary>Acepta las formas afirmativas y negativas de los dos idiomas de la interfaz.</summary>
        private static bool TryParseBoolean(string value, out bool result)
        {
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            result = false;

            if (normalized is "sí" or "si" or "yes" or "true" or "1" or "x")
            {
                result = true;
                return true;
            }

            return normalized is "no" or "false" or "0" or "";
        }

        /// <summary>Comprueba que la ruta es absoluta y sin caracteres imposibles.</summary>
        private static bool IsRootedAndValid(string value)
        {
            try
            {
                string full = Path.GetFullPath(value.Trim());
                return Path.IsPathRooted(full) && Path.GetFileName(full).Length > 0;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        private static string SetAttributes(string path, string value)
        {
            if (!Enum.TryParse(value, ignoreCase: true, out FileAttributes attributes))
                throw new ArgumentException("Atributos no válidos. Ejemplo: Archive, ReadOnly, Hidden.");

            File.SetAttributes(path, attributes);
            return path;
        }

        private static bool TryParseDate(string value, out DateTime date)
        {
            string[] formats =
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-dd"
            };

            if (DateTime.TryParseExact(value?.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;

            return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
        }

        /// <summary>Fija las tres marcas de tiempo del sistema de ficheros. No lanza.</summary>
        public static void SetFileTimes(string path, DateTime date) => SetFileTimes(path, date, date, date);

        /// <summary>Restaura marcas de tiempo concretas. No lanza.</summary>
        public static void SetFileTimes(string path, DateTime creation, DateTime lastWrite, DateTime lastAccess)
        {
            try
            {
                if (File.Exists(path))
                {
                    // Un fichero de solo lectura rechaza los cambios de fecha: se restaura el atributo al terminar.
                    var attributes = File.GetAttributes(path);
                    bool readOnly = attributes.HasFlag(FileAttributes.ReadOnly);
                    if (readOnly) File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);

                    File.SetCreationTime(path, creation);
                    File.SetLastWriteTime(path, lastWrite);
                    File.SetLastAccessTime(path, lastAccess);

                    if (readOnly) File.SetAttributes(path, attributes);
                }
                else if (Directory.Exists(path))
                {
                    Directory.SetCreationTime(path, creation);
                    Directory.SetLastWriteTime(path, lastWrite);
                    Directory.SetLastAccessTime(path, lastAccess);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
            {
                // Ajustar fechas es un extra: si el sistema lo impide, la limpieza sigue siendo válida.
            }
        }
    }
}
