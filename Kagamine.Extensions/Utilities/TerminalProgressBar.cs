// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

namespace Kagamine.Extensions.Utilities;

/// <summary>
/// Sends ANSI escape codes to display a progress bar in the terminal and remove it when disposed.
/// </summary>
/// <remarks>
/// <see href="https://learn.microsoft.com/en-us/windows/terminal/tutorials/progress-bar-sequences"/>
/// </remarks>
public sealed class TerminalProgressBar : IDisposable
{
    private readonly TextWriter? writer;

    public TerminalProgressBar()
        : this(!Console.IsOutputRedirected ? Console.Out :
               !Console.IsErrorRedirected ? Console.Error :
               null)
    { }

    public TerminalProgressBar(TextWriter? writer)
    {
        this.writer = writer;

        SetProgress(0);
    }

    /// <summary>
    /// Updates the state of the progress bar.
    /// </summary>
    /// <param name="value">The progress value between zero and <paramref name="maxValue"/>.</param>
    /// <param name="maxValue">The value that would equal 100%.</param>
    public void SetProgress(int value, int maxValue)
        => SetProgress((float)value / maxValue);

    /// <summary>
    /// Updates the state of the progress bar.
    /// </summary>
    /// <param name="value">The progress value between zero and one.</param>
    public void SetProgress(float value)
    {
        int progress = Math.Clamp((int)Math.Round(value * 100), 0, 100);
        writer?.Write($"\x1b]9;4;1;{progress}\x07");
    }

    /// <summary>
    /// Removes the progress bar.
    /// </summary>
    public void ClearProgress()
    {
        writer?.Write("\x1b]9;4;0;0\x07");
    }

    public void Dispose() => ClearProgress();
}
