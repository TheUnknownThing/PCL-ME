using Avalonia.Headless;
using PCL.Frontend.Avalonia.Test;

// Reuse Avalonia's assembly session worker while keeping a fresh application
// for each Dispatch. Disposing a worker after every test can race its queue's
// cancellation and CompleteAdding calls in Avalonia 11.3.
[assembly: AvaloniaTestApplication(typeof(FrontendAnimationTest.AnimationTestApplication))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]
