// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

namespace Kagamine.Extensions.Utilities;

public sealed class HttpClientRateLimiterOptions
{
    /// <summary>
    /// <para>
    ///     The amount of time to wait between requests, per host. Defaults to three seconds. May be overridden on a
    ///     host-by-host basis using <see cref="TimeBetweenRequestsByHost"/>.
    /// </para>
    /// <para>
    ///     Set to <see langword="null"/> to disable rate limiting except for the specific hosts configured in <see
    ///     cref="TimeBetweenRequestsByHost"/>.
    /// </para>
    /// </summary>
    public TimeSpan? TimeBetweenRequests { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The amount of time to wait between requests for a given <see cref="Uri.Host"/>, case-insensitive. Overrides <see
    /// cref="TimeBetweenRequests"/>. Set to <see langword="null"/> to disable rate limiting for a particular host.
    /// </summary>
    public Dictionary<string, TimeSpan?> TimeBetweenRequestsByHost { get; } = new(StringComparer.OrdinalIgnoreCase);
}
