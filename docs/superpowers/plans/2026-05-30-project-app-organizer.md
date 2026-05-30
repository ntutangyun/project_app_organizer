# Project App Organizer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Windows desktop app that groups each project's terminals and app windows into a tabbed "stage", with a per-project title, description, and todo list, so you can switch project context instantly.

**Architecture:** Three projects — `ProjectAppOrganizer.Core` (pure, testable: models, persistence, view-models, stage logic behind an `IWindowManager` interface), `ProjectAppOrganizer.App` (WPF UI + concrete Win32 interop + DI wiring), and `ProjectAppOrganizer.Core.Tests` (xUnit). The Docker/stage model moves/resizes real OS windows into a reserved screen region rather than embedding them.

**Tech Stack:** C# / .NET 10 (`net10.0-windows`), WPF, CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection, System.Text.Json, xUnit. Win32 via P/Invoke (`user32.dll`).

**Prerequisite:** .NET 10 SDK installed (`dotnet --version` ≥ 10.0). Confirmed present on this machine.

---

## File Structure

```
ProjectAppOrganizer.sln
src/
  ProjectAppOrganizer.Core/
    ProjectAppOrganizer.Core.csproj
    Models/ShellType.cs
    Models/TodoItem.cs
    Models/TerminalProfile.cs
    Models/Project.cs
    Persistence/IProjectStore.cs
    Persistence/JsonProjectStore.cs
    Windows/PixelRect.cs
    Windows/WindowInfo.cs
    Windows/IWindowManager.cs
    Windows/WindowFilter.cs
    Windows/StageManager.cs
    Terminals/ShellCommand.cs
    Terminals/ITerminalLauncher.cs
    ViewModels/TabKind.cs
    ViewModels/WindowTab.cs
    ViewModels/ProjectViewModel.cs
    ViewModels/MainViewModel.cs
  ProjectAppOrganizer.App/
    ProjectAppOrganizer.App.csproj
    Windows/Win32Interop.cs
    Windows/Win32WindowManager.cs
    Terminals/ProcessTerminalLauncher.cs
    App.xaml / App.xaml.cs
    MainWindow.xaml / MainWindow.xaml.cs
    Views/PickerWindow.xaml / .cs
tests/
  ProjectAppOrganizer.Core.Tests/
    ProjectAppOrganizer.Core.Tests.csproj
    Fakes/FakeWindowManager.cs
    (one test file per Task)
```

---

## Task 0: Solution & project scaffolding

**Files:**
- Create: `ProjectAppOrganizer.sln`
- Create: `src/ProjectAppOrganizer.Core/ProjectAppOrganizer.Core.csproj`
- Create: `src/ProjectAppOrganizer.App/ProjectAppOrganizer.App.csproj`
- Create: `tests/ProjectAppOrganizer.Core.Tests/ProjectAppOrganizer.Core.Tests.csproj`
- Test: `tests/ProjectAppOrganizer.Core.Tests/SmokeTest.cs`

- [ ] **Step 1: Create the solution and Core class library**

```bash
cd "C:/Users/capyb/Desktop/project_app_organizer"
dotnet new sln -n ProjectAppOrganizer
dotnet new classlib -n ProjectAppOrganizer.Core -o src/ProjectAppOrganizer.Core -f net8.0-windows
dotnet sln add src/ProjectAppOrganizer.Core/ProjectAppOrganizer.Core.csproj
rm src/ProjectAppOrganizer.Core/Class1.cs
dotnet add src/ProjectAppOrganizer.Core package CommunityToolkit.Mvvm
```

- [ ] **Step 2: Create the WPF app project**

```bash
dotnet new wpf -n ProjectAppOrganizer.App -o src/ProjectAppOrganizer.App -f net8.0-windows
dotnet sln add src/ProjectAppOrganizer.App/ProjectAppOrganizer.App.csproj
dotnet add src/ProjectAppOrganizer.App reference src/ProjectAppOrganizer.Core/ProjectAppOrganizer.Core.csproj
dotnet add src/ProjectAppOrganizer.App package CommunityToolkit.Mvvm
dotnet add src/ProjectAppOrganizer.App package Microsoft.Extensions.DependencyInjection
```

- [ ] **Step 3: Create the test project**

```bash
dotnet new xunit -n ProjectAppOrganizer.Core.Tests -o tests/ProjectAppOrganizer.Core.Tests -f net8.0-windows
dotnet sln add tests/ProjectAppOrganizer.Core.Tests/ProjectAppOrganizer.Core.Tests.csproj
dotnet add tests/ProjectAppOrganizer.Core.Tests reference src/ProjectAppOrganizer.Core/ProjectAppOrganizer.Core.csproj
rm tests/ProjectAppOrganizer.Core.Tests/UnitTest1.cs
```

- [ ] **Step 4: Write a smoke test**

Create `tests/ProjectAppOrganizer.Core.Tests/SmokeTest.cs`:

```csharp
namespace ProjectAppOrganizer.Core.Tests;

public class SmokeTest
{
    [Fact]
    public void TestRunnerWorks()
    {
        Assert.Equal(4, 2 + 2);
    }
}
```

- [ ] **Step 5: Build and test**

Run: `dotnet build` then `dotnet test`
Expected: build succeeds; 1 test passes.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution, core/app/test projects"
```

---

## Task 1: Domain models

**Files:**
- Create: `src/ProjectAppOrganizer.Core/Models/ShellType.cs`
- Create: `src/ProjectAppOrganizer.Core/Models/TodoItem.cs`
- Create: `src/ProjectAppOrganizer.Core/Models/TerminalProfile.cs`
- Create: `src/ProjectAppOrganizer.Core/Models/Project.cs`
- Test: `tests/ProjectAppOrganizer.Core.Tests/ModelsTest.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using ProjectAppOrganizer.Core.Models;

namespace ProjectAppOrganizer.Core.Tests;

public class ModelsTest
{
    [Fact]
    public void NewProject_HasIdAndEmptyCollections()
    {
        var p = new Project { Name = "Demo" };

        Assert.False(string.IsNullOrWhiteSpace(p.Id));
        Assert.Equal("Demo", p.Name);
        Assert.Empty(p.Todos);
        Assert.Empty(p.TerminalProfiles);
    }

