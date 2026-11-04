using Avalonia.Headless;
using PCL.Frontend.Avalonia.Test;

// Reuse Avalonia's assembly session worker while keeping a fresh application
// for each Dispatch. Disposing a worker after every test can race its queue's
// cancellation and CompleteAdding calls in Avalonia 11.3.
// Image tests must use this worker too: Dispatch completes before application
// teardown, so separate workers can reset each other's global dispatcher.
[assembly: AvaloniaTestApplication(typeof(FrontendAnimationTest.AnimationTestApplication))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]
