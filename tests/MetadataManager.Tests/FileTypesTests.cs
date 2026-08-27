using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using MetadataManager.Services;
using Xunit;

namespace MetadataManager.Tests;

public class FileTypesTests
{
    [Theory]
    [InlineData("foto.jpg", "Imagen JPEG")]
    [InlineData("hoja.xlsx", "Excel")]
    [InlineData("documento.pdf", "PDF")]
    [InlineData("archivo.desconocido", "DESCONOCIDO")]
    public void Extensions_are_described(string name, string expected)
    {
        Assert.Equal(expected, FileTypes.Describe(name));
    }

    [Fact]
    public void Folders_are_described_as_folders()
    {
        using var workspace = new TempWorkspace();

        Assert.Equal("Carpeta", FileTypes.Describe(workspace.Root));
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(2048, "2 KB")]
    [InlineData(0, "0 B")]
    [InlineData(-1, "")]
    public void Compact_sizes_are_readable(long bytes, string expected)
    {
        Assert.Equal(expected, FileTypes.FormatCompactSize(bytes));
    }

    [Fact]
    public void Full_size_includes_the_exact_byte_count()
    {
        Assert.Contains("1.048.576", FileTypes.FormatSize(1024 * 1024).Replace(',', '.'));
    }

    [Fact]
    public void Png_content_is_detected_regardless_of_extension()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateImage("mentira.txt", ImageFormat.Png, Color.Green);

        Assert.Equal("Png", FileTypes.DetectByContent(path));
        Assert.False(FileTypes.ExtensionMatches(path, "Png"));
    }

    [Fact]
    public void Pdf_content_is_detected()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.Combine("documento.pdf");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("%PDF-1.7\n%contenido"));

        Assert.Equal("PDF", FileTypes.DetectByContent(path));
        Assert.True(FileTypes.ExtensionMatches(path, "PDF"));
    }

    [Fact]
    public void Office_documents_are_told_apart_from_plain_zips()
    {
        using var workspace = new TempWorkspace();
        string document = workspace.CreateOfficeDocument("informe.docx", "Autor", "Empresa");

        Assert.Equal("Word (OOXML)", FileTypes.DetectByContent(document));
        Assert.True(FileTypes.ExtensionMatches(document, "Word (OOXML)"));
    }

    [Fact]
    public void Unknown_content_returns_null()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("notas.txt", "solo texto plano");

        Assert.Null(FileTypes.DetectByContent(path));
    }

    [Fact]
    public void Unknown_types_never_raise_a_mismatch_warning()
    {
        Assert.True(FileTypes.ExtensionMatches("archivo.xyz", "Formato exótico"));
    }

    [Fact]
    public void Image_and_office_helpers_agree_with_the_extension()
    {
        Assert.True(FileTypes.IsImage("foto.JPEG"));
        Assert.False(FileTypes.IsImage("documento.pdf"));
        Assert.True(FileTypes.IsOpenXml("informe.docx"));
        Assert.False(FileTypes.IsOpenXml("informe.doc"));
    }
}

public class LosslessImageStripperTests
{
    [Fact]
    public void Orientation_segment_has_the_expected_shape()
    {
        byte[] segment = LosslessImageStripper.BuildOrientationSegment(6);

        Assert.Equal(36, segment.Length);
        Assert.Equal(0xFF, segment[0]);
        Assert.Equal(0xE1, segment[1]);
        Assert.Equal("Exif", Encoding.ASCII.GetString(segment, 4, 4));
        Assert.Equal(6, segment[28]);
    }

    [Fact]
    public void Orientation_values_are_clamped_to_the_valid_range()
    {
        Assert.Equal(1, LosslessImageStripper.BuildOrientationSegment(0)[28]);
        Assert.Equal(8, LosslessImageStripper.BuildOrientationSegment(99)[28]);
    }

    [Fact]
    public void Non_image_content_is_left_to_other_strategies()
    {
        Assert.Null(LosslessImageStripper.Strip(Encoding.ASCII.GetBytes("no soy una imagen")));
    }

    [Fact]
    public void Jpeg_and_png_signatures_are_recognised()
    {
        using var workspace = new TempWorkspace();
        byte[] jpeg = File.ReadAllBytes(workspace.CreateImage("a.jpg", ImageFormat.Jpeg, Color.Red));
        byte[] png = File.ReadAllBytes(workspace.CreateImage("a.png", ImageFormat.Png, Color.Red));

        Assert.True(LosslessImageStripper.IsJpeg(jpeg));
        Assert.True(LosslessImageStripper.IsPng(png));
        Assert.False(LosslessImageStripper.IsPng(jpeg));
    }
}
