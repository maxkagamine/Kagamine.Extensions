// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Kagamine.Extensions.IO;

public sealed class TemporaryFileProvider : ITemporaryFileProvider
{
    private static readonly ushort FileExists =
        (ushort)(OperatingSystem.IsWindows() ? 0x50 /* ERROR_FILE_EXISTS */ : 17 /* EEXIST */);

    private readonly string tempDirectory;
    private readonly TemporaryFileProviderOptions? options;

    /// <summary>
    /// Initializes a <see cref="TemporaryFileProvider"/> using a subdirectory of the user's temp folder with the name
    /// of the assembly. The directory will be created if it does not exist, and will be removed when the <see
    /// cref="TemporaryFileProvider"/> is disposed if empty.
    /// </summary>
    public TemporaryFileProvider() : this(tempDirectory: null)
    { }

    /// <summary>
    /// Initializes a <see cref="TemporaryFileProvider"/> with the provided options.
    /// </summary>
    public TemporaryFileProvider(TemporaryFileProviderOptions options) : this(options.TempDirectory)
    {
        this.options = options;
    }

    /// <summary>
    /// Initializes a <see cref="TemporaryFileProvider"/> with the options provided from dependency injection.
    /// </summary>
    public TemporaryFileProvider(IHostEnvironment hostEnv, IOptions<TemporaryFileProviderOptions> options)
        : this(options.Value.TempDirectory ?? hostEnv.ApplicationName)
    {
        this.options = options.Value;
    }

    // Internal for testing; use the options class to change the temp directory
    internal TemporaryFileProvider(string? tempDirectory)
    {
        tempDirectory ??= Assembly.GetEntryAssembly()?.GetName().Name ?? ""; // Same default as HostBuilder

        this.tempDirectory = Path.GetFullPath(tempDirectory, Path.GetTempPath());
        Directory.CreateDirectory(this.tempDirectory);
    }

    public TemporaryFile Create() => Create("", ".tmp");

    public TemporaryFile Create(string suffix) => Create("", suffix);

    public TemporaryFile Create(string prefix, string suffix)
    {
        while (true)
        {
            try
            {
                string path = Path.Combine(tempDirectory, $"{prefix}{CreateBaseFileName()}{suffix}");

                // Attempt to create the file. Note that we don't wrap a stream with the TemporaryFile class, just the
                // file path, as the calling code may need to open multiple streams or pass the path to another process.
                new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite).Dispose();

                return new TemporaryFile(path);
            }
            catch (IOException ex) when ((ushort)ex.HResult == FileExists) { }
        }
    }

    /// <summary>
    /// Returns a unique file name to which a suffix may be appended.
    /// </summary>
    private string CreateBaseFileName() => options?.CreateBaseFileName?.Invoke() ?? Guid.NewGuid().ToString();

    /// <summary>
    /// Deletes the temp directory if it's empty (all <see cref="TemporaryFile"/> instances and their streams have been
    /// disposed and nothing else is using the directory).
    /// </summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(tempDirectory);
        }
        catch { }
    }
}
