namespace Alga.sessions.Operations.GetClientToken;

internal static class H
{
    public static string Do(string id, string token, byte sessionTokenLength)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(token)) return string.Empty;

        var length = Random.Shared.Next(10, sessionTokenLength);
        var tksub = Operations.SecureRandomString.H.Do(length);

        return $"{id}{token}{tksub}";
    }
}
