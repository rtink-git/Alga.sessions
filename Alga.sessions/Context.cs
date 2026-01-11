using System;

namespace Alga.sessions;

class Context
{
    public SessionStore Store { get; }
    public Operations.LibSettings.Res Settings { get; }

    public Context(Operations.LibSettings.Req libSettingsReq)
    {
        Store = new SessionStore();
        Settings = Operations.LibSettings.H.Do(libSettingsReq);
    }
}
