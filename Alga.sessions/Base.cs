// using System.Collections.Concurrent;
// using System.Text.Json;

// namespace Alga.sessions;

// public abstract class Base
// {
//     /// <summary>
//     /// Key - is session id
//     /// Value - is token
//     /// </summary>
//     protected readonly ConcurrentDictionary<string, ValueModel> List = new();

//     public readonly ConfigModel ConfigM;
//     readonly int SessionTokenHalfLength;
//     public Base(ConfigModel? config = null)
//     {
//         ConfigM = config ?? new();
//         SessionTokenHalfLength = ConfigM.SessionIdLength / 2;

//         //_ = EngineWhileAsync();
//     }

//     /// <summary>
//     /// Рекомендуем создавать сессию только тогда когда пользователь авторизовался и вы возвращаете ему сессию для дальнейших действий, потому что любой выданная сессия будет занимать место в памяти довольно продолжительное время
//     /// </summary>
//     protected string? CreateBase(string serializedJsonSession)
//     {
//         try
//         {
//             var clientModel = _DeserializedJsonSession(serializedJsonSession);

//             if (clientModel == null) return null;

//             var id = Helpers.GenerateSecureRandomString(ConfigM.SessionIdLength);
//             var token = Helpers.GenerateSecureRandomString(ConfigM.SessionTokenLength);

//             clientModel["token"] = GetClientToken(id, token);

//             if (!List.TryAdd(id, new ValueModel { Token = token })) return null;

//             return JsonSerializer.Serialize(clientModel);
//         }
//         catch { return null; }

//         // BENCHMARK: 12 / 11 / 12 / 23 / 16 / 14 us
//     }

//     /// <summary>
//     /// Работает только с существующими сессиями
//     /// Необходима для усиления безопасности
//     /// Рекоммендуется обновлять чем можно тем чаще но так как мы включили дополнительные механизмы защиты то в любом случае не помешает осуществлять рефреш при заходе в ваше приложение, если сильно бесспокоитесь то при открытии каждой страницы или окна
//     /// </summary>
//     protected string? RefreshBase(string serializedJsonSession)
//     {
//         try
//         {
//             var sp = _DeserializedJsonSession(serializedJsonSession);

//             if (sp == null) return null;

//             var clientTokenO = sp["token"];

//             if (clientTokenO == null) return null;

//             var clientToken = clientTokenO.ToString();

//             if (clientToken == null) return null;

//             var kt = ConvertClientTokenToServerIdAndToken(clientToken);

//             if (kt != null && List.TryGetValue(kt.Value.Id, out var val))
//                 if (val.Token == kt.Value.Token)
//                     if (!IsOutdate(val) && val.ToLog != 2)
//                     {
//                         val.Token = Helpers.GenerateSecureRandomString(ConfigM.SessionTokenLength);
//                         val.Dt = DateTime.UtcNow;
//                         val.ToLog = 1;
//                         sp["token"] = GetClientToken(kt.Value.Id, val.Token);
//                         return JsonSerializer.Serialize(sp);
//                     }
//                     else val.ToLog = 2;
//                 else TryKill(val, clientToken);

//             return null;
//         }
//         catch { return null; }

//         // BENCHMARK (TRUE): 12 / 18 / 12 / 21 / 9 / 14 / 27 / 22 / 17 / 23 / 15 / 16 / 18 / 13 / 9 / 9 / 8 / 10 us
//     }

//     /// <summary>
//     /// Проверка сессии
//     /// Если данные сессии корректны проверка осущесьвляется почти мгновенно и выдается true
//     /// Но если данные не корректны срабатывает дополнительная система безопассности которая пытается сессию убить
//     /// </summary>
//     protected bool CheckBase(string serializedJsonSession) {
//         try {
//             var clientToken = GetCurrentClientToken(serializedJsonSession);

//             if (clientToken == null) return false;

//             var kt = ConvertClientTokenToServerIdAndToken(clientToken);

//             if(kt == null || !List.TryGetValue(kt.Value.Id, out var val)) return false;

//             if(val.Token == kt.Value.Token)
//                 if(!IsOutdate(val) && val.ToLog != 2) return true;
//                 else val.ToLog = 2; 
//             else TryKill(val, clientToken);

