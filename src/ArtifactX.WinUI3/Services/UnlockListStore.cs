using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>Where an unlock list lives: the account file (AccountSessionManager) or
/// the loaded save (SaveSessionManager, which remaps vLc for expeditions). Both stage
/// the whole array; nothing is written until the player saves.</summary>
public interface IUnlockListStore
{
    JArray? Read();
    void Stage(JArray ids);
}

public sealed class UnlockListStore : IUnlockListStore
{
    private readonly string[] _path;
    private readonly bool _account;

    private UnlockListStore(string[] path, bool account)
    {
        _path = path;
        _account = account;
    }

    /// <summary>The account-wide unlock list (B89/B1h).</summary>
    public static UnlockListStore Account { get; } = new(NmsAccountData.UnlockedItemsPath, account: true);

    public static UnlockListStore ForSave(string[] path) => new(path, account: false);

    public JArray? Read() => (_account ? AccountSessionManager.GetValue(_path) : SaveSessionManager.GetValue(_path)) as JArray;

    public void Stage(JArray ids)
    {
        if (_account) AccountSessionManager.StageEdit(ids, _path);
        else SaveSessionManager.StageEdit(ids, _path);
    }
}
