using System.Text;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private const int LaunchLogVisibleTailLineCount = 240;
    private const int LaunchLogOlderLoadBatchLineCount = 200;
    private const string EmptyLaunchLogText = "正在等待启动日志输出。";

    internal bool HasLaunchLogLines => _launchLogLines.Count > 0;

    internal bool TryLoadOlderLaunchLogLines()
    {
        if (_launchLogVisibleStartIndex == 0)
        {
            return false;
        }

        _isLaunchLogViewportPinned = false;
        _launchLogVisibleStartIndex = Math.Max(0, _launchLogVisibleStartIndex - LaunchLogOlderLoadBatchLineCount);
        UpdateLaunchLogVisibleText();
        return true;
    }

    internal void SetLaunchLogViewportPinned(bool isPinned)
    {
        if (_isLaunchLogViewportPinned == isPinned)
        {
            return;
        }

        _isLaunchLogViewportPinned = isPinned;
        if (isPinned)
        {
            TrimLaunchLogWindowToTail();
        }
    }

    private void ClearLaunchLogBuffer()
    {
        _launchLogLines.Clear();
        _launchLogVisibleStartIndex = 0;
        _isLaunchLogViewportPinned = true;
        UpdateLaunchLogVisibleText();
    }

    private void AppendLaunchLogEntry(string line)
    {
        _launchLogLines.Add(line);
        if (_isLaunchLogViewportPinned)
        {
            _launchLogVisibleStartIndex = Math.Max(0, _launchLogLines.Count - LaunchLogVisibleTailLineCount);
        }

        UpdateLaunchLogVisibleText();
    }

    private void TrimLaunchLogWindowToTail()
    {
        var desiredStartIndex = Math.Max(0, _launchLogLines.Count - LaunchLogVisibleTailLineCount);
        if (_launchLogVisibleStartIndex == desiredStartIndex)
        {
            return;
        }

        _launchLogVisibleStartIndex = desiredStartIndex;
        UpdateLaunchLogVisibleText();
    }

    private void UpdateLaunchLogVisibleText()
    {
        var nextText = BuildLaunchLogVisibleText();
        if (string.Equals(_launchLogVisibleText, nextText, StringComparison.Ordinal))
        {
            return;
        }

        _launchLogVisibleText = nextText;
        RaisePropertyChanged(nameof(LaunchLogText));
    }

    private string BuildLaunchLogVisibleText()
    {
        if (_launchLogLines.Count == 0)
        {
            return EmptyLaunchLogText;
        }

        var startIndex = Math.Clamp(_launchLogVisibleStartIndex, 0, _launchLogLines.Count - 1);
        var builder = new StringBuilder();
        for (var index = startIndex; index < _launchLogLines.Count; index++)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(_launchLogLines[index]);
        }

        return builder.ToString();
    }
}
