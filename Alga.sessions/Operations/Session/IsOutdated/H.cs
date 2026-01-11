namespace Alga.sessions.Operations.Session.IsOutdated;

static class H
{
    public static bool Do(SessionStore.ValueModel value, short sessionLifetimeInMin, int sessionMaxNumberOfErrors) => DateTime.UtcNow > value.Dt.AddMinutes(sessionLifetimeInMin) || value.NumberOfErrors > sessionMaxNumberOfErrors;
}
