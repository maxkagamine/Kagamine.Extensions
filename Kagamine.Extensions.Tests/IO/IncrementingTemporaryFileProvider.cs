// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Kagamine.Extensions.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Kagamine.Extensions.Tests.IO;

internal class IncrementingTemporaryFileProvider : TemporaryFileProvider
{
    private int i;

    public IncrementingTemporaryFileProvider(IHostEnvironment hostEnv, IOptions<TemporaryFileProviderOptions> options)
        : base(hostEnv, options)
    { }

    public IncrementingTemporaryFileProvider(string tempDirectory)
        : base(tempDirectory)
    { }

    protected override string CreateBaseFileName()
    {
        return Interlocked.Increment(ref i).ToString();
    }
}
