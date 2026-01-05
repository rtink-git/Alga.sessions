# Alga.sessions

A lightweight .NET library for streamlined session management: Create, Refresh, Validation, Deletion. Sessions are stored in RAM for quick access. For long-term storage of sessions, you can use an automatically created file that is updated once a minute, for this you just need to specify the path to the directory.





## How does this work. Step by step

1. **Install-Package** [Alga.sessions](https://www.nuget.org/packages/Alga.sessions)

2. **Setting up the configuration** (appsettings.json)

```
{
    ...,
    "AlgaSessionsConfig": {
        "SessionIdLength": 32, 
        "SessionTokenLength": 128,
        "SessionRefreshIntervalInMin": 5 
        "SessionLifetimeInMin": 5040,
        "SessionMaxNumberOfErrors": 10000000,
        "SecretKey": "aA1bB2cC3dD4efE5gG6hH7",
        "StorageDirectoryPath": "C:\\"
    }
}
```

**SessionIdLength** - Session's id length. Default is 32
**SessionTokenLength** - Session's token length. Default is 128
**SessionRefreshIntervalInMin** - Interval in minutes at which the session is proactively refreshed to extend its lifetime. This does not affect the maximum session lifetime. Default is 0 minutes.
**SessionLifetimeInMin** - Session's life time in min, if there was no refresh. Default is 10080 (7 day)
**SessionMaxNumberOfErrors** - Max error number. If the number of variables under the current key exceeds this number, the session will be deleted from memory immediately
**SecretKey** - The secret key is used to sign data and to encrypt data in a file.
**StorageDirectoryPath** - Path to the folder where your sessions will be stored for a long time. Optional parameter

3. **Registers sessions provider** as a singleton with config from 'AlgaSessionsConfig'.

```
var algaSessionsConfig = builder.Configuration.GetSection("AlgaSessionsConfig").Get<Alga.sessions.Models.Config>();
builder.Services.AddSingleton(sp => new Alga.sessions.Provider(algaSessionsConfig));
```

4. **Create session** (server code)

Storing sessions in memory is an expensive operation, so it is recommended to create a session only if the user is authenticated by: Login/Password or OAuth: Google / Facebook/ X /...

After authentication, it is necessary to define the data model that will be transferred to the client, they can be anything, a data: role, user id, etc.

```
var userContext = ${m.userId}:{m.roleId}; 
```

Create session:

```
var session = sessionProvider.Create(userContext);
```


### Upates

What has been changed in new build (2.0.0) compared to the previous version (1.1.1)

- Убрали json model { } токена для передачи между сервер-клиент-сервер, заменили на строку с разделителем {userId}:{roleId}:{token} чтобы избавиться от json  ссериализации и десериализации

- Добавили не обязательный но рекоммендуемый параметр Client Key в методы Creaate / Refresh / Check - для дополнительного механиза защиты сессий от подделки. Например для web это может название устройства с информацией о браузере, которая автоматически отправляется клиентом в context










# Alga.sessions

A lightweight .NET library for streamlined session management: Create, Refresh, Validation, Deletion. Sessions are stored in RAM for quick access. For long-term storage of sessions, you can use an automatically created file that is updated once a minute, for this you just need to specify the path to the directory.

## How does this work. Step by step

1. **Install-Package** [Alga.sessions](https://www.nuget.org/packages/Alga.sessions)

2. **Setting up the configuration** (appsettings.json)

```
{
    ...,
    "AlgaSessionsConfig": {
        "SessionIdLength": 32, 
        "SessionTokenLength": 128,
        "SessionRefreshIntervalInMin": 5 
        "SessionLifetimeInMin": 5040,
        "SessionMaxNumberOfErrors": 10000000,
        "StorageDirectoryPath": "C:\\",
        "StorageEncryptionKey": "aA1bB2cC3dD4efE5gG6hH7"
    }
}
```

**SessionIdLength** - Session's id length. Default is 32
**SessionTokenLength** - Session's token length. Default is 128
**SessionRefreshIntervalInMin** - Interval in minutes at which the session is proactively refreshed to extend its lifetime. This does not affect the maximum session lifetime. Default is 0 minutes.
**SessionLifetimeInMin** - Session's life time in min, if there was no refresh. Default is 10080 (7 day)
**SessionMaxNumberOfErrors** - Max error number. If the number of variables under the current key exceeds this number, the session will be deleted from memory immediately
**StorageDirectoryPath** - Path to the folder where your sessions will be stored for a long time. Optional parameter
**StorageEncryptionKey** - Key for encrypting data in storage file. Optional parameter

3. **Registers sessions provider** as a singleton with config from 'AlgaSessionsConfig'.

```
var algaSessionsConfig = builder.Configuration.GetSection("AlgaSessionsConfig").Get<Alga.sessions.Models.Config>();
builder.Services.AddSingleton(sp => new Alga.sessions.Provider(algaSessionsConfig));
```

4. **Create session** (server code)

Storing sessions in memory is an expensive operation, so it is recommended to create a session only if the user is authenticated by: Login/Password or OAuth: Google / Facebook/ X /...

After authentication, it is necessary to define the data model that will be transferred to the client, they can be anything, a data: role, user id, etc.

```
var userContext = new { userId = m.userId, roleId = m.roleId }; 
```

Create session:

```
var session = sessionProvider.Create(JsonSerializer.Serialize(userContext));
```

5. **Refresh session**

The client sends the user's data model to the server that it received before. The session token (getted form user's data model) is checked and the data model with the new key is returned to the client.

Client:

```
var headers = { };
headers["AlgaSession"] = localStorage.getItem("AlgaSession");
const response = await fetch($"/SessionRefresh", { "POST", headers });

if (response?.ok && response.status === 200) {
    var json = await response.json();

    if (json?.roleId) {
        localStorage.setItem("AlgaSession", JSON.stringify(json));
    } else localStorage.removeItem("AlgaSession");
}
```

Server:

```
app.MapGet($"/SessionRefresh", (HttpContext context, Alga.sessions.Simple sessionProvider) => { 
    var head = Context.Request.Headers["AlgaSession"].ToString();
    var session = sessionProvider.Refresh(head);
    return Results.Text(session);
});

```


6. **Check session**

```
Microsoft.Extensions.Primitives.StringValues value = ""; 
context.Request.Headers.TryGetValue(name, out value);
if (session.Check(value))
    return true;
```

7. **Delete session**

```
app.MapPost($"{UR_Auth}/Signout", (HttpContext context, Alga.sessions.Simple sessionProvider) => { 
    var head = Context.Request.Headers["AlgaSession"].ToString();
    var idF = sessionProvider.Delete(head);
    if (!idF) return Results.BadRequest();
    return Results.Ok();
});
```

### Upates

What has been changed in new build (1.1.3) compared to the previous version (1.1.1)

- Critical error fixed