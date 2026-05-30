using ProjectAppOrganizer.Core.Models;

namespace ProjectAppOrganizer.Core.Terminals;

/// <summary>Launches a terminal and returns its main window handle (0 if it could not be resolved).</summary>
public interface ITerminalLauncher
{
    nint Launch(TerminalProfile profile);
}
