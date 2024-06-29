// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Kagamine.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        var host = ConsoleApplication.CreateBuilder().Build();
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

        host.Run((CancellationToken cancellationToken) =>
        {
            Assert.True(lifetime.ApplicationStarted.IsCancellationRequested);
            Assert.False(lifetime.ApplicationStopped.IsCancellationRequested);
            Assert.False(cancellationToken.IsCancellationRequested);
            lifetime.StopApplication();
            Assert.True(cancellationToken.IsCancellationRequested);
        });

        Assert.True(lifetime.ApplicationStopped.IsCancellationRequested);
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
