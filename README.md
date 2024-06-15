# 🍊 Kagamine.Extensions

This repository contains a suite of libraries that provide facilities commonly needed when creating production-ready applications. (As Microsoft puts it.)

## Hosting

The main attraction here is a `ConsoleApplication.CreateBuilder()` and its simplified `Run()` methods which, as WebApplication does for ASP.NET Core, tailors the [Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host) framework for console apps. Using IHost is desirable for its dependency injection, logging, and configuration setup and consistency with web apps (not to mention EF Core migrations uses it to discover the DbContext), but the out-of-box experience is mainly designed for background workers which leads to some frustrations when trying to use it for a regular executable.

Wanting to use a source generator to create the `Run()` overloads (separate delegates for varying number of DI'd services × returning an exit code or not × being async or not = lots of overloads) was my main motivation for making this its own library.

Example Program.cs:

```cs
using Kagamine.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = ConsoleApplication.CreateBuilder();

builder.Services.AddDbContext<FooContext>();
builder.Services.AddScoped<IFooService, FooService>();

builder.Run((IFooService fooService, CancellationToken cancellationToken) =>
{
    fooService.DoStuff(cancellationToken);
});
```

> [!NOTE]
> ASP.NET Core projects include a launchSettings.json by default which sets the environment to "Development" in dev, but this [needs to be done manually](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/environments) for a console app. The easiest way in Visual Studio is to open Debug > {Project Name} Debug Properties and under Environment Variables add DOTNET_ENVIRONMENT = Development. Note that the `ASPNETCORE_` prefix won't work here, as it's not a WebApplication.

## Logging

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

TerminalProgressBar class which sends ANSI escape codes to display a [progress bar in the terminal](https://learn.microsoft.com/en-us/windows/terminal/tutorials/progress-bar-sequences) and clear it automatically when disposed:

```cs
using var progress = new TerminalProgressBar();

for (int i = 0; i < foos.Count; i++)
{
    logger.Information("Foo {Foo} of {TotalFoos}", i + 1, foos.Count);
    progress.SetProgress(i, foos.Count);

    await fooService.DoStuff(foos[i]);
}
```

RateLimitingHttpHandler, a DelegatingHandler that uses [System.Threading.RateLimiting](https://devblogs.microsoft.com/dotnet/announcing-rate-limiting-for-dotnet/) to force requests to the same host to wait for a configured period of time since the last request completed before sending a new request (run the sample ConsoleApp for a demo):

```cs
builder.Services.AddSingleton<RateLimitingHttpHandler>();
builder.Services.AddHttpClient(Options.DefaultName).AddHttpMessageHandler<RateLimitingHttpHandler>();
```

## EntityFramework

`ToHashSetAsync<T>()`, mirroring ToArrayAsync and ToListAsync. Implemented using `await foreach`, like the other two, making it slightly more performant than doing ToListAsync then ToHashSet:

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

`Update<T>(this DbSet<T> set, T entity, T valuesFrom)` for replacing an existing entity with a new instance, since EF will throw if you try to pass a detached entity to Update() while another instance with the same primary key is tracked (e.g. by another query performed elsewhere):

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
