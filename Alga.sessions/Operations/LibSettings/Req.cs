using System.Security.Cryptography;

namespace Alga.sessions.Operations.LibSettings;

public class Req
{
    /// <summary>
    /// Interval in minutes at which the session is proactively refreshed to extend its lifetime. This does not affect the maximum session lifetime. Default is 0 minutes.
    /// </summary>
    public byte SessionRefreshIntervalInMin { get; init; } = 0;

    /// <summary>
    /// // The secret key is used to sign data and to encrypt data in a file.
    /// </summary>
    public string SecretKey { get; init; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}