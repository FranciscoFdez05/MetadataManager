using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace MetadataManager.Services
{
    /// <summary>
    /// Limpieza de las propiedades de un documento PDF: el diccionario /Info
    /// (autor, título, aplicación de origen, fechas) y el bloque XMP del catálogo.
    /// </summary>
    public static class PdfCleaner
    {
        private static readonly string[] InfoKeys =
        {
            "/Author", "/Title", "/Subject", "/Keywords", "/Creator", "/Producer"
        };

        public static void Clean(string path, DateTime standardDate)
        {
            // Se trabaja en memoria para no mantener el archivo abierto mientras se reemplaza.
            byte[] output;

            using (var source = new MemoryStream(File.ReadAllBytes(path)))
            using (var document = PdfReader.Open(source, PdfDocumentOpenMode.Modify))
            {
                // Se eliminan también las claves privadas que algunas aplicaciones añaden a /Info.
                foreach (string key in document.Info.Elements.Keys.ToList())
                {
                    document.Info.Elements.Remove(key);
                }

                foreach (string key in InfoKeys)
                {
                    document.Info.Elements.SetString(key, string.Empty);
                }

                document.Info.CreationDate = standardDate;
                document.Info.ModificationDate = standardDate;

                // El XMP duplica los metadatos en XML y sobrevive a la limpieza de /Info.
                document.Internals.Catalog.Elements.Remove("/Metadata");

                using var buffer = new MemoryStream();
                document.Save(buffer, closeStream: false);
                output = buffer.ToArray();
            }

            SafeFileWriter.ReplaceContents(path, output);
        }

    }
}