//             return false;
//         } catch { return false; }

//         // BENCHMARK (TRUE): 7 / 4 / 7 / 3 / 7 / 5 / 9 / 11 / 8 / 9 / 2 / 1 / 2 / 2 / 3 / 3 / 2 / 2 / 2 / 3 / 3 us
//     }

//     protected bool DeleteBase(string serializedJsonSession)
//     {
//         try
//         {
//             var token = GetCurrentClientToken(serializedJsonSession);

//             if (token == null) return false;

//             var kt = ConvertClientTokenToServerIdAndToken(token);

//             if (kt == null || !List.TryGetValue(kt.Value.Id, out var val) || val.Token != kt.Value.Token) return false;

//             val.ToLog = 2;

//             return true;
//         }
//         catch { return false; }

//         // BENCHMARK (TRUE): 12 / 8 / 21 / 16 / 29 us
//     }

//     public string? GetCurrentClientToken(string serializedJsonSession)
//     {
//         var sp = _DeserializedJsonSession(serializedJsonSession);

//         if (sp == null) return null;

//         var token = sp["token"].ToString();

//         if (string.IsNullOrEmpty(token)) return null;

//         return token;
//     }

//     Dictionary<string, object>? _DeserializedJsonSession(string serializedJsonSession) => JsonSerializer.Deserialize<Dictionary<string, object>>(serializedJsonSession);

//     public string GetClientToken(string id, string token)
//     {
//         if (id == string.Empty || token == string.Empty) return string.Empty;

//         var random = new Random();
//         var length = random.Next(10, ConfigM.SessionTokenLength);
//         var tksub = Helpers.GenerateSecureRandomString(length);

//         return $"{id}{token}{tksub}";
//     }

//     protected (string Id, string Token)? ConvertClientTokenToServerIdAndToken(string tokenClient)
//     {
//         try
//         {
//             if (string.IsNullOrEmpty(tokenClient)) return null;

//             var key = tokenClient.Substring(0, ConfigM.SessionIdLength);
//             var token = tokenClient.Substring(ConfigM.SessionIdLength, ConfigM.SessionTokenLength);
//             return (key, token);
//         }
//         catch { return null; }
//     }

//     bool IsOutdate(ValueModel value) => DateTime.UtcNow > value.Dt.Add(ConfigM.SessionLifetime) || value.NumberOfErrors > ConfigM.MaxNumberOfErrors ? true : false;

//     bool TryKill(ValueModel value, string tokenClient)
//     {
//         try
//         {
//             if (tokenClient.Length != ConfigM.SessionIdLength + ConfigM.SessionTokenLength) return false;

//             var kt = ConvertClientTokenToServerIdAndToken(tokenClient);

//             if (kt == null) return false;

//             var f = false;

//             var sml = 0;
//             for (int i = 0; i < SessionTokenHalfLength; i++) if (value.Token[i] == kt.Value.Token[i]) sml++;
//             if (sml == SessionTokenHalfLength) f = true;

//             if (!f)
//             {
//                 var smlOne = 0;
//                 for (int i = ConfigM.SessionTokenLength; i >= SessionTokenHalfLength; i--) if (value.Token[i] == kt.Value.Token[i]) smlOne++;
//                 if (sml == SessionTokenHalfLength) f = true;
//             }

//             if (!f)
//             {
//                 var smlTwo = 0;
//                 for (int i = 0; i < ConfigM.SessionTokenLength; i += 2) if (value.Token[i] == kt.Value.Token[i]) smlTwo++;
//                 if (sml == SessionTokenHalfLength) f = true;
//             }

//             if (f) value.ToLog = 2; else value.NumberOfErrors++;

//             return f;
//         }
//         catch { return false; }
//     }

//     public class ValueModel
//     {
//         public required string Token { get; set; } // session token
//         public DateTime Dt { get; set; } = DateTime.UtcNow; // время создания или время последнего рефреша
//         public long NumberOfErrors { get; set; } = 0; // количество ошибочных попыток входа
//         public byte ToLog { get; set; } = 1; // ддобавить в log File если он существует. Where: 1 - обновить / 2 удалить
//     }

