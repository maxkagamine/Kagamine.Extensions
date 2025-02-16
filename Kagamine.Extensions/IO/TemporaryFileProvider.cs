// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Kagamine.Extensions.IO;

public class TemporaryFileProvider : ITemporaryFileProvider
{
    private static readonly ushort FileExists =
        (ushort)(OperatingSystem.IsWindows() ? 0x50 /* ERROR_FILE_EXISTS */ : 17 /* EEXIST */);

    private readonly string tempDirectory;

    /// <summary>
    /// Initializes a <see cref="TemporaryFileProvider"/> using the options provided from dependency injection.
    /// </summary>
    public TemporaryFileProvider(IHostEnvironment hostEnv, IOptions<TemporaryFileProviderOptions> options)
        : this(options.Value.TempDirectory ?? hostEnv.ApplicationName)
    { }

    /// <summary>
    /// Initializes a <see cref="TemporaryFileProvider"/> using the provided temp directory. The directory will be
    /// created if it does not exist, and will be removed when the <see cref="TemporaryFileProvider"/> is disposed if it
    /// is empty.
    /// </summary>
    /// <param name="tempDirectory">The temp directory, resolved relative to the user's temp folder if not an absolute
    /// path.</param>
    public TemporaryFileProvider(string tempDirectory)
    {
        this.tempDirectory = Path.GetFullPath(tempDirectory, Path.GetTempPath());
        Directory.CreateDirectory(this.tempDirectory);
    }

    public TemporaryFile Create() => Create(".tmp");

    public TemporaryFile Create(string suffix)
    {
        while (true)
        {
            try
            {
                string path = Path.Combine(tempDirectory, $"{CreateBaseFileName()}{suffix}");

                // Attempt to create the file. Note that we don't wrap a stream with the TemporaryFile class, just the
                // file path, as the calling code may need to open multiple streams or pass the path to another process.
                new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite).Dispose();

                return new TemporaryFile(path);
            }
            catch (IOException ex) when ((ushort)ex.HResult == FileExists) { }
        }
    }

    /// <summary>
    /// Returns a unique file name, to which a suffix may be appended. This method can be overridden to change the
    /// default temporary file name format (by default a guid). Be warned that the provider will continue calling this
    /// method in a loop until it produces a file name that does not already exist.
    /// </summary>
    protected virtual string CreateBaseFileName() => Guid.NewGuid().ToString();

    /// <summary>
    /// Deletes the temp directory if it's empty (all <see cref="TemporaryFile"/> instances and their streams have been
    /// disposed and nothing else is using the same temp directory name).
    /// </summary>
    public virtual void Dispose()
    {
        try
        {
            Directory.Delete(tempDirectory);
        }
        catch { }

        GC.SuppressFinalize(this);
    }
}
