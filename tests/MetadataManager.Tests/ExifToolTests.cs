using MetadataManager.Services;
using Xunit;

namespace MetadataManager.Tests;

/// <summary>
/// Conexión manual con la herramienta externa. Las pruebas nunca dejan una ruta
/// configurada: siempre restauran la detección automática al terminar.
/// </summary>
public class ExifToolTests : IDisposable
{
    public void Dispose() => ExifTool.Configure(null);

    [Fact]
    public void Connecting_to_a_missing_path_is_rejected()
    {
        using var workspace = new TempWorkspace();

        bool connected = ExifTool.TryConnect(workspace.Combine("no-existe.exe"), out string detail);

        Assert.False(connected);
        Assert.NotEmpty(detail);
    }

    [Fact]
    public void Connecting_to_an_empty_path_is_rejected()
    {
        Assert.False(ExifTool.TryConnect(string.Empty, out _));
        Assert.False(ExifTool.TryConnect("   ", out _));
    }

    [Fact]
    public void A_file_that_is_not_a_program_is_rejected()
    {
        using var workspace = new TempWorkspace();
        string fake = workspace.CreateText("exiftool.exe", "esto no es un ejecutable");

        bool connected = ExifTool.TryConnect(fake, out string detail);

        Assert.False(connected);
        Assert.NotEmpty(detail);
        Assert.NotEqual(fake, ExifTool.Locate());
    }

    [Fact]
    public void A_rejected_path_does_not_stay_configured()
    {
        using var workspace = new TempWorkspace();
        string fake = workspace.CreateText("exiftool.exe", "no soy un programa");

        ExifTool.TryConnect(fake, out _);

        Assert.Null(ExifTool.Version);
        Assert.False(ExifTool.IsAvailable && ExifTool.Locate() == fake);
    }

    [Fact]
    public void Configuring_null_restores_automatic_detection()
    {
        ExifTool.Configure(@"C:\ruta\inventada\exiftool.exe");
        Assert.Null(ExifTool.Locate());

        ExifTool.Configure(null);

        // Sin ExifTool instalado el resultado es null; con él, la ruta encontrada en el PATH.
        Assert.Equal(ExifTool.IsAvailable, ExifTool.Locate() is not null);
    }

    [Fact]
    public void Version_is_only_set_when_a_tool_is_connected()
    {
        ExifTool.Configure(@"C:\ruta\inventada\exiftool.exe");
        ExifTool.Locate();

        Assert.Null(ExifTool.Version);
    }
}
