using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MetadataManager.Resources;

namespace MetadataManager.Services
{
    /// <summary>
    /// Proceso de ExifTool que se mantiene abierto (<c>-stay_open</c>) y recibe las órdenes por la
    /// entrada estándar. Arrancar ExifTool cuesta cientos de milisegundos porque es un intérprete
    /// de Perl; reutilizar el proceso deja el coste de cada orden en lo que tarde el propio archivo.
    /// </summary>
    /// <remarks>
    /// Cada orden termina con <c>-echo4 {readyN:${status}}</c> y <c>-executeN</c>: ExifTool escribe
    /// <c>{readyN}</c> al final de la salida estándar y la marca con el código de estado al final de
    /// la de error, así que se sabe dónde acaba cada respuesta y si tuvo éxito.
    /// Una sesión no admite órdenes simultáneas: el pool de <see cref="ExifTool"/> la presta en exclusiva.
    /// </remarks>
    internal sealed class ExifToolSession : IDisposable
    {
        private readonly Process _process;
        private int _sequence;
        private bool _broken;

        private ExifToolSession(string executable, Process process)
        {
            Executable = executable;
            _process = process;
            _process.StandardInput.NewLine = "\n";
            _process.StandardInput.AutoFlush = false;
        }

        public string Executable { get; }

        /// <summary>La sesión puede seguir usándose: el proceso vive y no quedó a medias ninguna orden.</summary>
        public bool IsHealthy
        {
            get
            {
                try
                {
                    return !_broken && !_process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }
        }

        public static ExifToolSession Start(string executable)
        {
            // -common_args se aplica a cada orden; filename=utf8 permite rutas con cualquier carácter.
            var info = ExifTool.CreateStartInfo(executable, new[]
            {
                "-stay_open", "True", "-@", "-", "-common_args", "-charset", "filename=utf8"
            });
            info.StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            var process = Process.Start(info) ?? throw new InvalidOperationException(CoreStrings.ExifToolStartFailed);
            return new ExifToolSession(executable, process);
        }

        /// <summary>
        /// Ejecuta una orden. Los argumentos no pueden contener saltos de línea: cada línea es un argumento.
        /// </summary>
        /// <exception cref="OperationCanceledException">Se canceló la orden; la sesión queda inservible.</exception>
        /// <exception cref="TimeoutException">ExifTool no respondió a tiempo; la sesión queda inservible.</exception>
        public async Task<ExifToolResult> ExecuteAsync(IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            if (!IsHealthy) throw new InvalidOperationException(CoreStrings.ExifToolStartFailed);

            int id = Interlocked.Increment(ref _sequence);
            string outputMarker = "{ready" + id.ToString(CultureInfo.InvariantCulture) + "}";
            string errorMarker = "{ready" + id.ToString(CultureInfo.InvariantCulture) + ":";

            // Desde aquí, una interrupción deja respuestas pendientes en los flujos: la sesión no se reutiliza.
            _broken = true;

            var input = _process.StandardInput;
            foreach (string argument in arguments) await input.WriteLineAsync(argument).ConfigureAwait(false);
            await input.WriteLineAsync("-echo4").ConfigureAwait(false);
            await input.WriteLineAsync(errorMarker + "${status}}").ConfigureAwait(false);
            await input.WriteLineAsync("-execute" + id.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            await input.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Las lecturas no se cancelan por sí solas: si vence el plazo se mata el proceso, lo que las termina.
            Task<List<string>> output = ReadUntilAsync(_process.StandardOutput, line => line == outputMarker);
            Task<List<string>> error = ReadUntilAsync(_process.StandardError, line => line.StartsWith(errorMarker, StringComparison.Ordinal));
            Task responses = Task.WhenAll(output, error);

            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(timeout);

                try
                {
                    await responses.WaitAsync(deadline.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Kill();
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException(CoreStrings.ExifToolTimeout);
                }
            }

            List<string> outputLines = await output.ConfigureAwait(false);
            List<string> errorLines = await error.ConfigureAwait(false);

            // Si el proceso murió a mitad, falta la marca final.
            if (outputLines.Count == 0 || outputLines[^1] != outputMarker ||
                errorLines.Count == 0 || !errorLines[^1].StartsWith(errorMarker, StringComparison.Ordinal))
            {
                Kill();
                return new ExifToolResult(false, CoreStrings.ExifToolStartFailed);
            }

            _broken = false;

            int status = ParseStatus(errorLines[^1], errorMarker);
            string message = Join(errorLines);
            if (message.Length == 0) message = Join(outputLines);

            // ExifTool emite avisos por la salida de error aunque termine bien: solo manda el código.
            return status == 0
                ? new ExifToolResult(true, message)
                : new ExifToolResult(false, message.Length > 0
                    ? message
                    : string.Format(CultureInfo.CurrentCulture, CoreStrings.ExifToolExitCode, status));
        }

        /// <summary>Lee líneas hasta la marca (incluida) o hasta el final del flujo.</summary>
        private static async Task<List<string>> ReadUntilAsync(StreamReader reader, Func<string, bool> isMarker)
        {
            var lines = new List<string>();

            while (await reader.ReadLineAsync().ConfigureAwait(false) is string line)
            {
                lines.Add(line);
                if (isMarker(line)) break;
            }

            return lines;
        }

        private static int ParseStatus(string markerLine, string prefix)
        {
            string value = markerLine[prefix.Length..].TrimEnd('}');
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int status) ? status : -1;
        }

        /// <summary>Une las líneas de la respuesta sin la marca final.</summary>
        private static string Join(List<string> lines) =>
            string.Join(Environment.NewLine, lines.GetRange(0, lines.Count - 1)).Trim();

        private void Kill()
        {
            _broken = true;

            try
            {
                if (!_process.HasExited) _process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
            {
                // Ya había terminado.
            }
        }

        /// <summary>Pide a ExifTool que termine y, si no lo hace enseguida, lo mata.</summary>
        public void Dispose()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.StandardInput.WriteLine("-stay_open");
                    _process.StandardInput.WriteLine("False");
                    _process.StandardInput.Flush();

                    // Cerrar la entrada también libera a exiftool(-k).exe, que espera una tecla al salir.
                    _process.StandardInput.Close();
                    if (!_process.WaitForExit(2000)) Kill();
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
            {
                Kill();
            }
            finally
            {
                _process.Dispose();
            }
        }
    }
}
