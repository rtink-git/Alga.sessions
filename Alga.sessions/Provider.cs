using System.Collections.Concurrent;

namespace Alga.sessions;

public class Provider
{
    const byte SessionIdLength = 32; // Guid length
    const byte SessionTokenLength = 64;

    const string ActivateTokenKeyDefault = "0000000000000000000000000000000000000000000000000000000000000000";
    protected readonly ConcurrentDictionary<string, Models.ValueModel> List = new();
    readonly Models.Config _config;
    readonly int _sessionTokenHalfLength;

    public Provider(Models.Config? config)
    {
        _config = config ?? new();
        _sessionTokenHalfLength = SessionIdLength / 2;
    }

    public string? Create(ReadOnlySpan<char> session = default, string? clientKey = null)
    {
        try
        {
            var id = Helpers.GenerateSecureRandomString(SessionIdLength);
            var token = Helpers.GenerateSecureRandomString(SessionTokenLength);

            var dts = DateTime.UtcNow.ToString("yyyyMMdd");
            string tokenHidden = ComputeTokenHidden(session, id, clientKey);
            string activateTokenKey = ComputeActivateTokenKey(session, id, clientKey, dts);

            if (!List.TryAdd(id, new Models.ValueModel { Token = $"{activateTokenKey}{token}", TokenHidden = tokenHidden })) return null;

            return string.Concat(session, ":", $"{activateTokenKey}{GetClientToken(id, token)}");
        }
        catch { return null; }
    }

    public bool Check(string session, string? clientKey = null)
    {
        if (string.IsNullOrEmpty(session)) return false;

        try
        {
            var span = session.AsSpan();
            int lastColonIndex = span.LastIndexOf(':');
            if (lastColonIndex < 0) return false;

            var sessionClientPartSpan = span[..lastColonIndex];
            var clientTokenSpan = span[(lastColonIndex + 1)..];

            var kt = ConvertClientTokenToServerIdAndToken(clientTokenSpan);
            if (kt == null || !List.TryGetValue(kt.Value.Id, out var val)) return false;

            string tokenHidden = ComputeTokenHidden(sessionClientPartSpan, kt.Value.Id, clientKey);

            if (val.Token == $"{kt.Value.ActivateTokenKey}{kt.Value.Token}" && val.TokenHidden == tokenHidden)
            {
                if (!IsOutdated(val)) return true;

                List.TryRemove(kt.Value.Id, out _);
            }
            else { TryInvalidateSession(val, clientTokenSpan.ToString()); }
        }
        catch { }

        return false;
    }

    public string? Refresh(ReadOnlySpan<char> session, string? clientKey = null)
    {
        if (session.IsEmpty) return null;

        int lastColonIndex = session.LastIndexOf(':');
        if (lastColonIndex < 0 || lastColonIndex == session.Length - 1) return null;

        var sessionClientPartSpan = session[..lastColonIndex];
        var clientTokenSpan = session[(lastColonIndex + 1)..];

        var kt = ConvertClientTokenToServerIdAndToken(clientTokenSpan);
        if (kt == null) return null;

        var dts = DateTime.UtcNow.ToString("yyyyMMdd");
        string activateTokenKey = ComputeActivateTokenKey(sessionClientPartSpan, kt.Value.Id, clientKey, dts);
        string tokenHidden = ComputeTokenHidden(sessionClientPartSpan, kt.Value.Id, clientKey);

        if (!TryGetOrAddSession(kt.Value, activateTokenKey, tokenHidden)) return null;

        if (!List.TryGetValue(kt.Value.Id, out var val)) return null;

        if (val.Token != $"{kt.Value.ActivateTokenKey}{kt.Value.Token}") { TryInvalidateSession(val, clientTokenSpan.ToString()); return null; }

        bool needsRefresh = DateTime.UtcNow > val.Dt.AddMinutes(_config.SessionRefreshIntervalInMin);
        if (!needsRefresh && !IsOutdated(val)) return session.ToString();

        if (val.TokenHidden != tokenHidden) { TryInvalidateSession(val, clientTokenSpan.ToString()); return null; }

        if (IsOutdated(val)) { List.TryRemove(kt.Value.Id, out _); return null; }

        var tknew = Helpers.GenerateSecureRandomString(SessionTokenLength);
        val.Token = $"{activateTokenKey}{tknew}";
        val.Dt = DateTime.UtcNow;

        return string.Concat(sessionClientPartSpan.ToString(), ":", $"{activateTokenKey}{GetClientToken(kt.Value.Id, tknew)}");
    }


