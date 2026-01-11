namespace Alga.sessions.Operations.Session.TryInvalidateSession;

static class H
{
    public static bool Do(SessionStore.ValueModel value, string tokenClient, int sessionIdLength, byte sessionTokenLength, byte sessionTokenHalfLength, Context context)
    {
        if (tokenClient.Length != sessionIdLength + sessionTokenLength + 64) return false;

        var kt = Operations.ConvertClientTokenToServerIdAndToken.H.Do(tokenClient.AsSpan(), sessionIdLength, sessionTokenLength);
        if (kt == null) return false;

        var token = kt.Value.Token;
        if (token.Length != sessionTokenLength || value.Token.Length != sessionTokenLength + 64)
            return false;

        // Check token parts
        if (Operations.CheckTokenPartMatch.H.Do(value.Token, token, 0, sessionTokenHalfLength) ||
            Operations.CheckTokenPartMatch.H.Do(value.Token, token, sessionTokenHalfLength, sessionTokenHalfLength) ||
            Operations.CheckTokenPartMatch.H.Do(value.Token, token, 0, sessionTokenLength, 2))
        {
            context.Store.TryRemove(kt.Value.Id, out _);
            return true;
        }

        value.NumberOfErrors++;
        return false;
    }
}
