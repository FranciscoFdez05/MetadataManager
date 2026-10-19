using MetadataManager.Models;
using MetadataManager.Services;
using Xunit;

namespace MetadataManager.Tests;

public class FileScannerTests
{
    [Fact]
    public void Enumerates_nested_files_including_hidden_ones()
    {
        using var workspace = new TempWorkspace();
        string nested = Directory.CreateDirectory(workspace.Combine(Path.Combine("a", "b"))).FullName;

        workspace.CreateText("raiz.txt", "1");
        string deep = Path.Combine(nested, "profundo.txt");
        File.WriteAllText(deep, "2");
        string hidden = workspace.CreateText("oculto.txt", "3");
        File.SetAttributes(hidden, FileAttributes.Hidden);

        var files = FileScanner.EnumerateFiles(workspace.Root);

        Assert.Equal(3, files.Count);
        Assert.Contains(deep, files);
        Assert.Contains(hidden, files);

        File.SetAttributes(hidden, FileAttributes.Normal);
    }

    [Fact]
    public void Resolve_drops_duplicates_missing_paths_and_known_entries()
    {
        using var workspace = new TempWorkspace();
        string first = workspace.CreateText("uno.txt", "uno");
        string second = workspace.CreateText("dos.txt", "dos");
        string known = workspace.CreateText("tres.txt", "tres");

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { known };

        var result = FileScanner.Resolve(
            new[] { first, first.ToUpperInvariant(), workspace.Combine("no-existe.txt"), second, known, "\0" },
            existing);

        Assert.Equal(new[] { first, second }, result.Entries.Select(entry => entry.Path));
        Assert.Equal(2, result.Skipped);
    }

    [Fact]
    public void Resolve_reads_kind_and_size_once()
    {
        using var workspace = new TempWorkspace();
        string file = workspace.CreateText("datos.txt", "12345");

        var entries = FileScanner.Resolve(new[] { file, workspace.Root }).Entries;

        Assert.True(entries[0].IsFile);
        Assert.Equal(5, entries[0].Length);
        Assert.True(entries[1].IsDirectory);
        Assert.Equal(-1, entries[1].Length);
    }
}

public class FileEntryTests
{
    [Fact]
    public void Refresh_picks_up_size_changes()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("crece.txt", "a");

        var entry = FileEntry.TryCreate(path)!;
        Assert.Equal(1, entry.Length);

        File.WriteAllText(path, "abcdef");
        Assert.Equal(1, entry.Length);

        entry.Refresh();
        Assert.Equal(6, entry.Length);
    }

    [Fact]
    public void Kind_survives_a_rename()
    {
        using var workspace = new TempWorkspace();
        string path = workspace.CreateText("antes.txt", "x");
        var entry = FileEntry.TryCreate(path)!;

        string renamed = workspace.Combine("despues.txt");
        File.Move(path, renamed);
        entry.Path = renamed;

        Assert.True(entry.IsFile);
        Assert.True(entry.Exists);
        Assert.Equal("despues.txt", entry.DisplayName);
    }

    [Fact]
    public void Missing_paths_produce_no_entry()
    {
        using var workspace = new TempWorkspace();

        Assert.Null(FileEntry.TryCreate(workspace.Combine("fantasma.txt")));
    }
}

public class ReadManyTests
{
    [Fact]
    public void Results_keep_the_input_order_and_report_progress()
    {
        using var workspace = new TempWorkspace();
        var paths = Enumerable.Range(0, 12)
            .Select(index => workspace.CreateText($"archivo{index:00}.txt", new string('x', index + 1)))
            .ToList();

        int reported = 0;
        var progress = new SynchronousProgress(value => Interlocked.Exchange(ref reported, Math.Max(reported, value)));

        var results = MetadataService.ReadMany(paths, progress);

        Assert.Equal(paths.Count, results.Length);

        for (int index = 0; index < paths.Count; index++)
        {
            string name = results[index].Single(entry => entry.Category == "Archivo" && entry.Name == "Nombre").Value;
            Assert.Equal(Path.GetFileName(paths[index]), name);
        }

        Assert.Equal(paths.Count, reported);
    }

    [Fact]
    public void Cancellation_stops_the_batch()
    {
        using var workspace = new TempWorkspace();
        var paths = new[] { workspace.CreateText("a.txt", "a") };

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => MetadataService.ReadMany(paths, null, cancellation.Token));
    }

    /// <summary>Progress&lt;T&gt; publica en el contexto de sincronización; en las pruebas interesa el aviso inmediato.</summary>
    private sealed class SynchronousProgress(Action<int> report) : IProgress<int>
    {
        public void Report(int value) => report(value);
    }
}
