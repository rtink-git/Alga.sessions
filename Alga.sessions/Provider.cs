using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Alga.sessions;

public class Provider
{
    protected readonly ConcurrentDictionary<string, Models.ValueModel> List = new();

    public readonly Models.Config _Config;
    readonly int SessionTokenHalfLength;
    public Provider(Models.Config? config)
    {
        _Config = config ?? new ();
        SessionTokenHalfLength = _Config.SessionIdLength / 2;

        EngineWhileAsync();
    }
    public bool Check(string serializedJsonSession) {
        try {
            var clientToken = _GetCurrentClientToken(serializedJsonSession);

            if (clientToken == null) return false;

            var kt = _ConvertClientTokenToServerIdAndToken(clientToken);

            if(kt == null || !List.TryGetValue(kt.Value.Id, out var val)) return false;

            if(val.Token == kt.Value.Token)
                if(!_IsOutdate(val) && val.ToLog != 2) return true;
                else val.ToLog = 2; 
            else _TryKill(val, clientToken);

            return false;
        } catch { return false; }

        // BENCHMARK (TRUE): 7 / 4 / 7 / 3 / 7 / 5 / 9 / 11 / 8 / 9 / 2 / 1 / 2 / 2 / 3 / 3 / 2 / 2 / 2 / 3 / 3 us
    }
    public string? Create(string serializedJsonSession)
    {
        try
        {
            var clientModel = _DeserializedJsonSession(serializedJsonSession);

            if (clientModel == null) return null;

            var id = Helpers.GenerateSecureRandomString(_Config.SessionIdLength);
            var token = Helpers.GenerateSecureRandomString(_Config.SessionTokenLength);

            clientModel["token"] = _GetClientToken(id, token);

            if (!List.TryAdd(id, new Models.ValueModel { Token = token })) return null;

            return JsonSerializer.Serialize(clientModel);
        }
        catch { return null; }

        // BENCHMARK: 12 / 11 / 12 / 23 / 16 / 14 us
    }

    public bool Delete(string serializedJsonSession)
    {
        try
        {
            var token = _GetCurrentClientToken(serializedJsonSession);

            if (token == null) return false;

            var kt = _ConvertClientTokenToServerIdAndToken(token);

            if (kt == null || !List.TryGetValue(kt.Value.Id, out var val) || val.Token != kt.Value.Token) return false;

            val.ToLog = 2;

            return true;
        }
        catch { return false; }

        // BENCHMARK (TRUE): 12 / 8 / 21 / 16 / 29 us
    }

    public string? Refresh(string serializedJsonSession)
    {
        try
        {
            var sp = _DeserializedJsonSession(serializedJsonSession);

            if (sp == null) return null;

            var clientTokenO = sp["token"];

            if (clientTokenO == null) return null;

            var clientToken = clientTokenO.ToString();

            if (clientToken == null) return null;

            var kt = _ConvertClientTokenToServerIdAndToken(clientToken);

            if (kt != null && List.TryGetValue(kt.Value.Id, out var val))
                if (val.Token == kt.Value.Token)
                    if (!_IsOutdate(val) && val.ToLog != 2)
                    {
                        val.Token = Helpers.GenerateSecureRandomString(_Config.SessionTokenLength);
                        val.Dt = DateTime.UtcNow;
                        val.ToLog = 1;
                        sp["token"] = _GetClientToken(kt.Value.Id, val.Token);
                        return JsonSerializer.Serialize(sp);
                    }
                    else val.ToLog = 2;
                else _TryKill(val, clientToken);

            return null;
        }
        catch { return null; }

        // BENCHMARK (TRUE): 12 / 18 / 12 / 21 / 9 / 14 / 27 / 22 / 17 / 23 / 15 / 16 / 18 / 13 / 9 / 9 / 8 / 10 us
    }

