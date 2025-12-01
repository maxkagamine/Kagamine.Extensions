// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kagamine.Extensions.IO;

public static class DependencyInjectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds an <see cref="ITemporaryFileProvider"/> using a subdirectory of the user's temp folder with the name of
        /// the application (<see cref="IHostEnvironment.ApplicationName"/>, defaults to the assembly name). The
        /// directory will be created if it does not exist, and will be removed when the <see
        /// cref="TemporaryFileProvider"/> is disposed if empty.
        /// </summary>
        public IServiceCollection AddTemporaryFileProvider()
        {
            services.AddSingleton<ITemporaryFileProvider, TemporaryFileProvider>();

            return services;
        }

        /// <summary>
        /// Adds an <see cref="ITemporaryFileProvider"/> with the provided options.
        /// </summary>
        /// <param name="configure">The action used to configure the options.</param>
        public IServiceCollection AddTemporaryFileProvider(Action<TemporaryFileProviderOptions> configure)
        {
            services.AddTemporaryFileProvider();
            services.Configure(configure);

            return services;
        }
    }
}
