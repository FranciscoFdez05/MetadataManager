using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MetadataManager.Models;
using MetadataManager.Resources;

namespace MetadataManager.Services
{
    /// <summary>Formatos disponibles para guardar el informe de metadatos.</summary>
    public enum ExportFormat
    {
        Csv,
        Json,
        Text
    }

    /// <summary>
    /// Vuelca a disco la tabla de metadatos que se está mostrando, o la de varios archivos.
    /// Todo se escribe directamente en el archivo de destino, sin montar antes el informe
    /// completo en memoria: un lote de miles de archivos no dispara el consumo.
    /// </summary>
    public static class MetadataExporter
    {
        private const int BufferSize = 64 * 1024;
        private const string Separator = ";";

        /// <summary>El CSV lleva BOM para que Excel detecte UTF-8; el resto, no.</summary>
        private static readonly Encoding CsvEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        private static readonly Encoding PlainEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static readonly JsonWriterOptions JsonOptions = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static void Export(string destination, string sourcePath, IEnumerable<MetadataEntry> entries, ExportFormat format)
        {
            ArgumentNullException.ThrowIfNull(entries);

            switch (format)
            {
                case ExportFormat.Csv:
                    using (var writer = CreateWriter(destination, CsvEncoding))
                    {
                        writer.WriteLine(CoreStrings.ExportCsvHeader);
                        foreach (var entry in entries) WriteCsvRow(writer, null, entry);
                    }
                    break;

                case ExportFormat.Json:
                    using (var stream = CreateStream(destination))
                    using (var json = new Utf8JsonWriter(stream, JsonOptions))
                    {
                        json.WriteStartObject();
                        json.WriteString("archivo", sourcePath);
                        json.WriteString("generado", Timestamp());
                        WriteJsonProperties(json, entries);
                        json.WriteEndObject();
                    }
                    break;

                case ExportFormat.Text:
                    using (var writer = CreateWriter(destination, PlainEncoding))
                    {
                        writer.WriteLine(Format(CoreStrings.ExportTitle, sourcePath));
                        writer.WriteLine(Format(CoreStrings.ExportGenerated, Timestamp()));
                        writer.WriteLine(new string('-', 60));
                        WriteGrouped(writer, entries);
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }
        }

        /// <summary>
        /// Vuelca los metadatos de varios archivos en un único informe.
        /// </summary>
        public static void ExportBatch(
            string destination,
            IReadOnlyList<(string Path, IReadOnlyList<MetadataEntry> Entries)> files,
            ExportFormat format)
        {
            ArgumentNullException.ThrowIfNull(files);

            switch (format)
            {
                case ExportFormat.Csv:
                    using (var writer = CreateWriter(destination, CsvEncoding))
                    {
                        writer.WriteLine(CoreStrings.ExportBatchCsvHeader);

                        foreach (var (path, entries) in files)
                        {
                            foreach (var entry in entries) WriteCsvRow(writer, path, entry);
                        }
                    }
                    break;

                case ExportFormat.Json:
                    using (var stream = CreateStream(destination))
                    using (var json = new Utf8JsonWriter(stream, JsonOptions))
                    {
                        json.WriteStartObject();
                        json.WriteString("generado", Timestamp());
                        json.WriteStartArray("archivos");

                        foreach (var (path, entries) in files)
                        {
                            json.WriteStartObject();
                            json.WriteString("archivo", path);
                            WriteJsonProperties(json, entries);
                            json.WriteEndObject();
                        }

                        json.WriteEndArray();
                        json.WriteEndObject();
                    }
                    break;

                case ExportFormat.Text:
                    using (var writer = CreateWriter(destination, PlainEncoding))
                    {
                        writer.WriteLine(CoreStrings.ExportBatchTitle);
                        writer.WriteLine(Format(CoreStrings.ExportGenerated, Timestamp()));
                        writer.WriteLine(Format(CoreStrings.ExportFileCount, files.Count));

                        foreach (var (path, entries) in files)
                        {
                            writer.WriteLine();
                            writer.WriteLine(new string('=', 60));
                            writer.WriteLine(path);
                            writer.WriteLine(new string('=', 60));
                            WriteGrouped(writer, entries);
                        }
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }
        }

        /// <summary>Deduce el formato a partir de la extensión elegida por el usuario.</summary>
        public static ExportFormat FormatFromExtension(string path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => ExportFormat.Json,
            ".txt" => ExportFormat.Text,
            _ => ExportFormat.Csv
        };

        private static FileStream CreateStream(string destination) =>
            new(destination, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);

        private static StreamWriter CreateWriter(string destination, Encoding encoding) =>
            new(CreateStream(destination), encoding, BufferSize);

        private static string Format(string format, params object?[] arguments) =>
            string.Format(CultureInfo.CurrentCulture, format, arguments);

        private static string Timestamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        /// <param name="path">Columna de archivo, solo en los informes por lotes.</param>
        private static void WriteCsvRow(TextWriter writer, string? path, MetadataEntry entry)
        {
            if (path is not null)
            {
                WriteQuoted(writer, path);
                writer.Write(Separator);
            }

            WriteQuoted(writer, entry.Category);
            writer.Write(Separator);
            WriteQuoted(writer, entry.Name);
            writer.Write(Separator);
            WriteQuoted(writer, entry.Value);
            writer.WriteLine();
        }

        /// <summary>
        /// Se entrecomilla siempre: evita sorpresas con separadores, comillas y saltos de línea.
        /// </summary>
        private static void WriteQuoted(TextWriter writer, string value)
        {
            writer.Write('"');

            if (value.Contains('"', StringComparison.Ordinal)) writer.Write(value.Replace("\"", "\"\"", StringComparison.Ordinal));
            else writer.Write(value);

            writer.Write('"');
        }

        private static void WriteJsonProperties(Utf8JsonWriter json, IEnumerable<MetadataEntry> entries)
        {
            json.WriteStartArray("propiedades");

            foreach (var entry in entries)
            {
                json.WriteStartObject();
                json.WriteString("categoria", entry.Category);
                json.WriteString("propiedad", entry.Name);
                json.WriteString("valor", entry.Value);
                json.WriteEndObject();
            }

            json.WriteEndArray();
        }

        /// <summary>Escribe las propiedades agrupadas por categoría.</summary>
        private static void WriteGrouped(TextWriter writer, IEnumerable<MetadataEntry> entries)
        {
            string? currentCategory = null;

            foreach (var entry in entries)
            {
                if (!string.Equals(currentCategory, entry.Category, StringComparison.Ordinal))
                {
                    currentCategory = entry.Category;
                    writer.WriteLine();
                    writer.WriteLine("[" + currentCategory + "]");
                }

                writer.Write("  ");
                writer.Write(entry.Name);
                writer.Write(": ");
                writer.WriteLine(entry.Value);
            }
        }
    }
}