    public bool Delete(string session)
    {
        if (string.IsNullOrEmpty(session)) return false;

        var span = session.AsSpan();
        int lastColonIndex = span.LastIndexOf(':');

        if (lastColonIndex == -1 || lastColonIndex == span.Length - 1) return false;

        var clientTokenSpan = span[(lastColonIndex + 1)..];
        var kt = ConvertClientTokenToServerIdAndToken(clientTokenSpan);

        if (kt == null) return false;

        return List.TryRemove(kt.Value.Id, out _);
    }

    private bool CheckTokenPartMatch(string valueToken, string token, int start, int length, int step = 1)
    {
        int matchCount = 0;
        for (int i = start; i < start + length && i < valueToken.Length && i < token.Length; i += step)
            if (valueToken[i + 64] == token[i]) matchCount++;
        return matchCount == length / step;
    }

    string ComputeTokenHidden(ReadOnlySpan<char> session, string id, string? clientKey)
    {
        if (session.IsEmpty) return string.Empty;

        return Helpers.SignWithHmacSha256($"{session}:{id}", $"{_config.SecretKey}{id}{clientKey ?? string.Empty}");
    }

    string ComputeActivateTokenKey(ReadOnlySpan<char> session, string id, string? clientKey, string date) => string.IsNullOrEmpty(clientKey) ? ActivateTokenKeyDefault : Helpers.SignWithHmacSha256($"{session}:{id}", $"{_config.SecretKey}{id}{clientKey}{date}");

    (string ActivateTokenKey, string Id, string Token)? ConvertClientTokenToServerIdAndToken(ReadOnlySpan<char> tokenClient)
    {
        if (tokenClient.Length < SessionIdLength + SessionTokenLength + 64) return null;

        var activateTokenKey = tokenClient[..64];
        var idSpan = tokenClient.Slice(64, SessionIdLength);
        var tokenSpan = tokenClient.Slice(64 + SessionIdLength, SessionTokenLength);

        return (activateTokenKey.ToString(), idSpan.ToString(), tokenSpan.ToString());
    }

    bool IsOutdated(Models.ValueModel value) => DateTime.UtcNow > value.Dt.AddMinutes(_config.SessionLifetimeInMin) || value.NumberOfErrors > _config.SessionMaxNumberOfErrors;

    string GetClientToken(string id, string token)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(token)) return string.Empty;

        var length = Random.Shared.Next(10, SessionTokenLength);
        var tksub = Helpers.GenerateSecureRandomString(length);

        return $"{id}{token}{tksub}";
    }

    bool TryInvalidateSession(Models.ValueModel value, string tokenClient)
    {
        if (tokenClient.Length != SessionIdLength + SessionTokenLength + 64) return false;

        var kt = ConvertClientTokenToServerIdAndToken(tokenClient.AsSpan());
        if (kt == null) return false;

        var token = kt.Value.Token;
        if (token.Length != SessionTokenLength || value.Token.Length != SessionTokenLength + 64)
            return false;

        // Check token parts
        if (CheckTokenPartMatch(value.Token, token, 0, _sessionTokenHalfLength) ||
            CheckTokenPartMatch(value.Token, token, _sessionTokenHalfLength, _sessionTokenHalfLength) ||
            CheckTokenPartMatch(value.Token, token, 0, SessionTokenLength, step: 2))
        {
            List.TryRemove(kt.Value.Id, out _);
            return true;
        }

        value.NumberOfErrors++;
        return false;
    }

    bool TryGetOrAddSession((string ActivateTokenKey, string Id, string Token) kt, string activateTokenKey, string tokenHidden)
    {
        if (List.TryGetValue(kt.Id, out _)) return true;

        if (!string.IsNullOrEmpty(activateTokenKey) && kt.ActivateTokenKey != ActivateTokenKeyDefault && activateTokenKey == kt.ActivateTokenKey)
            return List.TryAdd(kt.Id, new Models.ValueModel { Token = $"{activateTokenKey}{kt.Token}", TokenHidden = tokenHidden });

        return false;
    }
}