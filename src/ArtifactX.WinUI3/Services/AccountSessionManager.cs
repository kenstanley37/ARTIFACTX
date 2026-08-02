using ArtifactX.Core;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Manages editing accountdata.hg - a deliberately separate concern from
/// SaveSessionManager's per-slot saves, not merged into it: account data
/// affects EVERY save slot on this platform account at once, not just
/// whichever one is currently active, so it gets its own explicit load/save
/// flow rather than riding along with the main title bar's Save/Reset
/// buttons (see AccountDataPage's own Save button).
///
/// Reuses SaveEditSession as-is for staging (Get/Stage/Revert), pointed
/// directly at the real accountdata.hg path as its own "working copy" -
/// unlike per-slot saves, there's no LZ4 container to strip/rewrap (see
/// NmsAccountData's doc comment), so there's no separate extract-to-a-
/// working-copy step needed at all: reading IS the real file, and
/// committing writes straight back to it.
/// </summary>
public static class AccountSessionManager
{
    private static SaveEditSession? _session;
    private static string? _accountDataPath;

    public static event EventHandler? PendingEditsChanged;

    public static bool IsLoaded => _session is not null;
    public static bool HasUnsavedChanges => _session?.HasUnsavedChanges ?? false;
    public static int PendingEditCount => _session?.PendingEditCount ?? 0;
    public static string? AccountDataPath => _accountDataPath;

    /// <summary>Derives accountdata.hg's path from whichever save slot is
    /// currently active - one accountdata.hg lives per platform-account
    /// folder, alongside every save slot in it, so any loaded slot points at
    /// the same file. Returns null if no save is loaded, or if this account
    /// genuinely has no accountdata.hg on disk.</summary>
    public static string? ResolveAccountDataPath()
    {
        var activeSlot = SaveSessionManager.ActiveSlot;
        if (activeSlot is null) return null;

        string? folder = Path.GetDirectoryName(activeSlot.PrimaryFile.FullPath);
        if (folder is null) return null;

        string candidate = Path.Combine(folder, "accountdata.hg");
        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>Loads (or re-uses an already-loaded session for) the account
    /// file tied to the currently active save slot. Returns false if there's
    /// no active slot or no accountdata.hg was found for it - callers should
    /// show that as "no account data available" rather than an error.</summary>
    public static async Task<bool> LoadAsync()
    {
        string? path = ResolveAccountDataPath();
        if (path is null)
        {
            _session = null;
            _accountDataPath = null;
            return false;
        }

        // Re-load only when the resolved path actually changes (switching
        // accounts) or there's no session yet - avoids re-reading from disk
        // (and discarding any staged edits) every time the page is simply
        // re-shown for the same account.
        if (_session is not null && string.Equals(_accountDataPath, path, StringComparison.OrdinalIgnoreCase))
            return true;

        _accountDataPath = path;
        _session = await Task.Run(() => SaveEditSession.LoadAsync(path));
        return true;
    }

    public static JToken? GetValue(params string[] path) => _session?.GetValue(path);

    public static void StageEdit(JToken? newValue, params string[] path)
    {
        _session?.StageEdit(newValue, path);
        PendingEditsChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void RevertEdit(params string[] path)
    {
        _session?.RevertEdit(path);
        PendingEditsChanged?.Invoke(null, EventArgs.Empty);
    }

    public static bool HasStagedEditsUnder(params string[] pathPrefix) => _session?.HasStagedEditsUnder(pathPrefix) ?? false;

    public static void RevertEditsUnder(params string[] pathPrefix)
    {
        _session?.RevertEditsUnder(pathPrefix);
        PendingEditsChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Writes staged edits straight back to the real accountdata.hg -
    /// there's no separate per-slot .hg targets to also update (see this
    /// class's doc comment). Backs up the file's original bytes once, before
    /// the very first write in this app session, since a bad edit here is
    /// riskier than a per-slot save (it affects every save on the account,
    /// not just one) and there's no NMS-side dual-file rotation to fall back
    /// on the way save.hg/save2.hg have for each other.</summary>
    public static async Task CommitAsync()
    {
        if (_session is null || _accountDataPath is null) return;

        string backupPath = _accountDataPath + ".artifactx-backup";
        if (!File.Exists(backupPath))
            File.Copy(_accountDataPath, backupPath);

        await _session.CommitAsync(Enumerable.Empty<string>());
        PendingEditsChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void CloseSession()
    {
        _session = null;
        _accountDataPath = null;
    }
}
