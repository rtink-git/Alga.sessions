using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace Alga.sessions;

internal static partial class Helpers
{
    internal static string SignWithHmacSha256(ReadOnlySpan<char> text, string secretKey)
    {
        if (text.IsEmpty) return string.Empty;

        int maxByteCount = Utf8.GetMaxByteCount(text.Length);
        byte[]? rentedBuffer = null;

        Span<byte> msgBuffer = maxByteCount <= 256 ? stackalloc byte[maxByteCount] : (rentedBuffer = BufferPool.Rent(maxByteCount));

        int actualBytes = Utf8.GetBytes(text, msgBuffer);
        Span<byte> msg = msgBuffer.Slice(0, actualBytes);

        Span<byte> hash = stackalloc byte[32];

        var secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
        using var hmac = new HMACSHA256(secretKeyBytes);
        if (!hmac.TryComputeHash(msg, hash, out _)) throw new CryptographicException("HMAC computation failed.");

        string result = ToHex(hash);

        if (rentedBuffer != null) BufferPool.Return(rentedBuffer);

        return result;
    }

    static readonly Encoding Utf8 = Encoding.UTF8;
    static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;

    static string ToHex(ReadOnlySpan<byte> data)
    {
        const string HexAlphabet = "0123456789ABCDEF";
        return string.Create(data.Length * 2, data, (span, source) =>
        {
            for (int i = 0; i < source.Length; i++)
            {
                byte b = source[i];
                span[i * 2] = HexAlphabet[b >> 4];
                span[i * 2 + 1] = HexAlphabet[b & 0xF];
            }
        });
    }
}
