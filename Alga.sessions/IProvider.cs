using System;

namespace Alga.sessions;

public interface IProvider
{
    string? Create(ReadOnlySpan<char> session = default, string? clientKey = null);

    bool Check(string session, string? clientKey = null);

    string? Refresh(ReadOnlySpan<char> session, string? clientKey = null);

    bool Delete(string session);
}
