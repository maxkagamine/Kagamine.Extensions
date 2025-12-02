// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kagamine.Extensions.Utilities;

public static class DependencyInjectionExtensions
{
    extension(IHttpClientBuilder builder)
    {
        /// <summary>
        /// Adds a <see cref="RateLimitingHttpHandler"/> to this client which forces requests to the same host to wait
        /// for a configured period of time since the last request completed before sending a new request.
        /// </summary>
        /// <remarks>
        /// The rate limiter is shared across all named clients. Use <c>services.Configure&lt;<see
        /// cref="RateLimitingHttpHandlerOptions"/>&gt;()</c> to change the default time between requests or set
        /// different rate limits per host.
        /// </remarks>
        public IHttpClientBuilder AddRateLimiting()
        {
            builder.Services.TryAddSingleton<RateLimitingHttpHandlerFactory>();

            builder.ConfigureAdditionalHttpMessageHandlers((handlers, provider) =>
            {
                // Avoid adding two rate limiters to the chain, which would cause a deadlock
                // (see RateLimitingHttpHandlerTests.PreventsDuplicateHandler)
                if (handlers.Any(h => h is RateLimitingHttpHandler))
                {
                    return;
                }

                var handler = provider.GetRequiredService<RateLimitingHttpHandlerFactory>().CreateHandler();
                handlers.Add(handler);
            });

            return builder;
        }
    }
}
