namespace Alga.sessions.Operations.CheckTokenPartMatch;

internal static class H
{
    public static bool Do(string valueToken, string token, int start, int length, int step = 1)
    {
        int matchCount = 0;
        for (int i = start; i < start + length && i < valueToken.Length && i < token.Length; i += step)
            if (valueToken[i + 64] == token[i]) matchCount++;
        return matchCount == length / step;
    }
}