    [Fact]
    public void TodoItem_TogglingDone_RaisesPropertyChanged()
    {
        var todo = new TodoItem { Text = "Task" };
        var raised = false;
        todo.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TodoItem.Done)) raised = true; };

        todo.Done = true;

        Assert.True(todo.Done);
        Assert.True(raised);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter ModelsTest`
Expected: FAIL — types `Project`, `TodoItem` do not exist.

- [ ] **Step 3: Write the models**

`Models/ShellType.cs`:

```csharp
namespace ProjectAppOrganizer.Core.Models;

public enum ShellType
{
    GitBash,
    Cmd,
    PowerShell
}
```

`Models/TodoItem.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace ProjectAppOrganizer.Core.Models;

public partial class TodoItem : ObservableObject
{
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private bool _done;
}
```

`Models/TerminalProfile.cs`:

```csharp
namespace ProjectAppOrganizer.Core.Models;

public class TerminalProfile
{
    public string Name { get; set; } = string.Empty;
    public ShellType Shell { get; set; } = ShellType.PowerShell;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string? StartupCommand { get; set; }
}
```

`Models/Project.cs`:

```csharp
using System.Collections.ObjectModel;

namespace ProjectAppOrganizer.Core.Models;

public class Project
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public ObservableCollection<TodoItem> Todos { get; set; } = new();
    public ObservableCollection<TerminalProfile> TerminalProfiles { get; set; } = new();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter ModelsTest`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add domain models (Project, TodoItem, TerminalProfile, ShellType)"
```

---

## Task 2: JSON persistence

**Files:**
- Create: `src/ProjectAppOrganizer.Core/Persistence/IProjectStore.cs`
- Create: `src/ProjectAppOrganizer.Core/Persistence/JsonProjectStore.cs`
- Test: `tests/ProjectAppOrganizer.Core.Tests/JsonProjectStoreTest.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Persistence;

namespace ProjectAppOrganizer.Core.Tests;

public class JsonProjectStoreTest : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"pao-test-{Guid.NewGuid():N}.json");

    [Fact]
    public void Load_WhenFileMissing_ReturnsEmptyList()
    {
        var store = new JsonProjectStore(_path);
        Assert.Empty(store.Load());
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsProjects()
    {
        var store = new JsonProjectStore(_path);
        var project = new Project { Id = "p1", Name = "Demo", Description = "d", FolderPath = @"C:\demo" };
        project.Todos.Add(new TodoItem { Text = "first", Done = true });
        project.TerminalProfiles.Add(new TerminalProfile
        {
            Name = "bash", Shell = ShellType.GitBash, WorkingDirectory = @"C:\demo", StartupCommand = "ls"
        });

        store.Save(new[] { project });
        var loaded = store.Load();

        Assert.Single(loaded);
        Assert.Equal("p1", loaded[0].Id);
        Assert.Equal("Demo", loaded[0].Name);
        Assert.Single(loaded[0].Todos);
        Assert.True(loaded[0].Todos[0].Done);
        Assert.Equal(ShellType.GitBash, loaded[0].TerminalProfiles[0].Shell);
        Assert.Equal("ls", loaded[0].TerminalProfiles[0].StartupCommand);
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter JsonProjectStoreTest`
Expected: FAIL — `IProjectStore` / `JsonProjectStore` not defined.

- [ ] **Step 3: Write the interface and implementation**

`Persistence/IProjectStore.cs`:

```csharp
using ProjectAppOrganizer.Core.Models;

namespace ProjectAppOrganizer.Core.Persistence;

public interface IProjectStore
{
    IList<Project> Load();
    void Save(IEnumerable<Project> projects);
}
```

`Persistence/JsonProjectStore.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectAppOrganizer.Core.Models;

namespace ProjectAppOrganizer.Core.Persistence;

public class JsonProjectStore : IProjectStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonProjectStore(string path) => _path = path;

    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProjectAppOrganizer", "projects.json");

    public IList<Project> Load()
    {
        if (!File.Exists(_path)) return new List<Project>();
        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<List<Project>>(json, Options) ?? new List<Project>();
    }

    public void Save(IEnumerable<Project> projects)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(projects.ToList(), Options);
        File.WriteAllText(_path, json);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter JsonProjectStoreTest`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add JSON-backed project store with round-trip persistence"
```

---

## Task 3: Window primitives & filter

**Files:**
- Create: `src/ProjectAppOrganizer.Core/Windows/PixelRect.cs`
- Create: `src/ProjectAppOrganizer.Core/Windows/WindowInfo.cs`
- Create: `src/ProjectAppOrganizer.Core/Windows/IWindowManager.cs`
- Create: `src/ProjectAppOrganizer.Core/Windows/WindowFilter.cs`
- Test: `tests/ProjectAppOrganizer.Core.Tests/WindowFilterTest.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.Core.Tests;

public class WindowFilterTest
{
    private static WindowInfo Win(nint handle, string title, bool visible = true, bool tool = false)
        => new(handle, title, "proc.exe", visible, tool);

    [Fact]
    public void ShouldList_TrueForNormalVisibleTitledWindow()
        => Assert.True(WindowFilter.ShouldList(Win(10, "Editor"), selfHandle: 99));

    [Fact]
    public void ShouldList_FalseForHiddenTitlelessOrToolWindows()
    {
        Assert.False(WindowFilter.ShouldList(Win(10, "X", visible: false), 99));
        Assert.False(WindowFilter.ShouldList(Win(10, "  "), 99));
        Assert.False(WindowFilter.ShouldList(Win(10, "X", tool: true), 99));
    }