//     public class ConfigModel
//     {
//         public int SessionIdLength { get; init; } = 32; // чем длинее длина ключа тем больше памяти будет расходоваться и время генерации будет больше но при этомм высокая уникальность ключа 
//         public int SessionTokenLength { get; init; } = 128; // чем длинее длина ключа тем больше памяти будет расходоваться и время генерации будет больше но при этомм высокая уникальность ключа
//         public TimeSpan SessionLifetime { get; init; } = TimeSpan.FromDays(7); // время жизни сесии, время жизни сессии это плюс время к созданию сессии или это время обновляется каждый раз поссле его обновления refresh
//         public long MaxNumberOfErrors { get; init; } = 100000000; // максимальное количество возможных ошибок
//         public LogsFileModel? LogsFile { get; init; } // Logs file содержит последние изменения в работе с сессиями что позволяет минимизировать потерю сессий на случай если сервер будет перезагружен или будет обновлена на более новую версию. но если нет ничего критичного в потере сессий и являетсся нормальным попросить клиента авторизоваться еще раз то данный подход можно и лучше не применять, он довольно затратен по времени
//     }

//     public class LogsFileModel
//     {
//         public string? DirecoryPath { get; init; }
//         public string? EncryptionKey { get; init; } // ключ для шифрования файла. есть вероятность что вы можете поменять путь к директории и этот файл останется хранится на сервере, чтобы ключи сессии не сохранялись в открытом виде и чтобы еще сильнее умменьшить вероятность попадания из в чужие руки вы можете закодировать все записи в файле.
//     }
// }








//     // SessionModel? SessionParse(string? serializedJsonSession) {
//     //     if(serializedJsonSession == null) return null;

//     //     try {
//     //         using var sessionModel = JsonDocument.Parse(serializedJsonSession);

//     //         if(sessionModel == null) return null;

//     //         var context = sessionModel.RootElement.GetProperty("context");

//     //         string? token = sessionModel.RootElement.GetProperty("token").GetString();

//     //         if(string.IsNullOrWhiteSpace(token)) return null;

//     //         return new SessionModel(token, context.Deserialize<dynamic>());
//     //     } catch { }

//     //     return null;
//     // }

//     //public int GetTokenHidden(string sessionInfSerialize) => string.IsNullOrWhiteSpace(sessionInfSerialize) ? 0 : sessionInfSerialize.GetHashCode();



// // using System.Collections.Concurrent;
// // using System.Text;

// // namespace Alga.sessions;
// // public abstract class Base {
// //         /// <summary>
// //         /// Key - is session id
// //         /// Value - is token
// //         /// </summary>
// //         protected readonly ConcurrentDictionary<string, ValueModel> List = new ();

// //         public readonly ConfigModel ConfigM;
// //         readonly int SessionTokenHalfLength;
// //         public Base(ConfigModel? config = null) {
// //             ConfigM = config ?? new ();
// //             SessionTokenHalfLength = ConfigM.SessionIdLength / 2;

// //             _ = EngineWhileAsync();
// //         }

// //         /// <summary>
// //         /// Рекомендуем создавать сессию только тогда когда пользователь авторизовался и вы возвращаете ему сессию для дальнейших действий, потому что любой выданная сессия будет занимать место в памяти довольно продолжительное время
// //         /// </summary>
// //         protected SessionModel<T>? CreateBase<T>(T? sessionInfo=default, int tokenHidden = 0) {
// //             try {
// //                 var id = Helpers.GenerateSecureRandomString(ConfigM.SessionIdLength);
// //                 var token = Helpers.GenerateSecureRandomString(ConfigM.SessionTokenLength);

// //                 var clientToken = GetClientToken(id, token);

// //                 if(clientToken == string.Empty) return null;

// //                 if(!List.TryAdd(id, new ValueModel { Token = token, TokenHidden = tokenHidden })) return null;

// //                 return new SessionModel<T> { token = clientToken, info = sessionInfo};
// //             } catch { return null; }
    
// //             // BENCHMARK: 12 / 11 / 12 / 23 / 16 / 14 us
// //         }

