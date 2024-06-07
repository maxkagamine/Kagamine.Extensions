// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        // Set up signal handlers and wait for any hosted services to start
        host.StartAsync().Wait();

        // Run the entrypoint function
        try
        {
            action(lifetime.ApplicationStopping);
        }
        catch (OperationCanceledException)
        {
            // no op
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Unhandled exception.");
            Environment.ExitCode = 255;
            Console.Write("\a"); // Flashes the taskbar if the terminal's not in the foreground
        }

        // Stop any running hosted services and trigger shutdown event
        host.StopAsync().Wait();
    }

    Task IHost.StartAsync(CancellationToken cancellationToken) => host.StartAsync(cancellationToken);

    Task IHost.StopAsync(CancellationToken cancellationToken) => host.StopAsync(cancellationToken);

    public void Dispose() => host.Dispose();
}
