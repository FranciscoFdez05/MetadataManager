using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MetadataManager.Services
{
    /// <summary>Resultado de una invocación de ExifTool.</summary>
    public readonly record struct ExifToolResult(bool Success, string Message);

    /// <summary>
    /// Envoltorio sobre la herramienta externa ExifTool, opcional pero necesaria
    /// para leer y escribir metadatos en formatos que no sabemos reescribir.
    /// </summary>
    public static class ExifTool
    {
        private static readonly object SyncRoot = new();
        private static string? _cachedPath;
        private static string? _configuredPath;
        private static bool _searched;

        /// <summary>Ruta del ejecutable si está disponible; null si no se encontró.</summary>
        public static string? Locate()
        {
            lock (SyncRoot)
            {
                if (_searched) return _cachedPath;
                _searched = true;
                _cachedPath = Search();
                return _cachedPath;
            }
        }

        public static bool IsAvailable => Locate() is not null;

        /// <summary>Versión de la herramienta conectada, o null si no hay ninguna.</summary>
        public static string? Version { get; private set; }

        /// <summary>
        /// Fija manualmente el ejecutable que debe usarse. Pasa null para volver
        /// a la detección automática.
        /// </summary>
        public static void Configure(string? path)
        {
            lock (SyncRoot)
            {
                _configuredPath = string.IsNullOrWhiteSpace(path) ? null : path;
                _searched = false;
                _cachedPath = null;
                Version = null;
            }
        }

        /// <summary>
        /// Comprueba que la ruta indicada es un ExifTool utilizable y, si lo es, la deja configurada.
        /// </summary>
        /// <param name="version">Versión detectada, o el motivo del fallo.</param>
        public static bool TryConnect(string path, out string version)
        {
            version = string.Empty;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                version = "La ruta indicada no existe.";
                return false;
            }

            string? detected = ReadVersion(path);

            if (detected is null)
            {
                version = "El ejecutable no respondió a la opción -ver.";
                return false;
            }

            Configure(path);
            Locate();
            version = detected;
            return true;
        }

        /// <summary>Olvida el resultado cacheado (por ejemplo tras instalar la herramienta).</summary>
        public static void ResetCache()
        {
            lock (SyncRoot)
            {
                _searched = false;
                _cachedPath = null;
                Version = null;
            }
        }

        private static string? Search()
        {
            // La ruta elegida por el usuario manda sobre cualquier detección automática.
            if (_configuredPath is not null && File.Exists(_configuredPath))
            {
                string? configured = ReadVersion(_configuredPath);
                if (configured is not null)
                {
                    Version = configured;
                    return _configuredPath;
                }
            }

            // Junto al ejecutable después: permite distribuir ExifTool con la aplicación.
            // El paquete oficial para Windows se llama exiftool(-k).exe.
            var candidates = new List<string>();

            foreach (string name in new[] { "exiftool.exe", "exiftool(-k).exe" })
            {
                string local = Path.Combine(AppContext.BaseDirectory, name);
                if (File.Exists(local)) candidates.Add(local);
            }

            candidates.Add("exiftool.exe");
            candidates.Add("exiftool");

            foreach (string candidate in candidates)
            {
                string? version = ReadVersion(candidate);
                if (version is null) continue;

                Version = version;
                return candidate;
            }

            return null;
        }

        /// <summary>Ejecuta <c>-ver</c> y devuelve la versión, o null si no es un ExifTool usable.</summary>
        private static string? ReadVersion(string executable)
        {
            try
            {
                using var process = Process.Start(CreateStartInfo(executable, "-ver"));
                if (process is null) return null;

                CloseInput(process);
                string output = process.StandardOutput.ReadToEnd();

                if (!process.WaitForExit(5000))
                {
                    TryKill(process);
                    return null;
                }

                if (process.ExitCode != 0) return null;

                // La variante exiftool(-k).exe añade un aviso de "pulse una tecla":
                // la versión es siempre la primera línea.
                string? version = output
                    .Split('\n')
                    .Select(line => line.Trim())
                    .FirstOrDefault(line => line.Length > 0);

                return string.IsNullOrEmpty(version) ? null : version;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
            {
                return null;
            }
        }

        /// <summary>
        /// Borra todos los metadatos del archivo y fija las fechas internas al valor indicado.
        /// </summary>
        /// <param name="orientation">
        /// Si es mayor que 1, la orientación se vuelve a escribir después del borrado para que
        /// la imagen no se muestre girada.
        /// </param>
        public static Task<ExifToolResult> StripAllAsync(
            string executable,
            string filePath,
            DateTime date,
            int orientation = 1,
            CancellationToken cancellationToken = default)
        {
            string stamp = date.ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);

            var arguments = new List<string>
            {
                "-overwrite_original",
                "-P",                       // las fechas del sistema de archivos las gestiona el limpiador
                "-all=",
                "-AllDates=" + stamp
            };

            // El orden importa: ExifTool aplica las operaciones de izquierda a derecha.
            if (orientation > 1) arguments.Add("-Orientation#=" + orientation.ToString(CultureInfo.InvariantCulture));

            arguments.Add(filePath);

            return RunAsync(executable, arguments, cancellationToken);
        }

        /// <summary>
        /// Escribe una etiqueta concreta. Un valor vacío elimina la etiqueta del archivo.
        /// </summary>
        public static Task<ExifToolResult> WriteTagAsync(
            string executable,
            string filePath,
            string tag,
            string value,
            CancellationToken cancellationToken = default)
        {
            var arguments = new List<string>
            {
                "-overwrite_original",
                "-preserve",
                $"-{tag}={value}",
                filePath
            };

            return RunAsync(executable, arguments, cancellationToken);
        }

        private static async Task<ExifToolResult> RunAsync(string executable, IEnumerable<string> arguments, CancellationToken cancellationToken)
        {
            try
            {
                using var process = Process.Start(CreateStartInfo(executable, arguments));
                if (process is null) return new ExifToolResult(false, "No se pudo iniciar ExifTool.");

                CloseInput(process);

                // Se leen ambos flujos en paralelo: leerlos en serie puede bloquear el proceso hijo.
                Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                Task<string> stderr = process.StandardError.ReadToEndAsync();

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromMinutes(2));

                try
                {
                    await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    TryKill(process);
                    return new ExifToolResult(false, "ExifTool no respondió a tiempo y se canceló.");
                }

                string output = (await stdout.ConfigureAwait(false)).Trim();
                string error = (await stderr.ConfigureAwait(false)).Trim();

                // ExifTool devuelve 0 aunque emita avisos por la salida de error: solo manda el código.
                if (process.ExitCode == 0) return new ExifToolResult(true, error.Length > 0 ? error : output);

                Debug.WriteLine($"exiftool ({process.ExitCode}): {error}");
                return new ExifToolResult(false, error.Length > 0 ? error : $"ExifTool devolvió el código {process.ExitCode}.");
            }
            catch (Exception ex)
            {
                return new ExifToolResult(false, ex.Message);
            }
        }

        private static ProcessStartInfo CreateStartInfo(string executable, params string[] arguments) =>
            CreateStartInfo(executable, (IEnumerable<string>)arguments);

        /// <summary>
        /// Los argumentos se pasan de uno en uno: así no hay que entrecomillar valores
        /// que contengan espacios o comillas.
        /// </summary>
        private static ProcessStartInfo CreateStartInfo(string executable, IEnumerable<string> arguments)
        {
            var info = new ProcessStartInfo(executable)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            foreach (string argument in arguments) info.ArgumentList.Add(argument);

            return info;
        }

        /// <summary>
        /// Cierra la entrada estándar del proceso. La variante exiftool(-k).exe espera una
        /// pulsación antes de salir; al encontrar el fin de la entrada termina sin bloquearse.
        /// </summary>
        private static void CloseInput(Process process)
        {
            try
            {
                process.StandardInput.Close();
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
            {
                // El proceso ya cerró su entrada por su cuenta.
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
            {
                // El proceso ya terminó por su cuenta.
            }
        }
    }
}
