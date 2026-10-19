using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace MetadataManager.Services
{
    /// <summary>
    /// Limpieza de las propiedades de documentos OOXML (.docx, .xlsx, .pptx), que son
    /// contenedores ZIP con las propiedades en docProps/.
    /// </summary>
    public static class OpenXmlCleaner
    {
        private static readonly XNamespace Cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
        private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
        private static readonly XNamespace DcTerms = "http://purl.org/dc/terms/";
        private static readonly XNamespace ExtendedProperties = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";

        private const string CoreEntry = "docProps/core.xml";
        private const string AppEntry = "docProps/app.xml";
        private const string CustomEntry = "docProps/custom.xml";

        /// <summary>Vacía autor, fechas y propiedades personalizadas del documento.</summary>
        public static void Clean(string path, DateTime standardDate)
        {
            SafeFileWriter.ReplaceContents(path, temporary =>
            {
                using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var sourceZip = new ZipArchive(source, ZipArchiveMode.Read);
                using var destination = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None);
                using var destinationZip = new ZipArchive(destination, ZipArchiveMode.Create);

                foreach (var entry in sourceZip.Entries)
                {
                    XDocument? replacement = entry.FullName switch
                    {
                        CoreEntry => CleanCoreProperties(entry, standardDate),
                        AppEntry => CleanApplicationProperties(entry),
                        CustomEntry => CleanCustomProperties(entry),
                        _ => null
                    };

                    var copy = destinationZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    copy.LastWriteTime = standardDate;

                    using var output = copy.Open();

                    if (replacement is not null)
                    {
                        replacement.Save(output, SaveOptions.DisableFormatting);
                    }
                    else
                    {
                        using var input = entry.Open();
                        input.CopyTo(output);
                    }
                }
            });
        }

        private static XDocument? CleanCoreProperties(ZipArchiveEntry entry, DateTime standardDate)
        {
            var document = Load(entry);
            if (document?.Root is null) return null;

            string stamp = standardDate.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

            Blank(document, Dc + "creator");
            Blank(document, Dc + "title");
            Blank(document, Dc + "subject");
            Blank(document, Dc + "description");
            Blank(document, Cp + "lastModifiedBy");
            Blank(document, Cp + "keywords");
            Blank(document, Cp + "category");
            Blank(document, Cp + "contentStatus");
            Blank(document, Cp + "revision", "1");
            Remove(document, Cp + "lastPrinted");

            SetOrAddTimestamp(document, DcTerms + "created", stamp);
            SetOrAddTimestamp(document, DcTerms + "modified", stamp);

            return document;
        }

        private static XDocument? CleanApplicationProperties(ZipArchiveEntry entry)
        {
            var document = Load(entry);
            if (document?.Root is null) return null;

            Blank(document, ExtendedProperties + "Company");
            Blank(document, ExtendedProperties + "Manager");
            Blank(document, ExtendedProperties + "TotalTime", "0");

            return document;
        }

        private static XDocument? CleanCustomProperties(ZipArchiveEntry entry)
        {
            var document = Load(entry);
            if (document?.Root is null) return null;

            // Se conserva el elemento raíz (las relaciones del paquete apuntan a él)
            // pero se eliminan todas las propiedades definidas por el usuario.
            document.Root.RemoveNodes();
            return document;
        }

        private static XDocument? Load(ZipArchiveEntry entry)
        {
            try
            {
                using var stream = entry.Open();
                return XDocument.Load(stream);
            }
            catch (System.Xml.XmlException)
            {
                // Si la parte está dañada se copia tal cual en lugar de romper el documento.
                return null;
            }
        }

        private static void Blank(XDocument document, XName name, string value = "")
        {
            foreach (var element in document.Descendants(name).ToList())
            {
                element.Value = value;
            }
        }

        private static void Remove(XDocument document, XName name)
        {
            foreach (var element in document.Descendants(name).ToList())
            {
                element.Remove();
            }
        }

        private static void SetOrAddTimestamp(XDocument document, XName name, string value)
        {
            var element = document.Descendants(name).FirstOrDefault();

            if (element is not null)
            {
                element.Value = value;
                return;
            }

            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
            document.Root?.Add(new XElement(name, new XAttribute(xsi + "type", "dcterms:W3CDTF"), value));
        }
    }
}
