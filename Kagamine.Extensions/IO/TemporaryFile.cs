// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;

namespace Kagamine.Extensions.IO;

/// <summary>
/// A temporary file which will be deleted when the <see cref="TemporaryFile"/> and all streams have been disposed.
/// </summary>
public sealed class TemporaryFile : IDisposable
{
    private volatile int refCount = 1;
    private bool isDisposed;
    private readonly string path;

    internal TemporaryFile(string path)
    {
        this.path = path;
    }

    /// <summary>
    /// The absolute path to the temporary file.
    /// </summary>
    /// <exception cref="ObjectDisposedException"/>
    public string Path
    {
        get
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            return path;
        }
    }

    /// <summary>
    /// Opens the file with the given file access and sharing.
    /// </summary>
    /// <param name="access">Determines whether to open the file for reading, writing, or both.</param>
    /// <param name="share">Determines how the file will be shared with other processes.</param>
    /// <remarks>
    /// The temporary file will remain on disk until the <see cref="TemporaryFile"/> and all streams have been disposed.
    /// </remarks>
    /// <exception cref="ObjectDisposedException"/>
    public FileStream Open(FileAccess access, FileShare share)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        int oldValue = refCount;
        while (true)
        {
            ObjectDisposedException.ThrowIf(oldValue == 0, this);

            int result = Interlocked.CompareExchange(ref refCount, oldValue + 1, oldValue);
            if (result == oldValue)
            {
                break;
            }

            oldValue = result;
        }

        return new TemporaryFileStream(this, access, share);
    }

    /// <summary>
    /// Opens the file for reading.
    /// </summary>
    /// <remarks>
    /// The temporary file will remain on disk until the <see cref="TemporaryFile"/> and all streams have been disposed.
    /// </remarks>
    /// <exception cref="ObjectDisposedException"/>
    public FileStream OpenRead() => Open(FileAccess.Read, FileShare.Read);

    /// <summary>
    /// Opens the file for writing.
    /// </summary>
    /// <remarks>
    /// The temporary file will remain on disk until the <see cref="TemporaryFile"/> and all streams have been disposed.
    /// </remarks>
    /// <exception cref="ObjectDisposedException"/>
    public FileStream OpenWrite() => Open(FileAccess.Write, FileShare.None);

    /// <summary>
    /// Copies the contents of <paramref name="stream"/> to the temporary file.
    /// </summary>
    /// <param name="stream">The stream from which to copy.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <exception cref="ObjectDisposedException"/>
    public async Task CopyFromAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        await using var tempFileStream = OpenWrite();
        await stream.CopyToAsync(tempFileStream, cancellationToken);
    }

    /// <summary>
    /// Disposes the <see cref="TemporaryFile"/> preventing any additional streams from being opened. Deletes the file
    /// if no there are no remaining streams.
    /// </summary>
    public void Dispose()
    {
        if (!Interlocked.Exchange(ref isDisposed, true))
        {
            DecrementRefCount();
        }
    }

    private void DecrementRefCount()
    {
        Debug.Assert(refCount > 0);

        if (Interlocked.Decrement(ref refCount) == 0)
        {
            // This was the last ref, so we can safely delete the file now
            File.Delete(path);
        }
    }

    /// <inheritdoc cref="Path"/>
    public override string ToString() => Path;

    private class TemporaryFileStream : FileStream
    {
        private TemporaryFile? tempFile;

        public TemporaryFileStream(TemporaryFile tempFile, FileAccess access, FileShare share)
            : base(tempFile.Path, FileMode.Open, access, share)
        {
            this.tempFile = tempFile;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Interlocked.Exchange(ref tempFile, null)?.DecrementRefCount();
        }
    }
}
