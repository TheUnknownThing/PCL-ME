using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using PCL.Extensions.Contracts;
using PCL.Extensions.Runtime;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.UI.Icons;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class ExtensionCardView : UserControl, IDisposable
{
    private readonly FrontendExtensionSession _session;
    private readonly ExtensionInfo _info;
    private readonly List<IDisposable> _nativeResources = [];
    private readonly CancellationTokenSource _lifetime = new();
    private readonly MenuItem _updateFile = new();
    private readonly MenuItem _updateUrl = new();
    private string? _renderedState;
    private bool _updating;
    private bool _busy;
    private bool _disposed;
    private ExtensionUrlInstallView? _updateInstaller;
    private ExtensionDetailsView? _detailsView;
    internal ExtensionInfo Info => _info;

    public ExtensionCardView(FrontendExtensionSession session, ExtensionInfo info)
    {
        _session = session;
        _info = info;
        InitializeComponent();
        Name = "Extension_" + info.Manifest.Id.Replace('.', '_').Replace('-', '_');
        ToggleEnabledButton.Command = new ActionCommand(() => Operate(() => session.Host.SetEnabledAsync(info.Manifest.Id, !info.IsEnabled)));
        UpdateButton.ContextFlyout = new MenuFlyout { Items = { _updateFile, _updateUrl } };
        UpdateButton.Command = new ActionCommand(() => UpdateButton.ContextFlyout?.ShowAt(UpdateButton));
        _updateFile.Click += (_, _) => Operate(async () =>
        {
            var path = await session.PickFileAsync(session.T("update_file"), ["*.pclext", "*.zip"]);
            if (path is not null && !_disposed) await session.Host.UpdateAsync(info.Manifest.Id, path, _lifetime.Token);
        });
        _updateUrl.Click += (_, _) =>
        {
            var installer = new ExtensionUrlInstallView();
            _updateInstaller = installer;
            installer.Installed += _ => { _renderedState = null; Refresh(); };
            installer.BusyChanged += Refresh;
            installer.Closed += () => { if (ReferenceEquals(_updateInstaller, installer)) _updateInstaller = null; };
            installer.Open(session, info.Manifest.Id);
        };
        RetryButton.Command = new ActionCommand(() => Operate(() => session.Host.SetEnabledAsync(info.Manifest.Id, true)));
        InfoButton.Command = new ActionCommand(() => ShowDetails(logs: false));
        LogButton.Command = new ActionCommand(() => ShowDetails(logs: true));
        UninstallButton.Command = new ActionCommand(() => Operate(() => session.Host.UninstallAsync(info.Manifest.Id)));
        ResetButton.Command = new ActionCommand(() => Operate(() =>
        {
            foreach (var setting in info.Manifest.Settings) SetDefault(info.Settings, setting);
            _renderedState = null;
            return ValueTask.CompletedTask;
        }));
        Card.PropertyChanged += (_, e) => { if (e.Property == PclCard.IsChevronExpandedProperty && Card.IsChevronExpanded) Refresh(); };
        void SizeHeader()
        {
            var title = Card.FindControl<TextBlock>("HeaderTextBlock")!;
            title.MaxWidth = Math.Max(0, Bounds.Width - HeaderActions.Bounds.Width - 66);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.HorizontalAlignment = HorizontalAlignment.Left;
            ToolTip.SetTip(title, Card.Header);
        }
        SizeChanged += (_, _) => SizeHeader();
        HeaderActions.SizeChanged += (_, _) => SizeHeader();
    }

    internal void Expand() => Card.IsChevronExpanded = true;

    internal void Refresh()
    {
        if (_disposed) return;
        _updating = true;
        try
        {
            var info = _info;
            var busy = _busy || _updateInstaller?.IsBusy == true;
            Card.Header = info.Text.Get("extension.name", info.Text.Get("title", info.Manifest.Name));
            if (info.IsEnabled) Card.ClearValue(PclCard.HeaderForegroundProperty);
            else Card.Bind(PclCard.HeaderForegroundProperty, new DynamicResourceExtension("ColorBrushEntrySecondaryIdle"));
            var opacity = info.IsEnabled ? 1 : .56;
            Card.FindControl<TextBlock>("HeaderTextBlock")!.Opacity = opacity;
            DescriptionText.Opacity = opacity;
            ConfigureAction(ToggleEnabledButton, info.IsEnabled ? "disable" : "enable", info.IsEnabled ? PclIcons.DisableCircle : PclIcons.EnableCircle);
            ToggleEnabledButton.IsEnabled = !busy && !info.RemovalPending;
            DescriptionText.Text = info.Text.Get("extension.description", info.Manifest.Description);
            ErrorText.Text = info.Error;
            ErrorText.IsVisible = !string.IsNullOrWhiteSpace(info.Error);
            NoticeText.Text = _session.T(info.RemovalPending ? "pending_removal" : "restart_required");
            NoticeText.IsVisible = info.RestartRequired;
            RetryButton.Text = _session.T("retry");
            RetryButton.IsVisible = info.IsEnabled && info.Status == ExtensionStatus.Faulted;
            ConfigureAction(UpdateButton, "update");
            UpdateButton.IsEnabled = !info.IsBuiltIn && !busy && !info.RemovalPending && !info.RestartRequired;
            if (info.IsBuiltIn) ToolTip.SetTip(UpdateButton, _session.T("built_in_update"));
            else if (info.RestartRequired) ToolTip.SetTip(UpdateButton, _session.T("restart_required"));
            _updateFile.Header = _session.T("update_file");
            _updateUrl.Header = _session.T("update_url");
            _updateInstaller?.RefreshText();
            ConfigureAction(UninstallButton, "uninstall");
            UninstallButton.IsEnabled = !info.IsBuiltIn && !busy && !info.RemovalPending;
            if (info.IsBuiltIn) ToolTip.SetTip(UninstallButton, _session.T("built_in_remove"));
            ConfigureAction(InfoButton, "info", PclIcons.InfoCircle);
            ConfigureAction(LogButton, "logs");
            if (info.Status == ExtensionStatus.Faulted) ToolTip.SetTip(LogButton, _session.T("logs") + " · " + _session.T("states.faulted"));
            _detailsView?.Refresh();
            ResetButton.Text = _session.T("reset");
            ResetButton.IsVisible = info.Manifest.Settings.Length > 0;
            RetryButton.IsEnabled = ResetButton.IsEnabled = !busy;
            SettingsPanel.IsEnabled = ShortcutsPanel.IsEnabled = NativeSettingsPanel.IsEnabled = !busy;
            // Lifecycle events for another extension must not replace focused editors or native views.
            var state = string.Join('|', info.Text.Locale, info.Manifest.Version, info.Status, _session.SettingsRevision(info.Manifest.Id),
                string.Join(';', _session.Host.Commands.Where(command => command.OwnerId == info.Manifest.Id).Select(command => command.Definition.Id + ":" + command.Definition.Shortcut)));
            if (Card.IsChevronExpanded && !busy && state != _renderedState)
            {
                ClearEditors();
                RenderEditors();
                _renderedState = state;
            }
        }
        finally { _updating = false; }
    }

    private void ConfigureAction(PclIconButton button, string key, PclIcon icon)
    {
        button.IconData = icon.Data;
        button.IconScale = icon.Scale;
        ToolTip.SetTip(button, _session.T(key));
        global::Avalonia.Automation.AutomationProperties.SetName(button, Card.Header + " · " + _session.T(key));
    }

    private void ConfigureAction(PclButton button, string key)
    {
        button.Text = _session.T(key);
        ToolTip.SetTip(button, _session.T(key));
        global::Avalonia.Automation.AutomationProperties.SetName(button, Card.Header + " · " + _session.T(key));
    }

    private void ShowDetails(bool logs)
    {
        if (_disposed) return;
        var view = new ExtensionDetailsView(_session, _info, logs);
        _detailsView = view;
        view.Closed += () => { if (ReferenceEquals(_detailsView, view)) _detailsView = null; };
        view.Open();
    }

    private void RenderEditors()
    {
        foreach (var setting in _info.Manifest.Settings)
        {
            try { SettingsPanel.Children.Add(CreateSetting(setting)); }
            catch (Exception ex) { ShowError(ex); }
        }
        var commands = _session.Host.Commands.Where(command => command.OwnerId == _info.Manifest.Id).ToArray();
        if (commands.Length > 0) ShortcutsPanel.Children.Add(Label(_session.T("shortcuts"), true));
        foreach (var command in commands)
        {
            var title = command.Definition.TitleKey is { } key ? _info.Text.Get(key, command.Definition.Title) : command.Definition.Title;
            var field = new TextBox { Name = "Shortcut_" + command.Definition.Id.Replace('.', '_').Replace('-', '_'), Text = command.Definition.Shortcut ?? "", Watermark = _session.T("shortcut_hint"), MinWidth = 180 };
            var save = new PclButton { Name = "SaveShortcut_" + command.Definition.Id.Replace('.', '_').Replace('-', '_'), Text = _session.T("save"), Command = new ActionCommand(() => SaveShortcut(command.Definition.Id, field.Text)) };
            var editor = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = 8 };
            editor.Children.Add(field); Grid.SetColumn(save, 1); editor.Children.Add(save);
            ShortcutsPanel.Children.Add(Row(title, editor));
        }
        foreach (var view in _session.CreateSettingsViews(_info.Manifest.Id))
        {
            NativeSettingsPanel.Children.Add(view);
            if (view is IDisposable resource) _nativeResources.Add(resource);
        }
    }

    private Control CreateSetting(ExtensionSetting setting)
    {
        var title = setting.TitleKey is { } key ? _info.Text.Get(key, setting.Title) : setting.Title;
        Control editor;
        switch (setting.Kind)
        {
            case ExtensionSettingKind.Boolean:
                var checkbox = new CheckBox { Content = Label(title), IsChecked = _info.Settings.Get(setting.Key, bool.TryParse(setting.DefaultValue, out var flag) && flag) };
                checkbox.IsCheckedChanged += (_, _) => Save(() => _info.Settings.Set(setting.Key, checkbox.IsChecked == true));
                editor = checkbox;
                break;
            case ExtensionSettingKind.Number:
                var number = new NumericUpDown
                {
                    Minimum = (decimal)(setting.Minimum ?? -1_000_000), Maximum = (decimal)(setting.Maximum ?? 1_000_000),
                    Increment = setting.Maximum is <= 2 ? .25m : 1,
                    Value = (decimal)_info.Settings.Get(setting.Key, double.Parse(setting.DefaultValue ?? "0", CultureInfo.InvariantCulture))
                };
                number.ValueChanged += (_, _) => { if (number.Value is decimal value) Save(() => _info.Settings.Set(setting.Key, (double)value)); };
                editor = number;
                break;
            case ExtensionSettingKind.Choice:
                var combo = new ComboBox
                {
                    ItemsSource = setting.Choices.Select(choice => choice.TitleKey is { } choiceKey ? _info.Text.Get(choiceKey, choice.Title) : choice.Title).ToArray(),
                    SelectedIndex = Array.FindIndex(setting.Choices, choice => choice.Value == _info.Settings.Get(setting.Key, setting.DefaultValue ?? ""))
                };
                combo.SelectionChanged += (_, _) => { if (combo.SelectedIndex >= 0) Save(() => _info.Settings.Set(setting.Key, setting.Choices[combo.SelectedIndex].Value)); };
                editor = combo;
                break;
            default:
                var field = new TextBox { Text = _info.Settings.Get(setting.Key, setting.DefaultValue ?? ""), IsReadOnly = setting.Kind == ExtensionSettingKind.File };
                if (setting.Kind == ExtensionSettingKind.File)
                {
                    var choose = new PclButton { Text = _session.T("choose_file"), Command = new ActionCommand(() => Operate(async () =>
                    {
                        var path = await _session.PickFileAsync(title, setting.FilePatterns);
                        if (path is null || _disposed) return;
                        _info.Settings.Set(setting.Key, path);
                        field.Text = _info.Settings.Get(setting.Key, "");
                    })) };
                    var file = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = 8 };
                    file.Children.Add(field); Grid.SetColumn(choose, 1); file.Children.Add(choose); editor = file;
                }
                else
                {
                    field.TextChanged += (_, _) => Save(() => _info.Settings.Set(setting.Key, field.Text ?? ""));
                    editor = field;
                }
                break;
        }
        editor.Name = "Setting_" + setting.Key;
        return setting.Kind == ExtensionSettingKind.Boolean ? editor : Row(title, editor);
    }

    private void SaveShortcut(string id, string? text) => Save(() =>
    {
        var shortcut = text?.Trim();
        if (!string.IsNullOrEmpty(shortcut))
        {
            var parsed = Parse(shortcut);
            if (_session.Host.Commands.Any(other => other.Definition.Id != id && !string.IsNullOrWhiteSpace(other.Definition.Shortcut) && Parse(other.Definition.Shortcut!).Equals(parsed)))
                throw new InvalidOperationException(_session.T("shortcut_conflict"));
        }
        _session.Host.SetShortcut(id, string.IsNullOrEmpty(shortcut) ? null : shortcut);
        static KeyGesture Parse(string value) => KeyGesture.Parse(value.Replace("Primary", OperatingSystem.IsMacOS() ? "Meta" : "Ctrl", StringComparison.Ordinal));
    });

    private void Save(Action action)
    {
        if (_updating || _disposed || _busy || _updateInstaller?.IsBusy == true) return;
        try { action(); OperationErrorText.IsVisible = false; }
        catch (Exception ex) { ShowError(ex); }
    }

    private async void Operate(Func<ValueTask> action)
    {
        if (_updating || _disposed || _busy || _updateInstaller?.IsBusy == true) return;
        _busy = true;
        OperationErrorText.IsVisible = false;
        Refresh();
        try { await action(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_disposed) { Expand(); ShowError(ex); } }
        finally { _busy = false; Refresh(); }
    }

    private void ShowError(Exception ex)
    {
        OperationErrorText.Text = _session.T("operation_failed") + " " + ex.Message;
        OperationErrorText.IsVisible = true;
    }

    private static TextBlock Label(string text, bool heading = false)
    {
        var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        if (heading) label.FontWeight = FontWeight.SemiBold;
        return label;
    }

    private static Grid Row(string title, Control editor)
    {
        var grid = new Grid { ColumnDefinitions = new("160,*"), ColumnSpacing = 20 };
        grid.Children.Add(Label(title)); Grid.SetColumn(editor, 1); grid.Children.Add(editor);
        return grid;
    }

    private static void SetDefault(IExtensionSettings settings, ExtensionSetting setting)
    {
        if (setting.Kind == ExtensionSettingKind.Boolean) settings.Set(setting.Key, bool.TryParse(setting.DefaultValue, out var flag) && flag);
        else if (setting.Kind == ExtensionSettingKind.Number) settings.Set(setting.Key, double.Parse(setting.DefaultValue ?? "0", CultureInfo.InvariantCulture));
        else settings.Set(setting.Key, setting.DefaultValue ?? "");
    }

    private void ClearEditors()
    {
        NativeSettingsPanel.Children.Clear();
        foreach (var resource in _nativeResources)
            try { resource.Dispose(); }
            catch (Exception ex) { _session.Run(() => new ValueTask(_session.Host.ReportFaultAsync(_info.Manifest.Id, ex))); }
        _nativeResources.Clear(); SettingsPanel.Children.Clear(); ShortcutsPanel.Children.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        UpdateButton.ContextFlyout?.Hide();
        _updateInstaller?.Close();
        _detailsView?.Close();
        ClearEditors();
    }
}
