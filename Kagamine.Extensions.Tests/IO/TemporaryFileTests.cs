// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Kagamine.Extensions.IO;

namespace Kagamine.Extensions.Tests.IO;

public sealed class TemporaryFileTests : IDisposable
{
    private readonly TemporaryFile tempFile;
    private readonly string path;

    public TemporaryFileTests()
    {
        path = Path.Combine(Path.GetTempPath(), $"{nameof(TemporaryFileTests)}-{Guid.NewGuid()}");
        File.Create(path).Dispose();
        tempFile = new TemporaryFile(path);
    }

    public void Dispose()
    {
        // Make sure the file is deleted if the test fails
        File.Delete(path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpensFileForReading(bool deleteWhenClosed)
    {
        using (var stream = tempFile.OpenRead(deleteWhenClosed))
        {
            Assert.True(stream.CanRead);
            Assert.True(stream.CanSeek);
            Assert.False(stream.CanWrite);

            // FileShare.Read
            File.OpenRead(path).Dispose();

            // !FileShare.Write
            Assert.Throws<IOException>(() => File.OpenWrite(path).Dispose());
        }

        Assert.Equal(!deleteWhenClosed, File.Exists(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpensFileForWriting(bool deleteWhenClosed)
    {
        using (var stream = tempFile.OpenWrite(deleteWhenClosed))
        {
            Assert.False(stream.CanRead);
            Assert.True(stream.CanSeek);
            Assert.True(stream.CanWrite);

            // !FileShare.Read
            Assert.Throws<IOException>(() => File.OpenRead(path).Dispose());

            // !FileShare.Write
            Assert.Throws<IOException>(() => File.OpenWrite(path).Dispose());
        }

        Assert.Equal(!deleteWhenClosed, File.Exists(path));
    }

    [Fact]
    public async Task CopiesStreamToFile()
    {
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
        Assert.True(File.Exists(path));
        tempFile.Dispose();
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void CanBeDisposedWhileStreamIsOpen()
    {
        // This is for a use case such as the following:

        void DoFileConversion(string inputFile, string outputFile) { /* run ffmpeg or whatever */ }

        Stream ConvertFileAndReturnStream(string inputFile)
        {
            using TemporaryFile tempFile = this.tempFile; // tempFileProvider.Create();

            // Do some file conversion that might fail. If this throws, TemporaryFile.Dispose() will delete the file
            // without us having to do anything.
            DoFileConversion(inputFile, tempFile.Path);

            // If it succeeds, return a FileStream backed by the temp file which cleans it up when disposed; this sets a
            // a flag in TemporaryFile which will prevent it from deleting the file when this method returns.
            return tempFile.OpenRead(deleteWhenClosed: true);
        }

        using Stream stream = ConvertFileAndReturnStream("");

        // File was not deleted, and nothing threw
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void DisposingTwiceCantDeleteADifferentTempFile()
    {
        // Covers an edge case where, once the temp file has been deleted, its filename becomes available again and by
        // sheer chance is taken by another TemporaryFile, but then the first one is disposed again -- e.g. due to a
        // deleteWhenClosed stream's finalizer calling Dispose(false) -- resulting in it deleting the new temp file that
        // doesn't belong to it.

        Assert.True(File.Exists(path));
        tempFile.Dispose();
        Assert.False(File.Exists(path));

        File.Create(path).Dispose(); // Same filename now belongs to someone else

        Assert.True(File.Exists(path));
        tempFile.Dispose();
        Assert.True(File.Exists(path)); // File was not mistakenly deleted

        // Reset and try with a deleteWhenClosed stream this time
        var stream = new TemporaryFile(path).OpenRead(deleteWhenClosed: true);
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

        tempFile.Dispose();
        File.Create(path).Dispose(); // Same filename now belongs to someone else

        Assert.Throws<InvalidOperationException>(() => tempFile.OpenRead().Dispose());
        Assert.Throws<InvalidOperationException>(() => tempFile.OpenWrite().Dispose());
        Assert.Throws<InvalidOperationException>(() => _ = tempFile.Path);
    }
}
