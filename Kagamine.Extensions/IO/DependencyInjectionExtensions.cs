// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;

namespace Kagamine.Extensions.IO;

public static class DependencyInjectionExtensions
{
    /// <summary>
    /// Adds an <see cref="ITemporaryFileProvider"/> using a subdirectory of the user's temp folder with the name of the
    /// application (defaults to the assembly name). The directory will be created if it does not exist, and will be
    /// removed when the <see cref="TemporaryFileProvider"/> is disposed if it is empty.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddTemporaryFileProvider(this IServiceCollection services)
    {
        services.AddSingleton<ITemporaryFileProvider, TemporaryFileProvider>();

        return services;
    }

    /// <summary>
    /// Adds an <see cref="ITemporaryFileProvider"/> using the provided temp directory. The directory will be created if
    /// it does not exist, and will be removed when the <see cref="TemporaryFileProvider"/> is disposed if it is empty.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="tempDirectory">The temp directory, resolved relative to the user's temp folder if not an absolute
    /// path.</param>
    public static IServiceCollection AddTemporaryFileProvider(this IServiceCollection services, string tempDirectory)
    {
        services.AddTemporaryFileProvider();
        services.Configure<TemporaryFileProviderOptions>(options => options.TempDirectory = tempDirectory);

        return services;
    }
}
