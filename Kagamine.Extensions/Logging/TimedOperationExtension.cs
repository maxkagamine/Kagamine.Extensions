// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Serilog;
using Serilog.Events;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Disposables;

namespace Kagamine.Extensions.Logging;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class TimedOperationExtension
{
    // Inspired by SerilogMetrics

    /// <summary>
    /// Wraps code in a <see langword="using"/> block and logs the start and end of the operation along with how long it
    /// took to complete.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="level">The log level for the start and end log events.</param>
    /// <param name="message">A description of the operation being timed, as a message template.</param>
    /// <param name="args">Objects positionally formatted into the message template.</param>
    /// <returns>An object that signals the completion of the timed operation when disposed.</returns>
    public static IDisposable BeginTimedOperation(this ILogger logger, LogEventLevel level, string message, params string[] args)
    {
        var sublogger = logger.ForContext("TimedOperationId", Guid.NewGuid());
        var sw = new Stopwatch();
        var disposable = Disposable.Create(() =>
        {
            sw.Stop();
            sublogger.Write(level, message + ": Completed in {Milliseconds:F2} ms", [.. args, sw.Elapsed.TotalMilliseconds]);
        });

        sublogger.Write(level, message + ": Starting", args);

        sw.Start();
        return disposable;
    }

    /// <inheritdoc cref="BeginTimedOperation(ILogger, LogEventLevel, string, string[])"/>
    public static IDisposable BeginTimedOperation(this ILogger logger, string message, params string[] args)
        => BeginTimedOperation(logger, LogEventLevel.Information, message, args);
}
