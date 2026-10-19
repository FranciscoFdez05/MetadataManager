using System.Globalization;
using MetadataManager.Models;
using MetadataManager.Services;
using Xunit;

namespace MetadataManager.Tests;

public class LocalizationTests
{
    [Fact]
    public void Properties_follow_the_interface_language()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("nota.txt", "hola");

        IReadOnlyList<MetadataEntry> english;
        using (TestCulture.Use(TestCulture.English)) english = MetadataService.Read(path);
        var spanish = MetadataService.Read(path);

        Assert.Contains(english, entry => entry.Category == "File" && entry.Name == "Name");
        Assert.Contains(english, entry => entry.Name == "Read-only" && entry.Value is "Yes" or "No");
        Assert.Contains(spanish, entry => entry.Category == "Archivo" && entry.Name == "Nombre");
    }

    [Fact]
    public void File_types_are_described_in_the_interface_language()
    {
        using var workspace = new TempWorkspace();

        using (TestCulture.Use(TestCulture.English))
        {
            Assert.Equal("JPEG image", FileTypes.Describe("foto.jpg"));
            Assert.Equal("Folder", FileTypes.Describe(workspace.Root));
            Assert.Equal("PDF", FileTypes.Describe("informe.pdf"));
        }

        Assert.Equal("Imagen JPEG", FileTypes.Describe("foto.jpg"));
    }

    [Fact]
    public void Extension_check_accepts_names_in_any_language()
    {
        using var workspace = new TempWorkspace();
        string executable = workspace.Combine("programa.exe");
        File.WriteAllBytes(executable, new byte[] { 0x4D, 0x5A, 0, 0, 0, 0 });

        string spanish = FileTypes.DetectByContent(executable)!;
        string english;
        using (TestCulture.Use(TestCulture.English)) english = FileTypes.DetectByContent(executable)!;

        Assert.Equal("Ejecutable de Windows", spanish);
        Assert.Equal("Windows executable", english);
        Assert.False(FileTypes.ExtensionMatches("disfrazado.jpg", spanish));

        using (TestCulture.Use(TestCulture.English))
        {
            Assert.False(FileTypes.ExtensionMatches("disfrazado.jpg", english));
            Assert.True(FileTypes.ExtensionMatches("programa.dll", english));
        }
    }

    [Fact]
    public void Validation_messages_are_translated()
    {
        string spanish = MetadataEditor.Validate(MetadataEditKind.FileName, "")!;
        string english;
        using (TestCulture.Use(TestCulture.English)) english = MetadataEditor.Validate(MetadataEditKind.FileName, "")!;

        Assert.Equal("El nombre no puede estar vacío.", spanish);
        Assert.Equal("The name cannot be empty.", english);
    }

    [Fact]
    public void Every_core_string_has_an_english_translation()
    {
        var manager = Resources.CoreStrings.ResourceManager;
        var neutral = manager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false)!;
        var english = manager.GetResourceSet(TestCulture.English, createIfNotExists: true, tryParents: false)
                      ?? manager.GetResourceSet(CultureInfo.GetCultureInfo("en"), createIfNotExists: true, tryParents: false)!;

        var missing = neutral.Cast<System.Collections.DictionaryEntry>()
            .Select(entry => (string)entry.Key)
            .Where(key => english.GetString(key) is null)
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Coordinates_are_identified_by_key_not_by_name()
    {
        var entry = new MetadataEntry("Summary", "Coordinates", "40.4, -3.7", key: MetadataKeys.Coordinates);

        Assert.Equal(MetadataKeys.Coordinates, entry.WithValue("1, 2").Key);
    }
}
