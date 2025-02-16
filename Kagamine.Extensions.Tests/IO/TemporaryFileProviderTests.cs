// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Kagamine.Extensions.IO;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace Kagamine.Extensions.Tests.IO;

public sealed class TemporaryFileProviderTests : IDisposable
{
    private readonly string tempDirName;
    private readonly string tempDirPath;

    public TemporaryFileProviderTests()
    {
        // Avoid clashing between tests
        tempDirName = $"{nameof(TemporaryFileProviderTests)}_{Guid.NewGuid()}";
        tempDirPath = Path.Combine(Path.GetTempPath(), tempDirName);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirPath))
        {
            Directory.Delete(tempDirPath, true);
        }
    }

    [Fact]
    public void CreatesTempDirUsingApplicationName()
    {
        HostingEnvironment hostEnv = new() { ApplicationName = tempDirName };
        _ = new TemporaryFileProvider(hostEnv, Options.Create(new TemporaryFileProviderOptions()));

        Assert.True(Directory.Exists(tempDirPath));
    }

    [Fact]
    public void CreatesTempDirFromRelativePath()
    {
        HostingEnvironment hostEnv = new() { ApplicationName = "wrong" };
        _ = new TemporaryFileProvider(hostEnv, Options.Create(new TemporaryFileProviderOptions()
        {
            TempDirectory = tempDirName
        }));

        Assert.True(Directory.Exists(tempDirPath));
    }

    [Fact]
    public void CreatesTempDirFromAbsolutePath()
    {
        HostingEnvironment hostEnv = new() { ApplicationName = "wrong" };
        _ = new TemporaryFileProvider(hostEnv, Options.Create(new TemporaryFileProviderOptions()
        {
            TempDirectory = tempDirPath
        }));

        Assert.True(Directory.Exists(tempDirPath));
    }

    [Fact]
    public void DeletesTempDirWhenDisposed()
    {
        new TemporaryFileProvider(tempDirName).Dispose();

        Assert.False(Directory.Exists(tempDirPath));
    }

    [Fact]
    public void DoesNotDeleteTempDirIfNotEmpty()
    {
        var fooFile = Path.Combine(tempDirPath, "foo.txt");
        using (new TemporaryFileProvider(tempDirName))
        {
            File.Create(fooFile).Dispose();
        }

        Assert.True(Directory.Exists(tempDirPath));
        Assert.True(File.Exists(fooFile));
    }

    [Fact]
    public void CreatesTempFile()
    {
        TemporaryFileProvider provider = new(tempDirName);
        TemporaryFile tempFile = provider.Create();

        Assert.Equal(tempDirPath, Path.GetDirectoryName(tempFile.Path));
        Assert.EndsWith(".tmp", tempFile.Path);
        Assert.True(Guid.TryParse(Path.GetFileNameWithoutExtension(tempFile.Path), out _));
    }

    [Fact]
    public void CreatesTempFileWithSuffix()
    {
        string suffix = "-stuff.txt";

        TemporaryFileProvider provider = new(tempDirName);
        TemporaryFile tempFile = provider.Create(suffix);

        Assert.Equal(tempDirPath, Path.GetDirectoryName(tempFile.Path));
        Assert.EndsWith(suffix, tempFile.Path);
        Assert.True(Guid.TryParse(Path.GetFileName(tempFile.Path)[..^suffix.Length], out _));
    }

    [Fact]
    public void LoopsWhileFileNameExists()
    {
        IncrementingTemporaryFileProvider provider = new(tempDirName);

        File.Create(Path.Combine(tempDirPath, "1.tmp")).Dispose();
        File.Create(Path.Combine(tempDirPath, "2.tmp")).Dispose();
        File.Create(Path.Combine(tempDirPath, "3.tmp")).Dispose();

        TemporaryFile tempFile = provider.Create();

        Assert.Equal(Path.Combine(tempDirPath, "4.tmp"), tempFile.Path);
    }
}
