using System.Security.Cryptography;

namespace Alga.sessions.Models;

public class Config
{
    public byte SessionRefreshIntervalInMin { get; init; } = 0;  // Interval in minutes at which the session is proactively refreshed to extend its lifetime. This does not affect the maximum session lifetime. Default is 0 minutes.
    public string SecretKey { get; init; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)); // The secret key is used to sign data and to encrypt data in a file.
}