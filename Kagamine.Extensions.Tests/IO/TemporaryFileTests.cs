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
        File.Delete(tempFile.Path);
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

            // Do some file conversion that might fail. If it throws, and the file isn't open, the TemporaryFile's
            // Dispose() method will delete the file without us having to do anything.
            DoFileConversion(inputFile, tempFile.Path);

            // If it succeeds, return a FileStream backed by the temp file which cleans it up when disposed; since a
            // stream is open, the TemporaryFile's Dispose() won't delete the file when this method returns.
            return tempFile.OpenRead(deleteWhenClosed: true);
        }

        using Stream stream = ConvertFileAndReturnStream("");

        // File was not deleted, and nothing threw
        Assert.True(File.Exists(path));
    }
}
