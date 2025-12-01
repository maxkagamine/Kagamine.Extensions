// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Hosting;

namespace Kagamine.Extensions.IO;

public class TemporaryFileProviderOptions
{
    /// <summary>
    /// <para>
    ///     The temp directory, either as an absolute path or relative to the user's temp folder. The directory will be
    ///     created if it does not exist, and will be removed when the <see cref="TemporaryFileProvider"/> is disposed
    ///     if empty.
    /// </para>
    /// <para>
    ///     If not set, <see cref="IHostEnvironment.ApplicationName"/> will be used if dependency injected, otherwise
    ///     the assembly name (which is the default for the former). Set to an empty string to not create a subdirectory
    ///     in the temp folder.
    /// </para>
    /// </summary>
    public string? TempDirectory { get; set; }

    /// <summary>
    /// A function which can be set to customize the base filename (by default a guid). Be warned that the provider will
    /// continue calling this function in a loop until it produces a file name that does not already exist.
    /// </summary>
    public Func<string>? CreateBaseFileName { get; set; }
}
