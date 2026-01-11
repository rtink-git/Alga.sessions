namespace Alga.sessions;

public class Provider
{
    private readonly Context _context;

    public Provider(Operations.LibSettings.Req libSettingsReq) => _context = new Context(libSettingsReq);

    public string? Create(ReadOnlySpan<char> session = default, string? clientKey = null) => Operations.Session.Create.H.Do(_context, session, clientKey);

    public bool Check(string session, string? clientKey = null) => Operations.Session.Check.H.Do(_context, session, clientKey);

    public string? Refresh(ReadOnlySpan<char> session, string? clientKey = null) => Operations.Session.Refresh.H.Do(_context, session, clientKey);

    public bool Delete(string session) => Operations.Session.Delete.H.Do(session, _context);
}