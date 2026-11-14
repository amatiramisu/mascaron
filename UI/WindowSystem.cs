using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

namespace Mascaron.UI;

public sealed class MascaronWindowSystem : IDisposable
{
    private readonly WindowSystem system;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly Window mainWindow;

    public MascaronWindowSystem(IDalamudPluginInterface pluginInterface, Window mainWindow, params Window[] windows)
    {
        this.pluginInterface = pluginInterface;
        this.mainWindow = mainWindow;

        system = new WindowSystem("Mascaron");
        system.AddWindow(mainWindow);
        foreach (var window in windows)
            system.AddWindow(window);

        pluginInterface.UiBuilder.Draw += system.Draw;
        pluginInterface.UiBuilder.OpenMainUi += OpenMainWindow;
        pluginInterface.UiBuilder.OpenConfigUi += OpenMainWindow;
    }

    public void OpenMainWindow()
    {
        mainWindow.IsOpen = true;
    }

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= system.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= OpenMainWindow;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenMainWindow;
        system.RemoveAllWindows();
    }
}
