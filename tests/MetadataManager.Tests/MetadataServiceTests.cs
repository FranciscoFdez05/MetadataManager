using System.Drawing;
using System.Drawing.Imaging;
using MetadataManager.Models;
using MetadataManager.Services;
using Xunit;

namespace MetadataManager.Tests;

public class MetadataServiceTests
{
    [Fact]
    public void File_properties_are_reported()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateJpegWithMetadata("foto.jpg", "X");

        var entries = MetadataService.Read(path);

        Assert.Contains(entries, e => e.DisplayName == "Archivo - Nombre" && e.Value == "foto.jpg");
        Assert.Contains(entries, e => e.DisplayName == "Resumen - Resolución" && e.Value == "40 x 30 px");
        Assert.Contains(entries, e => e.Category == "JPEG");
    }

    [Fact]
    public void Property_keys_are_unique()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateJpegWithMetadata("foto.jpg", "X");

        var entries = MetadataService.Read(path);

        Assert.Equal(entries.Count, entries.Select(e => e.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Only_writable_properties_are_editable_without_exiftool()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateJpegWithMetadata("foto.jpg", "X");

        var entries = MetadataService.Read(path);

        Assert.All(entries.Where(e => e.IsEditable), entry =>
            Assert.Contains(entry.Name, new[]
            {
                "Nombre", "Ruta", "Fecha de creación", "Fecha de modificación",
                "Último acceso", "Atributos", "Solo lectura"
            }));
    }

    [Fact]
    public void Content_type_is_detected_and_mismatches_are_reported()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateImage("disfrazada.txt", ImageFormat.Png, Color.Blue);

        var entries = MetadataService.Read(path);

        Assert.Contains(entries, e => e.DisplayName == "Archivo - Tipo real (contenido)" && e.Value == "Png");
        Assert.Contains(entries, e => e.DisplayName == "Archivo - Aviso");
    }

    [Fact]
    public void Matching_extension_produces_no_warning()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateImage("correcta.png", ImageFormat.Png, Color.Blue);

        var entries = MetadataService.Read(path);

        Assert.DoesNotContain(entries, e => e.DisplayName == "Archivo - Aviso");
    }

    [Fact]
    public void Directories_report_their_contents()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateText("uno.txt", "1");
        workspace.CreateText("dos.txt", "2");

        var entries = MetadataService.Read(workspace.Root);

        Assert.Contains(entries, e => e.DisplayName == "Contenido - Ficheros" && e.Value == "2");
    }

    [Fact]
    public void Missing_paths_do_not_throw()
    {
        using var workspace = new TempWorkspace();

        var entries = MetadataService.Read(workspace.Combine("no-existe.txt"));

        Assert.Single(entries);
    }

    [Fact]
    public void Sha256_matches_the_known_vector()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("hash.txt", "abc");

        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            MetadataService.ComputeSha256(path));
    }

    [Fact]
    public void Sha256_honours_cancellation()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("grande.bin", new string('x', 400_000));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => MetadataService.ComputeSha256(path, cancellation.Token));
    }

    [Fact]
    public void File_system_properties_expose_their_edit_kind()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateJpegWithMetadata("foto.jpg", "X");

        var entries = MetadataService.Read(path);

        MetadataEditKind KindOf(string name) => entries.First(e => e.Name == name).EditKind;

        Assert.Equal(MetadataEditKind.FileName, KindOf("Nombre"));
        Assert.Equal(MetadataEditKind.FullPath, KindOf("Ruta"));
        Assert.Equal(MetadataEditKind.LastAccessTime, KindOf("Último acceso"));
        Assert.Equal(MetadataEditKind.ReadOnlyFlag, KindOf("Solo lectura"));
        Assert.Equal(MetadataEditKind.ReadOnly, KindOf("Tamaño"));
    }

    [Fact]
    public void Directories_share_the_same_editable_properties()
    {
        using var workspace = new TempWorkspace();

        var entries = MetadataService.Read(workspace.Root);

        Assert.Contains(entries, e => e.Name == "Ruta" && e.EditKind == MetadataEditKind.FullPath);
        Assert.Contains(entries, e => e.Name == "Último acceso" && e.EditKind == MetadataEditKind.LastAccessTime);
    }

    [Fact]
    public void Entry_display_name_joins_category_and_name()
    {
        var entry = new MetadataEntry("Exif IFD0", "Make", "Canon");

        Assert.Equal("Exif IFD0 - Make", entry.DisplayName);
        Assert.False(entry.IsEditable);
        Assert.Equal("Nikon", entry.WithValue("Nikon").Value);
    }
}
