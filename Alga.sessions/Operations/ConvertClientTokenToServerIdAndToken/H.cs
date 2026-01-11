using System;

namespace Alga.sessions.Operations.ConvertClientTokenToServerIdAndToken;

static class H
{
    public static (string ActivateTokenKey, string Id, string Token)? Do(ReadOnlySpan<char> tokenClient, int sessionIdLength, byte sessionTokenLength)
    {
        if (tokenClient.Length < sessionIdLength + sessionTokenLength + 64) return null;

        var activateTokenKey = tokenClient[..64];
        var idSpan = tokenClient.Slice(64, sessionIdLength);
        var tokenSpan = tokenClient.Slice(64 + sessionIdLength, sessionTokenLength);

        return (activateTokenKey.ToString(), idSpan.ToString(), tokenSpan.ToString());
    }
}
