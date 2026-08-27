using System.Text;
using System.Text.Json;
using MetadataManager.Models;
using MetadataManager.Services;
using Xunit;

namespace MetadataManager.Tests;

public class MetadataExporterTests
{
    private static readonly List<MetadataEntry> Sample = new()
    {
        new MetadataEntry("Archivo", "Nombre", "foto;con \"comillas\".jpg"),
        new MetadataEntry("Resumen", "Coordenadas", "40.416775, -3.703790")
    };

    [Fact]
    public void Csv_quotes_every_field()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.Combine("salida.csv");

        MetadataExporter.Export(path, "origen.jpg", Sample, ExportFormat.Csv);
        string content = File.ReadAllText(path);

        Assert.Contains("Categoria;Propiedad;Valor", content);
        Assert.Contains("\"foto;con \"\"comillas\"\".jpg\"", content);
    }

    [Fact]
    public void Json_is_valid_and_complete()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.Combine("salida.json");

        MetadataExporter.Export(path, "origen.jpg", Sample, ExportFormat.Json);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("origen.jpg", document.RootElement.GetProperty("archivo").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("propiedades").GetArrayLength());
    }

    [Fact]
    public void Text_groups_by_category()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.Combine("salida.txt");

        MetadataExporter.Export(path, "origen.jpg", Sample, ExportFormat.Text);
        string content = File.ReadAllText(path);

        Assert.Contains("[Archivo]", content);
        Assert.Contains("[Resumen]", content);
    }

    [Fact]
    public void Batch_csv_includes_a_file_column()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.Combine("informe.csv");

        var files = new List<(string, IReadOnlyList<MetadataEntry>)>
        {
            ("a.jpg", Sample),
            ("b.jpg", Sample)
        };

        MetadataExporter.ExportBatch(path, files, ExportFormat.Csv);
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);

        Assert.Equal("Archivo;Categoria;Propiedad;Valor", lines[0]);
        Assert.Equal(5, lines.Count(line => line.Length > 0));
        Assert.Contains(lines, line => line.StartsWith("\"a.jpg\""));
        Assert.Contains(lines, line => line.StartsWith("\"b.jpg\""));
    }

    [Fact]
    public void Batch_json_nests_each_file()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.Combine("informe.json");

        var files = new List<(string, IReadOnlyList<MetadataEntry>)> { ("a.jpg", Sample) };
        MetadataExporter.ExportBatch(path, files, ExportFormat.Json);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var archivos = document.RootElement.GetProperty("archivos");

        Assert.Equal(1, archivos.GetArrayLength());
        Assert.Equal(2, archivos[0].GetProperty("propiedades").GetArrayLength());
    }

    [Theory]
    [InlineData("informe.json", ExportFormat.Json)]
    [InlineData("informe.txt", ExportFormat.Text)]
    [InlineData("informe.csv", ExportFormat.Csv)]
    [InlineData("informe.desconocido", ExportFormat.Csv)]
    public void Format_is_taken_from_the_extension(string name, ExportFormat expected)
    {
        Assert.Equal(expected, MetadataExporter.FormatFromExtension(name));
    }
}

public class SafeFileWriterTests
{
    [Fact]
    public void Content_is_replaced_even_on_read_only_files()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("protegido.txt", "original");
        File.SetAttributes(path, FileAttributes.ReadOnly);

        SafeFileWriter.ReplaceContents(path, Encoding.UTF8.GetBytes("nuevo"));

        Assert.Equal("nuevo", File.ReadAllText(path));
        Assert.True(File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly));

        File.SetAttributes(path, FileAttributes.Normal);
    }

    [Fact]
    public void No_temporary_files_are_left_behind()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("archivo.txt", "original");

        SafeFileWriter.ReplaceContents(path, Encoding.UTF8.GetBytes("nuevo"));

        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp"));
    }

    [Fact]
    public void A_failed_write_leaves_the_original_intact()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("archivo.txt", "original");

        Assert.Throws<InvalidOperationException>(() =>
            SafeFileWriter.ReplaceContents(path, _ => throw new InvalidOperationException("fallo simulado")));

        Assert.Equal("original", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp"));
    }
}

public class SettingsTests
{
    [Fact]
    public void Defaults_are_conservative()
    {
        var settings = new AppSettings();

        Assert.Equal(CleanOutputMode.Backup, settings.OutputMode);
        Assert.True(settings.PreserveOrientation);
        Assert.True(settings.ResetFileDates);
        Assert.Equal(new DateTime(2000, 1, 1), settings.StandardDate);
    }

    [Fact]
    public void An_invalid_date_falls_back_to_the_default()
    {
        var settings = new AppSettings { NormalizationDate = "ayer por la tarde" };

        Assert.Equal(new DateTime(2000, 1, 1), settings.StandardDate);
    }

    [Fact]
    public void Clean_options_reflect_the_settings()
    {
        var settings = new AppSettings
        {
            NormalizationDate = "2010-05-04 03:02:01",
            OutputMode = CleanOutputMode.Copy,
            PreserveOrientation = false,
            UseExifTool = false
        };

        var options = settings.ToCleanOptions();

        Assert.Equal(new DateTime(2010, 5, 4, 3, 2, 1), options.StandardDate);
        Assert.Equal(CleanOutputMode.Copy, options.OutputMode);
        Assert.False(options.PreserveOrientation);
        Assert.False(options.UseExifTool);
    }

    [Fact]
    public void Clone_is_independent()
    {
        var settings = new AppSettings { Language = "en" };
        var copy = settings.Clone();

        copy.Language = "es";

        Assert.Equal("en", settings.Language);
    }

    [Fact]
    public void Settings_survive_a_serialisation_round_trip()
    {
        var settings = new AppSettings
        {
            Language = "en",
            OutputMode = CleanOutputMode.Copy,
            SplitterDistance = 512,
            LastFolder = @"C:\fotos"
        };

        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;

        Assert.Equal("en", restored.Language);
        Assert.Equal(CleanOutputMode.Copy, restored.OutputMode);
        Assert.Equal(512, restored.SplitterDistance);
        Assert.Equal(@"C:\fotos", restored.LastFolder);
    }
}
