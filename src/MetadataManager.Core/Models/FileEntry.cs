using System;
using System.IO;

namespace MetadataManager.Models
{
    /// <summary>
    /// Elemento de la lista de trabajo: un fichero o un directorio del disco.
    /// La ruta es mutable porque un renombrado la cambia sin cambiar de elemento.
    /// </summary>
    /// <remarks>
    /// El tipo (fichero o carpeta) y el tamaño se leen una sola vez y se guardan: la interfaz
    /// los consulta al pintar, ordenar o habilitar comandos, y no debe tocar el disco cada vez.
    /// <see cref="Refresh"/> vuelve a leer el tamaño tras modificar el archivo.
    /// </remarks>
    public sealed class FileEntry
    {
        public FileEntry(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            IsDirectory = Directory.Exists(path);
            Refresh();
        }

        private FileEntry(string path, bool isDirectory, long length)
        {
            Path = path;
            IsDirectory = isDirectory;
            Length = length;
        }

        /// <summary>
        /// Crea la entrada con una única consulta al disco. Devuelve null si la ruta no existe.
        /// </summary>
        public static FileEntry? TryCreate(string path)
        {
            try
            {
                var file = new FileInfo(path);
                if (file.Exists) return new FileEntry(file.FullName, isDirectory: false, file.Length);

                return Directory.Exists(path) ? new FileEntry(System.IO.Path.GetFullPath(path), isDirectory: true, -1) : null;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        public string Path { get; set; }

        /// <summary>Indica si el elemento es una carpeta. Un renombrado no cambia el tipo.</summary>
        public bool IsDirectory { get; }

        /// <summary>Indica si el elemento es un fichero (no comprueba que siga existiendo).</summary>
        public bool IsFile => !IsDirectory;

        /// <summary>Comprueba en el disco que el elemento sigue existiendo.</summary>
        public bool Exists => IsDirectory ? Directory.Exists(Path) : File.Exists(Path);

        /// <summary>Tamaño en bytes leído en la última actualización; -1 en carpetas o si no se pudo leer.</summary>
        public long Length { get; private set; } = -1;

        public string DisplayName =>
            IsDirectory ? new DirectoryInfo(Path).Name : System.IO.Path.GetFileName(Path);

        /// <summary>Vuelve a leer el tamaño del disco.</summary>
        public void Refresh()
        {
            if (IsDirectory)
            {
                Length = -1;
                return;
            }

            try
            {
                var info = new FileInfo(Path);
                Length = info.Exists ? info.Length : -1;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                Length = -1;
            }
        }

        public override string ToString() => Path;
    }
}
