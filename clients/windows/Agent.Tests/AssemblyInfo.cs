using Xunit;

// Run the Agent.Tests assembly's tests SEQUENTIALLY (no cross-class parallelism),
// mirroring App.Tests / Rendering.Tests.
//
// Agent.Tests are pure [Fact]s (engine / persona / keys / LLM-client — plain CLR + System.Net.Http,
// no DispatcherObject), so STA affinity is not strictly required here. The DisableTestParallelization
// discipline is kept for parity with the other test projects and to keep the DpapiSecretStore
// temp-file round-trip deterministic (no concurrent file writers to the same temp path).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
