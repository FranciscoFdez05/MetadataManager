using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text;

namespace MetadataManager.Tests;

/// <summary>
/// Carpeta temporal con utilidades para fabricar archivos de prueba.
/// Se borra al liberar la instancia.
/// </summary>
public sealed class TempWorkspace : IDisposable
{
    public TempWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "mdm-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string Combine(string name) => Path.Combine(Root, name);

    /// <summary>Crea un JPEG válido con un segmento APP1 falso que hace de metadato.</summary>
    public string CreateJpegWithMetadata(string name, string marker, int width = 40, int height = 30)
    {
        string path = Combine(name);

        using (var bitmap = new Bitmap(width, height))
        {
            using var g = Graphics.FromImage(bitmap);
            g.Clear(Color.CornflowerBlue);
            bitmap.Save(path, ImageFormat.Jpeg);
        }

        byte[] original = File.ReadAllBytes(path);
        byte[] payload = Encoding.ASCII.GetBytes("Exif\0\0" + marker);

        var segment = new List<byte>
        {
            0xFF, 0xE1, (byte)((payload.Length + 2) >> 8), (byte)((payload.Length + 2) & 0xFF)
        };
        segment.AddRange(payload);

        var result = new List<byte>();
        result.AddRange(original.Take(2));
        result.AddRange(segment);
        result.AddRange(original.Skip(2));

        File.WriteAllBytes(path, result.ToArray());
        return path;
    }

    /// <summary>Crea un PNG válido con un chunk tEXt que hace de metadato.</summary>
    public string CreatePngWithText(string name, string marker)
    {
        string path = Combine(name);

        using (var bitmap = new Bitmap(24, 24))
        {
            using var g = Graphics.FromImage(bitmap);
            g.Clear(Color.Firebrick);
            bitmap.Save(path, ImageFormat.Png);
        }

        byte[] png = File.ReadAllBytes(path);
        byte[] data = Encoding.ASCII.GetBytes("Author\0" + marker);
        byte[] body = Encoding.ASCII.GetBytes("tEXt").Concat(data).ToArray();
        uint crc = Crc32(body);

        var chunk = new List<byte>
        {
            (byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length
        };
        chunk.AddRange(body);
        chunk.AddRange(new[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc });

        const int afterHeader = 8 + 25;   // firma + chunk IHDR completo
        var result = new List<byte>();
        result.AddRange(png.Take(afterHeader));
        result.AddRange(chunk);
        result.AddRange(png.Skip(afterHeader));

        File.WriteAllBytes(path, result.ToArray());
        return path;
    }

    /// <summary>Crea un JPEG con un IFD0 real formado por etiquetas ASCII.</summary>
    public string CreateJpegWithExif(string name, params (ushort Tag, string Value)[] tags)
    {
        string path = Combine(name);

        using (var bitmap = new Bitmap(60, 40))
        {
            using var g = Graphics.FromImage(bitmap);
            g.Clear(Color.SteelBlue);
            bitmap.Save(path, ImageFormat.Jpeg);
        }

        byte[] jpeg = File.ReadAllBytes(path);
        byte[] app1 = BuildExifApp1(tags);

        File.WriteAllBytes(path, jpeg.Take(2).Concat(app1).Concat(jpeg.Skip(2)).ToArray());
        return path;
    }

    private static byte[] BuildExifApp1((ushort Tag, string Value)[] entries)
    {
        var body = new MemoryStream();
        var writer = new BinaryWriter(body);

        writer.Write(Encoding.ASCII.GetBytes("II"));
        writer.Write((ushort)0x002A);
        writer.Write(8u);
        writer.Write((ushort)entries.Length);

        int dataOffset = 8 + 2 + entries.Length * 12 + 4;
        var data = new MemoryStream();

        foreach (var (tag, value) in entries)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value + "\0");

            writer.Write(tag);
            writer.Write((ushort)2);                 // ASCII
            writer.Write((uint)bytes.Length);

            if (bytes.Length <= 4)
            {
                byte[] inline = new byte[4];
                bytes.CopyTo(inline, 0);
                writer.Write(inline);
            }
            else
            {
                writer.Write((uint)(dataOffset + data.Length));
                data.Write(bytes, 0, bytes.Length);
                if (data.Length % 2 == 1) data.WriteByte(0);
            }
        }

        writer.Write(0u);
        writer.Write(data.ToArray());
        writer.Flush();

        byte[] payload = Encoding.ASCII.GetBytes("Exif\0\0").Concat(body.ToArray()).ToArray();
        int length = payload.Length + 2;

        return new byte[] { 0xFF, 0xE1, (byte)(length >> 8), (byte)(length & 0xFF) }.Concat(payload).ToArray();
    }

    public string CreateImage(string name, ImageFormat format, Color color, int size = 16)
    {
        string path = Combine(name);

        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(color);
        }

        bitmap.Save(path, format);
        return path;
    }

    /// <summary>Crea un documento OOXML mínimo con autor, empresa y fechas.</summary>
    public string CreateOfficeDocument(string name, string author, string company)
    {
        string path = Combine(name);

        using var file = new FileStream(path, FileMode.Create);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);

        void Write(string entryName, string content)
        {
            using var writer = new StreamWriter(archive.CreateEntry(entryName).Open(), Encoding.UTF8);
            writer.Write(content);
        }

        Write("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\" />");
        Write("word/document.xml", "<document>contenido intacto</document>");
        Write("docProps/core.xml",
            "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" " +
            "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\">" +
            $"<dc:creator>{author}</dc:creator><cp:lastModifiedBy>Otro Usuario</cp:lastModifiedBy>" +
            "<dcterms:created>2024-05-05T10:00:00Z</dcterms:created></cp:coreProperties>");
        Write("docProps/app.xml",
            "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\">" +
            $"<Company>{company}</Company></Properties>");
        Write("docProps/custom.xml",
            "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/custom-properties\">" +
            "<property name=\"Secreto\"><vt>valor</vt></property></Properties>");

        return path;
    }

    public string CreateText(string name, string content)
    {
        string path = Combine(name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Lee el archivo como texto latino para buscar marcadores en binarios.</summary>
    public static string ReadRaw(string path) => File.ReadAllText(path, Encoding.Latin1);

    private static uint Crc32(byte[] data)
    {
        uint[] table = new uint[256];

        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[i] = c;
        }

        uint crc = 0xFFFFFFFFu;
        foreach (byte b in data) crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);

        return crc ^ 0xFFFFFFFFu;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Un temporal que Windows aún tiene abierto no debe hacer fallar la prueba.
        }
    }
}
