using ArtifactX.GameData;
using System;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels.ShipDesignerViewModels;

/// <summary>What the Ship Designer's view model asks of its page: dialogs, the 3D preview and navigation.</summary>
public interface IShipDesignerHost
{
    Task ShowMessageAsync(string title, string text);

    Task<bool> ConfirmAsync(string title, string text, string action);

    /// <summary>Asks for a design name; null when cancelled.</summary>
    Task<string?> AskDesignNameAsync(string suggested);

    /// <summary>Shows the apply choice; true when the player picked a ship and pressed Apply.</summary>
    Task<bool> ShowApplyDesignAsync(ApplyDesignViewModel choice);

    void ShowPreview(ShipMesh? mesh, Func<string, ModelTexture?>? textureOf, bool hologram);

    Task<byte[]?> SnapshotPreviewAsync(int width, int height);

    /// <summary>Leaves paint mode for a new, empty designer.</summary>
    void OpenNewDesigner();
}
