namespace Alga.sessions.Operations.Session.Create;

internal static class H // byte sessionTokenLength, string activateTokenKeyDefault, string secretKey
{
    public static string? Do(Context context, ReadOnlySpan<char> session = default, string? clientKey = null)
    {
        try
        {
            var id = Guid.NewGuid().ToString();
            var token = Operations.SecureRandomString.H.Do(context.Settings.SessionTokenLength);
            var dts = Operations.GetDTUNowShort.H.Do();

            string activateTokenKey = Operations.ComputeActivateTokenKey.H.Do(session, id, clientKey, dts, context.Settings.ActivateTokenKeyDefault, context.Settings.SecretKey);

            if (!context.Store.TryAdd(id, new SessionStore.ValueModel { Token = $"{activateTokenKey}{token}" })) return null;

            return string.Concat(session, ":", $"{activateTokenKey}{Operations.GetClientToken.H.Do(id, token, context.Settings.SessionTokenLength)}");
        }
        catch { return null; }
    }
}
