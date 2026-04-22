using System.Text;

namespace ProjectArrange.Infrastructure.P2p;

public static class LineWire
{
    public static async Task WriteLineAsync(Stream stream, string line, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<string?> ReadLineAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        var buf = new List<byte>(Math.Min(maxBytes, 1024));
        var one = new byte[1];

        while (buf.Count < maxBytes)
        {
            var read = await stream.ReadAsync(one, cancellationToken);
            if (read == 0)
            {
                if (buf.Count == 0) return null;
                break;
            }

            if (one[0] == (byte)'\n') break;
            if (one[0] != (byte)'\r') buf.Add(one[0]);
        }

        return Encoding.UTF8.GetString(buf.ToArray());
    }

    public static async Task ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var remaining = count;
        while (remaining > 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, remaining), cancellationToken);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
            remaining -= read;
        }
    }
}

