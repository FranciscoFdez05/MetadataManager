using System;
using System.Diagnostics;
using System.Windows.Forms;
using MetadataManager.Resources;
using MetadataManager.Services;

namespace MetadataManager
{
    internal static class Program
    {
        /// <summary>Punto de entrada de la aplicación.</summary>
        [STAThread]
        private static void Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ReportFatalError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportFatalError(e.ExceptionObject as Exception);

            var settings = SettingsService.Load();

            // El idioma debe fijarse antes de crear cualquier ventana.
            Localization.Apply(settings.Language);
            ExifTool.Configure(settings.ExifToolPath);

            ApplicationConfiguration.Initialize();

            try
            {
                Application.Run(new MainForm(settings, args));
            }
            finally
            {
                // ExifTool queda abierto entre órdenes para no pagar su arranque cada vez.
                ExifTool.Shutdown();
            }
        }

        /// <summary>
        /// Muestra los errores no controlados en lugar de cerrar la aplicación sin explicación.
        /// </summary>
        private static void ReportFatalError(Exception? exception)
        {
            Debug.WriteLine(exception);

            MessageBox.Show(
                $"{Strings.ErrorUnexpected}:\n\n{exception?.Message ?? Strings.ErrorUnknown}",
                "MetadataManager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
