using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using MetadataManager.Services;
using Xunit;

namespace MetadataManager.Tests;

public class PreviewServiceTests
{
    [Fact]
    public void Images_are_decoded_as_a_picture()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateImage("foto.png", ImageFormat.Png, Color.Teal, size: 64);

        using var preview = PreviewService.Create(path, 32, allowExecutable: false, CancellationToken.None);

        Assert.Equal(PreviewKind.Image, preview.Kind);
        Assert.NotNull(preview.Image);
        Assert.True(preview.Image!.Width <= 32 && preview.Image.Height <= 32);
    }

    [Fact]
    public void Plain_text_files_show_their_first_lines()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("notas.txt", "primera línea\nsegunda línea");

        using var preview = PreviewService.Create(path, 240, allowExecutable: false, CancellationToken.None);

        Assert.Equal(PreviewKind.Text, preview.Kind);
        Assert.Contains("primera línea", preview.Text);
        Assert.Contains("segunda línea", preview.Text);
    }

    [Fact]
    public void Executables_wait_for_the_user_authorisation()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("instalador.exe", "no importa el contenido");

        using var preview = PreviewService.Create(path, 240, allowExecutable: false, CancellationToken.None);

        Assert.Equal(PreviewKind.Blocked, preview.Kind);
        Assert.Null(preview.Image);
    }

    [Fact]
    public void Executables_disguised_with_another_extension_are_also_blocked()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.Combine("factura.pdf");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("MZ").Concat(new byte[64]).ToArray());

        using var preview = PreviewService.Create(path, 240, allowExecutable: false, CancellationToken.None);

        Assert.Equal(PreviewKind.Blocked, preview.Kind);
    }

    [Fact]
    public void An_authorised_executable_is_no_longer_blocked()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("script.bat", "@echo off");

        using var preview = PreviewService.Create(path, 240, allowExecutable: true, CancellationToken.None);

        Assert.NotEqual(PreviewKind.Blocked, preview.Kind);
    }

    [Fact]
    public void Unknown_binary_files_fall_back_to_the_shell_icon()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.Combine("datos.bin");
        File.WriteAllBytes(path, new byte[] { 0x00, 0x01, 0x02, 0x03, 0x00, 0xFF, 0x10, 0x00 });

        using var preview = PreviewService.Create(path, 96, allowExecutable: false, CancellationToken.None);

        Assert.Equal(PreviewKind.Image, preview.Kind);
        Assert.NotNull(preview.Image);
    }

    [Fact]
    public void Missing_files_have_no_preview()
    {
        using var workspace = new TempWorkspace();

        using var preview = PreviewService.Create(workspace.Combine("no-existe.txt"), 96, allowExecutable: false, CancellationToken.None);

        Assert.Equal(PreviewKind.None, preview.Kind);
    }
}
