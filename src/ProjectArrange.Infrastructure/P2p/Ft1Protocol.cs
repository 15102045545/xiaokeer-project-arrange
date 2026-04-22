using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProjectArrange.Infrastructure.P2p;

public static class Ft1Protocol
{
    public sealed record Offer(
        string TransferId,
        string FileName,
        long SizeBytes,
        string Sha256Hex,
        int ChunkBytes);

    public static string ToOfferLine(Offer offer) =>
        "FT1 OFFER " + JsonSerializer.Serialize(offer);

    public static bool TryParseOffer(string line, out Offer offer)
    {
        offer = default!;
        if (!line.StartsWith("FT1 OFFER ", StringComparison.Ordinal)) return false;
        try
        {
            var json = line.Substring("FT1 OFFER ".Length);
            var o = JsonSerializer.Deserialize<Offer>(json);
            if (o is null) return false;
            if (string.IsNullOrWhiteSpace(o.TransferId)) return false;
            if (string.IsNullOrWhiteSpace(o.FileName)) return false;
            if (o.SizeBytes < 0) return false;
            if (string.IsNullOrWhiteSpace(o.Sha256Hex)) return false;
            if (o.ChunkBytes <= 0) return false;
            offer = o;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string AcceptLine(string transferId, long offset) =>
        $"FT1 ACCEPT {transferId} {offset}";

    public static bool TryParseAccept(string line, out (string TransferId, long Offset) accept)
    {
        accept = default;
        if (!line.StartsWith("FT1 ACCEPT ", StringComparison.Ordinal)) return false;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4) return false;
        if (!long.TryParse(parts[3], out var offset)) return false;
        accept = (parts[2], offset);
        return true;
    }

    public static string DataLine(string transferId, long offset, int count) =>
        $"FT1 DATA {transferId} {offset} {count}";

    public static bool TryParseData(string line, out (string TransferId, long Offset, int Count) data)
    {
        data = default;
        if (!line.StartsWith("FT1 DATA ", StringComparison.Ordinal)) return false;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) return false;
        if (!long.TryParse(parts[3], out var offset)) return false;
        if (!int.TryParse(parts[4], out var count)) return false;
        if (count < 0) return false;
        data = (parts[2], offset, count);
        return true;
    }

    public static string DoneLine(string transferId, string sha256Hex) =>
        $"FT1 DONE {transferId} {sha256Hex}";

    public static bool TryParseDone(string line, out (string TransferId, string Sha256Hex) done)
    {
        done = default;
        if (!line.StartsWith("FT1 DONE ", StringComparison.Ordinal)) return false;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4) return false;
        done = (parts[2], parts[3]);
        return true;
    }

    public static string OkLine(string transferId) => $"FT1 OK {transferId}";
    public static string FailLine(string transferId, string reason) => $"FT1 FAIL {transferId} {reason}";

    public static bool TryParseOk(string line, out string transferId)
    {
        transferId = "";
        if (!line.StartsWith("FT1 OK ", StringComparison.Ordinal)) return false;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) return false;
        transferId = parts[2];
        return true;
    }

    public static bool TryParseFail(string line, out (string TransferId, string Reason) fail)
    {
        fail = default;
        if (!line.StartsWith("FT1 FAIL ", StringComparison.Ordinal)) return false;
        var parts = line.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return false;
        var reason = parts.Length == 4 ? parts[3] : "";
        fail = (parts[2], reason);
        return true;
    }

    public static async Task<string> ComputeSha256HexAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var fs = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, cancellationToken);
        return Convert.ToHexString(hash);
    }
}