    [Fact]
    public void ShouldList_FalseForOwnWindow()
        => Assert.False(WindowFilter.ShouldList(Win(99, "Organizer"), selfHandle: 99));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter WindowFilterTest`
Expected: FAIL — types not defined.

- [ ] **Step 3: Write the primitives, interface, and filter**

`Windows/PixelRect.cs`:

```csharp
namespace ProjectAppOrganizer.Core.Windows;

public readonly record struct PixelRect(int X, int Y, int Width, int Height);
```

`Windows/WindowInfo.cs`:

```csharp
namespace ProjectAppOrganizer.Core.Windows;

public record WindowInfo(
    nint Handle,
    string Title,
    string ProcessName,
    bool IsVisible,
    bool IsToolWindow);
```

`Windows/IWindowManager.cs`:

```csharp
namespace ProjectAppOrganizer.Core.Windows;

public interface IWindowManager
{
    IReadOnlyList<WindowInfo> EnumerateWindows();
    bool IsWindow(nint handle);
    void MoveResize(nint handle, PixelRect rect);
    void Minimize(nint handle);
    void Restore(nint handle);
    void BringToFront(nint handle);
}
```

`Windows/WindowFilter.cs`:

```csharp
namespace ProjectAppOrganizer.Core.Windows;

public static class WindowFilter
{
    public static bool ShouldList(WindowInfo w, nint selfHandle)
    {
        if (w.Handle == selfHandle) return false;
        if (!w.IsVisible) return false;
        if (w.IsToolWindow) return false;
        if (string.IsNullOrWhiteSpace(w.Title)) return false;
        return true;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter WindowFilterTest`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add window primitives, IWindowManager, and window filter"
```

---

## Task 4: Fake window manager (test double)

**Files:**
- Create: `tests/ProjectAppOrganizer.Core.Tests/Fakes/FakeWindowManager.cs`
- Test: `tests/ProjectAppOrganizer.Core.Tests/FakeWindowManagerTest.cs`

- [ ] **Step 1: Write the fake**

`Fakes/FakeWindowManager.cs`:

```csharp
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.Core.Tests.Fakes;

public class FakeWindowManager : IWindowManager
{
    public List<WindowInfo> Windows { get; } = new();
    public List<string> Calls { get; } = new();
    public HashSet<nint> Existing { get; } = new();

    public IReadOnlyList<WindowInfo> EnumerateWindows() => Windows;

    public bool IsWindow(nint handle) => Existing.Contains(handle) || Windows.Any(w => w.Handle == handle);

    public void MoveResize(nint handle, PixelRect rect)
        => Calls.Add($"MoveResize:{handle}:{rect.X},{rect.Y},{rect.Width},{rect.Height}");

    public void Minimize(nint handle) => Calls.Add($"Minimize:{handle}");
    public void Restore(nint handle) => Calls.Add($"Restore:{handle}");
    public void BringToFront(nint handle) => Calls.Add($"BringToFront:{handle}");
}
```

- [ ] **Step 2: Write a test that exercises the fake**

`FakeWindowManagerTest.cs`:

```csharp
using ProjectAppOrganizer.Core.Tests.Fakes;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.Core.Tests;

public class FakeWindowManagerTest
{
    [Fact]
    public void RecordsCallsInOrder()
    {
        var wm = new FakeWindowManager();
        wm.Restore(5);
        wm.MoveResize(5, new PixelRect(1, 2, 3, 4));
        wm.BringToFront(5);

        Assert.Equal(new[] { "Restore:5", "MoveResize:5:1,2,3,4", "BringToFront:5" }, wm.Calls);
    }
}
```

- [ ] **Step 3: Run test to verify it passes**

Run: `dotnet test --filter FakeWindowManagerTest`
Expected: PASS (1 test).

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "test: add FakeWindowManager test double"
```

---

## Task 5: Stage manager

**Files:**
- Create: `src/ProjectAppOrganizer.Core/Windows/StageManager.cs`
- Test: `tests/ProjectAppOrganizer.Core.Tests/StageManagerTest.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using ProjectAppOrganizer.Core.Tests.Fakes;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.Core.Tests;

public class StageManagerTest
{
    private static readonly PixelRect Stage = new(100, 50, 800, 600);

    [Fact]
    public void Stage_RestoresMovesAndFocusesWindow()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);

        sm.Stage(7, Stage);

        Assert.Equal(
            new[] { "Restore:7", "MoveResize:7:100,50,800,600", "BringToFront:7" },
            wm.Calls);
        Assert.Equal((nint)7, sm.Current);
    }

    [Fact]
    public void Stage_MinimizesPreviouslyStagedWindow()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);

        sm.Stage(7, Stage);
        wm.Calls.Clear();
        sm.Stage(8, Stage);

        Assert.Equal("Minimize:7", wm.Calls[0]);
        Assert.Contains("MoveResize:8:100,50,800,600", wm.Calls);
        Assert.Equal((nint)8, sm.Current);
    }

    [Fact]
    public void Stage_SameWindowTwice_DoesNotMinimizeIt()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);

        sm.Stage(7, Stage);
        wm.Calls.Clear();
        sm.Stage(7, Stage);

        Assert.DoesNotContain("Minimize:7", wm.Calls);
    }

    [Fact]
    public void Reapply_RepositionsCurrentWindowOnly()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);
        sm.Stage(7, Stage);
        wm.Calls.Clear();

        sm.Reapply(new PixelRect(0, 0, 1000, 700));

        Assert.Equal(new[] { "MoveResize:7:0,0,1000,700" }, wm.Calls);
    }

    [Fact]
    public void Reapply_WithNothingStaged_DoesNothing()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);

        sm.Reapply(Stage);

        Assert.Empty(wm.Calls);
    }

    [Fact]
    public void MinimizeCurrent_MinimizesAndKeepsCurrentForRestore()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);
        sm.Stage(7, Stage);
        wm.Calls.Clear();

        sm.MinimizeCurrent();

        Assert.Equal(new[] { "Minimize:7" }, wm.Calls);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter StageManagerTest`
Expected: FAIL — `StageManager` not defined.

- [ ] **Step 3: Write the StageManager**

`Windows/StageManager.cs`:

```csharp
namespace ProjectAppOrganizer.Core.Windows;

/// <summary>
/// Drives the Docker/stage model: positions the active window into the stage
/// rectangle, minimizes the previously staged window, and re-aligns the active
/// window when the stage moves/resizes.
/// </summary>
public class StageManager
{
    private readonly IWindowManager _wm;

    public StageManager(IWindowManager wm) => _wm = wm;

    public nint? Current { get; private set; }

    public void Stage(nint handle, PixelRect stage)
    {
        if (Current is { } prev && prev != handle)
            _wm.Minimize(prev);

        _wm.Restore(handle);
        _wm.MoveResize(handle, stage);
        _wm.BringToFront(handle);
        Current = handle;
    }

    public void Reapply(PixelRect stage)
    {
        if (Current is { } h)
            _wm.MoveResize(h, stage);
    }

    public void MinimizeCurrent()
    {
        if (Current is { } h)
            _wm.Minimize(h);
    }

