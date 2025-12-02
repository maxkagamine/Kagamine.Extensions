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
    private readonly HttpClient client;

    private readonly List<(TimeSpan Time, HttpRequestMessage Request)> requests = [];

    public RateLimitingHttpHandlerTests()
    {
        handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        ServiceCollection services = new();

        services.Configure<RateLimitingHttpHandlerOptions>(options => options.TimeBetweenRequests = TimeBetweenRequests);
        services.ConfigureHttpClientDefaults(builder => builder.AddRateLimiting()
           .ConfigurePrimaryHttpMessageHandler(() => handler.Object));

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        client = serviceProvider.GetRequiredService<HttpClient>();

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

        services.AddHttpClient(Options.DefaultName).AddRateLimiting()
            .ConfigurePrimaryHttpMessageHandler(() => handler.Object);

        services.AddHttpClient("foo").AddRateLimiting()
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
    public async Task CanSetRateLimitPerHost()
    {
        ServiceCollection services = new();

        TimeSpan t1 = TimeSpan.FromMilliseconds(450);
        TimeSpan t2 = TimeSpan.FromMilliseconds(700);

        services.Configure<RateLimitingHttpHandlerOptions>(options =>
        {
            options.TimeBetweenRequests = t1;
            options.TimeBetweenRequestsByHost.Add("example.org", t2);
        });

        services.ConfigureHttpClientDefaults(builder => builder.AddRateLimiting()
           .ConfigurePrimaryHttpMessageHandler(() => handler.Object));

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        var client = serviceProvider.GetRequiredService<HttpClient>();

        await Task.WhenAll(
            client.GetAsync("http://example.com/first"),
            client.GetAsync("http://example.com/second"),
            client.GetAsync("http://example.org/third"),
            client.GetAsync("http://example.org/fourth"),
            client.GetAsync("http://example.org/fifth"),
            client.GetAsync("http://example.com/sixth")
        );

        // first   0ms
        // third          0ms
        // second  450ms
        // fourth         700ms
        // sixth   900ms
        // fifth          1400ms

        Assert.Equal(6, requests.Count);
        Assert.Equal(new Uri("http://example.com/first"), requests[0].Request.RequestUri);
        Assert.Equal(new Uri("http://example.org/third"), requests[1].Request.RequestUri);
        Assert.Equal(new Uri("http://example.com/second"), requests[2].Request.RequestUri);
        Assert.Equal(new Uri("http://example.org/fourth"), requests[3].Request.RequestUri);
        Assert.Equal(new Uri("http://example.com/sixth"), requests[4].Request.RequestUri);
        Assert.Equal(new Uri("http://example.org/fifth"), requests[5].Request.RequestUri);

        Assert.Equal(requests[0].Time.TotalMilliseconds, requests[1].Time.TotalMilliseconds, tolerance: MillisecondsTolerance);

        Assert.Equal(t1.TotalMilliseconds, (requests[2].Time - requests[0].Time).TotalMilliseconds, tolerance: MillisecondsTolerance);
        Assert.Equal(t1.TotalMilliseconds, (requests[4].Time - requests[2].Time).TotalMilliseconds, tolerance: MillisecondsTolerance);

        Assert.Equal(t2.TotalMilliseconds, (requests[3].Time - requests[1].Time).TotalMilliseconds, tolerance: MillisecondsTolerance);
        Assert.Equal(t2.TotalMilliseconds, (requests[5].Time - requests[3].Time).TotalMilliseconds, tolerance: MillisecondsTolerance);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CanSelectivelyDisableRateLimiting(bool defaultIsNull)
    {
        ServiceCollection services = new();

        services.Configure<RateLimitingHttpHandlerOptions>(options =>
        {
            // These should have the same effect on the below requests
            if (defaultIsNull)
            {
                options.TimeBetweenRequests = null;
                options.TimeBetweenRequestsByHost.Add("example.org", TimeBetweenRequests);
            }
            else
            {
                options.TimeBetweenRequests = TimeBetweenRequests;
                options.TimeBetweenRequestsByHost.Add("example.com", null);
            }
        });

        services.ConfigureHttpClientDefaults(builder => builder.AddRateLimiting()
           .ConfigurePrimaryHttpMessageHandler(() => handler.Object));

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        var client = serviceProvider.GetRequiredService<HttpClient>();

        await Task.WhenAll(
            client.GetAsync("http://example.com/first"),
            client.GetAsync("http://example.com/second"),
            client.GetAsync("http://example.org/third"),
            client.GetAsync("http://example.org/fourth"),
            client.GetAsync("http://example.org/fifth"),
            client.GetAsync("http://example.com/sixth")
        );

        Assert.Equal(6, requests.Count);

        var first = requests.Single(r => r.Request.RequestUri == new Uri("http://example.com/first")).Time.TotalMilliseconds;
        var second = requests.Single(r => r.Request.RequestUri == new Uri("http://example.com/second")).Time.TotalMilliseconds;
        var third = requests.Single(r => r.Request.RequestUri == new Uri("http://example.org/third")).Time.TotalMilliseconds;
        var fourth = requests.Single(r => r.Request.RequestUri == new Uri("http://example.org/fourth")).Time.TotalMilliseconds;
        var fifth = requests.Single(r => r.Request.RequestUri == new Uri("http://example.org/fifth")).Time.TotalMilliseconds;
        var sixth = requests.Single(r => r.Request.RequestUri == new Uri("http://example.com/sixth")).Time.TotalMilliseconds;

        // All three .com's and the first .org happen at the same time
        Assert.Equal(first, second, tolerance: MillisecondsTolerance);
        Assert.Equal(first, third, tolerance: MillisecondsTolerance);
        Assert.Equal(first, sixth, tolerance: MillisecondsTolerance);

        Assert.Equal(TimeBetweenRequests.TotalMilliseconds, fourth - first, tolerance: MillisecondsTolerance);
        Assert.Equal(TimeBetweenRequests.TotalMilliseconds, fifth - fourth, tolerance: MillisecondsTolerance);
    }

    [Fact]
    public async Task PreventsDuplicateHandler()
    {
        ServiceCollection services = new();

        services.Configure<RateLimitingHttpHandlerOptions>(options =>
        {
            options.TimeBetweenRequests = TimeBetweenRequests;
        });

        // Suppose the main project configures rate limiting for all clients
        services.ConfigureHttpClientDefaults(builder => builder.AddRateLimiting()
            .ConfigurePrimaryHttpMessageHandler(() => handler.Object));

        // But a library also configures rate limiting for its own client
        services.AddHttpClient("foo").AddRateLimiting()
            .ConfigurePrimaryHttpMessageHandler(() => handler.Object);

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        var client = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("foo");

        // If we simply used AddHttpMessageHandler, this would end up with two rate limiters which would deadlock
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));

        await Task.WhenAll(
            client.GetAsync("http://example.com/first", cts.Token),
            client.GetAsync("https://example.com/second", cts.Token)
        );

        Assert.Equal(2, requests.Count);
        Assert.Equal(TimeBetweenRequests.TotalMilliseconds, (requests[1].Time - requests[0].Time).TotalMilliseconds, tolerance: MillisecondsTolerance);
    }

    [Fact]
    public async Task CanUseWithoutDI()
    {
        using RateLimitingHttpHandlerFactory rateLimiterFactory = new(new RateLimitingHttpHandlerOptions()
        {
            TimeBetweenRequests = TimeBetweenRequests,
        });

        RateLimitingHttpHandler rateLimiter = rateLimiterFactory.CreateHandler();
        rateLimiter.InnerHandler = handler.Object;
        HttpClient client = new(rateLimiter);

        await Task.WhenAll(
            client.GetAsync("http://example.com/first"),
            client.GetAsync("https://example.com/second")
        );

        Assert.Equal(2, requests.Count);
        Assert.Equal(TimeBetweenRequests.TotalMilliseconds, (requests[1].Time - requests[0].Time).TotalMilliseconds, tolerance: MillisecondsTolerance);
    }
}
