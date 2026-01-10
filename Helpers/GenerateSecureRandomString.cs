using System.Security.Cryptography;

namespace Alga.sessions;
internal static partial class Helpers { 
    internal static string GenerateSecureRandomString(int length) {
        try {
            if (length <= 0) return string.Empty;

            byte[] randomBytes = new byte[length];
            RandomNumberGenerator.Fill(randomBytes);
            return Convert.ToBase64String(randomBytes).TrimEnd('=').Substring(0, length);
        } catch { return string.Empty; }
    }
}
