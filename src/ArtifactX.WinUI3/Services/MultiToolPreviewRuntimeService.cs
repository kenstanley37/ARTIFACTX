using Microsoft.Win32.SafeHandles;
using ArtifactX.Core.Universe;
using System.Collections.Generic;
using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Read-only preview input for explicitly verified NMS.exe builds (see <see cref="Layouts"/>).
/// The comparison UI uses the player manager's weapon owner's animation controller, not the offered
/// model's controller. When neither OVERHEAT name exists, the duration is zero.
/// Present but inactive animations use their initialized frame fields. Active
/// animations and differing possible camera-name durations remain unresolved. No game functions are executed and no memory is written.
/// </summary>
internal static class MultiToolPreviewRuntimeService
{
    /// <summary>Image-relative addresses and structure offsets for one NMS.exe build.</summary>
    private sealed record BuildLayout(string Hash, int Build,
        ulong OwnerCallSite, string OwnerCallBytes, ulong ManagerGlobal, ulong OwnerOffset, ulong ControllerOffset,
        ulong PlaybackGlobal, ulong Period60Global, ulong Period30Global,
        ulong IllegalBranchSite, string IllegalBranchBytes, ulong ForcedBestGlobal,
        ulong SeasonFlagA, ulong SeasonFlagB, ulong SeasonOverrideFlag)
    {
        /// <summary>Active game mode; the pending mode (7 = none) follows it and is copied in on load.</summary>
        public ulong ActiveModeGlobal => ManagerGlobal + 0xC;
    }

    /// <summary>
    /// Build 180836: traced in Ghidra/Unicorn (docs/research/MULTITOOL_EQUIPPED_STATS.md, PIRATE_STATION_MULTITOOLS.md).
    /// Build 185034: re-found 2026-11-17 by read-only scans of the running game (Slot 8, docked in a purple system;
    /// scripts/diagnostics/find_multitool_preview_layout.py). Owner call 0x1406BB644 (add rcx,0x72D1C0); the controller
    /// pointer moved owner+0x23D0 -> +0x2400 (34 valid animation names, no OVERHEAT, so duration 0 - matching two
    /// cabinets' in-game Damage Potential 400.0 and 460.0 where the default duration gave 333.3 and 391.1); owner+0xF74
    /// mode unchanged (17 code references). Duration function 0x142DCB550 keeps every entry offset
    /// (0x1908/0x1920/0x123/0xDC/0xC8/0xCC) and reads 1/30 at 0x4BF5584, 1/60 at 0x4BF5290, playback at 0x50E7330.
    /// Illegal-module override 0x140EE3392: same mode/flag shape, flags manager+0x4CD572/3 and +0x4C66CC; the
    /// function's only byte global, 0x53278A8, forces the best roll (old 0x524AEC4). Standard roll: MWC*100 vs 0x5326D34.
    /// </summary>
    private static readonly BuildLayout[] Layouts =
    [
        new("13D5060D4EFB9D2A6A6B1B349BC4257231056CC2A055DF4BB15D816262CC3499", 180836,
            0x6AD2B4, "488B0D4D047E064D8BC74881C1D0CA720033D2", 0x6E8D708, 0x72CAD0, 0x23D0,
            0x500D040, 0x4B27608, 0x4B278FC,
            0xEC4602, "8B050C91FC0585C0", 0x524AEC4, 0x4CCF82, 0x4CCF83, 0x4C636C),
        new("092236962EE050E419424699B5C343A0173F7773D5AD0866B7EC366D334C07D4", 185034,
            0x6BB644, "488B0D3D0C8B064D8BC74881C1C0D1720033D2", 0x6F6C288, 0x72D1C0, 0x2400,
            0x50E7330, 0x4BF5290, 0x4BF5584,
            0xEE3392, "8B05FC8E080685C0", 0x53278A8, 0x4CD572, 0x4CD573, 0x4C66CC),
    ];

    private static readonly object VerificationLock = new();
    private static (string Path, long Stamp, long Length, BuildLayout? Layout)? _verified;