    public void Clear() => Current = null;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter StageManagerTest`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add StageManager snap/minimize/follow logic"
```

---

## Task 6: Shell command builder

**Files:**
- Create: `src/ProjectAppOrganizer.Core/Terminals/ShellCommand.cs`
- Create: `src/ProjectAppOrganizer.Core/Terminals/ITerminalLauncher.cs`
- Test: `tests/ProjectAppOrganizer.Core.Tests/ShellCommandTest.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Terminals;

namespace ProjectAppOrganizer.Core.Tests;

public class ShellCommandTest
{
    [Fact]
    public void Cmd_WithStartupCommand_UsesSlashK()
    {
        var p = new TerminalProfile { Shell = ShellType.Cmd, WorkingDirectory = @"C:\x", StartupCommand = "dir" };
        var spec = ShellCommand.Build(p, gitBashPath: @"C:\Git\git-bash.exe");

        Assert.Equal("cmd.exe", spec.FileName);
        Assert.Equal(@"C:\x", spec.WorkingDirectory);
        Assert.Equal("/K \"dir\"", spec.Arguments);
    }

    [Fact]
    public void Cmd_WithoutStartupCommand_HasNoArguments()
    {
        var p = new TerminalProfile { Shell = ShellType.Cmd, WorkingDirectory = @"C:\x" };
        var spec = ShellCommand.Build(p, @"C:\Git\git-bash.exe");
        Assert.Equal("", spec.Arguments);
    }

    [Fact]
    public void PowerShell_WithStartupCommand_UsesNoExitCommand()
    {
        var p = new TerminalProfile { Shell = ShellType.PowerShell, WorkingDirectory = @"C:\x", StartupCommand = "ls" };
        var spec = ShellCommand.Build(p, @"C:\Git\git-bash.exe");

        Assert.Equal("powershell.exe", spec.FileName);
        Assert.Equal("-NoExit -Command \"ls\"", spec.Arguments);
    }

    [Fact]
    public void GitBash_UsesGitBashPathAndCdArgument()
    {
        var p = new TerminalProfile { Shell = ShellType.GitBash, WorkingDirectory = @"C:\repo" };
        var spec = ShellCommand.Build(p, @"C:\Git\git-bash.exe");

        Assert.Equal(@"C:\Git\git-bash.exe", spec.FileName);
        Assert.Equal("--cd=\"C:\\repo\"", spec.Arguments);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter ShellCommandTest`
Expected: FAIL — `ShellCommand` not defined.

- [ ] **Step 3: Write the launcher abstraction and builder**

`Terminals/ITerminalLauncher.cs`:

```csharp
using ProjectAppOrganizer.Core.Models;

namespace ProjectAppOrganizer.Core.Terminals;

/// <summary>Launches a terminal and returns its main window handle (0 if it could not be resolved).</summary>
public interface ITerminalLauncher
{
    nint Launch(TerminalProfile profile);
}
```

`Terminals/ShellCommand.cs`:

```csharp
using ProjectAppOrganizer.Core.Models;

namespace ProjectAppOrganizer.Core.Terminals;

/// <summary>A resolved, OS-ready launch specification for a terminal profile.</summary>
public readonly record struct ShellSpec(string FileName, string Arguments, string WorkingDirectory);

public static class ShellCommand
{
    public static ShellSpec Build(TerminalProfile p, string gitBashPath)
    {
        var cmd = p.StartupCommand;
        return p.Shell switch
        {
            ShellType.Cmd => new ShellSpec(
                "cmd.exe",
                string.IsNullOrWhiteSpace(cmd) ? "" : $"/K \"{cmd}\"",
                p.WorkingDirectory),

            ShellType.PowerShell => new ShellSpec(
                "powershell.exe",
                string.IsNullOrWhiteSpace(cmd) ? "-NoExit" : $"-NoExit -Command \"{cmd}\"",
                p.WorkingDirectory),

            ShellType.GitBash => new ShellSpec(
                gitBashPath,
                $"--cd=\"{p.WorkingDirectory}\"",
                p.WorkingDirectory),

            _ => throw new ArgumentOutOfRangeException(nameof(p.Shell))
        };
    }
}
```

> Note: the PowerShell-without-command case returns `-NoExit` (keeps the window open). The test only asserts the with-command case; the bare `-NoExit` is intentional.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter ShellCommandTest`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add shell command builder and terminal launcher interface"
```

---

## Task 7: Window tab model

**Files:**
- Create: `src/ProjectAppOrganizer.Core/ViewModels/TabKind.cs`
- Create: `src/ProjectAppOrganizer.Core/ViewModels/WindowTab.cs`
- Test: `tests/ProjectAppOrganizer.Core.Tests/WindowTabTest.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using ProjectAppOrganizer.Core.ViewModels;

namespace ProjectAppOrganizer.Core.Tests;

public class WindowTabTest
{
    [Fact]
    public void Terminal_TabIsOwnedTrue()
    {
        var tab = new WindowTab(handle: 12, title: "git bash", TabKind.Terminal);
        Assert.Equal(TabKind.Terminal, tab.Kind);
        Assert.True(tab.IsTerminal);
        Assert.Equal((nint)12, tab.Handle);
    }

    [Fact]
    public void Attached_TabIsTerminalFalse()
    {
        var tab = new WindowTab(handle: 13, title: "VS Code", TabKind.Attached);
        Assert.False(tab.IsTerminal);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter WindowTabTest`
Expected: FAIL — types not defined.

- [ ] **Step 3: Write the tab model**

`ViewModels/TabKind.cs`:

```csharp
namespace ProjectAppOrganizer.Core.ViewModels;

public enum TabKind
{
    Terminal,
    Attached
}
```

`ViewModels/WindowTab.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace ProjectAppOrganizer.Core.ViewModels;

public partial class WindowTab : ObservableObject
{
    [ObservableProperty] private string _title;

    public WindowTab(nint handle, string title, TabKind kind)
    {
        Handle = handle;
        _title = title;
        Kind = kind;
    }

    public nint Handle { get; }
    public TabKind Kind { get; }
    public bool IsTerminal => Kind == TabKind.Terminal;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter WindowTabTest`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add WindowTab model"
```

---

## Task 8: ProjectViewModel (tabs + todos + close behavior)

**Files:**
- Create: `src/ProjectAppOrganizer.Core/ViewModels/ProjectViewModel.cs`
- Test: `tests/ProjectAppOrganizer.Core.Tests/ProjectViewModelTest.cs`

This view-model owns one project's tabs and todo operations, and drives the
StageManager. It depends on `IWindowManager`, `ITerminalLauncher`, and a callback
that supplies the current stage rectangle (the View provides real screen coords).

- [ ] **Step 1: Write the failing test**

```csharp
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
```

This test requires a `Close` capability on `IWindowManager`. Add it now.

- [ ] **Step 2: Add `Close` to IWindowManager and the fake**

Modify `src/ProjectAppOrganizer.Core/Windows/IWindowManager.cs` — add to the interface:

```csharp
    void Close(nint handle);
```

Modify `tests/ProjectAppOrganizer.Core.Tests/Fakes/FakeWindowManager.cs` — add:

```csharp
    public void Close(nint handle) => Calls.Add($"Close:{handle}");
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test --filter ProjectViewModelTest`
Expected: FAIL — `ProjectViewModel` not defined.

- [ ] **Step 4: Write the ProjectViewModel**

`ViewModels/ProjectViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Terminals;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.Core.ViewModels;

public partial class ProjectViewModel : ObservableObject
{
    private readonly IWindowManager _wm;
    private readonly ITerminalLauncher _launcher;
    private readonly StageManager _stage;
    private readonly Func<PixelRect> _stageRect;

