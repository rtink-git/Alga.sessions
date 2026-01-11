using System.Collections.Concurrent;

namespace Alga.sessions;

class SessionStore
{
    readonly ConcurrentDictionary<string, ValueModel> _sessions = new();

    public bool TryAdd(string sessionId, ValueModel value) { return _sessions.TryAdd(sessionId, value); }

    public bool TryGetValue(string sessionId, out ValueModel value) => _sessions.TryGetValue(sessionId, out value) ? true : false;

    public bool TryRemove(string sessionId, out ValueModel value) => _sessions.TryRemove(sessionId, out value);

    public bool Contains(string sessionId) => _sessions.ContainsKey(sessionId);

    public class ValueModel
    {
        public required string Token { get; set; } // session token
        public DateTime Dt { get; set; } = DateTime.UtcNow; // время создания или время последнего рефреша
        public long NumberOfErrors { get; set; } = 0; // количество ошибочных попыток входа

        //public byte ToLog { get; set; } = 1; // добавить в log File если он существует. Where: 1 - обновить / 2 удалить
    }
}