    /// <summary>
    /// Native 0x140ec4602..0x140ec4636 selects the ordinary illegal-module chance
    /// only outside the network/perk override. Unknown or changing state fails closed.
    /// </summary>
    internal static bool HasStandardIllegalModuleRoll()
    {
        var processes = Process.GetProcessesByName("NMS");
        try
        {
            if (processes.Length != 1) return false;
            var module = processes[0].MainModule;
            if (module is null || LayoutFor(module.FileName) is not { } layout) return false;
            using var handle = OpenProcess(0x410, false, processes[0].Id);
            if (handle.IsInvalid) return false;
            var reader = new Reader(handle);
            ulong image = unchecked((ulong)module.BaseAddress.ToInt64());
            byte[] branch = Convert.FromHexString(layout.IllegalBranchBytes);
            if (!reader.Bytes(image + layout.IllegalBranchSite, branch.Length).AsSpan().SequenceEqual(branch)) return false;
            ulong manager = reader.UInt64(image + layout.ManagerGlobal);
            if (manager == 0) return false;
            bool Standard()
            {
                if (reader.Bytes(image + layout.ForcedBestGlobal, 1)[0] != 0) return false;
                int mode = reader.Int32(image + layout.ActiveModeGlobal);
                return mode != 0 && mode != 6
                    || (reader.Bytes(manager + layout.SeasonFlagA, 1)[0] == 0
                        && reader.Bytes(manager + layout.SeasonFlagB, 1)[0] == 0)
                    || reader.Bytes(manager + layout.SeasonOverrideFlag, 1)[0] == 0;
            }
            return Standard() && reader.UInt64(image + layout.ManagerGlobal) == manager && Standard();
        }
        catch (Exception error)
        {
            Debug.WriteLine($"[MultiToolPreviewRuntime] Illegal-module state unavailable: {error.Message}");
            return false;
        }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    /// <summary>Call on a worker thread. Null means runtime state could not be established.</summary>
    internal static float? ReadOverheatDuration()
    {
        var processes = Process.GetProcessesByName("NMS");
        try
        {
            if (processes.Length != 1) return null;
            var module = processes[0].MainModule;
            if (module is null || LayoutFor(module.FileName) is not { } layout) return null;
            using var handle = OpenProcess(0x410, false, processes[0].Id);
            if (handle.IsInvalid) return null;
            var reader = new Reader(handle);
            ulong image = unchecked((ulong)module.BaseAddress.ToInt64());
            // Also verify the running image: the file at its path could have been replaced.
            byte[] ownerCall = Convert.FromHexString(layout.OwnerCallBytes);
            if (!reader.Bytes(image + layout.OwnerCallSite, ownerCall.Length).AsSpan().SequenceEqual(ownerCall)) return null;
            ulong manager = reader.UInt64(image + layout.ManagerGlobal);
            if (manager == 0) return null;
            ulong owner = manager + layout.OwnerOffset;
            ulong controller = reader.UInt64(owner + layout.ControllerOffset);
            if (controller == 0) return null;
            int mode = reader.Int32(owner + 0xF74);
            if (mode < 0 || mode >= 21) return null;
            ulong table = controller + 0xF0;
            int count = reader.Int32(table + 0x1920);
            if (count <= 0 || count > 2048 || reader.Int32(controller + 0xC0) != count) return null;
            ulong entries = reader.UInt64(table + 0x1908);
            if (entries == 0) return null;
            var overheat = new Dictionary<string, (int Index, ulong Entry)>(StringComparer.Ordinal);
            for (int index = 0; index < count; index++)
            {
                ulong entry = reader.UInt64(entries + (ulong)index * 8);
                if (entry == 0) return null;
                byte[] bytes = reader.Bytes(entry + 0x50, 16);
                int end = Array.IndexOf(bytes, (byte)0);
                if (end < 0) end = bytes.Length;
                if (end == 0) return null;
                string name = Encoding.ASCII.GetString(bytes, 0, end);
                foreach (char c in name)
                    if (!(c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')) return null;
                if ((name is "OVERHEAT" or "OVERHEAT_3P") && !overheat.TryAdd(name, (index, entry))) return null;
            }
            uint activeMask = reader.UInt32(table + 0xE4);
            if ((activeMask & 0xFFFF0000) != 0) return null;
            var activeIndices = new Dictionary<int, int>();
            for (int slot = 0; slot < 16; slot++)
            {
                if ((activeMask & (1u << slot)) == 0) continue;
                int index = reader.Int32(table + 0x244 + (ulong)slot * 0x180);
                if (index < 0 || index >= count) return null;
                activeIndices.Add(slot, index);
            }
            var timings = new Dictionary<ulong, (int Start, int End, float Speed, byte Hz30)>();
            float playback = reader.Single(image + layout.PlaybackGlobal);
            float? Duration(string name)
            {
                if (!overheat.TryGetValue(name, out var animation)) return 0f;
                if (activeIndices.ContainsValue(animation.Index)) return null;
                var timing = (Start: reader.Int32(animation.Entry + 0xCC), End: reader.Int32(animation.Entry + 0xC8),
                    Speed: reader.Single(animation.Entry + 0xDC), Hz30: reader.Bytes(animation.Entry + 0x123, 1)[0]);
                if (timing.Hz30 > 1) return null;
                timings[animation.Entry] = timing;
                float period = reader.Single(image + (timing.Hz30 == 1 ? layout.Period30Global : layout.Period60Global));
                return PreviewAnimationTiming.InactiveDuration(timing.Start, timing.End, timing.Speed, period, playback);
            }
            float? ordinary = Duration("OVERHEAT"), thirdPerson = Duration("OVERHEAT_3P");
            if (ordinary is null || thirdPerson is null || ordinary != thirdPerson) return null;
            // A switch/load during sampling invalidates the entire result.
            if (reader.UInt64(image + layout.ManagerGlobal) != manager
                || reader.UInt64(owner + layout.ControllerOffset) != controller || reader.Int32(owner + 0xF74) != mode
                || reader.Int32(table + 0x1920) != count || reader.UInt64(table + 0x1908) != entries
                || reader.Int32(controller + 0xC0) != count || reader.UInt32(table + 0xE4) != activeMask
                || reader.Single(image + layout.PlaybackGlobal) != playback) return null;
            foreach (var active in activeIndices)
                if (reader.Int32(table + 0x244 + (ulong)active.Key * 0x180) != active.Value) return null;
            foreach (var timing in timings)
                if (reader.Int32(timing.Key + 0xCC) != timing.Value.Start || reader.Int32(timing.Key + 0xC8) != timing.Value.End
                    || reader.Single(timing.Key + 0xDC) != timing.Value.Speed || reader.Bytes(timing.Key + 0x123, 1)[0] != timing.Value.Hz30) return null;
            return ordinary;
        }
        catch (Exception error)
        {
            Debug.WriteLine($"[MultiToolPreviewRuntime] Unavailable: {error.Message}");
            return null;
        }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    /// <summary>The verified layout for the executable at <paramref name="path"/>, or null for any other build.</summary>
    private static BuildLayout? LayoutFor(string path)
    {
        var info = new FileInfo(path);
        lock (VerificationLock)
        {
            if (_verified is { } prior && prior.Path == path && prior.Stamp == info.LastWriteTimeUtc.Ticks
                && prior.Length == info.Length) return prior.Layout;
            using var stream = File.OpenRead(path);
            string hash = Convert.ToHexString(SHA256.HashData(stream));
            var layout = Array.Find(Layouts, l => l.Hash == hash);
            if (layout is null) Debug.WriteLine($"[MultiToolPreviewRuntime] Unsupported NMS.exe {hash}; live preview state off.");
            _verified = (path, info.LastWriteTimeUtc.Ticks, info.Length, layout);
            return layout;
        }
    }

    private sealed class Reader(SafeProcessHandle handle)
    {
        internal byte[] Bytes(ulong address, int length)
        {
            var bytes = new byte[length];
            if (!ReadProcessMemory(handle, unchecked((nint)address), bytes, (nuint)length, out nuint read)
                || read != (nuint)length) throw new IOException("Game state changed or could not be read.");
            return bytes;
        }
        internal ulong UInt64(ulong address) => BinaryPrimitives.ReadUInt64LittleEndian(Bytes(address, 8));
        internal uint UInt32(ulong address) => BinaryPrimitives.ReadUInt32LittleEndian(Bytes(address, 4));
        internal float Single(ulong address) => BitConverter.Int32BitsToSingle(Int32(address));
        internal int Int32(ulong address) => BinaryPrimitives.ReadInt32LittleEndian(Bytes(address, 4));
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(SafeProcessHandle process, nint address,
        [Out] byte[] buffer, nuint length, out nuint bytesRead);
}
