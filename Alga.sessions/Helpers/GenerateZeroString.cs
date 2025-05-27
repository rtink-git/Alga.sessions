using System.Text;

namespace Alga.sessions;

internal static partial class Helpers {
    internal static string GenerateZeroString(int length) {
        var stringBuilder = new StringBuilder(length);
        for (int i = 0; i < length; i++) stringBuilder.Append(0);
        return stringBuilder.ToString();
    }
}