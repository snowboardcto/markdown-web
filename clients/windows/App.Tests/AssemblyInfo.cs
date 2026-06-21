using Xunit;

// Run the App.Tests assembly's tests SEQUENTIALLY (no cross-class parallelism).
//
// WHY: several `[StaFact]` tests construct the real WPF `MainWindow`, which calls
// `Application.LoadComponent` to load the compiled BAML out of the packaged app resources.
// `System.IO.Packaging.PackagePart` (used by that load path) is NOT thread-safe — its internal
// requested-streams list races when multiple STA test threads load the same XAML resource at
// once, surfacing as an intermittent
//   System.ArgumentOutOfRangeException in PackagePart.CleanUpRequestedStreamsList()
// inside InitializeComponent(). xUnit runs distinct test CLASSES (collections) in parallel by
// default, so the more window-constructing classes exist (ShellWindowTests,
// ToolbarAccessibilityTests, AddressBarWindowTests, ...), the more likely the race fires.
//
// Disabling parallelization serializes window construction, making the WPF resource load
// deterministic. The suite is small and fast (~10s), so the throughput cost is negligible.
// Xunit.StaFact still gives each test its own STA thread; this only removes CONCURRENCY between
// tests, not the STA affinity each one needs.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
