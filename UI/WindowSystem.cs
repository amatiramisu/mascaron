using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

namespace Mascaron.UI;

public sealed class MascaronWindowSystem : IDisposable
{
    private readonly WindowSystem system;
    private readonly IDalamudPluginInterface pluginInterface;

    public Window MainWindow { get; }

    public MascaronWindowSystem(IDalamudPluginInterface pluginInterface, Window mainWindow, params Window[] windows)
    {
        this.pluginInterface = pluginInterface;
        MainWindow = mainWindow;

        system = new WindowSystem("Mascaron");
        system.AddWindow(mainWindow);
        foreach (var window in windows)
            system.AddWindow(window);

        pluginInterface.UiBuilder.Draw += system.Draw;
        pluginInterface.UiBuilder.OpenMainUi += OnOpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi += OnOpenMainUi;
    }

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= system.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= OnOpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi -= OnOpenMainUi;
        system.RemoveAllWindows();
    }

    private void OnOpenMainUi()
    {
        MainWindow.IsOpen = true;
    }
}
