namespace Alga.sessions.Models;

public class Config
{
    public int SessionIdLength { get; init; } = 32; // Session's id length
    public int SessionTokenLength { get; init; } = 128; // Session's token length
    public int SessionLifetimeInMin { get; init; } = 10080; // Session's life time in min, if there was no refresh. Default is 7 day
    public long SessionMaxNumberOfErrors { get; init; } = 10000000; // Max error number. If the number of variables under the current key exceeds this number, the session will be deleted from memory immediately
    public string? StorageDirectoryPath { get; init; } // Path to the folder where your sessions will be stored for a long time. Optional parameter
    public string? StorageEncryptionKey { get; init;} // Key for encrypting data in storage file. Optional parameter
}