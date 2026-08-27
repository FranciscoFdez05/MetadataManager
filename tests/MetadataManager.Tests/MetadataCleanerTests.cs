using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Xml.Linq;
using MetadataManager.Services;
using Xunit;

namespace MetadataManager.Tests;

public class MetadataCleanerTests
{
    private static CleanOptions NoExifTool => new() { UseExifTool = false };

    [Fact]
    public async Task Jpeg_metadata_is_removed_without_recompressing()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateJpegWithMetadata("foto.jpg", "SECRETO-GPS");

        Assert.Contains("SECRETO-GPS", TempWorkspace.ReadRaw(path));

        var result = await MetadataCleaner.CleanAsync(path, NoExifTool);

        Assert.Equal(CleanScope.Complete, result.Scope);
        Assert.DoesNotContain("SECRETO-GPS", TempWorkspace.ReadRaw(path));

        using var image = Image.FromFile(path);
        Assert.Equal(40, image.Width);
        Assert.Equal(30, image.Height);
    }

    [Fact]
    public async Task Png_text_chunks_are_removed()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreatePngWithText("captura.png", "Francisco-PRIVADO");

        Assert.Contains("Francisco-PRIVADO", TempWorkspace.ReadRaw(path));

        var result = await MetadataCleaner.CleanAsync(path, NoExifTool);

        Assert.Equal(CleanScope.Complete, result.Scope);
        Assert.DoesNotContain("Francisco-PRIVADO", TempWorkspace.ReadRaw(path));

        using var image = Image.FromFile(path);
        Assert.Equal(24, image.Width);
    }

    [Fact]
    public async Task Indexed_gif_is_cleaned_without_throwing()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateImage("animacion.gif", ImageFormat.Gif, Color.Green);

        using (var probe = Image.FromFile(path))
        {
            Assert.True((probe.PixelFormat & PixelFormat.Indexed) != 0);
        }

        var result = await MetadataCleaner.CleanAsync(path, NoExifTool);

        Assert.True(result.Success);

        using var cleaned = Image.FromFile(path);
        Assert.Equal(16, cleaned.Width);
    }

    [Fact]
    public async Task File_dates_are_normalised()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateJpegWithMetadata("fecha.jpg", "X");

        var options = new CleanOptions { UseExifTool = false, StandardDate = new DateTime(2001, 2, 3, 4, 5, 6) };
        await MetadataCleaner.CleanAsync(path, options);

        Assert.Equal(options.StandardDate, File.GetLastWriteTime(path));
        Assert.Equal(options.StandardDate, File.GetCreationTime(path));
    }

    [Fact]
    public async Task File_dates_are_left_alone_when_disabled()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateJpegWithMetadata("fecha.jpg", "X");

        var original = new DateTime(2015, 6, 7, 8, 9, 10);
        File.SetLastWriteTime(path, original);

        await MetadataCleaner.CleanAsync(path, new CleanOptions { UseExifTool = false, ResetFileDates = false });

        Assert.Equal(original, File.GetLastWriteTime(path));
    }

    [Fact]
    public async Task Backup_mode_keeps_a_copy_of_the_original()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateJpegWithMetadata("con-backup.jpg", "SECRETO");

        var result = await MetadataCleaner.CleanAsync(path,
            new CleanOptions { UseExifTool = false, OutputMode = CleanOutputMode.Backup });

        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));
        Assert.Contains("SECRETO", TempWorkspace.ReadRaw(result.BackupPath!));
        Assert.DoesNotContain("SECRETO", TempWorkspace.ReadRaw(path));
    }

    [Fact]
    public async Task Copy_mode_leaves_the_original_untouched()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateJpegWithMetadata("original.jpg", "SECRETO");

        var result = await MetadataCleaner.CleanAsync(path,
            new CleanOptions { UseExifTool = false, OutputMode = CleanOutputMode.Copy });

        Assert.NotEqual(path, result.OutputPath);
        Assert.EndsWith("original_limpio.jpg", result.OutputPath);
        Assert.Contains("SECRETO", TempWorkspace.ReadRaw(path));
        Assert.DoesNotContain("SECRETO", TempWorkspace.ReadRaw(result.OutputPath));
    }

    [Fact]
    public async Task Copy_mode_does_not_overwrite_an_existing_copy()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateJpegWithMetadata("imagen.jpg", "SECRETO");
        workspace.CreateText("imagen_limpio.jpg", "no me pises");

        var result = await MetadataCleaner.CleanAsync(path,
            new CleanOptions { UseExifTool = false, OutputMode = CleanOutputMode.Copy });

        Assert.Equal("no me pises", File.ReadAllText(workspace.Combine("imagen_limpio.jpg")));
        Assert.Contains("(2)", result.OutputPath);
    }

    [Fact]
    public async Task Orientation_is_preserved_when_requested()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateJpegWithMetadata("girada.jpg", "SECRETO");

        // Se inyecta una orientación 6 (90 grados) antes de limpiar.
        WriteOrientation(path, 6);
        Assert.Equal(6, MetadataCleaner.ReadOrientation(path));

        await MetadataCleaner.CleanAsync(path, new CleanOptions { UseExifTool = false, PreserveOrientation = true });

        Assert.Equal(6, MetadataCleaner.ReadOrientation(path));
        Assert.DoesNotContain("SECRETO", TempWorkspace.ReadRaw(path));
    }

    [Fact]
    public async Task Orientation_is_dropped_when_not_requested()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateJpegWithMetadata("girada.jpg", "SECRETO");
        WriteOrientation(path, 6);

        await MetadataCleaner.CleanAsync(path, new CleanOptions { UseExifTool = false, PreserveOrientation = false });

        Assert.Equal(1, MetadataCleaner.ReadOrientation(path));
    }

    [Fact]
    public async Task Office_document_properties_are_emptied_without_breaking_the_package()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateOfficeDocument("informe.docx", "Francisco Autor", "Empresa Secreta");

        var result = await MetadataCleaner.CleanAsync(path, NoExifTool);
        Assert.True(result.Success);

        using var archive = ZipFile.OpenRead(path);

        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        XNamespace dcterms = "http://purl.org/dc/terms/";
        XNamespace cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
        XNamespace ep = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";

        var core = XDocument.Load(archive.GetEntry("docProps/core.xml")!.Open());
        Assert.Equal(string.Empty, core.Descendants(dc + "creator").Single().Value);
        Assert.Equal(string.Empty, core.Descendants(cp + "lastModifiedBy").Single().Value);
        Assert.Equal("2000-01-01T00:00:00Z", core.Descendants(dcterms + "created").Single().Value);

        var app = XDocument.Load(archive.GetEntry("docProps/app.xml")!.Open());
        Assert.Equal(string.Empty, app.Descendants(ep + "Company").Single().Value);

        var custom = XDocument.Load(archive.GetEntry("docProps/custom.xml")!.Open());
        Assert.Empty(custom.Root!.Elements());

        using var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open());
        Assert.Contains("contenido intacto", reader.ReadToEnd());
    }

    [Fact]
    public async Task Pdf_information_and_xmp_are_removed()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.Combine("documento.pdf");

        using (var document = new PdfSharp.Pdf.PdfDocument())
        {
            document.AddPage();
            document.Info.Author = "Francisco";
            document.Info.Title = "Informe confidencial";
            document.Info.Subject = "Datos internos";
            document.Save(path);
        }

        var result = await MetadataCleaner.CleanAsync(path, NoExifTool);
        Assert.Equal(CleanScope.Complete, result.Scope);

        using var cleaned = PdfSharp.Pdf.IO.PdfReader.Open(path, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        Assert.Equal(string.Empty, cleaned.Info.Author);
        Assert.Equal(string.Empty, cleaned.Info.Title);
        Assert.Equal(string.Empty, cleaned.Info.Subject);
        Assert.Equal(1, cleaned.PageCount);
    }

    [Fact]
    public async Task Unsupported_format_reports_timestamps_only()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("datos.bin", "contenido");

        var result = await MetadataCleaner.CleanAsync(path, NoExifTool);

        Assert.Equal(CleanScope.TimestampsOnly, result.Scope);
    }

    [Fact]
    public async Task Missing_file_fails_gracefully()
    {
        using var workspace = new TempWorkspace();

        var result = await MetadataCleaner.CleanAsync(workspace.Combine("no-existe.jpg"), NoExifTool);

        Assert.Equal(CleanScope.Failed, result.Scope);
    }

    [Theory]
    [InlineData(1, RotateFlipType.RotateNoneFlipNone)]
    [InlineData(3, RotateFlipType.Rotate180FlipNone)]
    [InlineData(6, RotateFlipType.Rotate90FlipNone)]
    [InlineData(8, RotateFlipType.Rotate270FlipNone)]
    public void Orientation_maps_to_the_expected_rotation(int orientation, RotateFlipType expected)
    {
        Assert.Equal(expected, MetadataCleaner.GetRotation(orientation));
    }

    /// <summary>Sustituye el APP1 del archivo por uno que solo declara la orientación.</summary>
    private static void WriteOrientation(string path, int orientation)
    {
        byte[] data = File.ReadAllBytes(path);
        byte[] segment = LosslessImageStripper.BuildOrientationSegment(orientation);

        var result = new List<byte>();
        result.AddRange(data.Take(2));
        result.AddRange(segment);
        result.AddRange(data.Skip(2));

        File.WriteAllBytes(path, result.ToArray());
    }
}
