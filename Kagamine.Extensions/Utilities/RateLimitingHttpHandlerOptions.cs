// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

namespace Kagamine.Extensions.Utilities;

public sealed class RateLimitingHttpHandlerOptions
{
    /// <summary>
    /// The amount of time to wait between requests, per host.
    /// </summary>
    public TimeSpan TimeBetweenRequests { get; set; } = TimeSpan.FromSeconds(3);
}