// //         /// <summary>
// //         /// Работает только с существующими сессиями
// //         /// Необходима для усиления безопасности
// //         /// Рекоммендуется обновлять чем можно тем чаще но так как мы включили дополнительные механизмы защиты то в любом случае не помешает осуществлять рефреш при заходе в ваше приложение, если сильно бесспокоитесь то при открытии каждой страницы или окна
// //         /// </summary>
// //         protected SessionModel<T>? RefreshBase<T>(SessionModel<T> clientSession, int tokenHidden = 0) {
// //             try {
// //                 var kt = ConvertClientTokenToServerIdAndToken(clientSession.token);

// //                 if(kt != null && List.TryGetValue(kt.Value.Id, out var val))
// //                     if(val.Token == kt.Value.Token) {
// //                         if(val.TokenHidden == tokenHidden)
// //                             if(!IsOutdate(val)) {
// //                                 val.Token = Helpers.GenerateSecureRandomString(ConfigM.SessionTokenLength);
// //                                 val.Dt = DateTime.UtcNow;
// //                                 val.ToLog = 1;
// //                                 clientSession.token = GetClientToken(kt.Value.Id, val.Token);
// //                                 return clientSession;
// //                             } else val.ToLog = 2;
// //                         else val.NumberOfErrors ++;
// //                     } else TryKill(val, clientSession.token);

// //                 return null;
// //             } catch { return null; }
            
// //             // BENCHMARK (TRUE): 12 / 18 / 12 / 21 / 9 / 14 / 27 / 22 / 17 / 23 / 15 / 16 / 18 / 13 / 9 / 9 / 8 / 10 us
// //         }

// //         protected string? DeleteBase(string tokenClient) {
// //             try {
// //                 var kt = ConvertClientTokenToServerIdAndToken(tokenClient);
// //                 if(kt == null || !List.TryGetValue(kt.Value.Id, out var val) || val.Token != kt.Value.Token) return null;

// //                 val.ToLog = 2;
// //                 return kt.Value.Id;
// //             } catch { return null; }

// //             // BENCHMARK (TRUE): 12 / 8 / 21 / 16 / 29 us
// //         }

// //         /// <summary>
// //         /// Проверка сессии
// //         /// Если данные сессии корректны проверка осущесьвляется почти мгновенно и выдается true
// //         /// Но если данные не корректны срабатывает дополнительная система безопассности которая пытается сессию убить
// //         /// </summary>
// //         protected SessionModel<T>? CheckBase<T>(SessionModel<T> clientSession, int tokenHidden = 0) {
// //             try {
// //                 if(clientSession == null) return null;

// //                 var kt = ConvertClientTokenToServerIdAndToken(clientSession.token);

// //                 if(kt == null || !List.TryGetValue(kt.Value.Id, out var val)) return null;

// //                 if(val.Token == kt.Value.Token) {
// //                     if(val.TokenHidden == tokenHidden)
// //                         if(!IsOutdate(val)) return clientSession; //return new SessionModel<T> { Token = GetClientToken(kt.Key, val.Token), Info = sessionInfo }; //return kt.Key;
// //                         else val.ToLog = 2;
// //                     else val.NumberOfErrors ++;
// //                 } else TryKill(val, clientSession.token);

// //                 return null;
// //             } catch { return null; }

// //             // BENCHMARK (TRUE): 7 / 4 / 7 / 3 / 7 / 5 / 9 / 11 / 8 / 9 / 2 / 1 / 2 / 2 / 3 / 3 / 2 / 2 / 2 / 3 / 3 us
// //         }

// //         bool TryKill(ValueModel value, string tokenClient) {
// //             try {
// //                 if(tokenClient.Length != ConfigM.SessionIdLength + ConfigM.SessionTokenLength) return false;

// //                 var kt = ConvertClientTokenToServerIdAndToken(tokenClient);

// //                 if(kt == null) return false;

// //                 var f = false;

// //                 var sml = 0;
// //                 for (int i = 0; i < SessionTokenHalfLength; i++) if (value.Token[i] == kt.Value.Token[i]) sml ++;
// //                 if(sml == SessionTokenHalfLength) f = true;

