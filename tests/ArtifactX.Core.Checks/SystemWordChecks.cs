using ArtifactX.Core.Universe;

/// <summary>The game's economy, wealth and conflict word pick (SystemWords) against systems whose
/// words were read from the owner's save (user.db source "save", 2026-11-14).</summary>
internal static class SystemWordChecks
{
    public static void Check()
    {
        // ARTIFACTX System (packed 0x1D09E8F80732): Mathematical // High Supply, Untroubled.
        CheckSystem(0x1D09E8F80732UL, "UI_ECON_CLASS_SCIENCE_4", "UI_ECON_LEVEL_HIGH_7", "UI_CONFLICT_LEVEL_LOW_7");

        // Packed 0x1C005C6DC00C: Mass Production // Unsuccessful, Formidable.
        CheckSystem(0x1C005C6DC00CUL, "UI_ECON_CLASS_MANUFACT_4", "UI_ECON_LEVEL_LOW_8", "UI_CONFLICT_LEVEL_HIGH_8");

        // Sennum-Bebae (packed 0x3400FF67B291): Promising, Tranquil. Its economy was recorded as the plain
        // "Mercantile" (word 1, which one screen always shows), so only the level words are checked.
        var sennum = Keys(0x3400FF67B291UL);
        if (sennum?.Wealth != "UI_ECON_LEVEL_MED_3" || sennum.Conflict != "UI_CONFLICT_LEVEL_LOW_3"
            || sennum.PlainEconomy != "UI_ECON_CLASS_TRADE_1")
            throw new InvalidOperationException($"Sennum-Bebae words: got {sennum}");

        Console.WriteLine("System words: ARTIFACTX System, 1C005C6DC00C and Sennum-Bebae OK");
    }

    private static SystemWordKeys? Keys(ulong packed)
    {
        ulong portal = (((packed >> 40) & 0xFFF) << 32) | (packed & 0xFFFFFFFF);
        int galaxy = (int)((packed >> 32) & 0xFF);
        return SystemWords.Keys(portal, galaxy, SystemGenerator.Attributes(portal, galaxy));
    }

    private static void CheckSystem(ulong packed, string economy, string wealth, string conflict)
    {
        var keys = Keys(packed);
        var expected = new SystemWordKeys(economy, wealth, conflict);
        if (keys != expected)
            throw new InvalidOperationException($"{packed:X} words: expected {expected}, got {keys}");
    }
}
