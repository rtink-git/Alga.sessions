using System.Collections.Concurrent;

namespace Alga.sessions.Operations.Session.Check;

static class H
{
    public static bool Do(Context context, string session, string? clientKey = null)
    {
        if (string.IsNullOrEmpty(session)) return false;

        try
        {
            var span = session.AsSpan();
            int lastColonIndex = span.LastIndexOf(':');
            if (lastColonIndex < 0) return false;

            var sessionClientPartSpan = span[..lastColonIndex];
            var clientTokenSpan = span[(lastColonIndex + 1)..];

            var kt = Operations.ConvertClientTokenToServerIdAndToken.H.Do(clientTokenSpan, session.Length, context.Settings.SessionTokenLength);
            if (kt == null || !context.Store.TryGetValue(kt.Value.Id, out var val)) return false;

            if (val.Token == $"{kt.Value.ActivateTokenKey}{kt.Value.Token}")
            {
                if (!Operations.Session.IsOutdated.H.Do(val, context.Settings.SessionLifetimeInMin, context.Settings.SessionMaxNumberOfErrors)) return true;

                context.Store.TryRemove(kt.Value.Id, out _);
            }
            else { Operations.Session.TryInvalidateSession.H.Do(val, clientTokenSpan.ToString(), context.Settings.SessionIdLength, context.Settings.SessionTokenLength, context.Settings.SessionTokenHalfLength, context); }
        }
        catch { }

        return false;
    }
}
