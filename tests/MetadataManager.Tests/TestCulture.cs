using System.Globalization;
using System.Runtime.CompilerServices;

namespace MetadataManager.Tests;

/// <summary>
/// Las pruebas comprueban textos en español, el idioma neutro de la aplicación. Se fija aquí
/// para que el resultado no dependa del idioma del equipo que las ejecuta (en CI, inglés).
/// </summary>
internal static class TestCulture
{
    public static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    public static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    [ModuleInitializer]
    internal static void Initialize()
    {
        CultureInfo.DefaultThreadCurrentUICulture = Spanish;
        CultureInfo.CurrentUICulture = Spanish;
    }

    /// <summary>Cambia el idioma de la interfaz del hilo actual hasta liberar el resultado.</summary>
    public static IDisposable Use(CultureInfo culture) => new Scope(culture);

    private sealed class Scope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentUICulture;

        public Scope(CultureInfo culture) => CultureInfo.CurrentUICulture = culture;

        public void Dispose() => CultureInfo.CurrentUICulture = _previous;
    }
}
