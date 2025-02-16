// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

namespace Kagamine.Extensions.IO;

public class TemporaryFileProviderOptions
{
    /// <summary>
    /// The temp directory, resolved relative to the user's temp folder if not an absolute path. If not set, a
    /// subdirectory of the user's temp folder with the name of the application will be used (defaults to the assembly
    /// name). The directory will be created if it does not exist, and will be removed when the <see
    /// cref="TemporaryFileProvider"/> is disposed if it is empty.
    /// </summary>
    public string? TempDirectory { get; set; }
}
