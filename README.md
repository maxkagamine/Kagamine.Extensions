# <img src="https://raw.githubusercontent.com/maxkagamine/Kagamine.Extensions/bf0cdc7eea084e4bb7fbee740147242df87b975c/icon.svg" width="38" height="38" alt="🍊️" align="top" />&nbsp;Kagamine.Extensions

[![Kagamine.Extensions](https://img.shields.io/nuget/v/Kagamine.Extensions?logo=nuget&label=Kagamine.Extensions)](https://www.nuget.org/packages/Kagamine.Extensions) [![Kagamine.Extensions.EntityFramework](https://img.shields.io/nuget/v/Kagamine.Extensions.EntityFramework?logo=nuget&label=Kagamine.Extensions.EntityFramework)](https://www.nuget.org/packages/Kagamine.Extensions.EntityFramework)

[日本語](README.ja.md)

This repository contains a suite of libraries that provide facilities commonly needed when creating production-ready applications (as Microsoft puts it). Human-coded, as with all of my work.

- [Hosting](#hosting)
  - [ConsoleApplication.CreateBuilder()](#consoleapplicationcreatebuilder)
- [Collections](#collections)
  - [ValueArray\<T\>](#valuearrayt)
- [IO](#io)
  - [TemporaryFileProvider](#temporaryfileprovider)
- [Http](#http)
  - [RateLimitingHttpHandler](#ratelimitinghttphandler)
- [Logging](#logging)
  - [BeginTimedOperation](#begintimedoperation)
- [Utilities](#utilities)
  - [TerminalProgressBar](#terminalprogressbar)
- [EntityFramework](#entityframework)
  - [Update\<T\>(this DbSet\<T\> set, T entity, T valuesFrom)](#updatetthis-dbsett-set-t-entity-t-valuesfrom)
  - [ToHashSetAsync\<T\>()](#tohashsetasynct)

## Hosting

### ConsoleApplication.CreateBuilder()

Tailors the [Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host) framework for console apps as WebApplication does for ASP.NET Core. Using IHost is desirable for its dependency injection, logging, and configuration setup as well as for consistency with web apps (not to mention EF Core migrations uses it to discover the DbContext), but the out-of-box experience is mainly designed for background workers which leads to some frustrations when trying to use it in a regular executable.

Example Program.cs:

```cs
using Kagamine.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = ConsoleApplication.CreateBuilder();

builder.Services.AddDbContext<FooContext>();
builder.Services.AddScoped<IFooService, FooService>();

// May optionally be async and/or return an exit code
builder.Run((IFooService fooService, CancellationToken cancellationToken) =>
{
    fooService.DoStuff(cancellationToken);
});
```

Compared to repurposing IHostedService or BackgroundService to run a console app:

- The entry point is much cleaner and more natural (reminiscent of minimal APIs)
- `SIGINT`, `SIGQUIT`, and `SIGTERM` produce the correct exit codes (in background services, a Ctrl+C is supposed to trigger a "graceful" shutdown and exit with zero, which only makes sense for a long-lived worker or server)
- Unhandled exceptions are the same as a regular console app, just going through the ILogger instead (not printed twice with a message for developers tacked on; services are also disposed to ensure logs are flushed)
- Application lifetime events are properly handled (which a lot of IHostedService examples actually get wrong; implementing it correctly is surprisingly unintuitive, and it's not meant for this anyway)

Several real-world examples of this being used can be found [in Serifu.org's projects](https://github.com/maxkagamine/Serifu.org/blob/master/Serifu.Importer.Skyrim/Program.cs).

> [!NOTE]
> ASP.NET Core projects include a launchSettings.json by default which sets the environment to "Development" in dev, but you have to [create this file yourself](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/environments) for a console app. The easiest way in Visual Studio is to open Debug > {Project Name} Debug Properties and under Environment Variables add `DOTNET_ENVIRONMENT` = `Development`. Note that the `ASPNETCORE_` prefix won't work here, as that's specific to WebApplication.

## Collections

### ValueArray&lt;T&gt;

There's currently no solution in .NET for putting a collection in a record while maintaining both immutability and value semantics. It's also sometimes necessary to have access to the underlying array for interop with APIs that do not support spans (especially for byte arrays, where copying can have a significant performance impact).

To solve this, I've created a ValueArray&lt;T&gt; type which represents a read-only array with value type semantics suitable for use in immutable records:

| Type                        | Immutable        | Value equality | To/from array w/o copying |
| --------------------------- | ---------------- | -------------- | ------------------------- |
| T[]                         | ❌               | ❌            | ✅                        |
| List&lt;T&gt;               | ❌               | ❌            | ❌                        |
| ReadOnlyCollection&lt;T&gt; | ✅<sup>1</sup>   | ❌            | ❌                        |
| IReadOnlyList&lt;T&gt;      | ✅<sup>1</sup>   | ❌            | ❌                        |
| ImmutableArray&lt;T&gt;<sup>2</sup> | ✅<sup>3,4</sup> | ❌    | ✅<sup>3</sup>            |
| ReadOnlyMemory&lt;T&gt;     | ✅<sup>4,5</sup> | ❌            | ⚠<sup>5</sup>            |
| **ValueArray&lt;T&gt;**     | ✅<sup>4,6</sup> | ✅            | ✅<sup>6</sup>            |

> 1. ReadOnlyCollection&lt;T&gt; is merely a read-only view of a List&lt;T&gt;, and IReadOnlyList&lt;T&gt; is usually the List&lt;T&gt; itself.
> 2. Has a bug caused by misuse of the null suppression operator that can cause a null reference exception which won't be caught by static analysis if any code returns its `default`. (ValueArray&lt;T&gt; fixes this by treating a null array as empty, as it is also a struct.)
> 3. [ImmutableCollectionsMarshal](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.immutablecollectionsmarshal) can be used to access the underlying array or create an instance backed by an existing array.
> 4. Can be modified inadvertently if a reference is held to the array used to construct it, or if the underlying buffer is accessed and passed to a method that does not treat it as read-only.
> 5. Depending on how the ReadOnlyMemory&lt;T&gt; was created, it may be possible to access the buffer using [MemoryMarshal](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.memorymarshal.trygetarray), but there's no guarantee the instance is backed by an actual array, or it may represent a slice of an array (like Span&lt;T&gt;).
> 6. Supports implicit conversion from T[], and the underlying array can be accessed via explicit cast to T[].

ValueArray&lt;T&gt; supports both [collection expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/collection-expressions) and array initializers (via implicit cast):

```cs
record Song(string Title, ValueArray<string> Artists);

Song song = new("Promise", ["samfree", "Kagamine Rin", "Hatsune Miku"]);
Song song2 = song with { Artists = [.. song.Artists] /* Clone the array */ };

// These would fail if Artists were List<T>, despite the contents being identical
Assert.True(song == song2);
Assert.True(song.Artists == song2.Artists);

ValueArray<Song> songs = new[] { song, song2 };
```

It's interoperable with spans as well as APIs requiring arrays such as Entity Framework. Using a value converter, a ValueArray&lt;byte&gt; can be cast to its underlying byte[] to use as a BLOB column without the overhead of copying an array:

```cs
entity.Property<ValueArray<byte>>(x => x.Data)
    .HasColumnName("data")
    .HasConversion(model => (byte[])model, column => column);
```

When `T` is an [unmanaged type](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/unmanaged-types), ValueArray&lt;T&gt; can also be marshaled to and from ReadOnlySpan&lt;byte&gt;. This could be used, for instance, to store an array of structs in a database as an opaque blob using their binary representation.

I've created a JsonConverter that uses this to efficiently serialize a ValueArray&lt;T&gt; as a base64 string:

```cs
ValueArray<DateTime> dates = [ DateTime.Parse("2007-08-31"), DateTime.Parse("2007-12-27") ];

var options = new JsonSerializerOptions() { Converters = { new JsonBase64ValueArrayConverter() } };
var json = JsonSerializer.Serialize(dates, options); // "AIAeAnm5yQgAAN2OMhbKCA=="
```

To deserialize a JSON array as ValueArray&lt;T&gt; (as System.Text.Json cannot natively deserialize to a custom readonly collection), use the JsonValueArrayConverter. Both converters have generic versions to mix-and-match for specific T's.

## IO

### TemporaryFileProvider

Provides a number of advantages for working with temp files over `Path.GetTempFileName()`:

- Unlike `GetTempFileName()`, it's possible to specify a file extension or suffix, which may be necessary when passing the file path to certain programs (and unlike common solutions on Stack Overflow, it guarantees that the file name is unique / avoids race conditions);
- Temp files are stored in an application-specific directory which is removed if empty when the application quits;
- The TemporaryFile can be placed in a `using` which will automatically clean up the temp file when disposed;
- TemporaryFile _doesn't_ maintain an open handle to the file, which allows for the file path to be passed to other programs like ffmpeg which may overwrite or replace the file (and expect it to not be in use);
- 👉 **Most importantly:** TemporaryFile keeps a ref count and only deletes the file once it and all streams have been disposed, which means a method can return a FileStream backed by the temp file and not have to worry about cleanup, vastly simplifying common error handling patterns such as:

```cs
public async Task<Stream> ConvertToOpus(Stream inputStream, CancellationToken cancellationToken)
{
    using TemporaryFile inputFile = tempFileProvider.Create();
    await inputFile.CopyFromAsync(inputStream);

    using TemporaryFile outputFile = tempFileProvider.Create(".opus");

    await FFMpegArguments
        .FromFileInput(inputFile.Path)
        .OutputToFile(outputFile.Path, overwrite: true, options => options
            .WithAudioBitrate(Bitrate))
        .CancellableThrough(cancellationToken)
        .ProcessAsynchronously();

    // If ffmpeg throws, both temp files will be deleted.
    // 
    // If it succeeds, the input file is deleted, but the output file remains on
    // disk until the returned stream is closed, at which point the remaining
    // temp file will be cleaned up automatically.
    return outputFile.OpenRead();
}
```

ITemporaryFileProvider is added to the service container like so:

```cs
services.AddTemporaryFileProvider();
```

Or you can construct a TemporaryFileProvider yourself if not using DI. The temp directory and base filename format (by default a guid) can be changed via the options (see its overloads).

## Http

### RateLimitingHttpHandler

A DelegatingHandler that uses [System.Threading.RateLimiting](https://devblogs.microsoft.com/dotnet/announcing-rate-limiting-for-dotnet/) to force requests to the same host to wait for a configured period of time since the last request completed before sending a new request:

```cs
// Add the rate limiter to all HttpClients
builder.Services.ConfigureHttpClientDefaults(builder => builder.AddRateLimiting());

// In libraries, consider adding it only to your own named or typed client; the
// rate limit won't stack even if the top-level project adds it to all clients
builder.Services.AddHttpClient("foo").AddRateLimiting();

// Alternatively, if not using DI
using RateLimitingHttpHandlerFactory rateLimiterFactory = new();
RateLimitingHttpHandler rateLimiter = rateLimiterFactory.CreateHandler();
rateLimiter.InnerHandler = new HttpClientHandler();
HttpClient client = new(rateLimiter);
```

When using DI, the per-host rate limit is shared across all named clients. This avoids accidentally hitting a host more frequently than intended simply because the code happens to use multiple clients.

To change the default time between requests or set different rate limits per host:

```cs
builder.Services.Configure<RateLimitingHttpHandlerOptions>(options =>
{
    // Setting it to null disables rate limiting by default; can also leave rate
    // limiting on by default and disable it for specific hosts instead.
    // Libraries that need to enforce a particular rate limit to their APIs
    // should avoid relying on the global TimeBetweenRequests.
    options.TimeBetweenRequests = null;
    options.TimeBetweenRequestsByHost.Add("example.com", TimeSpan.FromSeconds(5));
});
```

Note that the timer starts _after_ the response has been received and returned to the caller, not before sending the request. Otherwise, slow responses and network latency could result in requests exhibiting effectively no rate limit.

Run the sample [ConsoleApp](Samples/ConsoleApp/Program.cs) for a demo.

## Logging

### BeginTimedOperation

A small extension method inspired by SerilogMetrics, which I've used on a number of projects in the past:

```cs
using (logger.BeginTimedOperation(nameof(DoStuff)))
{
    logger.Debug("Doing stuff...");
}
// [12:00:00 INF] DoStuff: Starting
// [12:00:00 DBG] Doing stuff...
// [12:00:01 INF] DoStuff: Completed in 39 ms
```

## Utilities

### TerminalProgressBar

Sends ANSI escape codes to display a [progress bar in the terminal](https://learn.microsoft.com/en-us/windows/terminal/tutorials/progress-bar-sequences) and clear it automatically when disposed:

```cs
using var progress = new TerminalProgressBar();

for (int i = 0; i < foos.Count; i++)
{
    logger.Information("Foo {Foo} of {TotalFoos}", i + 1, foos.Count);
    progress.SetProgress(i, foos.Count);

    await fooService.DoStuff(foos[i]);
}
```

## EntityFramework

### Update&lt;T&gt;(this DbSet&lt;T&gt; set, T entity, T valuesFrom)

Allows for replacing an existing entity with a new instance, correctly transferring both regular property values and navigation properties, since EF will throw if you try to pass a detached entity to Update() while another instance with the same primary key is tracked (e.g. by another query performed elsewhere):

```cs
var existingEntities = await db.Foos.ToDictionaryAsync(f => f.Id);

foreach (var entity in entities)
{
    if (existingEntities.Remove(entity.Id, out var existingEntity))
    {
        db.Foos.Update(existingEntity, entity);
    }
    else
    {
        db.Foos.Add(entity);
    }
}

db.Foos.RemoveRange(existingEntities.Values);
await db.SaveChangesAsync();
```

> [!TIP]
> If it makes sense for your application, consider using IDbContextFactory instead and [creating a new context for each unit of work](https://github.com/maxkagamine/Serifu.org/blob/master/Serifu.Data.Sqlite/SqliteService.cs). Doing so can avoid the sort of "[spooky action at a distance](https://en.wikipedia.org/wiki/Action_at_a_distance_(computer_programming))" (other parts of the code affecting the state of the change tracker unpredictably) that makes doing something like this necessary.

### ToHashSetAsync&lt;T&gt;()

⚠ **Removed in v2.0.0, as this was made official in EF 9. Older projects can copy the method [from here](https://github.com/maxkagamine/Kagamine.Extensions/blob/v1.10.2/Kagamine.Extensions.EntityFramework/EntityFrameworkExtensions.cs).**

Mirrors ToArrayAsync and ToListAsync. Implemented using `await foreach`, like the other two, making it slightly more performant than doing ToListAsync then ToHashSet :

```cs
HashSet<string> referencedFiles = await db.Foos
    .Select(f => f.FilePath)
    .ToHashSetAsync(StringComparer.OrdinalIgnoreCase);

foreach (var file in Directory.EnumerateFiles(dir))
{
    if (!referencedFiles.Contains(file))
    {
        logger.Warning("Deleting orphaned file {Path}", file);
        File.Delete(file);
    }
}
```
