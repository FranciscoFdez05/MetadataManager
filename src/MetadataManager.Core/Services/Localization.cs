using System;
using System.Globalization;
using System.Threading;

namespace MetadataManager.Services
{
    /// <summary>
    /// Selección del idioma de la interfaz. Los textos viven en Resources\Strings.resx
    /// (español, idioma neutro) y Resources\Strings.en.resx (satélite en inglés).
    /// </summary>
    public static class Localization
    {
        public const string Automatic = "auto";
        public const string Spanish = "es";
        public const string English = "en";

        /// <summary>
        /// Fija la cultura de interfaz del hilo actual y de los que se creen después.
        /// Debe llamarse antes de construir la primera ventana.
        /// </summary>
        public static void Apply(string? language)
        {
            CultureInfo? culture = language switch
            {
                Spanish => new CultureInfo(Spanish),
                English => new CultureInfo(English),
                _ => null
            };

            if (culture is null) return;

            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
    }
}
