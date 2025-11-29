// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using System.Threading.RateLimiting;

namespace Kagamine.Extensions.Utilities;

/// <summary>
/// A <see cref="DelegatingHandler"/> that forces requests to the same host to wait for a configured period of time
/// since the last request completed before sending a new request.
/// </summary>
/// <remarks>
/// As the Microsoft.Extensions.Http infrastructure rotates inner handlers and expects delegating handlers to be
/// transient, the rate limiter itself is held outside of the handler in <see cref="RateLimitingHttpHandlerFactory"/>.
/// Use the factory to create an instance of this type.
/// </remarks>
public sealed class RateLimitingHttpHandler : DelegatingHandler
{
    private readonly PartitionedRateLimiter<HttpRequestMessage> rateLimiter;
    private readonly Func<RateLimitingHttpHandlerOptions> optionsAccessor;

    internal RateLimitingHttpHandler(
        PartitionedRateLimiter<HttpRequestMessage> rateLimiter,
        Func<RateLimitingHttpHandlerOptions> optionsAccessor)
    {
        this.rateLimiter = rateLimiter;
        this.optionsAccessor = optionsAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RateLimitLease lease = await rateLimiter.AcquireAsync(request, permitCount: 1, cancellationToken);

        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(optionsAccessor().TimeBetweenRequests);
                lease.Dispose();
            }, CancellationToken.None);
        }
    }
}
