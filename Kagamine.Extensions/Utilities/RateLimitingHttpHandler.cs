// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using System.Threading.RateLimiting;

namespace Kagamine.Extensions.Utilities;

/// <summary>
/// A <see cref="DelegatingHandler"/> that forces requests to the same host to wait for a configured period of time
/// since the last request completed before sending a new request.
/// </summary>
/// <remarks>
/// This type cannot be constructed directly. See <see cref="RateLimitingHttpHandlerFactory"/> and <see
/// cref="DependencyInjectionExtensions.AddRateLimiter(Microsoft.Extensions.DependencyInjection.IHttpClientBuilder)"/>.
/// </remarks>
public sealed class RateLimitingHttpHandler : DelegatingHandler
{
    private readonly PartitionedRateLimiter<HttpRequestMessage> rateLimiter;
    private readonly Func<HttpClientRateLimiterOptions> optionsAccessor;

    internal RateLimitingHttpHandler(
        PartitionedRateLimiter<HttpRequestMessage> rateLimiter,
        Func<HttpClientRateLimiterOptions> optionsAccessor)
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
                HttpClientRateLimiterOptions options = optionsAccessor();
                string host = request.RequestUri is Uri { IsAbsoluteUri: true } uri ? uri.Host : "";
                TimeSpan timeBetweenRequests = options.TimeBetweenRequestsByHost.GetValueOrDefault(host, options.TimeBetweenRequests);

                await Task.Delay(timeBetweenRequests);
                lease.Dispose();
            }, CancellationToken.None);
        }
    }
}
