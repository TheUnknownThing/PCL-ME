using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PCL.Frontend.Spike.Icons;

namespace PCL.Frontend.Spike.Desktop;

internal sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        BackButton.IconData = FrontendIconCatalog.Back.Data;
        MinimizeButton.IconData = FrontendIconCatalog.Minimize.Data;
        MaximizeButton.IconData = FrontendIconCatalog.Maximize.Data;
        CloseButton.IconData = FrontendIconCatalog.Close.Data;
        PromptQueueButton.IconData = FrontendIconCatalog.PromptQueue.Data;
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            BeginMoveDrag(e);
        }
        catch (InvalidOperationException)
        {
            // Ignore drag attempts that happen before the window is ready.
        }
    }

    private void MinimizeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void MinimizeGlyph_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CloseGlyph_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Close();
    }

    private void TitleBar_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        ToggleMaximize();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
}