    [ObservableProperty] private WindowTab? _activeTab;

    public ProjectViewModel(
        Project project,
        IWindowManager wm,
        ITerminalLauncher launcher,
        StageManager stage,
        Func<PixelRect> stageRect)
    {
        Project = project;
        _wm = wm;
        _launcher = launcher;
        _stage = stage;
        _stageRect = stageRect;
    }

    public Project Project { get; }
    public ObservableCollection<WindowTab> Tabs { get; } = new();

    public void OpenTerminal(TerminalProfile profile)
    {
        var handle = _launcher.Launch(profile);
        if (handle == 0) return;
        var tab = new WindowTab(handle, profile.Name, TabKind.Terminal);
        Tabs.Add(tab);
        Activate(tab);
    }

    public void AttachWindow(WindowInfo window)
    {
        if (Tabs.Any(t => t.Handle == window.Handle)) return;
        var tab = new WindowTab(window.Handle, window.Title, TabKind.Attached);
        Tabs.Add(tab);
        Activate(tab);
    }

    public void Activate(WindowTab tab)
    {
        ActiveTab = tab;
        _stage.Stage(tab.Handle, _stageRect());
    }

    public void CloseTab(WindowTab tab)
    {
        Tabs.Remove(tab);

        if (tab.IsTerminal)
        {
            _wm.Close(tab.Handle);
        }
        else
        {
            // Detach only: hand the window back to the user, floating.
            _wm.Restore(tab.Handle);
        }

        if (ReferenceEquals(ActiveTab, tab))
        {
            _stage.Clear();
            ActiveTab = Tabs.LastOrDefault();
            if (ActiveTab is { } next) Activate(next);
        }
    }