    async Task EngineWhileAsync() {
        try {
            string? storageFilePath = null;
            var delToken = Helpers.GenerateZeroString(_Config.SessionTokenLength);

            if (Directory.Exists(_Config.StorageDirectoryPath))
            {
                var logsFileSubPath = Path.Combine(_Config.StorageDirectoryPath, $"alga.sessions.logs_{_Config.SessionIdLength}_{_Config.SessionTokenLength}.dat");

                if (File.Exists(logsFileSubPath)) storageFilePath = logsFileSubPath;
                else
                {
                    File.Create(logsFileSubPath).Dispose();
                    storageFilePath = logsFileSubPath;
                }

                if (storageFilePath != null)
                {
                    var delList = new HashSet<string>();

                    using var fs = new FileStream(storageFilePath, FileMode.Open, FileAccess.Read);

                    var blockSize = _Config.SessionIdLength + _Config.SessionTokenLength;

                    long position = fs.Length;

                    while (position > 0)
                    {
                        int bytesToRead = (int)Math.Min(blockSize, position);
                        position -= bytesToRead;

                        var buffer = new byte[bytesToRead];
                        fs.Seek(position, SeekOrigin.Begin);

                        int bytesRead = 0;
                        while (bytesRead < bytesToRead)
                        {
                            int result = fs.Read(buffer, bytesRead, bytesToRead - bytesRead);
                            if (result == 0)
                                break;
                            bytesRead += result;
                        }

                        var block = Encoding.UTF8.GetString(buffer);
                        if (_Config.StorageEncryptionKey != null)
                            block = Helpers.XorEncryptDecrypt(block, _Config.StorageEncryptionKey);

                        string key = block.Substring(0, _Config.SessionIdLength);
                        string token = block.Substring(_Config.SessionIdLength);

                        if (token != delToken) List.TryAdd(key, new() { Token = token });
                        else delList.Add(key);
                    }

                    foreach (var i in delList) List.TryRemove(i, out _);

                    if (List.Count > 0) File.WriteAllText(storageFilePath, string.Empty);
                }
            }

            var n = 0;
            while (true)
            {
                if (storageFilePath != null)
                {
                    if (n > 60)
                    {
                        n = 0;
                        File.WriteAllText(storageFilePath, string.Empty);
                        foreach (var i in List)
                            if (i.Value.ToLog == 0)
                                i.Value.ToLog = 1;
                    }
                    else n++;

                    using var writer = new StreamWriter(storageFilePath, append: true);
                    foreach (var i in List)
                        if (i.Value.ToLog > 0)
                        {
                            if (i.Value.ToLog == 1) i.Value.ToLog = 0;
                            else if (i.Value.ToLog == 2) i.Value.Token = delToken;

                            var block = $"{i.Key}{i.Value.Token}";
                            if (_Config.StorageEncryptionKey != null)
                                block = Helpers.XorEncryptDecrypt(block, _Config.StorageEncryptionKey);
                            writer.Write(block);
                        }
                }

                foreach (var i in List)
                    if (i.Value.ToLog == 2)
                        List.TryRemove(i.Key, out _);

                await Task.Delay(60000);
            }
        } catch { } 
    }

    public string? _GetCurrentClientToken(string serializedJsonSession)
    {
        var sp = _DeserializedJsonSession(serializedJsonSession);

        if (sp == null) return null;

        var token = sp["token"].ToString();

        if (string.IsNullOrEmpty(token)) return null;

        return token;
    }

    (string Id, string Token)? _ConvertClientTokenToServerIdAndToken(string tokenClient)
    {
        try
        {
            if (string.IsNullOrEmpty(tokenClient)) return null;

            var key = tokenClient.Substring(0, _Config.SessionIdLength);
            var token = tokenClient.Substring(_Config.SessionIdLength, _Config.SessionTokenLength);
            return (key, token);
        }
        catch { return null; }
    }

    Dictionary<string, object>? _DeserializedJsonSession(string serializedJsonSession) => JsonSerializer.Deserialize<Dictionary<string, object>>(serializedJsonSession);

    bool _IsOutdate(Models.ValueModel value) => DateTime.UtcNow > value.Dt.AddMinutes(_Config.SessionLifetimeInMin) || value.NumberOfErrors > _Config.SessionMaxNumberOfErrors ? true : false;

    string _GetClientToken(string id, string token)
    {
        if (id == string.Empty || token == string.Empty) return string.Empty;

        var random = new Random();
        var length = random.Next(10, _Config.SessionTokenLength);
        var tksub = Helpers.GenerateSecureRandomString(length);

        return $"{id}{token}{tksub}";
    }

    bool _TryKill(Models.ValueModel value, string tokenClient)
    {
        try
        {
            if (tokenClient.Length != _Config.SessionIdLength + _Config.SessionTokenLength) return false;

            var kt = _ConvertClientTokenToServerIdAndToken(tokenClient);

            if (kt == null) return false;

            var f = false;

            var sml = 0;
            for (int i = 0; i < SessionTokenHalfLength; i++) if (value.Token[i] == kt.Value.Token[i]) sml++;
            if (sml == SessionTokenHalfLength) f = true;

            if (!f)
            {
                var smlOne = 0;
                for (int i = _Config.SessionTokenLength; i >= SessionTokenHalfLength; i--) if (value.Token[i] == kt.Value.Token[i]) smlOne++;
                if (sml == SessionTokenHalfLength) f = true;
            }

            if (!f)
            {
                var smlTwo = 0;
                for (int i = 0; i < _Config.SessionTokenLength; i += 2) if (value.Token[i] == kt.Value.Token[i]) smlTwo++;
                if (sml == SessionTokenHalfLength) f = true;
            }

            if (f) value.ToLog = 2; else value.NumberOfErrors++;

            return f;
        }
        catch { return false; }
    }
}