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

    public static IDisposable BeginTimedOperation(this ILogger logger, string message, params string[] args)
        => BeginTimedOperation(logger, LogEventLevel.Information, message, args);
}
