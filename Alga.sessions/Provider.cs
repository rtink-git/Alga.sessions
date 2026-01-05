using System.Collections.Concurrent;

namespace Alga.sessions;

public class Provider
{
    const string ActivateTokenKeyDefault = "0000000000000000000000000000000000000000000000000000000000000000";
    protected readonly ConcurrentDictionary<string, Models.ValueModel> List = new();
    readonly Models.Config _config;
    readonly int _sessionTokenHalfLength;

    public Provider(Models.Config? config)
    {
        _config = config ?? new();
        _sessionTokenHalfLength = _config.SessionIdLength / 2;
    }

    public string? Create(ReadOnlySpan<char> session = default, string? clientKey = null)
    {
        try
        {
            var id = Helpers.GenerateSecureRandomString(_config.SessionIdLength);
            var token = Helpers.GenerateSecureRandomString(_config.SessionTokenLength);

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

        var tknew = Helpers.GenerateSecureRandomString(_config.SessionTokenLength);
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
        if (tokenClient.Length < _config.SessionIdLength + _config.SessionTokenLength + 64) return null;

        var activateTokenKey = tokenClient[..64];
        var idSpan = tokenClient.Slice(64, _config.SessionIdLength);
        var tokenSpan = tokenClient.Slice(64 + _config.SessionIdLength, _config.SessionTokenLength);

        return (activateTokenKey.ToString(), idSpan.ToString(), tokenSpan.ToString());
    }

    bool IsOutdated(Models.ValueModel value) => DateTime.UtcNow > value.Dt.AddMinutes(_config.SessionLifetimeInMin) || value.NumberOfErrors > _config.SessionMaxNumberOfErrors;

    string GetClientToken(string id, string token)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(token)) return string.Empty;

        var length = Random.Shared.Next(10, _config.SessionTokenLength);
        var tksub = Helpers.GenerateSecureRandomString(length);

