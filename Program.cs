using System;
using System.Diagnostics;
using System.Windows.Forms;
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
            Application.Run(new MainForm(settings, args));
        }

        /// <summary>
        /// Muestra los errores no controlados en lugar de cerrar la aplicación sin explicación.
        /// </summary>
        private static void ReportFatalError(Exception? exception)
        {
            Debug.WriteLine(exception);

            MessageBox.Show(
                "Se ha producido un error inesperado:\n\n" + (exception?.Message ?? "Error desconocido"),
                "MetadataManager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
