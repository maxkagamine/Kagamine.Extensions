// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

namespace Kagamine.Extensions.IO;

/// <summary>
/// A temporary file which will be deleted when disposed.
/// </summary>
public sealed class TemporaryFile : IDisposable
{
    private bool hasDeleteWhenClosedStream;

    internal TemporaryFile(string path)
    {
        Path = path;
    }

    /// <summary>
    /// The absolute path to the temporary file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Opens the file with the given file access and sharing.
    /// </summary>
    /// <param name="access">Determines whether to open the file for reading, writing, or both.</param>
    /// <param name="share">Determines how the file will be shared with other processes.</param>
    /// <param name="deleteWhenClosed">Whether to delete the temporary file when the stream is closed.</param>
    /// <returns>A <see cref="FileStream"/>. If <paramref name="deleteWhenClosed"/> is <see langword="false"/>, the
    /// caller is responsible for disposing the stream prior to disposing the <see cref="TemporaryFile"/>. Otherwise,
    /// the <see cref="TemporaryFile"/> can be safely disposed or thrown away, as the returned stream will take care of
    /// cleaning up the file.</returns>
    public FileStream Open(FileAccess access, FileShare share, bool deleteWhenClosed = false)
    {
        hasDeleteWhenClosedStream |= deleteWhenClosed;

        // FileOptions.DeleteOnClose seems to conflict with FileShare.Read, so we override FileStream.Dispose() instead
        return deleteWhenClosed ?
            new TemporaryFileStream(this, access, share) :
            new FileStream(Path, FileMode.Open, access, share);
    }

    /// <summary>
    /// Opens the file for reading.
    /// </summary>
    /// <returns>A <see cref="FileStream"/>. If <paramref name="deleteWhenClosed"/> is <see langword="false"/>, the
    /// caller is responsible for disposing the stream prior to disposing the <see cref="TemporaryFile"/>. Otherwise,
    /// the <see cref="TemporaryFile"/> can be safely disposed or thrown away, as the returned stream will take care of
    /// cleaning up the file.</returns>
    public FileStream OpenRead(bool deleteWhenClosed = false) =>
        Open(FileAccess.Read, FileShare.Read, deleteWhenClosed);

    /// <summary>
    /// Opens the file for writing.
    /// </summary>
    /// <returns>A <see cref="FileStream"/>. If <paramref name="deleteWhenClosed"/> is <see langword="false"/>, the
    /// caller is responsible for disposing the stream prior to disposing the <see cref="TemporaryFile"/>. Otherwise,
    /// the <see cref="TemporaryFile"/> can be safely disposed or thrown away, as the returned stream will take care of
    /// cleaning up the file.</returns>
    public FileStream OpenWrite(bool deleteWhenClosed = false) =>
        Open(FileAccess.Write, FileShare.None, deleteWhenClosed);

    /// <summary>
    /// Copies the contents of <paramref name="stream"/> to the temporary file.
    /// </summary>
    /// <param name="stream">The stream from which to copy.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    public async Task CopyFromAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        await using var tempFileStream = OpenWrite();
        await stream.CopyToAsync(tempFileStream, cancellationToken);
    }

    /// <summary>
    /// Deletes the temporary file, unless any streams were opened with <c>deleteWhenClosed</c>.
    /// </summary>
    public void Dispose()
    {
        if (!hasDeleteWhenClosedStream)
        {
            File.Delete(Path);
        }
    }

    private class TemporaryFileStream(TemporaryFile tempFile, FileAccess access, FileShare share)
        : FileStream(tempFile.Path, FileMode.Open, access, share)
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            File.Delete(tempFile.Path);
        }
    }
}