    public void AddTodo(string text) => Project.Todos.Add(new TodoItem { Text = text });
    public void RemoveTodo(TodoItem item) => Project.Todos.Remove(item);
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter ProjectViewModelTest`
Expected: PASS (6 tests).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add ProjectViewModel with tabs, staging, smart close, and todos"
```

---

## Task 9: MainViewModel (project list + persistence)

**Files:**
- Create: `src/ProjectAppOrganizer.Core/ViewModels/MainViewModel.cs`
- Test: `tests/ProjectAppOrganizer.Core.Tests/MainViewModelTest.cs`

`MainViewModel` owns the project collection, selection, add/remove, and save. It
builds a `ProjectViewModel` per project via an injected factory (so the View can
supply the real stage-rect callback). For tests we inject a simple factory.

- [ ] **Step 1: Write the failing test**

```csharp
using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Persistence;
using ProjectAppOrganizer.Core.Tests.Fakes;
using ProjectAppOrganizer.Core.Terminals;
using ProjectAppOrganizer.Core.ViewModels;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.Core.Tests;

public class MainViewModelTest
{
    private sealed class InMemoryStore : IProjectStore
    {
        public List<Project> Saved = new();
        public IList<Project> Seed = new List<Project>();
        public IList<Project> Load() => Seed;
        public void Save(IEnumerable<Project> projects) => Saved = projects.ToList();
    }

    private sealed class NullLauncher : ITerminalLauncher
    {
        public nint Launch(TerminalProfile profile) => 1;
    }

    private static MainViewModel Make(InMemoryStore store)
    {
        var wm = new FakeWindowManager();
        return new MainViewModel(
            store,
            project => new ProjectViewModel(
                project, wm, new NullLauncher(), new StageManager(wm),
                () => new PixelRect(0, 0, 10, 10)));
    }

    [Fact]
    public void Load_PopulatesProjectsFromStore()
    {
        var store = new InMemoryStore { Seed = new List<Project> { new() { Name = "A" }, new() { Name = "B" } } };
        var vm = Make(store);

        vm.Load();

        Assert.Equal(2, vm.Projects.Count);
        Assert.Equal("A", vm.SelectedProject!.Project.Name);
    }

    [Fact]
    public void AddProject_AddsSelectsAndIsAvailableImmediately()
    {
        var vm = Make(new InMemoryStore());

        vm.AddProject("New");

        Assert.Single(vm.Projects);
        Assert.Equal("New", vm.SelectedProject!.Project.Name);
    }

    [Fact]
    public void RemoveProject_RemovesAndReselects()
    {
        var vm = Make(new InMemoryStore());
        vm.AddProject("A");
        vm.AddProject("B");
        var a = vm.Projects[0];

        vm.RemoveProject(a);

        Assert.Single(vm.Projects);
        Assert.Equal("B", vm.SelectedProject!.Project.Name);
    }

    [Fact]
    public void Save_WritesUnderlyingProjectsToStore()
    {
        var store = new InMemoryStore();
        var vm = Make(store);
        vm.AddProject("A");

        vm.Save();

        Assert.Single(store.Saved);
        Assert.Equal("A", store.Saved[0].Name);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter MainViewModelTest`
Expected: FAIL — `MainViewModel` not defined.

- [ ] **Step 3: Write the MainViewModel**

`ViewModels/MainViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Persistence;

namespace ProjectAppOrganizer.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IProjectStore _store;
    private readonly Func<Project, ProjectViewModel> _factory;

    [ObservableProperty] private ProjectViewModel? _selectedProject;

    public MainViewModel(IProjectStore store, Func<Project, ProjectViewModel> factory)
    {
        _store = store;
        _factory = factory;
    }

    public ObservableCollection<ProjectViewModel> Projects { get; } = new();

    public void Load()
    {
        Projects.Clear();
        foreach (var p in _store.Load())
            Projects.Add(_factory(p));
        SelectedProject = Projects.FirstOrDefault();
    }

    public void AddProject(string name)
    {
        var vm = _factory(new Project { Name = name });
        Projects.Add(vm);
        SelectedProject = vm;
    }

    public void RemoveProject(ProjectViewModel vm)
    {
        var index = Projects.IndexOf(vm);
        Projects.Remove(vm);
        if (ReferenceEquals(SelectedProject, vm))
            SelectedProject = Projects.ElementAtOrDefault(Math.Max(0, index - 1)) ?? Projects.FirstOrDefault();
    }

    public void Save() => _store.Save(Projects.Select(p => p.Project));
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter MainViewModelTest`
Expected: PASS (4 tests).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test`
Expected: all tests green.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add MainViewModel with project list, selection, and persistence"
```

---

## Task 10: Win32 window manager (concrete interop)

**Files:**
- Create: `src/ProjectAppOrganizer.App/Windows/Win32Interop.cs`
- Create: `src/ProjectAppOrganizer.App/Windows/Win32WindowManager.cs`

This is the real `IWindowManager`. Verified manually (Task 14), not unit-tested.

- [ ] **Step 1: Write the P/Invoke declarations**

`Windows/Win32Interop.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Text;

namespace ProjectAppOrganizer.App.Windows;

internal static class Win32Interop
{
    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(nint hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_TOOLWINDOW = 0x00000080;

    public const int SW_MINIMIZE = 6;
    public const int SW_RESTORE = 9;

    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_NOZORDER = 0x0004;
    public static readonly nint HWND_TOP = 0;

    public const uint WM_CLOSE = 0x0010;
}
```

- [ ] **Step 2: Write the Win32WindowManager**

`Windows/Win32WindowManager.cs`:

```csharp
using System.Text;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.App.Windows;

public class Win32WindowManager : IWindowManager
{
    public nint SelfHandle { get; set; }

    public IReadOnlyList<WindowInfo> EnumerateWindows()
    {
        var result = new List<WindowInfo>();

        Win32Interop.EnumWindows((hWnd, _) =>
        {
            var len = Win32Interop.GetWindowTextLength(hWnd);
            var sb = new StringBuilder(len + 1);
            Win32Interop.GetWindowText(hWnd, sb, sb.Capacity);
            var title = sb.ToString();

            var exStyle = Win32Interop.GetWindowLong(hWnd, Win32Interop.GWL_EXSTYLE);
            var isTool = ((long)(uint)exStyle & Win32Interop.WS_EX_TOOLWINDOW) != 0;

            Win32Interop.GetWindowThreadProcessId(hWnd, out var pid);
            var procName = SafeProcessName(pid);

            var info = new WindowInfo(hWnd, title, procName, Win32Interop.IsWindowVisible(hWnd), isTool);
            if (WindowFilter.ShouldList(info, SelfHandle))
                result.Add(info);
            return true;
        }, 0);

        return result;
    }

    public bool IsWindow(nint handle) => Win32Interop.IsWindow(handle);

    public void MoveResize(nint handle, PixelRect rect) =>
        Win32Interop.SetWindowPos(handle, Win32Interop.HWND_TOP,
            rect.X, rect.Y, rect.Width, rect.Height, Win32Interop.SWP_SHOWWINDOW);

    public void Minimize(nint handle) => Win32Interop.ShowWindow(handle, Win32Interop.SW_MINIMIZE);

    public void Restore(nint handle) => Win32Interop.ShowWindow(handle, Win32Interop.SW_RESTORE);

    public void BringToFront(nint handle) => Win32Interop.SetForegroundWindow(handle);

    public void Close(nint handle) =>
        Win32Interop.PostMessage(handle, Win32Interop.WM_CLOSE, 0, 0);

    private static string SafeProcessName(uint pid)
    {
        try { return System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; }
        catch { return string.Empty; }
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add Win32 window manager (P/Invoke implementation)"
```

---

## Task 11: Process terminal launcher (concrete)

**Files:**
- Create: `src/ProjectAppOrganizer.App/Terminals/ProcessTerminalLauncher.cs`

Spawns the shell and resolves its main window handle by polling the process.

- [ ] **Step 1: Write the launcher**

`Terminals/ProcessTerminalLauncher.cs`:

```csharp
using System.Diagnostics;
using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Terminals;

namespace ProjectAppOrganizer.App.Terminals;

public class ProcessTerminalLauncher : ITerminalLauncher
{
    private readonly string _gitBashPath;

    public ProcessTerminalLauncher(string? gitBashPath = null)
        => _gitBashPath = gitBashPath ?? DetectGitBash();

    public nint Launch(TerminalProfile profile)
    {
        var spec = ShellCommand.Build(profile, _gitBashPath);
        var psi = new ProcessStartInfo
        {
            FileName = spec.FileName,
            Arguments = spec.Arguments,
            WorkingDirectory =
                Directory.Exists(spec.WorkingDirectory) ? spec.WorkingDirectory : Environment.CurrentDirectory,
            UseShellExecute = true
        };

        var process = Process.Start(psi);
        if (process is null) return 0;

        // Poll up to ~3s for the main window handle to appear.
        for (var i = 0; i < 30; i++)
        {
            process.Refresh();
            if (process.MainWindowHandle != 0) return process.MainWindowHandle;
            Thread.Sleep(100);
        }
        return process.MainWindowHandle;
    }

    private static string DetectGitBash()
    {
        string[] candidates =
        {
            @"C:\Program Files\Git\git-bash.exe",
            @"C:\Program Files (x86)\Git\git-bash.exe"
        };
        return candidates.FirstOrDefault(File.Exists) ?? "git-bash.exe";
    }
}
```

> Note: git bash (mintty) often reports `MainWindowHandle == 0` because the visible
> window belongs to a child `mintty` process. If staging git bash proves unreliable
> in manual testing (Task 14), fall back to enumerating the new `mintty` window by
> title via `IWindowManager.EnumerateWindows()` after launch. Capture that as a
> follow-up rather than blocking v1.

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: add process-based terminal launcher with window-handle resolution"
```

---

## Task 12: WPF shell — App, DI, MainWindow layout

**Files:**
- Modify: `src/ProjectAppOrganizer.App/App.xaml`
- Modify: `src/ProjectAppOrganizer.App/App.xaml.cs`
- Modify: `src/ProjectAppOrganizer.App/MainWindow.xaml`
- Modify: `src/ProjectAppOrganizer.App/MainWindow.xaml.cs`

Implements the split-sidebar layout and wires DI. The View supplies the real
stage rectangle (screen pixels) computed from the stage `Border` element.

- [ ] **Step 1: Configure DI in App.xaml.cs**

Replace `App.xaml.cs` with:

```csharp
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ProjectAppOrganizer.App.Terminals;
using ProjectAppOrganizer.App.Windows;
using ProjectAppOrganizer.Core.Persistence;
using ProjectAppOrganizer.Core.Terminals;
using ProjectAppOrganizer.Core.ViewModels;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sc = new ServiceCollection();
        sc.AddSingleton<Win32WindowManager>();
        sc.AddSingleton<IWindowManager>(sp => sp.GetRequiredService<Win32WindowManager>());
        sc.AddSingleton<ITerminalLauncher>(_ => new ProcessTerminalLauncher());
        sc.AddSingleton<IProjectStore>(_ => new JsonProjectStore(JsonProjectStore.DefaultPath));
        sc.AddSingleton<MainWindow>();
        Services = sc.BuildServiceProvider();

