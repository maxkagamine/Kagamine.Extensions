// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Kagamine.Extensions.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Contrib.HttpClient;
using System.Diagnostics;
using System.Net;

namespace Kagamine.Extensions.Tests.Utilities;

public class RateLimitingHttpHandlerTests
{
    private static readonly TimeSpan TimeBetweenRequests = TimeSpan.FromMilliseconds(500);
    private static readonly double MillisecondsTolerance = 100;

    private readonly Mock<HttpMessageHandler> handler;
    private readonly RateLimitingHttpHandler limiter;
    private readonly HttpClient client;

    private readonly List<(TimeSpan Time, HttpRequestMessage Request)> requests = [];

    public RateLimitingHttpHandlerTests()
    {
        handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        limiter = new RateLimitingHttpHandlerFactory(new() { TimeBetweenRequests = TimeBetweenRequests }).CreateHandler();
        limiter.InnerHandler = handler.Object;

        client = new HttpClient(limiter);

        var sw = Stopwatch.StartNew();

        handler.SetupAnyRequest()
            .ReturnsResponse(HttpStatusCode.OK)
            .Callback((HttpRequestMessage request, CancellationToken _) =>
            {
                var elapsed = sw.Elapsed;
                lock (requests)
                {
                    requests.Add((elapsed, request));
                }
            });
    }

    [Fact]
    public async Task RateLimitsRequestsToSameHost()
    {
        await Task.WhenAll(
            client.GetAsync("http://example.com/first"),
            client.GetAsync("https://example.com/second"),
            client.GetAsync("http://EXAMPLE.COM/third")
        );

        Assert.Equal(3, requests.Count);
        Assert.Equal(TimeBetweenRequests.TotalMilliseconds, (requests[1].Time - requests[0].Time).TotalMilliseconds, tolerance: MillisecondsTolerance);
        Assert.Equal(TimeBetweenRequests.TotalMilliseconds, (requests[2].Time - requests[1].Time).TotalMilliseconds, tolerance: MillisecondsTolerance);
    }

    [Fact]
    public async Task DifferentHostsAreRateLimitedSeparately()
    {
        await Task.WhenAll(
            client.GetAsync("http://example.com/first"),
            client.GetAsync("http://example.com/second"),
            client.GetAsync("http://example.org/third"),
            client.GetAsync("http://example.org/fourth")
        );

        Assert.Equal(4, requests.Count);
        Assert.Equal(new Uri("http://example.com/first"), requests[0].Request.RequestUri);
        Assert.Equal(new Uri("http://example.org/third"), requests[1].Request.RequestUri);

        Assert.Equal(requests[0].Time.TotalMilliseconds, requests[1].Time.TotalMilliseconds, tolerance: MillisecondsTolerance);
        Assert.Equal(requests[2].Time.TotalMilliseconds, requests[3].Time.TotalMilliseconds, tolerance: MillisecondsTolerance);

        Assert.Equal(TimeBetweenRequests.TotalMilliseconds, (requests[2].Time - requests[0].Time).TotalMilliseconds, tolerance: MillisecondsTolerance);
        Assert.Equal(TimeBetweenRequests.TotalMilliseconds, (requests[3].Time - requests[0].Time).TotalMilliseconds, tolerance: MillisecondsTolerance);
    }

    [Fact]
    public async Task ResponseDoesNotWaitForRateLimitToBeReleased()
    {
        var sw = Stopwatch.StartNew();
        await client.GetAsync("http://example.com");
        Assert.True(sw.Elapsed < TimeBetweenRequests);
    }

    [Fact]
    public async Task StartsClockOnlyOnceResponseReceived()
    {
        var extraDelay = TimeSpan.FromSeconds(1);

        handler.SetupRequest("http://example.com/slow")
            .Returns(async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                await Task.Delay(extraDelay, cancellationToken);
                return new(HttpStatusCode.OK);
            });

        var sw = Stopwatch.StartNew();

        await client.GetAsync("http://example.com/slow");
        await client.GetAsync("http://example.com");

