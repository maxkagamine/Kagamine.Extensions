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

    public TerminalProgressBar(bool stderr = false)
    {
        writer = stderr ? Console.IsErrorRedirected ? null : Console.Error :
                          Console.IsOutputRedirected ? null : Console.Out;

        SetProgress(0);
    }

    public void SetProgress(int value, int maxValue)
        => SetProgress((float)value / maxValue);

    public void SetProgress(float value)
    {
        int progress = Math.Clamp((int)Math.Round(value * 100), 0, 100);
        writer?.Write($"\x1b]9;4;1;{progress}\x07");
    }

    public void ClearProgress()
    {
        writer?.Write("\x1b]9;4;0;0\x07");
    }

    public void Dispose() => ClearProgress();
}
