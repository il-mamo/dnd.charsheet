using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace DnD.CharSheet.Infrastructure;

/// <summary>Bounded structural/decompression validation for managed 8-bit RGB/RGBA PNGs.</summary>
internal static class BackupPngValidator
{
    public static void Validate(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length is < 57 or > ManagedCharacterPortraits.MaximumBytes) throw Invalid();
        var bytes = new byte[(int)file.Length];
        file.ReadExactly(bytes);
        if (!bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) throw Invalid();
        var offset = 8;
        var width = 0;
        var height = 0;
        var channels = 0;
        var sawData = false;
        var endedData = false;
        var sawEnd = false;
        using var compressed = new MemoryStream();
        while (offset < bytes.Length)
        {
            if (bytes.Length - offset < 12) throw Invalid();
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            if (length > bytes.Length - offset - 12) throw Invalid();
            var count = (int)length;
            var type = bytes.AsSpan(offset + 4, 4);
            if (type.ToArray().Any(b => b is not (>= 65 and <= 90) and not (>= 97 and <= 122)) || (type[2] & 32) != 0) throw Invalid();
            var payload = bytes.AsSpan(offset + 8, count);
            var crc = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8 + count, 4));
            if (Crc(bytes.AsSpan(offset + 4, count + 4)) != crc) throw Invalid();
            var name = Encoding.ASCII.GetString(type);
            if (offset == 8 && name != "IHDR") throw Invalid();
            switch (name)
            {
                case "IHDR":
                    if (offset != 8 || count != 13) throw Invalid();
                    width = unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(payload[..4]));
                    height = unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4)));
                    if (width is < 1 or > 4096 || height is < 1 or > 4096 || (long)width * height > 16_000_000 || payload[8] != 8 || payload[9] is not (2 or 6) || payload[10] != 0 || payload[11] != 0 || payload[12] != 0) throw Invalid();
                    channels = payload[9] == 2 ? 3 : 4;
                    break;
                case "IDAT":
                    if (endedData || sawEnd) throw Invalid();
                    sawData = true;
                    compressed.Write(payload);
                    break;
                case "IEND":
                    if (!sawData || count != 0 || offset + 12 != bytes.Length) throw Invalid();
                    sawEnd = true;
                    break;
                case "PLTE":
                    if (sawData || count is < 3 or > 768 || count % 3 != 0) throw Invalid();
                    break;
                default:
                    if ((type[0] & 32) == 0) throw Invalid();
                    if (sawData) endedData = true;
                    break;
            }
            offset += count + 12;
        }
        if (!sawEnd) throw Invalid();
        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        var row = new byte[checked(width * channels)];
        try
        {
            for (var y = 0; y < height; y++)
            {
                var filter = zlib.ReadByte();
                if (filter is < 0 or > 4) throw Invalid();
                zlib.ReadExactly(row);
            }
            if (zlib.ReadByte() != -1) throw Invalid();
        }
        catch (EndOfStreamException) { throw Invalid(); }
    }
    private static uint Crc(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xEDB88320u : 0u);
        }
        return ~crc;
    }
    private static InvalidDataException Invalid() => new("Portrait is not a supported complete PNG image.");
}