        Assert.Equal((TimeBetweenRequests + extraDelay).TotalMilliseconds, sw.Elapsed.TotalMilliseconds, tolerance: MillisecondsTolerance);
    }

    [Fact]
    public async Task MultipleClientsShareRateLimiter()
    {
        ServiceCollection services = new();

        services.Configure<RateLimitingHttpHandlerOptions>(options => options.TimeBetweenRequests = TimeBetweenRequests);

        services.AddHttpClient(Options.DefaultName).AddRateLimiter()
            .ConfigurePrimaryHttpMessageHandler(() => handler.Object);

        services.AddHttpClient("foo").AddRateLimiter()
            .ConfigurePrimaryHttpMessageHandler(() => handler.Object);

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        await Parallel.ForAsync(0, 3, async (i, cancellationToken) =>
        {
            using var scope = serviceProvider.CreateScope();
            var client = i % 2 == 0 ?
                scope.ServiceProvider.GetRequiredService<HttpClient>() :
                scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("foo");

            await client.GetAsync("http://example.com", cancellationToken);
        });

        Assert.Equal(3, requests.Count);
        Assert.Equal(TimeBetweenRequests.TotalMilliseconds, (requests[1].Time - requests[0].Time).TotalMilliseconds, tolerance: MillisecondsTolerance);
        Assert.Equal(TimeBetweenRequests.TotalMilliseconds, (requests[2].Time - requests[1].Time).TotalMilliseconds, tolerance: MillisecondsTolerance);
    }

    [Fact]
    public async Task NamedRateLimitersAreIndependent()
    {
        ServiceCollection services = new();

        Stopwatch sw = Stopwatch.StartNew();

        List<TimeSpan> timesDefault = [];
        Mock<HttpMessageHandler> handlerDefault = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerDefault.SetupAnyRequest()
            .ReturnsResponse(HttpStatusCode.OK)
            .Callback((HttpRequestMessage request, CancellationToken _) =>
            {
                var elapsed = sw.Elapsed;
                lock (timesDefault)
                {
                    timesDefault.Add(elapsed);
                }
            });

        List<TimeSpan> timesFoo = [];
        Mock<HttpMessageHandler> handlerFoo = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerFoo.SetupAnyRequest()
            .ReturnsResponse(HttpStatusCode.OK)
            .Callback((HttpRequestMessage request, CancellationToken _) =>
            {
                var elapsed = sw.Elapsed;
                lock (timesFoo)
                {
                    timesFoo.Add(elapsed);
                }
            });

        services.AddHttpClient(Options.DefaultName)
            .AddRateLimiter(options => options.TimeBetweenRequests = TimeBetweenRequests)
            .ConfigurePrimaryHttpMessageHandler(() => handlerDefault.Object);

        services.AddHttpClient("foo")
            .AddRateLimiter("foo", options => options.TimeBetweenRequests = TimeBetweenRequests * 2)
            .ConfigurePrimaryHttpMessageHandler(() => handlerFoo.Object);

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        await Parallel.ForAsync(0, 6, async (i, cancellationToken) =>
        {
            using var scope = serviceProvider.CreateScope();
            var client = i % 2 == 0 ?
                scope.ServiceProvider.GetRequiredService<HttpClient>() :
                scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("foo");

            await client.GetAsync("http://example.com", cancellationToken);
        });

        // Default    Foo
        // 0ms        0ms
        // 500ms      1000ms
        // 1000ms     2000ms

        Assert.Equal(3, timesDefault.Count);
        Assert.Equal(TimeBetweenRequests.TotalMilliseconds, (timesDefault[1] - timesDefault[0]).TotalMilliseconds, tolerance: MillisecondsTolerance);
        Assert.Equal(TimeBetweenRequests.TotalMilliseconds, (timesDefault[2] - timesDefault[1]).TotalMilliseconds, tolerance: MillisecondsTolerance);

        Assert.Equal(3, timesFoo.Count);
        Assert.Equal(TimeBetweenRequests.TotalMilliseconds * 2, (timesFoo[1] - timesFoo[0]).TotalMilliseconds, tolerance: MillisecondsTolerance);
        Assert.Equal(TimeBetweenRequests.TotalMilliseconds * 2, (timesFoo[2] - timesFoo[1]).TotalMilliseconds, tolerance: MillisecondsTolerance);

        Assert.Equal(timesDefault[0].TotalMilliseconds, timesFoo[0].TotalMilliseconds, tolerance: MillisecondsTolerance);
        Assert.Equal(timesDefault[2].TotalMilliseconds, timesFoo[1].TotalMilliseconds, tolerance: MillisecondsTolerance);
    }
}
