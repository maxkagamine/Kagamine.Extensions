// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;
using System.Reactive.Disposables;

namespace Kagamine.Extensions.Logging;

public static class TimedOperationExtensions
{
    // Inspired by SerilogMetrics

    /// <summary>
    /// Wraps code in a <see langword="using"/> block and logs the start and end of the operation along with how long it
    /// took to complete.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="level">The log level for the start and end log events.</param>
    /// <param name="messageTemplate">A description of the operation being timed, as a message template.</param>
    /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
    /// <returns>An object that signals the completion of the timed operation when disposed.</returns>
    [MessageTemplateFormatMethod(nameof(messageTemplate))]
    public static IDisposable BeginTimedOperation(this ILogger logger, LogEventLevel level, string messageTemplate, params object?[] propertyValues)
    {
        var sublogger = logger.ForContext("TimedOperationId", Guid.NewGuid());
        var sw = new Stopwatch();
        var disposable = Disposable.Create(() =>
        {
            sw.Stop();
            sublogger.Write(level, messageTemplate + ": Completed in {Milliseconds:F2} ms", [.. propertyValues, sw.Elapsed.TotalMilliseconds]);
        });

        sublogger.Write(level, messageTemplate + ": Starting", propertyValues);

        sw.Start();
        return disposable;
    }

    /// <inheritdoc cref="BeginTimedOperation(ILogger, LogEventLevel, string, object[])"/>
    [MessageTemplateFormatMethod(nameof(messageTemplate))]
    public static IDisposable BeginTimedOperation(this ILogger logger, string messageTemplate, params object?[] propertyValues)
        => BeginTimedOperation(logger, LogEventLevel.Information, messageTemplate, propertyValues);
}