        var window = Services.GetRequiredService<MainWindow>();
        window.Show();
    }
}
```

Ensure `App.xaml` has no `StartupUri` (DI shows the window):

```xml
<Application x:Class="ProjectAppOrganizer.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources/>
</Application>
```

- [ ] **Step 2: Write the MainWindow layout (split sidebar)**

Replace `MainWindow.xaml` with:

```xml
<Window x:Class="ProjectAppOrganizer.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Project App Organizer" Height="800" Width="1280"
        WindowState="Maximized" Background="#161A21">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="180"/>   <!-- project rail -->
            <ColumnDefinition Width="240"/>   <!-- detail panel -->
            <ColumnDefinition Width="*"/>     <!-- main area -->
        </Grid.ColumnDefinitions>

        <!-- Project rail -->
        <DockPanel Grid.Column="0" Background="#1B212B">
            <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="6">
                <Button Content="+ Project" Click="AddProject_Click" Padding="6,2"/>
            </StackPanel>
            <ListBox x:Name="ProjectList" Background="Transparent" BorderThickness="0"
                     Foreground="#C5D0E0"
                     ItemsSource="{Binding Projects}"
                     SelectedItem="{Binding SelectedProject, Mode=TwoWay}"
                     DisplayMemberPath="Project.Name"/>
        </DockPanel>

        <!-- Detail panel -->
        <StackPanel Grid.Column="1" Background="#222A36" Margin="0"
                    DataContext="{Binding SelectedProject}">
            <TextBox Text="{Binding Project.Name, UpdateSourceTrigger=PropertyChanged}"
                     FontSize="16" Margin="8" Background="Transparent" Foreground="White" BorderThickness="0"/>
            <TextBox Text="{Binding Project.Description, UpdateSourceTrigger=PropertyChanged}"
                     AcceptsReturn="True" Height="60" Margin="8,0" TextWrapping="Wrap"
                     Background="#2A3140" Foreground="#A9B4C6" BorderThickness="0"/>
            <TextBlock Text="TODO" Foreground="#6B7688" Margin="8,10,8,2" FontSize="11"/>
            <ItemsControl ItemsSource="{Binding Project.Todos}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <DockPanel Margin="8,2">
                            <CheckBox IsChecked="{Binding Done}" DockPanel.Dock="Left"/>
                            <TextBox Text="{Binding Text, UpdateSourceTrigger=PropertyChanged}"
                                     Background="Transparent" Foreground="#A9B4C6" BorderThickness="0"/>
                        </DockPanel>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
            <Button Content="+ Todo" Click="AddTodo_Click" Margin="8" HorizontalAlignment="Left" Padding="6,2"/>
        </StackPanel>

        <!-- Main area: tab bar + stage -->
        <DockPanel Grid.Column="2" DataContext="{Binding SelectedProject}">
            <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Background="#1B212B">
                <ItemsControl ItemsSource="{Binding Tabs}">
                    <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate><StackPanel Orientation="Horizontal"/></ItemsPanelTemplate>
                    </ItemsControl.ItemsPanel>
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Border Background="#2A3140" Margin="2" CornerRadius="3,3,0,0">
                                <StackPanel Orientation="Horizontal">
                                    <Button Content="{Binding Title}" Click="ActivateTab_Click"
                                            Tag="{Binding}" Background="Transparent" Foreground="#AEB9CB"
                                            BorderThickness="0" Padding="8,4"/>
                                    <Button Content="✕" Click="CloseTab_Click" Tag="{Binding}"
                                            Background="Transparent" Foreground="#7F8A9C" BorderThickness="0" Padding="4"/>
                                </StackPanel>
                            </Border>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
                <Button Content="+ Terminal" Click="OpenTerminal_Click" Margin="4" Padding="6,2"/>
                <Button Content="Attach Window" Click="AttachWindow_Click" Margin="4" Padding="6,2"/>
            </StackPanel>

            <!-- Stage placeholder: the real OS window is positioned over this rect -->
            <Border x:Name="Stage" Background="#1A1F28"/>
        </DockPanel>
    </Grid>
