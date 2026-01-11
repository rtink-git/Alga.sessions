using System.Collections.Concurrent;

namespace Alga.sessions.Operations.Session.Delete;

internal static class H
{
    public static bool Do(string session, Context context)
    {
        if (string.IsNullOrEmpty(session)) return false;

        var span = session.AsSpan();
        int lastColonIndex = span.LastIndexOf(':');

        if (lastColonIndex == -1 || lastColonIndex == span.Length - 1) return false;

        var clientTokenSpan = span[(lastColonIndex + 1)..];
        var kt = Operations.ConvertClientTokenToServerIdAndToken.H.Do(clientTokenSpan, context.Settings.SessionIdLength, context.Settings.SessionTokenLength);

        if (kt == null) return false;

        return context.Store.TryRemove(kt.Value.Id, out _);
    }
}