        return $"{id}{token}{tksub}";
    }

    bool TryInvalidateSession(Models.ValueModel value, string tokenClient)
    {
        if (tokenClient.Length != _config.SessionIdLength + _config.SessionTokenLength + 64) return false;

        var kt = ConvertClientTokenToServerIdAndToken(tokenClient.AsSpan());
        if (kt == null) return false;

        var token = kt.Value.Token;
        if (token.Length != _config.SessionTokenLength || value.Token.Length != _config.SessionTokenLength + 64)
            return false;

        // Check token parts
        if (CheckTokenPartMatch(value.Token, token, 0, _sessionTokenHalfLength) ||
            CheckTokenPartMatch(value.Token, token, _sessionTokenHalfLength, _sessionTokenHalfLength) ||
            CheckTokenPartMatch(value.Token, token, 0, _config.SessionTokenLength, step: 2))
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


// using System.Collections.Concurrent;


// namespace Alga.sessions;

// public class Provider
// {
//     protected readonly ConcurrentDictionary<string, Models.ValueModel> List = new();
//     readonly Models.Config _Config;
//     readonly int SessionTokenHalfLength;
//     readonly string activateTokenKeyDefault = "0000000000000000000000000000000000000000000000000000000000000000";
//     public Provider(Models.Config? config)
//     {
//         _Config = config ?? new();
//         SessionTokenHalfLength = _Config.SessionIdLength / 2;
//     }

//     /// <summary>
//     /// Creates a new secure session token based on optional client-specific parameters.
//     /// </summary>
//     /// <param name="session">
//     /// Optional client data in the format <c>{client_param_1}:{client_param_2}</c>,
//     /// such as <c>user_id</c> or <c>role_id</c>. This can influence both server behavior
//     /// (e.g. access control) and client UI logic.
//     /// </param>
//     /// <returns>
//     /// A combined session string in the format <c>{client_data}:{session_token}</c>,
//     /// or <c>null</c> if session creation failed.
//     /// </returns>
//     public string? Create(ReadOnlySpan<char> session = default, string? clientKey = null)
//     {
//         try
//         {
//             var id = Helpers.GenerateSecureRandomString(_Config.SessionIdLength);
//             var token = Helpers.GenerateSecureRandomString(_Config.SessionTokenLength);

//             var dts = DateTime.UtcNow.ToString("yyyyMMdd");
//             string tokenHidden = Helpers.SignWithHmacSha256($"{session}:{id}", $"{_Config.SecretKey}{id}{clientKey}");
//             string activateTokenKey = string.IsNullOrEmpty(clientKey) ? activateTokenKeyDefault : Helpers.SignWithHmacSha256($"{session}:{id}", $"{_Config.SecretKey}{id}{clientKey}{dts}");

//             if (!List.TryAdd(id, new Models.ValueModel { Token = $"{activateTokenKey}{token}", TokenHidden = tokenHidden }))
//                 return null;

//             return string.Concat(session, ":", $"{activateTokenKey}{_GetClientToken(id, token)}");
//         }
//         catch { return null; }
//     }

//     public bool Check(string session, string? clientKey = null)
//     {
//         if (string.IsNullOrEmpty(session)) return false;

//         try
//         {
//             var span = session.AsSpan();
//             int lastColonIndex = span.LastIndexOf(':');
//             if (lastColonIndex < 0) return false;

//             var sessionClientPartSpan = span.Slice(0, lastColonIndex);
//             string tokenSubHidden = sessionClientPartSpan.ToString();

//             var clientTokenSpan = span.Slice(lastColonIndex + 1);

//             var kt = _ConvertClientTokenToServerIdAndToken(clientTokenSpan);
//             if (kt == null || !List.TryGetValue(kt.Value.Id, out var val)) return false;

//             string tokenHidden = tokenSubHidden.Length > 0 ? Helpers.SignWithHmacSha256($"{tokenSubHidden}:{kt.Value.Id}", $"{_Config.SecretKey}{kt.Value.Id}{clientKey}") : string.Empty;

//             if (val.Token == $"{kt.Value.activateTokenKey}{kt.Value.Token}" && val.TokenHidden == tokenHidden)
//             {
//                 if (!_IsOutdate(val) && val.ToLog != 2) return true;
//                 val.ToLog = 2;
//             }
//             else _TryKill(val, clientTokenSpan.ToString());
//         }
//         catch { }
        
//         return false;
//     }

//     public string? Refresh(ReadOnlySpan<char> session, string? clientKey = null)
//     {
//         // 1. Быстрые проверки входа
//         if (session.IsEmpty) return null;

//         int lastColonIndex = session.LastIndexOf(':');
//         if (lastColonIndex < 0 || lastColonIndex == session.Length - 1) return null;

//         // 2. Разделяем сессию без аллокаций
//         ReadOnlySpan<char> sessionClientPartSpan = session.Slice(0, lastColonIndex);
//         ReadOnlySpan<char> clientTokenSpan = session.Slice(lastColonIndex + 1);


//         // 3. Проверяем токен и дату ДО вычисления HMAC
//         var kt = _ConvertClientTokenToServerIdAndToken(clientTokenSpan);

//         if (kt == null) return null;

//         var dts = DateTime.UtcNow.ToString("yyyyMMdd");
//         string activateTokenKey = string.IsNullOrEmpty(clientKey) ? activateTokenKeyDefault : Helpers.SignWithHmacSha256($"{sessionClientPartSpan}:{kt.Value.Id}", $"{_Config.SecretKey}{kt.Value.Id}{clientKey}{dts}");
//         string tokenSubHidden = sessionClientPartSpan.ToString();
//         string tokenHidden = tokenSubHidden.Length > 0  ? Helpers.SignWithHmacSha256($"{tokenSubHidden}:{kt.Value.Id}", $"{_Config.SecretKey}{kt.Value.Id}{clientKey}") : string.Empty;

//         if(!List.TryGetValue(kt.Value.Id, out var valx))
//         {
//             // пробуем добавить ключ в базу данных
//             if (!string.IsNullOrEmpty(clientKey) && kt.Value.activateTokenKey != activateTokenKeyDefault && activateTokenKey == kt.Value.activateTokenKey)
//             {
//                 if (!List.TryAdd(kt.Value.Id, new Models.ValueModel { Token = $"{activateTokenKey}{kt.Value.Token}", TokenHidden = tokenHidden }))
//                     return null;
//             }
//             else return null;
//         }

//         if(!List.TryGetValue(kt.Value.Id, out var val)) return null;

//         if (val.Token != $"{kt.Value.activateTokenKey}{kt.Value.Token}")
//         {
//             _TryKill(val, clientTokenSpan.ToString());
//             return null;
//         }

//         // 4. Проверяем срок действия без лишних вычислений
//         bool needsRefresh = DateTime.UtcNow > val.Dt.AddMinutes(_Config.SessionRefreshIntervalInMin);
//         if (!needsRefresh && !_IsOutdate(val) && val.ToLog != 2)
//             return new string(session); // Аллокация только здесь

//         // 5. Только если нужно — вычисляем HMAC


//         if (val.TokenHidden != tokenHidden)
//         {
//             _TryKill(val, clientTokenSpan.ToString());
//             return null;
//         }

//         if (_IsOutdate(val) || val.ToLog == 2)
//         {
//             val.ToLog = 2;
//             return null;
//         }

//         // 6. Обновляем токен
//         val.Token =  $"{activateTokenKey}{Helpers.GenerateSecureRandomString(_Config.SessionTokenLength)}";
//         val.Dt = DateTime.UtcNow;
//         val.ToLog = 1;

//         return string.Concat(tokenSubHidden, ":", $"{activateTokenKey}{_GetClientToken(kt.Value.Id, val.Token)}");
//     }

//     public bool Delete(string session)
//     {
//         if (string.IsNullOrEmpty(session))
//             return false;

//         ReadOnlySpan<char> span = session;
//         int lastColonIndex = span.LastIndexOf(':');

//         if (lastColonIndex == -1 || lastColonIndex == span.Length - 1)
//             return false;

//         var clientTokenSpan = span[(lastColonIndex + 1)..];

//         var kt = _ConvertClientTokenToServerIdAndToken(clientTokenSpan);
//         if (kt == null) return false;

//         if (!List.TryGetValue(kt.Value.Id, out var val) || val.Token != kt.Value.Token)
//             return false;

//         val.ToLog = 2;
//         return true;
//     }




//     (string activateTokenKey, string Id, string Token)? _ConvertClientTokenToServerIdAndToken(ReadOnlySpan<char> tokenClient)
//     {
//         if (tokenClient.Length < _Config.SessionIdLength + _Config.SessionTokenLength) return null;

//         var activateTokenKey = tokenClient.Slice(0, 64);
//         var idSpan = tokenClient.Slice(64, _Config.SessionIdLength);
//         var tokenSpan = tokenClient.Slice(64 + _Config.SessionIdLength, _Config.SessionTokenLength);

//         return (activateTokenKey.ToString(), idSpan.ToString(), tokenSpan.ToString());
//     }

//     bool _IsOutdate(Models.ValueModel value) => DateTime.UtcNow > value.Dt.AddMinutes(_Config.SessionLifetimeInMin) || value.NumberOfErrors > _Config.SessionMaxNumberOfErrors ? true : false;


//     string _GetClientToken(string id, string token)
//     {
//         if (id == string.Empty || token == string.Empty) return string.Empty;

//         var random = new Random();
//         var length = random.Next(10, _Config.SessionTokenLength);
//         var tksub = Helpers.GenerateSecureRandomString(length);

//         return $"{id}{token}{tksub}";
//     }

//     /// <summary>
//     /// Публичнвй ключ, сроком жизни в 1 сутки, который служит для восстанавления сессии если ссессия была утерена например по причине того что серевер был перезагружен 
//     /// </summary>
//     /// <returns></returns>


//     bool _TryKill(Models.ValueModel value, ReadOnlySpan<char> tokenClient)
//     {
//         // Проверка длины токена
//         if (tokenClient.Length != _Config.SessionIdLength + _Config.SessionTokenLength) return false;

//         var kt = _ConvertClientTokenToServerIdAndToken(tokenClient);
//         if (kt == null) return false;

//         ReadOnlySpan<char> token = kt.Value.Token;

//         if (token.Length != _Config.SessionTokenLength || value.Token.Length != _Config.SessionTokenLength) return false;

//         int matchCount;

//         // Вариант 1: первая половина
//         matchCount = 0;
//         for (int i = 0; i < SessionTokenHalfLength; i++)
//             if (value.Token[i] == token[i])
//                 matchCount++;
//         if (matchCount == SessionTokenHalfLength) { value.ToLog = 2; return true; }

//         // Вариант 2: вторая половина
//         matchCount = 0;
//         for (int i = SessionTokenHalfLength; i < _Config.SessionTokenLength; i++)
//             if (value.Token[i] == token[i])
//                 matchCount++;
//         if (matchCount == SessionTokenHalfLength) { value.ToLog = 2; return true; }

//         // Вариант 3: каждый второй символ (чётные индексы)
//         matchCount = 0;
//         for (int i = 0; i < _Config.SessionTokenLength; i += 2)
//             if (value.Token[i] == token[i])
//                 matchCount++;
//         if (matchCount == SessionTokenHalfLength) { value.ToLog = 2; return true; }

//         value.NumberOfErrors++;
//         return false;
//     }
// }