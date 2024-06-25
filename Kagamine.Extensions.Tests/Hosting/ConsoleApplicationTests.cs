// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Kagamine.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace Kagamine.Extensions.Tests.Hosting;

public class ConsoleApplicationTests
{
    public interface IMockHostedService : IHostedService; // Needed to differentiate type

    [Fact]
    public void InjectsDependencies()
    {
        var builder = ConsoleApplication.CreateBuilder();
        builder.Services.AddSingleton(this);
        builder.Run((ConsoleApplicationTests foo, CancellationToken _) =>
        {
            Assert.Same(this, foo);
        });
    }

    [Fact]
    public void RunsHostedServices()
    {
        var hostedService = new Mock<IMockHostedService>();

        var builder = ConsoleApplication.CreateBuilder();
        builder.Services.AddHostedService(_ => hostedService.Object);
        builder.Run((CancellationToken _) =>
        {
            hostedService.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()));
            hostedService.VerifyNoOtherCalls();
        });

        hostedService.Verify(x => x.StopAsync(It.IsAny<CancellationToken>()));
    }

    [Fact]
    public void CancelsWhenStopped()
    {
        var builder = ConsoleApplication.CreateBuilder();
        builder.Run((IHostApplicationLifetime lifetime, CancellationToken cancellationToken) =>
        {
            Assert.True(lifetime.ApplicationStarted.IsCancellationRequested);
            Assert.False(lifetime.ApplicationStopped.IsCancellationRequested);
            Assert.False(cancellationToken.IsCancellationRequested);
            lifetime.StopApplication();
            Assert.True(cancellationToken.IsCancellationRequested);
            Assert.True(lifetime.ApplicationStopped.IsCancellationRequested);
        });
    }

    [Fact]
    public void LogsUnhandledExceptionsAndSetsExitCode()
    {
        var logger = new Mock<ILogger<ConsoleApplication>>();
        var exception = new Exception("foo");

        var builder = ConsoleApplication.CreateBuilder();
        builder.Services.AddSingleton(logger.Object);
        builder.Run(void (CancellationToken _) =>
        {
            throw exception;
        });

        logger.Verify(x => x.Log(LogLevel.Critical, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), exception, It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

        Assert.Equal(255, Environment.ExitCode);
    }

    [Fact]
    public void DisposesServices()
    {
        var disposableService = new Mock<IDisposable>();

        var builder = ConsoleApplication.CreateBuilder();
        builder.Services.AddSingleton(_ => disposableService.Object);
        builder.Run((IDisposable _, CancellationToken _) => { });

        disposableService.Verify(x => x.Dispose());
    }
}
