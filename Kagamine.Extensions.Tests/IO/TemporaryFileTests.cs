// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Kagamine.Extensions.IO;

namespace Kagamine.Extensions.Tests.IO;

public sealed class TemporaryFileTests : IDisposable
{
    private readonly string path;

    public TemporaryFileTests()
    {
        path = Path.Combine(Path.GetTempPath(), $"{nameof(TemporaryFileTests)}-{Guid.NewGuid()}");
        File.Create(path).Dispose();
    }

    public void Dispose()
    {
        // Make sure the file is deleted
        File.Delete(path);
    }

    [Fact]
    public void OpensFileForReading()
    {
        using TemporaryFile tempFile = new(path);
        using var stream = tempFile.OpenRead();

        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
        Assert.False(stream.CanWrite);

        // FileShare.Read
        File.OpenRead(path).Dispose();

        // !FileShare.Write
        Assert.Throws<IOException>(() => File.OpenWrite(path).Dispose());
    }

    [Fact]
    public void OpensFileForWriting()
    {
        using TemporaryFile tempFile = new(path);
        using var stream = tempFile.OpenWrite();

        Assert.False(stream.CanRead);
        Assert.True(stream.CanSeek);
        Assert.True(stream.CanWrite);

        // !FileShare.Read
        Assert.Throws<IOException>(() => File.OpenRead(path).Dispose());

        // !FileShare.Write
        Assert.Throws<IOException>(() => File.OpenWrite(path).Dispose());
    }

    [Fact]
    public async Task CopiesStreamToFile()
    {
        using TemporaryFile tempFile = new(path);

        using MemoryStream ms = new("rin"u8.ToArray());
        await tempFile.CopyFromAsync(ms);

        await using var stream = tempFile.OpenRead();
        using var reader = new StreamReader(stream);
        string text = await reader.ReadToEndAsync();

        Assert.Equal("rin", text);
    }

    [Fact]
    public void DeletesFileWhenDisposed()
    {
        TemporaryFile tempFile = new(path);
        Assert.True(File.Exists(path));
        tempFile.Dispose();
        Assert.False(File.Exists(path));
    }

#pragma warning disable
    [Fact]
    public void CanBeDisposedWhileStreamIsOpen()
    {
        // This is for a use case such as the following:

        void DoFileConversion(string inputFile, string outputFile) { /* run ffmpeg or whatever */ }

        Stream ConvertFileAndReturnStream(string inputFile)
        {
            using TemporaryFile tempFile = new(path); // tempFileProvider.Create();

            // Do some file conversion that might fail. If this throws, TemporaryFile.Dispose() will delete the file
            // without us having to do anything.
            DoFileConversion(inputFile, tempFile.Path);

            // If it succeeds, return a FileStream to the temp file which cleans it up when disposed; TemporaryFile
            // keeps a ref count and won't delete the file until the stream has been disposed.
            return tempFile.OpenRead();
        }

        using Stream stream = ConvertFileAndReturnStream("");

        // File was not deleted, and nothing threw
        Assert.True(File.Exists(path));
    }
#pragma warning restore

    [Fact]
    public void DisposingTwiceCantDeleteADifferentTempFile()
    {
        // Covers an edge case where, once the temp file has been deleted, its filename becomes available again and by
        // sheer chance is taken by another TemporaryFile, but then the first one is disposed again, e.g. due to
        // FileStream's finalizer calling Dispose(false), resulting in it deleting the new temp file that doesn't belong
        // to it.

        TemporaryFile tempFile = new(path);

        Assert.True(File.Exists(path));
        tempFile.Dispose();
        Assert.False(File.Exists(path));

        File.Create(path).Dispose(); // Same filename now belongs to someone else

        Assert.True(File.Exists(path));
        tempFile.Dispose();
        Assert.True(File.Exists(path)); // File was not mistakenly deleted

        // Reset and try disposing a stream a second time instead
        tempFile = new(path);
        var stream = tempFile.OpenRead();
        tempFile.Dispose();
        stream.Dispose();
        Assert.False(File.Exists(path));

        File.Create(path).Dispose(); // Same filename now belongs to someone else (again)

        Assert.True(File.Exists(path));
        stream.Dispose();
        Assert.True(File.Exists(path)); // File was not mistakenly deleted
    }

    [Fact]
    public void CannotAccessFileOnceDisposed()
    {
        // Similar to the above, this avoids accidentally opening a different temp file after its filename was freed

        TemporaryFile tempFile = new(path);

        tempFile.Dispose();
        File.Create(path).Dispose(); // Same filename now belongs to someone else

        Assert.Throws<ObjectDisposedException>(() => tempFile.OpenRead().Dispose());
        Assert.Throws<ObjectDisposedException>(() => tempFile.OpenWrite().Dispose());
        Assert.Throws<ObjectDisposedException>(() => _ = tempFile.Path);
    }

    [Fact]
    public async Task RefCountLogicIsThreadSafe()
    {
        using (TemporaryFile tempFile = new(path))
        {
            // Spam a bunch of threads opening and closing streams
            await Parallel.ForAsync(0, 100, TestContext.Current.CancellationToken, async (i, cancellationToken) =>
            {
                Assert.True(File.Exists(path));

                using FileStream stream = tempFile.OpenRead();
                await Task.Delay(Random.Shared.Next(0, 10), cancellationToken);
            });

            // Open a bunch more streams and then dispose the TemporaryFile before closing them so that we know it's not
            // just its own Dispose() that's deleting the file
            var streams = Enumerable.Range(0, 100).Select(_ => tempFile.OpenRead()).AsParallel().ToArray();
            tempFile.Dispose();
            await Parallel.ForEachAsync(streams, async (s, _) => s.Close());
        }

        Assert.False(File.Exists(path));
    }
}