// //                 if(!f) {
// //                     var smlOne = 0;
// //                     for (int i = ConfigM.SessionTokenLength; i >= SessionTokenHalfLength; i--) if (value.Token[i] == kt.Value.Token[i]) smlOne ++;
// //                     if(sml == SessionTokenHalfLength) f = true;
// //                 }

// //                 if(!f) {
// //                     var smlTwo = 0;
// //                     for (int i = 0; i < ConfigM.SessionTokenLength; i+=2) if (value.Token[i] == kt.Value.Token[i]) smlTwo ++;
// //                     if(sml == SessionTokenHalfLength) f = true;
// //                 }

// //                 if(f) value.ToLog = 2;
// //                 else value.NumberOfErrors ++;

// //                 return f;
// //             } catch { return false; }
// //         }

// //         async Task EngineWhileAsync() {
// //             try {
// //                 string? logsFilePath = null;
// //                 var delToken = Helpers.GenerateZeroString(ConfigM.SessionTokenLength);

// //                 if(ConfigM.LogsFile != null && Directory.Exists(ConfigM.LogsFile.DirecoryPath)) {
// //                     var logsFileSubPath = Path.Combine(ConfigM.LogsFile.DirecoryPath, $"alga.sessions.logs_{ConfigM.SessionIdLength}_{ConfigM.SessionTokenLength}.dat");

// //                     if(File.Exists(logsFileSubPath)) logsFilePath = logsFileSubPath;
// //                     else {
// //                         File.Create(logsFileSubPath).Dispose();
// //                         logsFilePath = logsFileSubPath;
// //                     }

// //                     if(logsFilePath != null) {
// //                         var delList = new HashSet<string>();

// //                         using var fs = new FileStream(logsFilePath, FileMode.Open, FileAccess.Read);

// //                         var blockSize = ConfigM.SessionIdLength + ConfigM.SessionTokenLength;

// //                         long position = fs.Length;

// //                         while (position > 0) {
// //                             int bytesToRead = (int)Math.Min(blockSize, position);
// //                             position -= bytesToRead;

// //                             var buffer = new byte[bytesToRead];
// //                             fs.Seek(position, SeekOrigin.Begin);

// //                             int bytesRead = 0;
// //                             while (bytesRead < bytesToRead) {
// //                                 int result = fs.Read(buffer, bytesRead, bytesToRead - bytesRead);
// //                                 if (result == 0)
// //                                     break;
// //                                 bytesRead += result;
// //                             }

// //                             var block = Encoding.UTF8.GetString(buffer);
// //                             if(ConfigM.LogsFile.EncryptionKey != null)
// //                                 block = Helpers.XorEncryptDecrypt(block, ConfigM.LogsFile.EncryptionKey);

// //                             string key = block.Substring(0, ConfigM.SessionIdLength);
// //                             string token = block.Substring(ConfigM.SessionIdLength);

// //                             if(token != delToken) List.TryAdd(key, new () { Token = token });
// //                             else delList.Add(key);
// //                         }

// //                         foreach(var i in delList) List.TryRemove(i, out _);

// //                         if(List.Count > 0) File.WriteAllText(logsFilePath, string.Empty);
// //                     }
// //                 }

                
// //                 var n = 0;
// //                 while (true) {
// //                     if(ConfigM.LogsFile != null && logsFilePath != null) {
// //                         if(n > 60) {
// //                             n=0;
// //                             File.WriteAllText(logsFilePath, string.Empty);
// //                             foreach(var i in List) 
// //                                 if(i.Value.ToLog == 0) 
// //                                     i.Value.ToLog = 1;
// //                         } else n++;

// //                         using var writer = new StreamWriter(logsFilePath, append: true);
// //                         foreach(var i in List) 
// //                             if(i.Value.ToLog > 0) {
// //                                 if(i.Value.ToLog == 1) i.Value.ToLog = 0;
// //                                 else if(i.Value.ToLog == 2) i.Value.Token = delToken;

// //                                 var block = $"{i.Key}{i.Value.Token}";
// //                                 if(ConfigM.LogsFile.EncryptionKey != null)
// //                                     block = Helpers.XorEncryptDecrypt(block, ConfigM.LogsFile.EncryptionKey);
// //                                 writer.Write(block);
// //                             }
// //                     }

