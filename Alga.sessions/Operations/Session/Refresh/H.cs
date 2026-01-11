namespace Alga.sessions.Operations.Session.Refresh;

static class H
{
    public static string? Do(Context context, ReadOnlySpan<char> session, string? clientKey)
    {
        if (session.IsEmpty) return null;

        int lastColonIndex = session.LastIndexOf(':');
        if (lastColonIndex < 0 || lastColonIndex == session.Length - 1) return null;

        var sessionClientPartSpan = session[..lastColonIndex];
        var clientTokenSpan = session[(lastColonIndex + 1)..];

        var kt = Operations.ConvertClientTokenToServerIdAndToken.H.Do(clientTokenSpan, context.Settings.SessionIdLength, context.Settings.SessionTokenLength);
        if (kt == null) return null;

        var dts = Operations.GetDTUNowShort.H.Do();
        string activateTokenKey = Operations.ComputeActivateTokenKey.H.Do(sessionClientPartSpan, kt.Value.Id, clientKey, dts, context.Settings.ActivateTokenKeyDefault, context.Settings.SecretKey);

        if (!Operations.Session.TryGetOrAddSession.H.Do(context, context.Settings.ActivateTokenKeyDefault, kt.Value, activateTokenKey)) return null;

        if (!context.Store.TryGetValue(kt.Value.Id, out var val)) return null;

        if (val.Token != $"{kt.Value.ActivateTokenKey}{kt.Value.Token}") { Operations.Session.TryInvalidateSession.H.Do(val, clientTokenSpan.ToString(), context.Settings.SessionIdLength, context.Settings.SessionTokenLength, context.Settings.SessionTokenHalfLength, context); return null; }

        bool needsRefresh = DateTime.UtcNow > val.Dt.AddMinutes(context.Settings.SessionRefreshIntervalInMin);
        if (!needsRefresh && !Operations.Session.IsOutdated.H.Do(val, context.Settings.SessionLifetimeInMin, context.Settings.SessionMaxNumberOfErrors)) return session.ToString();

        if (Operations.Session.IsOutdated.H.Do(val, context.Settings.SessionLifetimeInMin, context.Settings.SessionMaxNumberOfErrors)) { context.Store.TryRemove(kt.Value.Id, out _); return null; }

        var tknew = Operations.SecureRandomString.H.Do(context.Settings.SessionTokenLength);
        val.Token = $"{activateTokenKey}{tknew}";
        val.Dt = DateTime.UtcNow;

        return string.Concat(sessionClientPartSpan.ToString(), ":", $"{activateTokenKey}{Operations.GetClientToken.H.Do(kt.Value.Id, tknew, context.Settings.SessionTokenLength)}");
    }
}
