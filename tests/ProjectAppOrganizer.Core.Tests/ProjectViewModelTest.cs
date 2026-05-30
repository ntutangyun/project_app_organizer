using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Tests.Fakes;
using ProjectAppOrganizer.Core.Terminals;
using ProjectAppOrganizer.Core.ViewModels;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.Core.Tests;

public class ProjectViewModelTest
{
    private sealed class FakeLauncher : ITerminalLauncher
    {
        public nint NextHandle = 100;
        public TerminalProfile? Launched;
        public nint Launch(TerminalProfile profile) { Launched = profile; return NextHandle; }
    }

    private static ProjectViewModel Make(
        out FakeWindowManager wm, out FakeLauncher launcher, Project? project = null)
    {
        wm = new FakeWindowManager();
        launcher = new FakeLauncher();
        var stage = new StageManager(wm);
        return new ProjectViewModel(
            project ?? new Project { Name = "P" },
            wm, launcher, stage,
            stageRect: () => new PixelRect(0, 0, 800, 600));
    }

    [Fact]
    public void OpenTerminal_AddsTabAndStagesIt()
    {
        var vm = Make(out var wm, out var launcher);
        var profile = new TerminalProfile { Name = "bash", Shell = ShellType.GitBash };

        vm.OpenTerminal(profile);

        Assert.Single(vm.Tabs);
        Assert.Equal(TabKind.Terminal, vm.Tabs[0].Kind);
        Assert.Same(vm.Tabs[0], vm.ActiveTab);
        Assert.Same(profile, launcher.Launched);
        Assert.Contains("MoveResize:100:0,0,800,600", wm.Calls);
    }

    [Fact]
    public void AttachWindow_AddsAttachedTabAndStagesIt()
    {
        var vm = Make(out var wm, out _);

        vm.AttachWindow(new WindowInfo(55, "VS Code", "Code.exe", true, false));

        Assert.Single(vm.Tabs);
        Assert.Equal(TabKind.Attached, vm.Tabs[0].Kind);
        Assert.Contains("MoveResize:55:0,0,800,600", wm.Calls);
    }

    [Fact]
    public void Activate_StagesSelectedTab()
    {
        var vm = Make(out var wm, out _);
        vm.AttachWindow(new WindowInfo(55, "A", "a.exe", true, false));
        vm.AttachWindow(new WindowInfo(66, "B", "b.exe", true, false));
        wm.Calls.Clear();

        vm.Activate(vm.Tabs[0]);

        Assert.Same(vm.Tabs[0], vm.ActiveTab);
        Assert.Contains("Minimize:66", wm.Calls);
        Assert.Contains("MoveResize:55:0,0,800,600", wm.Calls);
    }

    [Fact]
    public void CloseTab_Terminal_ClosesProcessWindow()
    {
        var vm = Make(out var wm, out _);
        vm.OpenTerminal(new TerminalProfile { Name = "t" });
        var tab = vm.Tabs[0];

        vm.CloseTab(tab);

        Assert.Empty(vm.Tabs);
        Assert.Contains("Close:100", wm.Calls); // terminals get closed
    }

    [Fact]
    public void CloseTab_Attached_DetachesWithoutClosing()
    {
        var vm = Make(out var wm, out _);
        vm.AttachWindow(new WindowInfo(55, "A", "a.exe", true, false));
        var tab = vm.Tabs[0];
        wm.Calls.Clear();

        vm.CloseTab(tab);

        Assert.Empty(vm.Tabs);
        Assert.DoesNotContain("Close:55", wm.Calls); // attached windows are NOT closed
        Assert.Contains("Restore:55", wm.Calls);     // restored to floating
    }

    [Fact]
    public void AddTodo_And_RemoveTodo_UpdateCollection()
    {
        var vm = Make(out _, out _);

        vm.AddTodo("write tests");
        Assert.Single(vm.Project.Todos);
        Assert.Equal("write tests", vm.Project.Todos[0].Text);

        vm.RemoveTodo(vm.Project.Todos[0]);
        Assert.Empty(vm.Project.Todos);
    }
}
