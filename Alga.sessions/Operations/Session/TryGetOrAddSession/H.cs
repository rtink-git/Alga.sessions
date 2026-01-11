namespace Alga.sessions.Operations.Session.TryGetOrAddSession;

static class H
{
    public static bool Do(Context _context, string activateTokenKeyDefault, (string ActivateTokenKey, string Id, string Token) kt, string activateTokenKey)
    {
        if (_context.Store.TryGetValue(kt.Id, out _)) return true;

        if (!string.IsNullOrEmpty(activateTokenKey) && kt.ActivateTokenKey != activateTokenKeyDefault && activateTokenKey == kt.ActivateTokenKey)
            return _context.Store.TryAdd(kt.Id, new SessionStore.ValueModel { Token = $"{activateTokenKey}{kt.Token}" });

        return false;
    }
}
