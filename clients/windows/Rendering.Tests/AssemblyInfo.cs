using Xunit;

// Run the Rendering.Tests assembly's tests SEQUENTIALLY (no cross-class parallelism).
//
// WHY: every Story 3.3 render test constructs and walks a WPF `FlowDocument` and its flow
// elements (`Paragraph`, `Run`, `Bold`, `Italic`, `Span`, `List`, `ListItem`, `Section`,
// `Table`, `BlockUIContainer`, `InlineUIContainer`, `Image`, `CheckBox`, `Hyperlink`). Each of
// those is a `System.Windows.Threading.DispatcherObject` with STA thread affinity, so the tests
// run via `Xunit.StaFact`'s `[StaFact]` (one STA thread per test). But WPF resource/packaging
// loading (`System.IO.Packaging.PackagePart`) is NOT thread-safe: concurrent WPF-object
// construction across multiple STA test threads can race its internal requested-streams list and
// surface as an intermittent
//   System.ArgumentOutOfRangeException in PackagePart.CleanUpRequestedStreamsList()
// — exactly the race App.Tests hit at Story 3.2 run #9 and fixed at run #11.
//
// xUnit runs distinct test CLASSES (collections) in parallel by default. Disabling
// parallelization serializes WPF-object construction so the resource load is deterministic.
// `[StaFact]` still gives each test its own STA thread; this attribute only removes CONCURRENCY
// between tests, not the STA affinity each one needs.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
