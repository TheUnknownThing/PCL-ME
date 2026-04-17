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
internal sealed record NavigationPalette(
    IBrush Background,
    IBrush Border,
    IBrush Foreground,
    IBrush Accent);

internal sealed record SurfacePalette(
    IBrush Background,
    IBrush Border,
    IBrush Accent,
    IBrush Foreground);
