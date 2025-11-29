// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace Kagamine.Extensions.Utilities;

/// <summary>
/// Factory for creating a <see cref="RateLimitingHttpHandler"/> which forces requests to the same host to wait for a
/// configured period of time since the last request completed before sending a new request. All handlers created by
/// this factory share the same rate limiter.
/// </summary>
public sealed class RateLimitingHttpHandlerFactory : IAsyncDisposable, IDisposable
{
    private readonly PartitionedRateLimiter<HttpRequestMessage> rateLimiter =
        PartitionedRateLimiter.Create((HttpRequestMessage req) =>
            RateLimitPartition.GetConcurrencyLimiter(
                partitionKey: req.RequestUri is Uri { IsAbsoluteUri: true } uri ? uri.Host : "",
                factory: _ => new ConcurrencyLimiterOptions()
                {
                    PermitLimit = 1,
                    QueueLimit = int.MaxValue,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }),
                equalityComparer: StringComparer.OrdinalIgnoreCase);

    private readonly Func<RateLimitingHttpHandlerOptions> optionsAccessor;

    /// <summary>
    /// Creates a new <see cref="RateLimitingHttpHandlerFactory"/> with default options.
    /// </summary>
    public RateLimitingHttpHandlerFactory() : this(new RateLimitingHttpHandlerOptions())
    { }

    /// <summary>
    /// Creates a new <see cref="RateLimitingHttpHandlerFactory"/> with the specified options.
    /// </summary>
    public RateLimitingHttpHandlerFactory(RateLimitingHttpHandlerOptions options)
    {
        optionsAccessor = () => options;
    }

    /// <summary>
    /// Creates a new <see cref="RateLimitingHttpHandlerFactory"/> via dependency injection.
    /// </summary>
    public RateLimitingHttpHandlerFactory(
        IOptionsMonitor<RateLimitingHttpHandlerOptions> options,
        [ServiceKey] string name)
    {
        optionsAccessor = () => options.Get(name);
    }

    /// <summary>
    /// Creates a new <see cref="RateLimitingHttpHandler"/>. All handlers created by this factory share the same rate
    /// limiter.
    /// </summary>
    public RateLimitingHttpHandler CreateHandler() => new(rateLimiter, optionsAccessor);

    public ValueTask DisposeAsync() => rateLimiter.DisposeAsync();
    public void Dispose() => rateLimiter.Dispose();
}