// //                     foreach(var i in List)
// //                         if(i.Value.ToLog == 2)
// //                             List.TryRemove(i.Key, out _);

// //                     await Task.Delay(60000);
// //                 }
// //             } catch { } 
// //         }

// //         bool IsOutdate(ValueModel value) => DateTime.UtcNow > value.Dt.Add(ConfigM.SessionLifetime) || value.NumberOfErrors > ConfigM.MaxNumberOfErrors ? true : false;

// //         protected (string Id, string Token)? ConvertClientTokenToServerIdAndToken(string tokenClient) {
// //             try {
// //                 if(string.IsNullOrEmpty(tokenClient)) return null;

// //                 var key = tokenClient.Substring(0, ConfigM.SessionIdLength);
// //                 var token = tokenClient.Substring(ConfigM.SessionIdLength, ConfigM.SessionTokenLength);
// //                 return (key, token);
// //             } catch { return null; }
// //         }

// //         // Helpers

// //         public string GetClientToken(string id, string token) {
// //             if(id == string.Empty || token == string.Empty) return string.Empty;
// //             return $"{id}{token}{GenerateRandomSub()}";
// //         }
        

// //         public string GenerateRandomSub() {
// //             var random = new Random();
// //             var length = random.Next(10, ConfigM.SessionTokenLength);
// //             return Helpers.GenerateSecureRandomString(length);
// //         }

// //         public int GetTokenHidden(string sessionInfSerialize) => string.IsNullOrWhiteSpace(sessionInfSerialize) ? 0 : sessionInfSerialize.GetHashCode();

// //         // Models

// //         public class ValueModel {
// //             public required string Token { get; set; } // session token
// //             public int TokenHidden { get; init; } // sub token - если у вас дополнительная логика создания ключа которая не будет видна клиеенту то стоит добавить, например вы можете использовать Header[User-Agent] получить его хеш код и использовать как секретный ключ, который будет использоваться на сервере и цель которого вызывать ошибки перебора чтобы по истечению лимита ссессия была удалена
// //             public DateTime Dt { get; set; } = DateTime.UtcNow; // время создания или время последнего рефреша
// //             public long NumberOfErrors { get; set; } = 0; // количество ошибочных попыток входа
// //             public byte ToLog { get; set; } = 1; // ддобавить в log File если он существует. Where: 1 - обновить / 2 удалить
// //         }

// //         public class ConfigModel {
// //             public int SessionIdLength { get; init; } = 32; // чем длинее длина ключа тем больше памяти будет расходоваться и время генерации будет больше но при этомм высокая уникальность ключа 
// //             public int SessionTokenLength { get; init; } = 128; // чем длинее длина ключа тем больше памяти будет расходоваться и время генерации будет больше но при этомм высокая уникальность ключа
// //             public TimeSpan SessionLifetime { get; init; }  = TimeSpan.FromDays(7); // время жизни сесии, время жизни сессии это плюс время к созданию сессии или это время обновляется каждый раз поссле его обновления refresh
// //             public long MaxNumberOfErrors { get; init; } = 100000000; // максимальное количество возможных ошибок
// //             public LogsFileModel? LogsFile { get; init; } // Logs file содержит последние изменения в работе с сессиями что позволяет минимизировать потерю сессий на случай если сервер будет перезагружен или будет обновлена на более новую версию. но если нет ничего критичного в потере сессий и являетсся нормальным попросить клиента авторизоваться еще раз то данный подход можно и лучше не применять, он довольно затратен по времени
// //         }

// //         public class LogsFileModel {
// //             public string? DirecoryPath { get; init; }
// //             public string? EncryptionKey { get; init; } // ключ для шифрования файла. есть вероятность что вы можете поменять путь к директории и этот файл останется хранится на сервере, чтобы ключи сессии не сохранялись в открытом виде и чтобы еще сильнее умменьшить вероятность попадания из в чужие руки вы можете закодировать все записи в файле.
// //         }

// //     public class SessionModel<T> {
// //         public required string token { get; set; }
// //         public T? info { get; set; }
// //     }
// // }
