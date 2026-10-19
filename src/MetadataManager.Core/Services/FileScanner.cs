using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MetadataManager.Models;

namespace MetadataManager.Services
{
    /// <summary>Resultado de convertir rutas sueltas en elementos de la lista.</summary>
    /// <param name="Entries">Elementos válidos, sin duplicados y en el orden recibido.</param>
    /// <param name="Skipped">Rutas descartadas por no existir o no ser válidas.</param>
    public sealed record ScanResult(IReadOnlyList<FileEntry> Entries, int Skipped);

    /// <summary>
    /// Recorrido del disco para la lista de trabajo. Pensado para ejecutarse fuera del hilo
    /// de la interfaz: con miles de rutas, consultar cada una lleva un tiempo apreciable.
    /// </summary>
    public static class FileScanner
    {
        /// <summary>
        /// Opciones de recorrido: incluye ocultos y de sistema (como <see cref="Directory.GetFiles(string)"/>),
        /// salta las carpetas sin permiso y no entra en enlaces simbólicos ni uniones, que podrían formar ciclos.
        /// </summary>
        private static readonly EnumerationOptions RecursiveOptions = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = 0,
            ReturnSpecialDirectories = false
        };

        /// <summary>Todos los ficheros bajo <paramref name="root"/>, incluidas sus subcarpetas.</summary>
        public static List<string> EnumerateFiles(string root, CancellationToken cancellationToken = default)
        {
            var files = new List<string>();

            try
            {
                foreach (string file in Directory.EnumerateFiles(root, "*", RecursiveOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    files.Add(file);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // La raíz dejó de estar accesible a mitad del recorrido: se devuelve lo encontrado.
            }

            return files;
        }

        /// <summary>
        /// Normaliza las rutas, descarta las inexistentes y los duplicados (también los que
        /// ya están en <paramref name="existing"/>) y lee una sola vez el tipo y el tamaño de cada una.
        /// </summary>
        public static ScanResult Resolve(IEnumerable<string> paths, IReadOnlySet<string>? existing = null, CancellationToken cancellationToken = default)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entries = new List<FileEntry>();
            int skipped = 0;

            foreach (string raw in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string path;

                try
                {
                    path = Path.GetFullPath(raw);
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    skipped++;
                    continue;
                }

                if (existing?.Contains(path) == true || !seen.Add(path)) continue;

                var entry = FileEntry.TryCreate(path);

                if (entry is null)
                {
                    skipped++;
                    continue;
                }

                entries.Add(entry);
            }

            return new ScanResult(entries, skipped);
        }
    }
}
