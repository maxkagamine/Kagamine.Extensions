// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

namespace Kagamine.Extensions.Utilities;

public sealed class HttpClientRateLimiterOptions
{
    /// <summary>
    /// The default amount of time to wait between requests, per host. May be overridden on a host-by-host basis using
    /// <see cref="TimeBetweenRequestsByHost"/>.
    /// </summary>
    public TimeSpan TimeBetweenRequests { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The amount of time to wait between requests for a given <see cref="Uri.Host"/>, case-insensitive. Overrides <see
    /// cref="TimeBetweenRequests"/>.
    /// </summary>
    public Dictionary<string, TimeSpan> TimeBetweenRequestsByHost { get; } = new(StringComparer.OrdinalIgnoreCase);
}
