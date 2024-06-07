// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reactive.Disposables;
using System.Runtime.InteropServices;

namespace Kagamine.Extensions.Hosting;

/// <summary>
/// A replacement for the default ConsoleLifetime that sets the appropriate exit code corresponding to the signal
/// received (the official stance seems to be that a "graceful" shutdown should exit with 0, which may make sense for a
/// background service but not necessarily for a CLI / foreground app).
/// </summary>
internal class ConsoleLifetime : IHostLifetime
{
    private readonly IHostApplicationLifetime lifetime;
    private readonly ILogger<ConsoleLifetime> logger;

    public ConsoleLifetime(IHostApplicationLifetime lifetime, ILogger<ConsoleLifetime> logger)
    {
        this.lifetime = lifetime;
        this.logger = logger;
    }

    public Task WaitForStartAsync(CancellationToken cancellationToken)
    {
        RegisterSignalHandlers();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Looks a bit nicer; not likely to be searched as a group anyway")]
    private void RegisterSignalHandlers()
    {
        Action<PosixSignalContext> CreateHandler(int exitCode) => context =>
        {
            context.Cancel = true;
            Environment.ExitCode = exitCode;
            logger.LogInformation($"{context.Signal} received.");
            lifetime.StopApplication();
        };

        CompositeDisposable registrations = [
            PosixSignalRegistration.Create(PosixSignal.SIGINT, CreateHandler(130)),
            PosixSignalRegistration.Create(PosixSignal.SIGQUIT, CreateHandler(131)),
            PosixSignalRegistration.Create(PosixSignal.SIGTERM, CreateHandler(143))
        ];

        lifetime.ApplicationStopping.Register(registrations.Dispose);
    }
}
