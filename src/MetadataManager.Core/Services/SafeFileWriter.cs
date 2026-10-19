using System;
using System.IO;

namespace MetadataManager.Services
{
    /// <summary>
    /// Sustitución del contenido de un fichero sin dejarlo a medias: se escribe primero
    /// un temporal en la misma carpeta y se reemplaza el original en un único paso.
    /// </summary>
    public static class SafeFileWriter
    {
        public static void ReplaceContents(string path, byte[] content)
        {
            ReplaceContents(path, temp => File.WriteAllBytes(temp, content));
        }

        public static void ReplaceContents(string path, Action<string> writeTemporaryFile)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? Path.GetTempPath();
            string temporary = Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

            try
            {
                writeTemporaryFile(temporary);

                var attributes = File.GetAttributes(path);
                bool readOnly = attributes.HasFlag(FileAttributes.ReadOnly);
                if (readOnly) File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);

                try
                {
                    File.Replace(temporary, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                }
                catch (Exception ex) when (ex is IOException or PlatformNotSupportedException or UnauthorizedAccessException)
                {
                    // File.Replace no funciona en algunos sistemas de ficheros de red: se reintenta en dos pasos.
                    File.Delete(path);
                    File.Move(temporary, path);
                }

                if (readOnly) File.SetAttributes(path, attributes);
            }
            finally
            {
                TryDelete(temporary);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Un temporal huérfano no justifica abortar la operación.
            }
        }
    }
}