</Window>
```

- [ ] **Step 3: Write the MainWindow code-behind (stage rect + event handlers)**

Replace `MainWindow.xaml.cs` with:

```csharp
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Persistence;
using ProjectAppOrganizer.Core.Terminals;
using ProjectAppOrganizer.Core.ViewModels;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly Win32WindowManager _wm;

    public MainWindow(Win32WindowManager wm, ITerminalLauncher launcher, IProjectStore store)
    {
        InitializeComponent();
        _wm = wm;

        _vm = new MainViewModel(
            store,
            project => new ProjectViewModel(project, wm, launcher, new StageManager(wm), GetStageRect));
        _vm.Load();
        DataContext = _vm;

        Loaded += (_, _) => _wm.SelfHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        LocationChanged += (_, _) => ReapplyStage();
        SizeChanged += (_, _) => ReapplyStage();
        StateChanged += OnStateChanged;
        Closing += (_, _) => _vm.Save();
    }

    /// <summary>Stage rect in physical screen pixels (DPI-aware).</summary>
    private PixelRect GetStageRect()
    {
        var topLeft = Stage.PointToScreen(new Point(0, 0));
        var source = PresentationSource.FromVisual(this);
        var m = source!.CompositionTarget!.TransformToDevice;
        var w = Stage.ActualWidth * m.M11;
        var h = Stage.ActualHeight * m.M22;
        return new PixelRect((int)topLeft.X, (int)topLeft.Y, (int)w, (int)h);
    }

    private void ReapplyStage() => _vm.SelectedProject?.ReapplyStage(GetStageRect());

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            _vm.SelectedProject?.MinimizeStage();
        else
            ReapplyStage();
    }

    private void AddProject_Click(object sender, RoutedEventArgs e) => _vm.AddProject("New Project");
    private void AddTodo_Click(object sender, RoutedEventArgs e) => _vm.SelectedProject?.AddTodo("New todo");

    private void OpenTerminal_Click(object sender, RoutedEventArgs e)
    {
        var project = _vm.SelectedProject;
        if (project is null) return;
        // Minimal ad-hoc: PowerShell in the project folder. Profile UI is a later iteration.
        project.OpenTerminal(new TerminalProfile
        {
            Name = "pwsh",
            Shell = ShellType.PowerShell,
            WorkingDirectory = project.Project.FolderPath
        });
    }

    private void AttachWindow_Click(object sender, RoutedEventArgs e)
    {
        var project = _vm.SelectedProject;
        if (project is null) return;
        var picker = new Views.PickerWindow(_wm.EnumerateWindows()) { Owner = this };
        if (picker.ShowDialog() == true && picker.Selected is { } w)
            project.AttachWindow(w);
    }

    private void ActivateTab_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is WindowTab tab)
            _vm.SelectedProject?.Activate(tab);
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is WindowTab tab)
            _vm.SelectedProject?.CloseTab(tab);
    }
}
```

- [ ] **Step 4: Add the two helper methods to ProjectViewModel**

Modify `src/ProjectAppOrganizer.Core/ViewModels/ProjectViewModel.cs` — add these methods to the class:

```csharp
    public void ReapplyStage(PixelRect rect) => _stage.Reapply(rect);
    public void MinimizeStage() => _stage.MinimizeCurrent();
```

- [ ] **Step 5: Build**

Run: `dotnet build`
Expected: build succeeds (PickerWindow is added in Task 13 — if building before Task 13, comment out the `AttachWindow_Click` body temporarily, or implement Task 13 first).

> Execution order note: implement Task 13 (PickerWindow) before building Task 12, or build after both. They are co-dependent at the View layer.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: WPF shell with split-sidebar layout, DI, and stage wiring"
```

---

## Task 13: Window picker dialog

**Files:**
- Create: `src/ProjectAppOrganizer.App/Views/PickerWindow.xaml`
- Create: `src/ProjectAppOrganizer.App/Views/PickerWindow.xaml.cs`

- [ ] **Step 1: Write the picker XAML**

`Views/PickerWindow.xaml`:

```xml
<Window x:Class="ProjectAppOrganizer.App.Views.PickerWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Attach a window" Height="420" Width="420"
        WindowStartupLocation="CenterOwner" Background="#1B212B">
    <DockPanel Margin="8">
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,8,0,0">
            <Button Content="Attach" Click="Ok_Click" Padding="12,4" Margin="4,0"/>
            <Button Content="Cancel" Click="Cancel_Click" Padding="12,4"/>
        </StackPanel>
        <ListBox x:Name="List" DisplayMemberPath="Title"
                 Background="#222A36" Foreground="#C5D0E0" BorderThickness="0"
                 MouseDoubleClick="Ok_Click"/>
    </DockPanel>
</Window>
```

- [ ] **Step 2: Write the picker code-behind**

`Views/PickerWindow.xaml.cs`:

```csharp
using System.Collections.Generic;
using System.Windows;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.App.Views;

public partial class PickerWindow : Window
{
    public WindowInfo? Selected { get; private set; }

    public PickerWindow(IReadOnlyList<WindowInfo> windows)
    {
        InitializeComponent();
        List.ItemsSource = windows;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Selected = List.SelectedItem as WindowInfo;
        if (Selected is null) return;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: build succeeds (both Task 12 and 13 present).

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add window picker dialog"
```

---

## Task 14: Manual end-to-end verification

**Files:** none (manual run).

- [ ] **Step 1: Run the app**

Run: `dotnet run --project src/ProjectAppOrganizer.App`
Expected: maximized window with project rail, detail panel, empty tab bar + stage.

- [ ] **Step 2: Create a project**

Click **+ Project**, rename it in the detail panel, set a description, add a couple of todos and check one. Confirm edits stick.

- [ ] **Step 3: Open a terminal**

Click **+ Terminal**. Expected: a PowerShell window opens and snaps into the stage; a tab appears.

- [ ] **Step 4: Attach a window**

Open VS Code or Explorer, click **Attach Window**, pick it from the list. Expected: it snaps into the stage; a tab appears; the previous staged window minimizes.

- [ ] **Step 5: Switch tabs**

Click between tabs. Expected: the selected window fills the stage, the previous one minimizes.

- [ ] **Step 6: Close tabs (smart behavior)**

Close the terminal tab → the PowerShell process closes.
Close the attached tab → the window detaches and floats free (NOT closed).

- [ ] **Step 7: Move/resize the app**

Un-maximize and drag/resize the window. Expected: the staged window follows the stage rectangle. Minimize the app → staged window minimizes too.

- [ ] **Step 8: Persistence**

Close the app, reopen it. Expected: projects, descriptions, and todos are restored (terminals/attachments are not — by design).

- [ ] **Step 9: Commit any fixes, then final commit**

```bash
git add -A
git commit -m "chore: manual verification fixes for v1"
```

---

## Self-Review Notes

- **Spec coverage:** Docker/stage (Task 5,10,12) · split-sidebar layout (Task 12) · project title/desc/todo (Task 1,8,12) · terminals launched from app + profiles model + ad-hoc (Task 6,8,11,12) · attach via picker list (Task 13) · minimize-previous on switch (Task 5,8) · smart close (Task 8) · JSON persistence in %APPDATA% (Task 2,12) · single monitor v1 (Task 12 stage rect). All covered.
- **Deferred to iteration (matches spec "Future"):** per-project terminal-profile editing UI (Task 12 uses a fixed ad-hoc PowerShell for now), git-bash handle fallback (Task 11 note), multi-monitor, drag-reorder. These are explicitly out of v1 scope.
- **Type consistency:** `IWindowManager` gains `Close` in Task 8 and is implemented in Task 10. `ProjectViewModel` ctor signature matches all call sites (Tasks 8, 9, 12). `ReapplyStage`/`MinimizeStage` added in Task 12 Step 4 and used in MainWindow. `GetStageRect` returns `PixelRect` matching `Func<PixelRect>`.
- **Co-dependency flagged:** Tasks 12 and 13 must both exist before the App project builds.
