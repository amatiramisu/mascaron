using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace Mascaron.GameBridge;

public unsafe class CharacterResolver
{
    private readonly IObjectTable objectTable;

    public CharacterResolver(IObjectTable objectTable)
    {
        this.objectTable = objectTable;
    }

    public CharacterBase* GetLocalPlayerCharacterBase()
    {
        var localPlayer = objectTable.LocalPlayer;
        if (localPlayer == null)
            return null;

        var gameObject = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)localPlayer.Address;
        if (gameObject == null)
            return null;

        return gameObject->GetCharacterBase();
    }

    public (byte Race, byte Sex, byte Tribe)? GetVisualAppearance()
    {
        var cBase = GetLocalPlayerCharacterBase();
        if (cBase == null || cBase->GetModelType() != CharacterBase.ModelType.Human)
            return null;

        var human = (Human*)cBase;
        var customize = human->Customize;
        return (customize.Race, customize.Sex, customize.Tribe);
    }
}
