using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Icons;

namespace PCL.Frontend.Avalonia.ViewModels;
internal enum NavigationVisualStyle
{
    TopLevel = 0,
    Sidebar = 1,
    Utility = 2
}

internal enum AvaloniaPromptLaneKind
{
    Startup = 0,
    Launch = 1,
    Crash = 2
}

internal enum UpdateSurfaceState
{
    Checking = 0,
    Available = 1,
    Latest = 2,
    Error = 3
}

internal enum LauncherUpdateMode
{
    AutoDownloadAndInstall = 0,
    AutoDownloadAndPrompt = 1,
    PromptOnly = 2,
    Disabled = 3
}
