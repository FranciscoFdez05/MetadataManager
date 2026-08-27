using System;
using System.IO;

namespace MetadataManager.Models
{
    /// <summary>
    /// Elemento de la lista de trabajo: un fichero o un directorio del disco.
    /// La ruta es mutable porque un renombrado la cambia sin cambiar de elemento.
    /// </summary>
    public sealed class FileEntry
    {
        public FileEntry(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public string Path { get; set; }

        public bool IsDirectory => Directory.Exists(Path);

        public bool IsFile => File.Exists(Path);

        public bool Exists => IsFile || IsDirectory;

        public string DisplayName =>
            IsDirectory ? new DirectoryInfo(Path).Name : System.IO.Path.GetFileName(Path);

        public override string ToString() => Path;
    }
}
