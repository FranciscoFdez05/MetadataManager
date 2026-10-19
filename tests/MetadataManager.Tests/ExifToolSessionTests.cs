using System.Diagnostics;
using System.Drawing.Imaging;
using MetadataManager.Services;
using Xunit;
using Xunit.Abstractions;

namespace MetadataManager.Tests;

/// <summary>
/// Prueba que solo se ejecuta si la variable de entorno EXIFTOOL_PATH apunta a un ExifTool real.
/// </summary>
public sealed class ExifToolFactAttribute : FactAttribute
{
    public static string? Executable
    {
        get
        {
            string? path = Environment.GetEnvironmentVariable("EXIFTOOL_PATH");
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
        }
    }

    public ExifToolFactAttribute()
    {
        if (Executable is null) Skip = "Define EXIFTOOL_PATH con la ruta de exiftool.exe para ejecutar esta prueba.";
    }
}

/// <summary>Las pruebas que tocan la configuración global de ExifTool no pueden ir en paralelo.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ExifToolCollection
{
    public const string Name = "ExifTool";
}

[Collection(ExifToolCollection.Name)]
public class ExifToolSessionTests(ITestOutputHelper output) : IDisposable
{
    public void Dispose()
    {
        ExifTool.Configure(null);
        ExifTool.Shutdown();
    }

    [ExifToolFact]
    public async Task A_session_runs_several_commands_in_one_process()
    {
        using var workspace = new TempWorkspace();
        string image = workspace.CreateImage("foto.jpg", ImageFormat.Jpeg, System.Drawing.Color.Teal);

        using var session = ExifToolSession.Start(ExifToolFactAttribute.Executable!);

        var write = await session.ExecuteAsync(new[] { "-overwrite_original", "-XMP:Title=Primera", image }, TimeSpan.FromSeconds(30), default);
        var read = await session.ExecuteAsync(new[] { "-s3", "-XMP:Title", image }, TimeSpan.FromSeconds(30), default);

        Assert.True(write.Success, write.Message);
        Assert.True(read.Success, read.Message);
        Assert.Equal("Primera", read.Message);
        Assert.True(session.IsHealthy);
    }

    [ExifToolFact]
    public async Task A_failed_command_reports_its_error_and_keeps_the_session_usable()
    {
        using var workspace = new TempWorkspace();
        using var session = ExifToolSession.Start(ExifToolFactAttribute.Executable!);

        var missing = await session.ExecuteAsync(new[] { "-XMP:Title", workspace.Combine("no-existe.jpg") }, TimeSpan.FromSeconds(30), default);
        var version = await session.ExecuteAsync(new[] { "-ver" }, TimeSpan.FromSeconds(30), default);

        Assert.False(missing.Success);
        Assert.Contains("not found", missing.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(version.Success);
        Assert.Matches(@"^\d+\.\d+", version.Message);
    }

    [ExifToolFact]
    public async Task Paths_with_non_ascii_characters_are_handled()
    {
        using var workspace = new TempWorkspace();
        string image = workspace.CreateImage("niño ñandú 写真.jpg", ImageFormat.Jpeg, System.Drawing.Color.Olive);

        Assert.True(ExifTool.TryConnect(ExifToolFactAttribute.Executable!, out _));
        var result = await ExifTool.WriteTagAsync(ExifTool.Locate()!, image, "XMP:Title", "Año 2026");

        Assert.True(result.Success, result.Message);
    }

    [ExifToolFact]
    public async Task Values_with_line_breaks_fall_back_to_a_separate_process()
    {
        using var workspace = new TempWorkspace();
        string image = workspace.CreateImage("multilinea.jpg", ImageFormat.Jpeg, System.Drawing.Color.Navy);

        Assert.True(ExifTool.TryConnect(ExifToolFactAttribute.Executable!, out _));
        var result = await ExifTool.WriteTagAsync(ExifTool.Locate()!, image, "XMP:Description", "línea 1\nlínea 2");

        Assert.True(result.Success, result.Message);
    }

    [ExifToolFact]
    public async Task Repeated_edits_are_much_faster_than_one_process_per_command()
    {
        using var workspace = new TempWorkspace();
        string image = workspace.CreateImage("rapida.jpg", ImageFormat.Jpeg, System.Drawing.Color.Maroon);
        Assert.True(ExifTool.TryConnect(ExifToolFactAttribute.Executable!, out _));
        string executable = ExifTool.Locate()!;

        // La primera orden arranca el proceso persistente; no se cuenta.
        await ExifTool.WriteTagAsync(executable, image, "XMP:Title", "calentamiento");

        const int edits = 10;
        var watch = Stopwatch.StartNew();
        for (int index = 0; index < edits; index++)
        {
            var result = await ExifTool.WriteTagAsync(executable, image, "XMP:Title", $"edición {index}");
            Assert.True(result.Success, result.Message);
        }
        var persistent = watch.Elapsed;

        // Referencia: un proceso por orden, como antes.
        watch.Restart();
        for (int index = 0; index < edits; index++)
        {
            using var process = Process.Start(ExifTool.CreateStartInfo(executable, "-overwrite_original", $"-XMP:Title=lento {index}", image))!;
            process.StandardInput.Close();
            await process.WaitForExitAsync();
        }
        var separate = watch.Elapsed;

        output.WriteLine($"{edits} ediciones: persistente {persistent.TotalMilliseconds:0} ms, un proceso por orden {separate.TotalMilliseconds:0} ms");
        Assert.True(persistent < separate, $"persistente {persistent}, separado {separate}");
    }
}
