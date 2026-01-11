using System;

namespace Alga.sessions.Operations.LibSettings;

class Res : Req
{
    public byte SessionIdLength { get; init; } = 36; // Guid length
    public byte SessionTokenHalfLength { get; init; } = 18;
    public byte SessionTokenLength { get; init; } = 64;
    public int SessionMaxNumberOfErrors { get; init; } = int.MaxValue; // Max error number. If the number of variables under the current key exceeds this number, the session will be deleted from memory immediately
    public short SessionLifetimeInMin { get; init; } = short.MaxValue; // Session's life time in min, if there was no refresh. Default is 22 day
    public string ActivateTokenKeyDefault { get; init; } = "0000000000000000000000000000000000000000000000000000000000000000"; // 64 !!!
}
