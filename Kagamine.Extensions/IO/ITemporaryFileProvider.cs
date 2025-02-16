// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

namespace Kagamine.Extensions.IO;

public interface ITemporaryFileProvider : IDisposable
{
    /// <summary>
    /// Creates a temporary file.
    /// </summary>
    /// <returns>A <see cref="TemporaryFile"/> class which deletes the file when disposed.</returns>
    TemporaryFile Create();

    /// <summary>
    /// Creates a temporary file.
    /// </summary>
    /// <param name="suffix">A suffix to append to the file name.</param>
    /// <returns>A <see cref="TemporaryFile"/> class which deletes the file when disposed.</returns>
    TemporaryFile Create(string suffix);
}
