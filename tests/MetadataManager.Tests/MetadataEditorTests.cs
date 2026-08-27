using MetadataManager.Models;
using MetadataManager.Services;
using Xunit;

namespace MetadataManager.Tests;

public class MetadataEditorTests
{
    [Fact]
    public void Renaming_returns_the_new_path()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("viejo.txt", "contenido");

        string renamed = MetadataEditor.Apply(path, MetadataEditKind.FileName, "nuevo.txt");

        Assert.True(File.Exists(renamed));
        Assert.False(File.Exists(path));
        Assert.EndsWith("nuevo.txt", renamed);
    }

    [Fact]
    public void Renaming_over_an_existing_file_fails_without_destroying_it()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("origen.txt", "origen");
        string other = workspace.CreateText("destino.txt", "destino");

        Assert.Throws<IOException>(() => MetadataEditor.Apply(path, MetadataEditKind.FileName, "destino.txt"));

        Assert.Equal("destino", File.ReadAllText(other));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Renaming_with_invalid_characters_is_rejected()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("archivo.txt", "x");

        Assert.Throws<ArgumentException>(() => MetadataEditor.Apply(path, MetadataEditKind.FileName, "a<b>c"));
    }

    [Fact]
    public void Dates_are_written_to_the_file_system()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("fechas.txt", "x");

        MetadataEditor.Apply(path, MetadataEditKind.LastWriteTime, "1999-12-31 23:58:57");
        MetadataEditor.Apply(path, MetadataEditKind.CreationTime, "1999-12-30 10:00:00");

        Assert.Equal(new DateTime(1999, 12, 31, 23, 58, 57), File.GetLastWriteTime(path));
        Assert.Equal(new DateTime(1999, 12, 30, 10, 0, 0), File.GetCreationTime(path));
    }

    [Fact]
    public void Attributes_are_written_to_the_file_system()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("atributos.txt", "x");

        MetadataEditor.Apply(path, MetadataEditKind.Attributes, "ReadOnly");

        Assert.True(File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly));
        File.SetAttributes(path, FileAttributes.Normal);
    }

    [Fact]
    public void Last_access_time_is_written()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("acceso.txt", "x");

        MetadataEditor.Apply(path, MetadataEditKind.LastAccessTime, "2005-03-04 05:06:07");

        Assert.Equal(new DateTime(2005, 3, 4, 5, 6, 7), File.GetLastAccessTime(path));
    }

    [Fact]
    public void The_read_only_flag_toggles_without_losing_other_attributes()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("marca.txt", "x");
        File.SetAttributes(path, FileAttributes.Hidden);

        MetadataEditor.Apply(path, MetadataEditKind.ReadOnlyFlag, "Sí");
        var attributes = File.GetAttributes(path);
        Assert.True(attributes.HasFlag(FileAttributes.ReadOnly));
        Assert.True(attributes.HasFlag(FileAttributes.Hidden));

        MetadataEditor.Apply(path, MetadataEditKind.ReadOnlyFlag, "No");
        attributes = File.GetAttributes(path);
        Assert.False(attributes.HasFlag(FileAttributes.ReadOnly));
        Assert.True(attributes.HasFlag(FileAttributes.Hidden));

        File.SetAttributes(path, FileAttributes.Normal);
    }

    [Fact]
    public void Editing_the_path_moves_the_file_to_another_folder()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("origen.txt", "contenido");
        string folder = Path.Combine(workspace.Root, "destino");
        Directory.CreateDirectory(folder);

        string moved = MetadataEditor.Apply(path, MetadataEditKind.FullPath, Path.Combine(folder, "movido.txt"));

        Assert.False(File.Exists(path));
        Assert.True(File.Exists(moved));
        Assert.Equal("contenido", File.ReadAllText(moved));
    }

    [Fact]
    public void Editing_the_path_to_a_missing_folder_is_rejected()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("origen.txt", "contenido");

        Assert.Throws<DirectoryNotFoundException>(() =>
            MetadataEditor.Apply(path, MetadataEditKind.FullPath, Path.Combine(workspace.Root, "no-existe", "x.txt")));

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Editing_the_path_never_overwrites_an_existing_file()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("origen.txt", "origen");
        string other = workspace.CreateText("ocupado.txt", "no me pises");

        Assert.Throws<IOException>(() => MetadataEditor.Apply(path, MetadataEditKind.FullPath, other));

        Assert.Equal("no me pises", File.ReadAllText(other));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Rewriting_the_same_path_is_a_no_op()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("igual.txt", "x");

        Assert.Equal(path, MetadataEditor.Apply(path, MetadataEditKind.FullPath, path));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Editing_a_missing_file_throws()
    {
        using var workspace = new TempWorkspace();

        Assert.Throws<FileNotFoundException>(() =>
            MetadataEditor.Apply(workspace.Combine("fantasma.txt"), MetadataEditKind.FileName, "otro.txt"));
    }

    [Theory]
    [InlineData(MetadataEditKind.FileName, "   ", false)]
    [InlineData(MetadataEditKind.FileName, "valido.txt", true)]
    [InlineData(MetadataEditKind.FileName, "a<b>.txt", false)]
    [InlineData(MetadataEditKind.CreationTime, "32 de mayo", false)]
    [InlineData(MetadataEditKind.CreationTime, "2020-02-29 12:00:00", true)]
    [InlineData(MetadataEditKind.LastWriteTime, "2020-13-01 00:00:00", false)]
    [InlineData(MetadataEditKind.Attributes, "Invisible", false)]
    [InlineData(MetadataEditKind.Attributes, "ReadOnly", true)]
    [InlineData(MetadataEditKind.LastAccessTime, "2020-01-01 00:00:00", true)]
    [InlineData(MetadataEditKind.LastAccessTime, "el martes", false)]
    [InlineData(MetadataEditKind.ReadOnlyFlag, "Sí", true)]
    [InlineData(MetadataEditKind.ReadOnlyFlag, "No", true)]
    [InlineData(MetadataEditKind.ReadOnlyFlag, "Yes", true)]
    [InlineData(MetadataEditKind.ReadOnlyFlag, "quizá", false)]
    [InlineData(MetadataEditKind.FullPath, "", false)]
    [InlineData(MetadataEditKind.FullPath, "relativa.txt", true)]
    public void Validation_accepts_only_usable_values(MetadataEditKind kind, string value, bool valid)
    {
        string? error = MetadataEditor.Validate(kind, value);

        Assert.Equal(valid, error is null);
    }

    [Theory]
    [InlineData("40.416775, -3.703790", true)]
    [InlineData("", true)]
    [InlineData("40.416775", false)]
    [InlineData("120.0, 0.0", false)]
    [InlineData("norte, sur", false)]
    public void Coordinate_validation_checks_the_pair(string value, bool valid)
    {
        string? error = MetadataEditor.Validate(MetadataEditKind.ExifTag, value, ExifWritableTags.GpsPositionTag);

        Assert.Equal(valid, error is null);
    }

    [Fact]
    public void Other_exif_tags_accept_free_text()
    {
        Assert.Null(MetadataEditor.Validate(MetadataEditKind.ExifTag, "Cualquier cosa", "Artist"));
    }

    [Fact]
    public void Read_only_files_still_accept_new_timestamps()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("protegido.txt", "x");
        File.SetAttributes(path, FileAttributes.ReadOnly);

        var date = new DateTime(2000, 1, 1);
        MetadataEditor.SetFileTimes(path, date);

        Assert.Equal(date, File.GetLastWriteTime(path));
        Assert.True(File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly));

        File.SetAttributes(path, FileAttributes.Normal);
    }

    [Fact]
    public async Task Writing_tags_without_exiftool_reports_the_reason()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateJpegWithMetadata("foto.jpg", "X");

        if (ExifTool.IsAvailable) return;   // Con ExifTool instalado esta ruta no aplica.

        await Assert.ThrowsAsync<InvalidOperationException>(() => MetadataEditor.ApplyTagAsync(path, "Artist", "Yo"));
    }

    [Theory]
    [InlineData("Exif IFD0", "Artist", "Artist")]
    [InlineData("Exif IFD0", "Windows XP Author", "XPAuthor")]
    [InlineData("Exif SubIFD", "Date/Time Original", "DateTimeOriginal")]
    [InlineData("Exif SubIFD", "Lens Model", "LensModel")]
    [InlineData("Exif SubIFD", "ISO Speed Ratings", "ISO")]
    [InlineData("GPS", "GPS Processing Method", "GPSProcessingMethod")]
    [InlineData("IPTC", "Caption/Abstract", "Caption-Abstract")]
    [InlineData("IPTC", "Province/State", "Province-State")]
    [InlineData("IPTC", "Writer/Editor", "Writer-Editor")]
    [InlineData("XMP", "dc:title", "XMP-dc:Title")]
    [InlineData("XMP", "photoshop:Credit", "XMP-photoshop:Credit")]
    public void Writable_tags_map_to_their_exiftool_name(string directory, string tag, string expected)
    {
        Assert.True(ExifWritableTags.TryGetTag(directory, tag, out string actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("JPEG", "Image Width")]              // no es una etiqueta escribible
    [InlineData("Exif IFD0", "Orientation")]         // se muestra descrita, no en bruto
    [InlineData("Exif SubIFD", "Exif Version")]
    [InlineData("XMP", "dc:subject[1]")]             // elemento de lista
    [InlineData("XMP", "sinDosPuntos")]
    public void Non_writable_tags_are_left_alone(string directory, string tag)
    {
        Assert.False(ExifWritableTags.TryGetTag(directory, tag, out _));
    }

    [Fact]
    public void Free_text_exif_tags_fall_back_to_a_derived_name()
    {
        Assert.True(ExifWritableTags.TryGetFreeTextTag("Exif IFD0", "Some Custom Note", "hola", "hola", out string tag));
        Assert.Equal("SomeCustomNote", tag);
    }

    [Fact]
    public void The_fallback_ignores_values_that_are_reformatted_for_display()
    {
        // MetadataExtractor decora el valor: devolverlo tal cual fallaría al escribir.
        Assert.False(ExifWritableTags.TryGetFreeTextTag("Exif SubIFD", "Exposure Time", 0.008, "1/125 sec", out _));
        Assert.False(ExifWritableTags.TryGetFreeTextTag("Exif SubIFD", "F-Number", "2.8", "f/2.8", out _));
        Assert.False(ExifWritableTags.TryGetFreeTextTag("JPEG", "Comment", "hola", "hola", out _));
    }

    [Theory]
    [InlineData("foto.jpg", true)]
    [InlineData("documento.pdf", true)]
    [InlineData("hoja.xlsx", false)]
    [InlineData("datos.bin", false)]
    public void Tag_writing_support_depends_on_the_format(string name, bool supported)
    {
        Assert.Equal(supported, ExifWritableTags.SupportsWriting(name));
    }
}
