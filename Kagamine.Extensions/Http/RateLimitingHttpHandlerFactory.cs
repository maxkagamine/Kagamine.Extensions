// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace Kagamine.Extensions.Http;

/// <summary>
/// Factory for creating <see cref="RateLimitingHttpHandler"/> instances which force requests to the same host to wait
/// for a configured period of time since the last request completed before sending a new request. All handlers created
/// by the factory share the same rate limiter.
/// </summary>
/// <remarks>
/// <para>
///     To use with dependency injection, see <see
///     cref="DependencyInjectionExtensions.AddRateLimiting(Microsoft.Extensions.DependencyInjection.IHttpClientBuilder)"/>.
/// </para>
/// <para>
///     This factory is necessary as the Microsoft.Extensions.Http infrastructure rotates inner handlers and expects the
///     delegating handlers that wrap around them to be new instances not already in use. The handlers therefore cannot
///     be singletons or hold state.
/// </para>
/// </remarks>
public sealed class RateLimitingHttpHandlerFactory : IAsyncDisposable, IDisposable
{
    private readonly Func<RateLimitingHttpHandlerOptions> optionsAccessor;

    private readonly PartitionedRateLimiter<string> rateLimiter =
        PartitionedRateLimiter.Create((string host) =>
            RateLimitPartition.GetConcurrencyLimiter(
                partitionKey: host,
                factory: _ => new ConcurrencyLimiterOptions()
                {
                    PermitLimit = 1,
                    QueueLimit = int.MaxValue,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }),
                equalityComparer: StringComparer.OrdinalIgnoreCase);

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
    public RateLimitingHttpHandlerFactory(IOptionsMonitor<RateLimitingHttpHandlerOptions> options)
    {
        optionsAccessor = () => options.CurrentValue;
    }

    /// <summary>
    /// Creates a new <see cref="RateLimitingHttpHandler"/>. All handlers created by this factory share the same rate
    /// limiter.
    /// </summary>
    public RateLimitingHttpHandler CreateHandler() => new(rateLimiter, optionsAccessor);

    public ValueTask DisposeAsync() => rateLimiter.DisposeAsync();
    public void Dispose() => rateLimiter.Dispose();
}
