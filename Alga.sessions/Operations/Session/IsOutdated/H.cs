using System;
using System.Security.Cryptography.X509Certificates;

namespace Alga.sessions.Operations.Session.IsOutdated;

internal static class H
{
    public static bool Do(SessionStore.ValueModel value, short sessionLifetimeInMin, int sessionMaxNumberOfErrors) => DateTime.UtcNow > value.Dt.AddMinutes(sessionLifetimeInMin) || value.NumberOfErrors > sessionMaxNumberOfErrors;
}
