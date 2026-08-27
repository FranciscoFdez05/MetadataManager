using System.Drawing;
using System.Drawing.Imaging;
using MetadataManager.Models;
using MetadataManager.Services;
using Xunit;

namespace MetadataManager.Tests;

/// <summary>
/// Campos que se ofrecen siempre para editar con ExifTool, existan o no en el archivo.
/// </summary>
public class QuickEditFieldsTests
{
    private const ushort TagImageDescription = 0x010E;
    private const ushort TagMake = 0x010F;
    private const ushort TagModel = 0x0110;
    private const ushort TagArtist = 0x013B;
    private const ushort TagCopyright = 0x8298;

    [Fact]
    public void A_file_without_metadata_still_offers_the_editable_fields()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateImage("vacio.png", ImageFormat.Png, Color.Orange);

        var entries = MetadataService.Read(path, default, allowTagEditing: true);
        var quick = entries.Where(e => e.Category == QuickEditFields.Category).ToList();

        Assert.Equal(8, quick.Count);
        Assert.All(quick, entry => Assert.Equal(MetadataEditKind.ExifTag, entry.EditKind));
        Assert.All(quick, entry => Assert.False(string.IsNullOrEmpty(entry.EditTarget)));
    }

    [Fact]
    public void The_fields_are_hidden_when_exiftool_is_not_available()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateImage("vacio.png", ImageFormat.Png, Color.Orange);

        var entries = MetadataService.Read(path);

        Assert.DoesNotContain(entries, e => e.Category == QuickEditFields.Category);
    }

    [Theory]
    [InlineData("Título", "XMP:Title")]
    [InlineData("Autor", "XMP:Creator")]
    [InlineData("Descripción", "XMP:Description")]
    [InlineData("Copyright", "XMP:Rights")]
    [InlineData("Palabras clave", "XMP:Subject")]
    [InlineData("Comentario", "UserComment")]
    [InlineData("Fecha de captura", "AllDates")]
    [InlineData("Coordenadas", "GPSPosition")]
    public void Each_field_writes_to_its_own_tag(string name, string tag)
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateImage("vacio.png", ImageFormat.Png, Color.Orange);

        var entries = MetadataService.Read(path, default, allowTagEditing: true);
        var entry = entries.Single(e => e.Category == QuickEditFields.Category && e.Name == name);

        Assert.Equal(tag, entry.EditTarget);
    }

    [Fact]
    public void Existing_values_are_shown_in_the_fields()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateJpegWithExif("foto.jpg",
            (TagArtist, "Francisco"),
            (TagImageDescription, "Una descripcion"),
            (TagCopyright, "(c) 2024"));

        var entries = MetadataService.Read(path, default, allowTagEditing: true);

        string ValueOf(string name) =>
            entries.Single(e => e.Category == QuickEditFields.Category && e.Name == name).Value;

        Assert.Equal("Francisco", ValueOf("Autor"));
        Assert.Equal("Una descripcion", ValueOf("Descripción"));
        Assert.Equal("(c) 2024", ValueOf("Copyright"));
        Assert.Equal(string.Empty, ValueOf("Palabras clave"));
    }

    [Fact]
    public void The_raw_directories_stay_editable_too()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateJpegWithExif("foto.jpg", (TagMake, "Canon"), (TagModel, "EOS 90D"));

        var entries = MetadataService.Read(path, default, allowTagEditing: true);

        Assert.Contains(entries, e => e.Category == "Exif IFD0" && e.Name == "Make" && e.EditTarget == "Make");
        Assert.Contains(entries, e => e.Category == "Exif IFD0" && e.Name == "Model" && e.EditTarget == "Model");
    }

    [Fact]
    public void The_summary_is_informative_and_not_editable()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateJpegWithExif("foto.jpg", (TagMake, "Canon"));

        var entries = MetadataService.Read(path, default, allowTagEditing: true);

        Assert.All(entries.Where(e => e.Category == "Resumen"), entry => Assert.False(entry.IsEditable));
    }

    [Fact]
    public void Formats_that_cannot_be_read_still_offer_the_fields()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.Combine("documento.pdf");

        using (var document = new PdfSharp.Pdf.PdfDocument())
        {
            document.AddPage();
            document.Save(path);
        }

        var entries = MetadataService.Read(path, default, allowTagEditing: true);

        Assert.Equal(8, entries.Count(e => e.Category == QuickEditFields.Category));
    }
}
