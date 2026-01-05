using System.Security.Cryptography;

namespace Alga.sessions.Models;
public class Config
{
    public int SessionIdLength { get; init; } = 32; // Session's id length
    public int SessionTokenLength { get; init; } = 128; // Session's token length
    public int SessionRefreshIntervalInMin { get; init; }  // Interval in minutes at which the session is proactively refreshed to extend its lifetime. This does not affect the maximum session lifetime. Default is 0 minutes.
    public int SessionLifetimeInMin { get; init; } = 10080; // Session's life time in min, if there was no refresh. Default is 7 day
    public long SessionMaxNumberOfErrors { get; init; } = 10000000; // Max error number. If the number of variables under the current key exceeds this number, the session will be deleted from memory immediately
    public string SecretKey { get; init; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)); // The secret key is used to sign data and to encrypt data in a file.
}