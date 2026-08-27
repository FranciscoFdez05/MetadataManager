using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Linq;
using System.Text.Json;
using MetadataManager.Models;

namespace MetadataManager.Services
{
    /// <summary>Formatos disponibles para guardar el informe de metadatos.</summary>
    public enum ExportFormat
    {
        Csv,
        Json,
        Text
    }

    /// <summary>Vuelca a disco la tabla de metadatos que se está mostrando.</summary>
    public static class MetadataExporter
    {
        public static void Export(string destination, string sourcePath, IEnumerable<MetadataEntry> entries, ExportFormat format)
        {
            switch (format)
            {
                case ExportFormat.Csv:
                    File.WriteAllText(destination, BuildCsv(entries), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    break;

                case ExportFormat.Json:
                    File.WriteAllText(destination, BuildJson(sourcePath, entries), new UTF8Encoding(false));
                    break;

                case ExportFormat.Text:
                    File.WriteAllText(destination, BuildText(sourcePath, entries), new UTF8Encoding(false));
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
            switch (format)
            {
                case ExportFormat.Csv:
                    File.WriteAllText(destination, BuildBatchCsv(files), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    break;

                case ExportFormat.Json:
                    File.WriteAllText(destination, BuildBatchJson(files), new UTF8Encoding(false));
                    break;

                case ExportFormat.Text:
                    File.WriteAllText(destination, BuildBatchText(files), new UTF8Encoding(false));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }
        }

        private static string BuildBatchCsv(IReadOnlyList<(string Path, IReadOnlyList<MetadataEntry> Entries)> files)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Archivo;Categoria;Propiedad;Valor");

            foreach (var (path, entries) in files)
            {
                foreach (var entry in entries)
                {
                    builder.Append(Quote(path)).Append(';')
                           .Append(Quote(entry.Category)).Append(';')
                           .Append(Quote(entry.Name)).Append(';')
                           .Append(Quote(entry.Value)).AppendLine();
                }
            }

            return builder.ToString();
        }

        private static string BuildBatchJson(IReadOnlyList<(string Path, IReadOnlyList<MetadataEntry> Entries)> files)
        {
            var payload = new
            {
                generado = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                archivos = files.Select(file => new
                {
                    archivo = file.Path,
                    propiedades = Project(file.Entries)
                }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            return JsonSerializer.Serialize(payload, options);
        }

        private static string BuildBatchText(IReadOnlyList<(string Path, IReadOnlyList<MetadataEntry> Entries)> files)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Informe de metadatos");
            builder.AppendLine("Generado: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            builder.AppendLine($"Archivos: {files.Count}");

            foreach (var (path, entries) in files)
            {
                builder.AppendLine();
                builder.AppendLine(new string('=', 60));
                builder.AppendLine(path);
                builder.AppendLine(new string('=', 60));
                AppendGrouped(builder, entries);
            }

            return builder.ToString();
        }

        /// <summary>Deduce el formato a partir de la extensión elegida por el usuario.</summary>
        public static ExportFormat FormatFromExtension(string path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => ExportFormat.Json,
            ".txt" => ExportFormat.Text,
            _ => ExportFormat.Csv
        };

        private static string BuildCsv(IEnumerable<MetadataEntry> entries)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Categoria;Propiedad;Valor");

            foreach (var entry in entries)
            {
                builder.Append(Quote(entry.Category)).Append(';')
                       .Append(Quote(entry.Name)).Append(';')
                       .Append(Quote(entry.Value)).AppendLine();
            }

            return builder.ToString();
        }

        private static string Quote(string value)
        {
            // Se entrecomilla siempre: evita sorpresas con separadores, comillas y saltos de línea.
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string BuildJson(string sourcePath, IEnumerable<MetadataEntry> entries)
        {
            var payload = new
            {
                archivo = sourcePath,
                generado = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                propiedades = Project(entries)
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            return JsonSerializer.Serialize(payload, options);
        }

        private static List<object> Project(IEnumerable<MetadataEntry> entries)
        {
            var list = new List<object>();

            foreach (var entry in entries)
            {
                list.Add(new { categoria = entry.Category, propiedad = entry.Name, valor = entry.Value });
            }

            return list;
        }

        private static string BuildText(string sourcePath, IEnumerable<MetadataEntry> entries)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Metadatos de: " + sourcePath);
            builder.AppendLine("Generado: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            builder.AppendLine(new string('-', 60));

            AppendGrouped(builder, entries);
            return builder.ToString();
        }

        /// <summary>Escribe las propiedades agrupadas por categoría.</summary>
        private static void AppendGrouped(StringBuilder builder, IEnumerable<MetadataEntry> entries)
        {
            string? currentCategory = null;

            foreach (var entry in entries)
            {
                if (!string.Equals(currentCategory, entry.Category, StringComparison.Ordinal))
                {
                    currentCategory = entry.Category;
                    builder.AppendLine();
                    builder.AppendLine("[" + currentCategory + "]");
                }

                builder.AppendLine($"  {entry.Name}: {entry.Value}");
            }
        }
    }
}
