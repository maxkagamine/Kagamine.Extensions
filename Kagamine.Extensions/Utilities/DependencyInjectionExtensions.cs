// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Kagamine.Extensions.Utilities;

public static class DependencyInjectionExtensions
{
    extension (IHttpClientBuilder builder)
    {
        /// <summary>
        /// Adds the default rate limiter to this client. The default rate limiter is shared between named clients.
        /// </summary>
        public IHttpClientBuilder AddRateLimiter() => builder.AddRateLimiter(Options.DefaultName);

        /// <summary>
        /// Adds a named rate limiter to this client. The same named rate limiter may be shared between multiple named
        /// clients.
        /// </summary>
        /// <param name="name">The rate limiter name.</param>
        public IHttpClientBuilder AddRateLimiter(string name)
        {
            builder.Services.TryAddKeyedSingleton<RateLimitingHttpHandlerFactory>(name);
            builder.AddHttpMessageHandler(provider => provider.GetRequiredKeyedService<RateLimitingHttpHandlerFactory>(name).CreateHandler());
            return builder;
        }

        /// <summary>
        /// Adds the default rate limiter to this client. The default rate limiter is shared between named clients.
        /// </summary>
        /// <param name="configure">An action used to configure the default rate limiter.</param>
        public IHttpClientBuilder AddRateLimiter(Action<RateLimitingHttpHandlerOptions> configure) =>
            builder.AddRateLimiter(Options.DefaultName, configure);

        /// <summary>
        /// Adds a named rate limiter to this client. The same named rate limiter may be shared between multiple named
        /// clients.
        /// </summary>
        /// <param name="name">The rate limiter name.</param>
        /// <param name="configure">An action used to configure the named rate limiter.</param>
        public IHttpClientBuilder AddRateLimiter(string name, Action<RateLimitingHttpHandlerOptions> configure)
        {
            builder.AddRateLimiter(name);
            builder.Services.Configure(name, configure);
            return builder;
        }
    }
}
