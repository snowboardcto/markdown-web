using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC10 (Task 6) — 3.1's inert <c>ShellViewModel.OnBack/OnForward/OnReload</c> are made REAL by
/// delegating to an injected <c>NavigationController</c>, while STILL setting <c>LastAction</c> so the
/// existing <c>ShellViewModelTests</c> (3.1) stay green. The wiring is ADDITIVE: the parameterless
/// ctor + the no-arg handlers keep their 3.1 contract; an overload taking a controller adds the real
/// behavior.
///
/// INTENDED API CONTRACT (Step-5 implementer must match; RED until it exists). The 3.1 surface is
/// preserved (see ShellViewModelTests); 3.5 ADDS:
///
///   namespace TheMarkdownWeb.App;
///   public sealed class ShellViewModel
///   {
///       public ShellViewModel();                                 // 3.1 — inert, LastAction only
///       public ShellViewModel(NavigationController controller);  // 3.5 — delegates to the controller
///       public ShellAction LastAction { get; }
///       public void OnBack();    // sets LastAction = Back  AND (if wired) controller.GoBackAsync()
///       public void OnForward(); // sets LastAction = Fwd   AND (if wired) controller.GoForwardAsync()
///       public void OnReload();  // sets LastAction = Reload AND (if wired) controller.ReloadAsync()
///   }
///
/// All [Fact] — pure; the NavigationController uses a fake fetcher (no window, no socket).
/// </summary>
public class ShellViewModelNavigationTests
{
    private sealed class FakeLauncher : IUrlLauncher
    {
        public void Open(Uri url) { }
    }

    private static (ShellViewModel vm, List<Uri> fetched) BuildWiredVm()
    {
        var fetched = new List<Uri>();
        Func<Uri, CancellationToken, Task<FetchResult>> fetch = (url, _) =>
        {
            fetched.Add(url);
            return Task.FromResult(FetchResult.Success("# md"));
        };
        var controller = new NavigationController(fetch, (_, __) => { }, () => { }, new FakeLauncher());
        var vm = new ShellViewModel(controller);
        return (vm, fetched);
    }

    [Fact] // OnBack still records LastAction (3.1 contract preserved) when wired to a controller.
    public void OnBack_StillSetsLastAction_WhenWired()
    {
        (ShellViewModel vm, _) = BuildWiredVm();

        vm.OnBack();

        Assert.Equal(ShellAction.Back, vm.LastAction);
    }

    [Fact] // OnReload delegates to the controller: with a current page, it re-fetches that page.
    public async Task OnReload_DelegatesToController_ReFetchesCurrent()
    {
        var fetched = new List<Uri>();
        Func<Uri, CancellationToken, Task<FetchResult>> fetch = (url, _) =>
        {
            fetched.Add(url);
            return Task.FromResult(FetchResult.Success("# md"));
        };
        var controller = new NavigationController(fetch, (_, __) => { }, () => { }, new FakeLauncher());
        var page = new Uri("https://themarkdownweb.com/a.md");
        await controller.NavigateToAsync(page); // establish a Current to reload
        int before = fetched.Count;

        var vm = new ShellViewModel(controller);
        vm.OnReload();
        await Task.Yield(); // let the fire-and-forget reload run

        Assert.Equal(ShellAction.Reload, vm.LastAction);
        Assert.True(fetched.Count > before, "OnReload must delegate to the controller and re-fetch Current.");
        Assert.Equal(page, fetched[^1]);
    }
}
