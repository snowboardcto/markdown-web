using Xunit;

namespace TheMarkdownWeb.App.Tests;

/// <summary>
/// AC2 / AC3 command-logic tests — pure <c>[Fact]</c>, NO STA, NO window. Proves the toolbar
/// buttons are wired to live (if inert) command logic rather than dead XAML.
///
/// INTENDED API CONTRACT for the Step-5 implementer (the VM does not exist yet, so this file is
/// RED until it is created — a compile error is the expected red state, which turns green once
/// the type below exists and matches this shape exactly):
///
///   namespace TheMarkdownWeb.App;
///   public sealed class ShellViewModel
///   {
///       // Last toolbar action invoked. Defaults to ShellAction.None.
///       public ShellAction LastAction { get; }   // observable / settable by the command handlers
///
///       public void OnBack();      // sets LastAction = ShellAction.Back
///       public void OnForward();   // sets LastAction = ShellAction.Forward
///       public void OnReload();    // sets LastAction = ShellAction.Reload
///   }
///
///   public enum ShellAction { None, Back, Forward, Reload }
///
/// The OnBack/OnForward/OnReload methods are the inert handlers the XAML Click/Command binds to
/// (no history stack, no fetch, no reload target — real navigation lands in Story 3-2 / 3-5).
/// They need only record the last action so this CI-cheap test can prove the wiring.
/// </summary>
public class ShellViewModelTests
{
    [Fact]
    public void NewShellViewModel_LastAction_DefaultsToNone()
    {
        var vm = new ShellViewModel();

        Assert.Equal(ShellAction.None, vm.LastAction);
    }

    [Fact]
    public void OnBack_SetsLastActionToBack()
    {
        var vm = new ShellViewModel();

        vm.OnBack();

        Assert.Equal(ShellAction.Back, vm.LastAction);
    }

    [Fact]
    public void OnForward_SetsLastActionToForward()
    {
        var vm = new ShellViewModel();

        vm.OnForward();

        Assert.Equal(ShellAction.Forward, vm.LastAction);
    }

    [Fact]
    public void OnReload_SetsLastActionToReload()
    {
        var vm = new ShellViewModel();

        vm.OnReload();

        Assert.Equal(ShellAction.Reload, vm.LastAction);
    }
}
