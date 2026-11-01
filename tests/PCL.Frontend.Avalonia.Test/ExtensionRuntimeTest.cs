using System.IO.Compression;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Extensions.Contracts;
using PCL.Extensions.Runtime;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
public sealed class ExtensionRuntimeTest
{
    [TestMethod]
    public async Task Activation_IsSerializedAndDisablingReleasesEveryContribution()
    {
        await using var fixture = new RuntimeFixture();
        var activations = 0; var calls = 0; var disposed = 0; var events = 0;
        CancellationToken lifetime = default;
        var manifest = Manifest("test.lifecycle");
        fixture.Host.AddBuiltIn(manifest, fixture.Root, () => new TestExtension(async context =>
        {
            activations++; lifetime = context.Lifetime;
            await Task.Delay(20);
            context.Commands.Register(manifest.Commands[0], _ => { calls++; return ValueTask.CompletedTask; });
            context.Search.Register("test.lifecycle.search", (_, _) => ValueTask.FromResult<IReadOnlyList<ExtensionSearchResult>>(
                [new() { Id = "result", Title = "Result", CommandId = manifest.Commands[0].Id }]));
            context.Events.Subscribe("launcher.ready", _ => events++);
            context.Own(new ExtensionRegistration(() => disposed++));
        }));
        await Task.WhenAll(fixture.Host.ActivateAsync(manifest.Id).AsTask(), fixture.Host.ActivateAsync(manifest.Id).AsTask());
        Assert.AreEqual(1, activations);
        await fixture.Host.ExecuteCommandAsync(manifest.Commands[0].Id);
        var results = await fixture.Host.QueryAsync(new("Result"));
        await fixture.Host.ExecuteSearchResultAsync(results.Single());
        fixture.Host.PublishHostEvent("launcher.ready", JsonSerializer.SerializeToElement(true));
        Assert.AreEqual(2, calls); Assert.AreEqual(1, events);
        await fixture.Host.SetEnabledAsync(manifest.Id, false);
        Assert.IsTrue(lifetime.IsCancellationRequested);
        Assert.AreEqual(1, disposed);
        Assert.AreEqual(0, fixture.Host.Commands.Count);
        Assert.AreEqual(0, (await fixture.Host.QueryAsync(new(""))).Count);
        fixture.Host.PublishHostEvent("launcher.ready", JsonSerializer.SerializeToElement(true));
        Assert.AreEqual(1, events);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.ExecuteSearchResultAsync(results.Single()).AsTask());
        await fixture.Host.SetEnabledAsync(manifest.Id, true);
        Assert.AreEqual(2, activations);
    }

    [TestMethod]
    public async Task FailedActivation_CleansUpAndLeavesOtherExtensionsOperational()
    {
        await using var fixture = new RuntimeFixture();
        var released = false;
        fixture.Host.AddBuiltIn(Manifest("test.broken"), fixture.Root, () => new TestExtension(context =>
        {
            context.Own(new ExtensionRegistration(() => released = true));
            context.Commands.Register(context.Manifest.Commands[0], _ => ValueTask.CompletedTask);
            throw new InvalidOperationException("Activation failed deliberately.");
        }));
        fixture.Host.AddBuiltIn(Manifest("test.healthy"), fixture.Root, () => new TestExtension(context =>
        {
            context.Commands.Register(context.Manifest.Commands[0], _ => ValueTask.CompletedTask);
            return ValueTask.CompletedTask;
        }));
        await fixture.Host.StartAsync();
        Assert.IsTrue(released);
        Assert.AreEqual(ExtensionStatus.Faulted, fixture.Host.Get("test.broken").Status);
        Assert.AreEqual(ExtensionStatus.Active, fixture.Host.Get("test.healthy").Status);
        await fixture.Host.ExecuteCommandAsync("test.healthy.run");
        Assert.IsFalse(fixture.Host.Commands.Any(command => command.Definition.Id == "test.broken.run"));
    }

    [TestMethod]
    public async Task LazyCommands_ActivateOnDemandAndCanDisableTheirOwnExtension()
    {
        await using var fixture = new RuntimeFixture();
        var activations = 0;
        var manifest = Manifest("test.lazy") with { StartWithLauncher = false };
        fixture.Host.AddBuiltIn(manifest, fixture.Root, () => new TestExtension(context =>
        {
            activations++;
            context.Commands.Register(manifest.Commands[0], token => context.DisableAsync(token));
            return ValueTask.CompletedTask;
        }));
        await fixture.Host.StartAsync(); Assert.AreEqual(0, activations);
        await fixture.Host.ExecuteCommandAsync(manifest.Commands[0].Id);
        Assert.AreEqual(1, activations);
        Assert.AreEqual(ExtensionStatus.Disabled, fixture.Host.Get(manifest.Id).Status);
    }

    [TestMethod]
    public async Task Dependencies_RejectCyclesAndPreventDisablingRequiredExtensions()
    {
        await using var fixture = new RuntimeFixture();
        var a = Manifest("test.alpha");
        var b = Manifest("test.beta") with { Dependencies = [new(a.Id, "1.0.0")] };
        fixture.Host.AddBuiltIn(a, fixture.Root, () => new TestExtension(_ => ValueTask.CompletedTask));
        fixture.Host.AddBuiltIn(b, fixture.Root, () => new TestExtension(_ => ValueTask.CompletedTask));
        await fixture.Host.ActivateAsync(b.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.SetEnabledAsync(a.Id, false).AsTask());
        var c = Manifest("test.cycleone") with { Dependencies = [new("test.cycletwo", "1.0.0")] };
        var d = Manifest("test.cycletwo") with { Dependencies = [new(c.Id, "1.0.0")] };
        fixture.Host.AddBuiltIn(c, fixture.Root, () => new TestExtension(_ => ValueTask.CompletedTask));
        fixture.Host.AddBuiltIn(d, fixture.Root, () => new TestExtension(_ => ValueTask.CompletedTask));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.ActivateAsync(c.Id).AsTask());
    }

    [TestMethod]
    public async Task SafeMode_PreservesPreferencesWithoutRunningExtensionCode()
    {
        await using var fixture = new RuntimeFixture(safeMode: true);
        var activated = false;
        fixture.Host.AddBuiltIn(Manifest("test.safemode"), fixture.Root, () => new TestExtension(_ => { activated = true; return ValueTask.CompletedTask; }));
        await fixture.Host.StartAsync();
        Assert.IsFalse(activated);
        Assert.IsTrue(fixture.Host.Get("test.safemode").IsEnabled);
        Assert.IsFalse(fixture.Host.Commands.Single().CanExecute);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.ActivateAsync("test.safemode").AsTask());
    }

    [TestMethod]
    public async Task DependencyFailure_StopsDependentsBeforeTheDependencyAndAllowsRetry()
    {
        await using var fixture = new RuntimeFixture();
        var stopped = new List<string>();
        var dependency = Manifest("test.dependency");
        var dependent = Manifest("test.dependent") with { Dependencies = [new(dependency.Id, "1.0.0")] };
        foreach (var manifest in new[] { dependency, dependent })
            fixture.Host.AddBuiltIn(manifest, fixture.Root, () => new TestExtension(context =>
            {
                context.Own(new ExtensionRegistration(() => stopped.Add(manifest.Id)));
                context.Commands.Register(manifest.Commands.Single(), _ => ValueTask.CompletedTask);
                return ValueTask.CompletedTask;
            }));
        await fixture.Host.ActivateAsync(dependent.Id);
        await fixture.Host.ReportFaultAsync(dependency.Id, new InvalidOperationException("Dependency failed"));
        CollectionAssert.AreEqual(new[] { dependent.Id, dependency.Id }, stopped);
        Assert.AreEqual(ExtensionStatus.Faulted, fixture.Host.Get(dependent.Id).Status);
        Assert.IsFalse(fixture.Host.Commands.Any(command => command.CanExecute));
        await fixture.Host.SetEnabledAsync(dependent.Id, true);
        Assert.AreEqual(ExtensionStatus.Active, fixture.Host.Get(dependency.Id).Status);
        Assert.AreEqual(ExtensionStatus.Active, fixture.Host.Get(dependent.Id).Status);
    }

    [TestMethod]
    public async Task Search_CancelsWithoutLettingOneProviderFailureHideOtherResults()
    {
        await using var fixture = new RuntimeFixture();
        using var broken = fixture.Host.RegisterHostSearch("test.broken", (_, _) => throw new InvalidOperationException("Broken provider"));
        using var healthy = fixture.Host.RegisterHostSearch("test.healthy", (_, _) => ValueTask.FromResult<IReadOnlyList<ExtensionSearchResult>>(
            [new() { Id = "one", Title = "Minecraft" }, new() { Id = "one", Title = "Minecraft" }]));
        Assert.AreEqual(1, (await fixture.Host.QueryAsync(new("Minecraft"))).Count);
        using var slow = fixture.Host.RegisterHostSearch("test.slow", async (_, token) => { await Task.Delay(10_000, token); return []; });
        using var cancellation = new CancellationTokenSource(30);
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Host.QueryAsync(new("test"), cancellation.Token).AsTask());
    }

    [TestMethod]
    public async Task Search_FiltersBeforeTheLimitAndDeduplicatesCommandDestinations()
    {
        await using var fixture = new RuntimeFixture();
        ExtensionSearchKind? receivedKind = null;
        using var first = fixture.Host.RegisterHostSearch("test.categories", (query, _) =>
        {
            receivedKind = query.Kind;
            return ValueTask.FromResult<IReadOnlyList<ExtensionSearchResult>>(
            [
                new() { Id = "page", Title = "Needle page", Kind = ExtensionSearchKind.Page, Score = 1 },
                new() { Id = "instance", Title = "Needle instance", Kind = ExtensionSearchKind.Instance },
                new() { Id = "command", Title = "Needle command", Kind = ExtensionSearchKind.Command, CommandId = "test.open" }
            ]);
        });
        using var second = fixture.Host.RegisterHostSearch("test.other", (_, _) => ValueTask.FromResult<IReadOnlyList<ExtensionSearchResult>>(
            [new() { Id = "duplicate", Title = "Needle command", Kind = ExtensionSearchKind.Command, CommandId = "test.open", Score = 1 }]));
        var instances = await fixture.Host.QueryAsync(new("Needle", 1, ExtensionSearchKind.Instance));
        Assert.AreEqual(ExtensionSearchKind.Instance, receivedKind);
        Assert.AreEqual("instance", instances.Single().Id, "Higher-scoring pages must not exhaust a filtered query's result limit.");
        var commands = await fixture.Host.QueryAsync(new("Needle", Kind: ExtensionSearchKind.Command));
        Assert.AreEqual("duplicate", commands.Single().Id, "The same command should appear once, using the best matching provider result.");
    }

    [TestMethod]
    public async Task PackageInstall_LoadsThePublicContractAndStagesAnActiveUpdate()
    {
        await using var fixture = new RuntimeFixture();
        var archive = MakePackage(fixture.Root, Manifest("test.external") with
        {
            EntryAssembly = "module.dll", EntryType = typeof(ExternalTestExtension).FullName
        });
        var installed = await fixture.Host.InstallAsync(archive);
        Assert.IsFalse(installed.IsEnabled);
        await fixture.Host.SetEnabledAsync(installed.Manifest.Id, true);
        Assert.IsTrue(installed.Settings.Get("activated", false));
        await fixture.Host.ExecuteCommandAsync("test.external.run");
        Assert.AreEqual(1, installed.Settings.Get("calls", 0));
        var update = MakePackage(fixture.Root, installed.Manifest with { Version = "1.1.0" });
        await fixture.Host.InstallAsync(update);
        Assert.IsTrue(installed.RestartRequired);
        Assert.AreEqual("1.0.0", installed.Manifest.Version);
        Assert.IsTrue(File.Exists(Path.Combine(fixture.Root, "Pending", "test.external", "extension.json")));
        await fixture.Host.UninstallAsync(installed.Manifest.Id);
        Assert.IsTrue(installed.RemovalPending);
        Assert.AreEqual(ExtensionStatus.Disabled, installed.Status);
    }

    [TestMethod]
    [DataRow("../escaped.txt")]
    [DataRow("/absolute.txt")]
    [DataRow("folder\\escaped.txt")]
    public async Task PackageInstall_RejectsEscapingPathsWithoutLeavingPartialPackages(string entryName)
    {
        await using var fixture = new RuntimeFixture();
        var path = Path.Combine(fixture.Root, "malformed.pclext");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(zip.CreateEntry(entryName).Open())) writer.Write("bad");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Host.InstallAsync(path).AsTask());
        Assert.AreEqual(0, fixture.Host.Extensions.Count);
        Assert.IsFalse(Directory.EnumerateDirectories(Path.Combine(fixture.Root, "Staging")).Any());
    }

    [TestMethod]
    public void Settings_AreAtomicAndSurviveReopening()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-extension-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(root, "settings.json");
            var settings = new ExtensionSettings(path);
            settings.Set("selected", "Alex"); settings.Set("scale", 1.25);
            var reopened = new ExtensionSettings(path);
            Assert.AreEqual("Alex", reopened.Get("selected", ""));
            Assert.AreEqual(1.25, reopened.Get("scale", 1d));
            Assert.AreEqual(1, Directory.GetFiles(root).Length);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task WorkerProcess_RoundTripsConcurrentRequestsAndCancelsBeforeBeingDisposedWithItsExtension()
    {
        await using var fixture = new RuntimeFixture(workerCommand: ["dotnet", typeof(global::Program).Assembly.Location]);
        File.Copy(typeof(HelloExtension.HelloWorker).Assembly.Location, Path.Combine(fixture.Root, "HelloExtension.dll"));
        IExtensionWorker? worker = null;
        var manifest = Manifest("test.worker");
        fixture.Host.AddBuiltIn(manifest, fixture.Root, () => new TestExtension(context =>
        {
            worker = context.StartWorker(new("HelloExtension.dll", "HelloExtension.HelloWorker"));
            return ValueTask.CompletedTask;
        }));
        await fixture.Host.StartAsync();
        Assert.IsNotNull(worker);
        var responses = await Task.WhenAll(Enumerable.Range(0, 3)
            .Select(value => worker.RequestAsync("echo", JsonSerializer.SerializeToElement(value)).AsTask()));
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, responses.Select(response => response.GetProperty("payload").GetInt32()).ToArray());
        using var cancellation = new CancellationTokenSource(30);
        await Assert.ThrowsAsync<OperationCanceledException>(() => worker.RequestAsync("delay", JsonSerializer.SerializeToElement(10_000), cancellation.Token).AsTask());
        var next = await worker.RequestAsync("echo", JsonSerializer.SerializeToElement("still alive"));
        Assert.AreEqual("still alive", next.GetProperty("payload").GetString());
        await fixture.Host.SetEnabledAsync(manifest.Id, false);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => worker.RequestAsync("echo", JsonSerializer.SerializeToElement(0)).AsTask());
    }

    [TestMethod]
    public async Task ExpectedOperationErrors_DoNotFaultAnOtherwiseHealthyExtension()
    {
        await using var fixture = new RuntimeFixture();
        var manifest = Manifest("test.busy");
        fixture.Host.AddBuiltIn(manifest, fixture.Root, () => new TestExtension(context =>
        {
            context.Commands.Register(manifest.Commands.Single(), _ => throw new ExtensionOperationException("launcher.busy", "Busy"));
            return ValueTask.CompletedTask;
        }));
        await Assert.ThrowsAsync<ExtensionOperationException>(() => fixture.Host.ExecuteCommandAsync(manifest.Commands.Single().Id).AsTask());
        Assert.AreEqual(ExtensionStatus.Active, fixture.Host.Get(manifest.Id).Status);
    }

    [TestMethod]
    public async Task LateCommandFailure_DoesNotFaultANewerActivation()
    {
        await using var fixture = new RuntimeFixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manifest = Manifest("test.latecommand");
        Func<CancellationToken, ValueTask> handler = async _ =>
        {
            entered.TrySetResult();
            await release.Task;
            throw new InvalidOperationException("The previous activation finished late.");
        };
        fixture.Host.AddBuiltIn(manifest, fixture.Root, () => new TestExtension(context =>
        {
            context.Commands.Register(manifest.Commands.Single(), handler);
            return ValueTask.CompletedTask;
        }));
        var execution = fixture.Host.ExecuteCommandAsync(manifest.Commands.Single().Id).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await fixture.Host.SetEnabledAsync(manifest.Id, false);
        await fixture.Host.SetEnabledAsync(manifest.Id, true);
        release.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => execution);
        Assert.AreEqual(ExtensionStatus.Active, fixture.Host.Get(manifest.Id).Status);
    }

    [TestMethod]
    public async Task StagedPackages_AreAppliedBeforeDiscoveryAndInterruptedActivationIsDisabled()
    {
        await using var fixture = new RuntimeFixture();
        var manifest = Manifest("test.pending") with { EntryAssembly = "module.dll", EntryType = typeof(ExternalTestExtension).FullName, Version = "1.2.0" };
        var archive = MakePackage(fixture.Root, manifest);
        ExtensionPackage.Extract(archive, Path.Combine(fixture.Root, "Pending", manifest.Id));
        var state = new ExtensionSettings(Path.Combine(fixture.Root, "state.json"));
        state.Set("activating", manifest.Id);
        state.Set("enabled." + manifest.Id, true);
        await using var restarted = new ExtensionHost(fixture.Root, new EmptyLauncher(), () => "en-US", action => action(), _ => new Dictionary<Type, object>());
        restarted.Discover();
        Assert.AreEqual("1.2.0", restarted.Get(manifest.Id).Manifest.Version);
        Assert.IsFalse(restarted.Get(manifest.Id).IsEnabled);
        Assert.AreEqual(ExtensionStatus.Faulted, restarted.Get(manifest.Id).Status);
    }

    internal static ExtensionManifest Manifest(string id) => new()
    {
        Id = id, Name = id, Version = "1.0.0", StartWithLauncher = true,
        Commands = [new() { Id = id + ".run", Title = "Run" }]
    };

    [TestMethod]
    public async Task CorruptStateAndSettings_ArePreservedWithoutPreventingHostStartup()
    {
        await using var fixture = new RuntimeFixture();
        await fixture.Host.DisposeAsync();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "state.json"), "{broken");
        var settingsDirectory = Path.Combine(fixture.Root, "Data", "test.recovery");
        Directory.CreateDirectory(settingsDirectory);
        await File.WriteAllTextAsync(Path.Combine(settingsDirectory, "settings.json"), "{broken");
        await using var restarted = new ExtensionHost(fixture.Root, new EmptyLauncher(), () => "en-US", action => action(), _ => new Dictionary<Type, object>());
        restarted.AddBuiltIn(Manifest("test.recovery"), fixture.Root, () => new TestExtension(_ => ValueTask.CompletedTask));
        await restarted.StartAsync();
        Assert.IsFalse(restarted.Get("test.recovery").IsEnabled);
        Assert.AreEqual(ExtensionStatus.Faulted, restarted.Get("test.recovery").Status);
        Assert.AreEqual(1, Directory.GetFiles(fixture.Root, "state.json.invalid-*").Length);
        Assert.AreEqual(1, Directory.GetFiles(settingsDirectory, "settings.json.invalid-*").Length);
        await restarted.SetEnabledAsync("test.recovery", true);
        Assert.AreEqual(ExtensionStatus.Active, restarted.Get("test.recovery").Status);
    }

    [TestMethod]
    public async Task InvalidSettingsAndLocaleMetadata_AreRejectedBeforeInstallation()
    {
        await using var fixture = new RuntimeFixture();
        var manifest = Manifest("test.invalidmetadata") with
        {
            EntryAssembly = "module.dll", EntryType = typeof(ExternalTestExtension).FullName,
            Settings = [new() { Key = "scale", Title = "Scale", Kind = ExtensionSettingKind.Number, DefaultValue = "NaN" }]
        };
        var package = MakePackage(fixture.Root, manifest);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Host.InstallAsync(package).AsTask());
        var directory = Path.Combine(fixture.Root, "locale-test");
        Directory.CreateDirectory(Path.Combine(directory, "Locales"));
        File.Copy(typeof(ExternalTestExtension).Assembly.Location, Path.Combine(directory, "module.dll"));
        await File.WriteAllTextAsync(Path.Combine(directory, "Locales", "en-US.json"), "{}");
        var localized = manifest with { Settings = [], Commands = [new() { Id = "test.invalidmetadata.run", Title = "Run", TitleKey = "missing" }] };
        Assert.Throws<InvalidDataException>(() => ExtensionPackage.Validate(localized, directory));
        Assert.AreEqual(0, fixture.Host.Extensions.Count);
    }

    internal static string MakePackage(string root, ExtensionManifest manifest)
    {
        var path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".pclext");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var output = zip.CreateEntry("extension.json").Open()) JsonSerializer.Serialize(output, manifest, ExtensionPackage.JsonOptions);
        zip.CreateEntryFromFile(typeof(ExternalTestExtension).Assembly.Location, manifest.EntryAssembly!);
        return path;
    }

    internal sealed class RuntimeFixture : IAsyncDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "pcl-extension-runtime-" + Guid.NewGuid().ToString("N"));
        public ExtensionHost Host { get; }
        public RuntimeFixture(bool safeMode = false, IReadOnlyList<string>? workerCommand = null) => Host = new(Root, new EmptyLauncher(), () => "en-US", action => action(), _ => new Dictionary<Type, object>(), safeMode: safeMode, workerCommand: workerCommand);
        public async ValueTask DisposeAsync() { await Host.DisposeAsync(); if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }

    internal sealed class TestExtension(Func<IExtensionContext, ValueTask> activate) : IExtension
    {
        public ValueTask ActivateAsync(IExtensionContext context, CancellationToken cancellationToken) => activate(context);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    internal sealed class EmptyLauncher : ILauncherServices
    {
        public ValueTask<IReadOnlyList<LauncherInstance>> GetInstancesAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<LauncherInstance>>([]);
        public IReadOnlyList<LauncherLocation> GetLocations() => [];
        public ValueTask NavigateAsync(string route, IReadOnlyDictionary<string, string>? arguments = null, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask LaunchAsync(string instanceId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public void ShowLauncher() { }
        public void Notify(string message, bool isError = false) { }
        public ValueTask<string?> PickFileAsync(string title, IReadOnlyList<string> patterns, CancellationToken cancellationToken = default) => ValueTask.FromResult<string?>(null);
    }
}

public sealed class ExternalTestExtension : IExtension
{
    public ValueTask ActivateAsync(IExtensionContext context, CancellationToken cancellationToken)
    {
        context.Settings.Set("activated", true);
        context.Commands.Register(context.Manifest.Commands.Single(), _ =>
        {
            context.Settings.Set("calls", context.Settings.Get("calls", 0) + 1);
            return ValueTask.CompletedTask;
        });
        return ValueTask.CompletedTask;
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
