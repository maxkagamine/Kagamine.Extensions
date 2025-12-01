// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Kagamine.Extensions.Hosting;

// This is set up in a similar way to WebApplication.CreateBuilder(). Another way to tackle this would have been to add
// extension methods to IHost and simply not call StartAsync(), but this approach is perhaps a bit more "correct" and
// still allows for the use of hosted services if required. Note that no matter what, we MUST build an IHost (not just
// the IServiceProvider), as EF relies on the "HostBuilt" diagnostic event to stop execution of the startup project:
// https://github.com/dotnet/runtime/blob/v8.0.6/src/libraries/Microsoft.Extensions.HostFactoryResolver/src/HostFactoryResolver.cs
// https://github.com/dotnet/runtime/blob/v8.0.6/src/libraries/Microsoft.Extensions.Hosting/src/HostBuilder.cs#L377
public sealed class ConsoleApplication : IHost
{
    private readonly IHost host;

    internal ConsoleApplication(IHost host)
    {
        this.host = host;
    }

    public static ConsoleApplicationBuilder CreateBuilder() => new();

    public static ConsoleApplicationBuilder CreateBuilder(string[]? args) => new(args);

    public static ConsoleApplicationBuilder CreateBuilder(HostApplicationBuilderSettings? settings) => new(settings);

    public IServiceProvider Services => host.Services;

    /// <summary>
    /// Runs the application.
    /// </summary>
    /// <param name="action">The application entry point. Passed a <see cref="CancellationToken"/> which is triggered
    /// upon receiving a Ctrl+C or other stop signal; any additional parameters are resolved as scoped dependencies. May
    /// be async and/or return an exit code.</param>
    public void Run(Action<CancellationToken> action)
    {
        var lifetime = Services.GetRequiredService<IHostApplicationLifetime>();
        var logger = Services.GetRequiredService<ILogger<ConsoleApplication>>();

        // Using the unhandled exception handler instead of a try-catch so that the debugger breaks on unhandled
        // exceptions without needing to set it to break on every thrown exception (which can be annoying)
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            logger.LogCritical((Exception)e.ExceptionObject, "Unhandled exception.");

            if (!Console.IsOutputRedirected)
            {
                Console.Write("\a"); // Flashes the taskbar if the terminal's not in the foreground
            }

            // Make sure services are disposed and logs are flushed
            try
            {
                host.Dispose();
            }
            catch { }

            // Prevent the CLR from handling the exception and printing it to the console a second time, unless we're
            // debugging in which case it's unavoidable (exiting here would prevent the debugger from breaking on the
            // exception, and Console.SetError() doesn't work as the write to stderr happens in the runtime)
            if (!Debugger.IsAttached)
            {
                Environment.Exit(255);
            }
        };

        // Set up signal handlers and wait for any hosted services to start
        host.StartAsync().GetAwaiter().GetResult();

        // Run the entrypoint function
        try
        {
            action(lifetime.ApplicationStopping);
        }
        catch (OperationCanceledException)
        {
            // no op
        }

        // Stop any running hosted services and trigger shutdown event
        host.StopAsync().GetAwaiter().GetResult();

        // Make sure services are disposed and logs are flushed
        host.Dispose();
    }

    Task IHost.StartAsync(CancellationToken cancellationToken) => host.StartAsync(cancellationToken);

    Task IHost.StopAsync(CancellationToken cancellationToken) => host.StopAsync(cancellationToken);

    public void Dispose() => host.Dispose();
}
