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
