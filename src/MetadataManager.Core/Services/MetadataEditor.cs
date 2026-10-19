using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MetadataManager.Models;
using Directory = System.IO.Directory;
using MetadataManager.Resources;

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
                throw new ArgumentException(CoreStrings.EditInvalidPath, nameof(path));

            bool isFile = File.Exists(path);
            bool isDirectory = Directory.Exists(path);

            if (!isFile && !isDirectory)
                throw new FileNotFoundException(CoreStrings.EditItemMissing, path);

            return kind switch
            {
                MetadataEditKind.FileName => Rename(path, newValue, isFile),
                MetadataEditKind.FullPath => MoveTo(path, newValue, isFile),
                MetadataEditKind.CreationTime => SetDate(path, newValue, isFile, DateKind.Creation),
                MetadataEditKind.LastWriteTime => SetDate(path, newValue, isFile, DateKind.LastWrite),
                MetadataEditKind.LastAccessTime => SetDate(path, newValue, isFile, DateKind.LastAccess),
                MetadataEditKind.Attributes => SetAttributes(path, newValue),
                MetadataEditKind.ReadOnlyFlag => SetReadOnly(path, newValue),
                MetadataEditKind.ExifTag => throw new NotSupportedException(CoreStrings.EditUseApplyTag),
                _ => throw new NotSupportedException(CoreStrings.EditReadOnlyProperty)
            };
        }

        /// <summary>
        /// Escribe una etiqueta incrustada mediante ExifTool. Un valor vacío la elimina.
        /// </summary>
        public static async Task ApplyTagAsync(string path, string tag, string value, CancellationToken cancellationToken = default)
        {
            string? executable = ExifTool.Locate();
            if (executable is null)
                throw new InvalidOperationException(CoreStrings.EditExifToolUnavailable);

            var result = await ExifTool.WriteTagAsync(executable, path, tag, value, cancellationToken).ConfigureAwait(false);
            if (!result.Success) throw new InvalidOperationException(result.Message);
        }

        /// <summary>Valida un valor antes de aceptarlo en la rejilla. Devuelve null si es válido.</summary>
        public static string? Validate(MetadataEditKind kind, string newValue, string? editTarget = null)
        {
            if (kind == MetadataEditKind.ExifTag)
            {
                return editTarget == ExifWritableTags.GpsPositionTag && !IsValidCoordinatePair(newValue)
                    ? CoreStrings.EditCoordinatesFormat
                    : null;
            }

            switch (kind)
            {
                case MetadataEditKind.FileName:
                    if (string.IsNullOrWhiteSpace(newValue))
                        return CoreStrings.EditNameEmpty;
                    if (newValue.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                        return CoreStrings.EditNameInvalidChars;
                    return null;

                case MetadataEditKind.FullPath:
                    if (string.IsNullOrWhiteSpace(newValue))
                        return CoreStrings.EditPathEmpty;
                    if (Path.GetFileName(newValue.Trim()).Length == 0)
                        return CoreStrings.EditPathNeedsName;
                    if (!IsRootedAndValid(newValue))
                        return CoreStrings.EditPathInvalid;
                    return null;

                case MetadataEditKind.CreationTime:
                case MetadataEditKind.LastWriteTime:
                case MetadataEditKind.LastAccessTime:
                    return TryParseDate(newValue, out _)
                        ? null
                        : CoreStrings.EditDateInvalid;

                case MetadataEditKind.Attributes:
                    return Enum.TryParse<FileAttributes>(newValue, ignoreCase: true, out _)
                        ? null
                        : CoreStrings.EditAttributesInvalid;

                case MetadataEditKind.ReadOnlyFlag:
                    return TryParseBoolean(newValue, out _)
                        ? null
                        : CoreStrings.EditYesNo;

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
                throw new InvalidOperationException(CoreStrings.EditNoParent);

            string destination = Path.Combine(parent, newName);
            if (string.Equals(destination, path, StringComparison.OrdinalIgnoreCase))
                return path;

            if (File.Exists(destination) || Directory.Exists(destination))
                throw new IOException(CoreStrings.EditNameExists);

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
                throw new DirectoryNotFoundException(string.Format(CultureInfo.CurrentCulture, CoreStrings.EditTargetFolderMissing, parent));

            if (Path.GetFileName(destination).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException(CoreStrings.EditNameInvalidChars);

            if (File.Exists(destination) || Directory.Exists(destination))
                throw new IOException(CoreStrings.EditTargetExists);

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
                throw new ArgumentException(CoreStrings.EditDateInvalid);

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
                throw new ArgumentException(CoreStrings.EditYesNo);

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
                throw new ArgumentException(CoreStrings.EditAttributesInvalid);

            File.SetAttributes(path, attributes);
            return path;
        }

        /// <summary>Formatos de fecha aceptados antes de recurrir a la cultura del usuario.</summary>
        private static readonly string[] DateFormats =
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd"
        };

        private static bool TryParseDate(string value, out DateTime date)
        {
            if (DateTime.TryParseExact(value?.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
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
