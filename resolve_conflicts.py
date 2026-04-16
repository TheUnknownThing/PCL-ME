import re
import sys

def read_file(path):
    with open(path, 'r', encoding='utf-8') as f:
        return f.read()

def write_file(path, content):
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)

def resolve_all_conflicts(content, file_resolvers):
    """Apply a list of (head, main, resolution) tuples to content."""
    for head_str, main_str, resolution in file_resolvers:
        conflict = f"<<<<<<< HEAD\n{head_str}\n=======\n{main_str}\n>>>>>>> origin/main"
        if conflict in content:
            content = content.replace(conflict, resolution, 1)
        else:
            print(f"WARNING: Could not find conflict block for resolution:\n  HEAD={repr(head_str[:80])}", file=sys.stderr)
    return content

def check_no_conflicts(content, path):
    if '<<<<<<< HEAD' in content:
        remaining = [(m.start(), content[m.start():m.start()+200]) for m in re.finditer(r'<<<<<<< HEAD', content)]
        print(f"WARNING: {len(remaining)} conflicts remain in {path}", file=sys.stderr)
        for pos, ctx in remaining:
            print(f"  at pos {pos}: {repr(ctx[:100])}", file=sys.stderr)
    return content


# ============================================================
# 1. App.axaml.cs - Combine fields
# ============================================================
path = 'PCL.Frontend.Avalonia/Desktop/App.axaml.cs'
content = read_file(path)
content = resolve_all_conflicts(content, [
    (
        '    private FrontendRuntimePaths? _runtimePaths;\n    private I18nService? _i18nService;',
        '    private readonly FrontendRuntimePaths _runtimePaths;\n    private readonly FrontendPlatformAdapter _platformAdapter;',
        '    private readonly FrontendRuntimePaths _runtimePaths;\n    private readonly FrontendPlatformAdapter _platformAdapter;\n    private I18nService? _i18nService;'
    )
])
check_no_conflicts(content, path)
write_file(path, content)
print(f"Resolved: {path}")

# ============================================================
# 2. DownloadInstallShellRightPaneView.axaml - Keep HEAD's PclButton
# ============================================================
path = 'PCL.Frontend.Avalonia/Desktop/ShellViews/Right/DownloadInstallShellRightPaneView.axaml'
content = read_file(path)
content = resolve_all_conflicts(content, [
    (
        '''
                    <controls:PclButton
                        Width="88"
                        HorizontalAlignment="Right"
                        IsVisible="{Binding CanClear}"
                        Command="{Binding ClearCommand}"
                        Text="{Binding $parent[UserControl].DataContext.DownloadInstallClearSelectionText}" />''',
        '',
        '''
                    <controls:PclButton
                        Width="88"
                        HorizontalAlignment="Right"
                        IsVisible="{Binding CanClear}"
                        Command="{Binding ClearCommand}"
                        Text="{Binding $parent[UserControl].DataContext.DownloadInstallClearSelectionText}" />'''
    )
])
check_no_conflicts(content, path)
write_file(path, content)
print(f"Resolved: {path}")

# ============================================================
# 3. DownloadResourceShellRightPaneView.axaml
# ============================================================
path = 'PCL.Frontend.Avalonia/Desktop/ShellViews/Right/DownloadResourceShellRightPaneView.axaml'
content = read_file(path)
content = resolve_all_conflicts(content, [
    (
        '          Header="{Binding DownloadResourceCurrentInstanceTitleText}"',
        '          Header="{Binding DownloadResourceCurrentInstanceCardTitle}"',
        '          Header="{Binding DownloadResourceCurrentInstanceCardTitle}"'
    ),
    (
        '                Text="{Binding DownloadResourceSwitchInstanceText}" />',
        '                Text="{Binding DownloadResourceCurrentInstanceActionText}" />',
        '                Text="{Binding DownloadResourceCurrentInstanceActionText}" />'
    )
])
check_no_conflicts(content, path)
write_file(path, content)
print(f"Resolved: {path}")

# ============================================================
# 4. SetupGameManageShellRightPaneView.axaml - Keep HEAD's i18n + 2-column Grid
# ============================================================
path = 'PCL.Frontend.Avalonia/Desktop/ShellViews/Right/SetupGameManageShellRightPaneView.axaml'
content = read_file(path)
content = resolve_all_conflicts(content, [
    (
        '                Text="{Binding SetupText.GameManage.InstallBehaviorLabel}" />\n            <Grid Grid.Row="8" Grid.Column="1" ColumnDefinitions="*,*">',
        '                Text="安装行为" />\n                        <Grid Grid.Row="8" Grid.Column="1" ColumnDefinitions="*">',
        '                Text="{Binding SetupText.GameManage.InstallBehaviorLabel}" />\n            <Grid Grid.Row="8" Grid.Column="1" ColumnDefinitions="*,*">'
    ),
    (
        '''              <CheckBox
                  Grid.Column="1"
                  Foreground="{DynamicResource ColorBrush1}"
                  Content="{Binding SetupText.GameManage.UpgradePartialAuthlibLabel}"
                  IsChecked="{Binding UpgradePartialAuthlib}" />''',
        '',
        '''              <CheckBox
                  Grid.Column="1"
                  Foreground="{DynamicResource ColorBrush1}"
                  Content="{Binding SetupText.GameManage.UpgradePartialAuthlibLabel}"
                  IsChecked="{Binding UpgradePartialAuthlib}" />'''
    )
])
check_no_conflicts(content, path)
write_file(path, content)
print(f"Resolved: {path}")

# ============================================================
# 5. SetupLaunchShellRightPaneView.axaml - Keep HEAD's full i18n version
# ============================================================
path = 'PCL.Frontend.Avalonia/Desktop/ShellViews/Right/SetupLaunchShellRightPaneView.axaml'
content = read_file(path)
head_str = '''                Text="{Binding SetupText.Launch.MicrosoftAuthLabel}" />
            <ComboBox
                Grid.Row="12"
                Grid.Column="1"
                Height="28"
                IsEnabled="False"
                ItemsSource="{Binding LaunchMicrosoftAuthOptions}"
                SelectedIndex="{Binding SelectedLaunchMicrosoftAuthIndex}" />

            <TextBlock
                Grid.Row="14"
                VerticalAlignment="Center"
                Foreground="{DynamicResource ColorBrush1}"
                FontSize="13.5"
                Margin="0,0,25,0"
                Text="{Binding SetupText.Launch.PreferredIpStackLabel}" />'''
main_str = '''                Text="IP 协议偏好" />'''
content = resolve_all_conflicts(content, [(head_str, main_str, head_str)])
check_no_conflicts(content, path)
write_file(path, content)
print(f"Resolved: {path}")

