namespace Alga.sessions.Operations.ComputeActivateTokenKey;

static class H
{
    public static string Do(ReadOnlySpan<char> session, string id, string? clientKey, string date, string activateTokenKeyDefault, string secretKey) => string.IsNullOrEmpty(clientKey) ? activateTokenKeyDefault : Operations.SignWithHmacSha256.H.Do($"{session}:{id}", $"{secretKey}{id}{clientKey}{date}");
}
