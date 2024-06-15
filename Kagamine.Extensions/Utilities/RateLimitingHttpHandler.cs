// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using System.Threading.RateLimiting;

namespace Kagamine.Extensions.Utilities;

/// <summary>
/// A <see cref="DelegatingHandler"/> that forces requests to the same host to wait for a configured period of time
/// since the last request completed before sending a new request.
/// </summary>
public class RateLimitingHttpHandler : DelegatingHandler
{
    private const int DefaultSecondsBetweenRequests = 3;

    private readonly PartitionedRateLimiter<HttpRequestMessage> rateLimiter;
    private readonly TimeSpan timeBetweenRequests;

    /// <summary>
    /// Creates a <see cref="DelegatingHandler"/> that forces requests to the same host to wait for a default number of
    /// seconds since the last request completed before sending a new request.
    /// </summary>
    public RateLimitingHttpHandler() : this(TimeSpan.FromSeconds(DefaultSecondsBetweenRequests))
    { }

    /// <summary>
    /// Creates a <see cref="DelegatingHandler"/> that forces requests to the same host to wait for a configured period
    /// of time since the last request completed before sending a new request.
    /// </summary>
    /// <param name="timeBetweenRequests">The amount of time to wait between requests, per host.</param>
    public RateLimitingHttpHandler(TimeSpan timeBetweenRequests)
    {
        this.timeBetweenRequests = timeBetweenRequests;

        rateLimiter = PartitionedRateLimiter.Create((HttpRequestMessage req) =>
            RateLimitPartition.GetConcurrencyLimiter(
                partitionKey: req.RequestUri is Uri { IsAbsoluteUri: true } uri ? uri.Host : "",
                factory: _ => new ConcurrencyLimiterOptions()
                {
                    PermitLimit = 1,
                    QueueLimit = int.MaxValue,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }),
                equalityComparer: StringComparer.OrdinalIgnoreCase);
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
                await Task.Delay(timeBetweenRequests);
                lease.Dispose();
            }, CancellationToken.None);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            rateLimiter.Dispose();
        }

        base.Dispose(disposing);
    }
}
