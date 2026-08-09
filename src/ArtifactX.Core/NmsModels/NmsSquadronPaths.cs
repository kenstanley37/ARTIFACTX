using System.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Path helpers for the 4 Squadron Pilot slots (the in-game "Squadron
/// Pilots" screen - one pilot NPC + assigned fighter per slot, shown as a
/// 2x2 grid). Confirmed structurally against a real save (Steam main save,
/// 100+ hours) by loading the decrypted JSON directly and finding the
/// vLc/6f= field shaped as an array of exactly 4 objects, each with exactly
/// 4 sub-keys - matching libMBIN's GcSquadronPilotData (NPCResource,
/// ShipResource, TraitsSeed, PilotRank) exactly. GcPlayerStateData.
/// SquadronPilots is `Size = 0x4` in libMBIN (a real fixed-4 array, unlike
/// Frigates/Settlement Perks which are declared-unbounded lists that just
/// happened to sample small) - see [[project_frigate_page]] for why that
/// distinction matters before assuming a cap.
///
/// Both NPCResource and ShipResource are GcResourceElement (a generic
/// "chosen procedural asset" shape used elsewhere in the game for anything
/// picked from a weighted table): Filename (which asset), Seed (drives its
/// procedural look, same [hasValue, hexString] GcSeed pair used everywhere
/// else in this app), ProceduralTexture (chosen texture variant, empty in
/// every sample seen), AltId (empty in every sample seen). ResHandle (a
/// runtime-only resource handle) is never present in save JSON at all - the
/// observed 4 JSON sub-keys per resource (not 5) match GcResourceElement's
/// Index order with ResHandle (Index=1) skipped entirely, same
/// JSON-key-order-matches-libMBIN-Index-order technique used throughout
/// this app.
///
/// NPCResource.Filename is CONFIRMED to be exactly one of 4 real values -
/// cross-checked against `GcPlayerSquadronConfig.RandomPilotNPCResources`
/// (the game's own pool of valid random-pilot NPCs, decoded via
/// `DataCataloger dumpfile aispaceshipglobals`): NPCGEK, NPCVYKEEN,
/// NPCKORVAX, NPCFOURTH. "NPCFOURTH" is the game's own internal filename
/// for the 4th playable race (labeled Autophage everywhere else in this
/// app, e.g. Language Words) - not a naming mismatch on ArtifactX's side.
///
/// ShipResource.Filename, in contrast, is NOT confirmed to come from a
/// small curated pool - the 4 real values sampled (FIGHTER_PROC,
/// BIOFIGHTER, S-CLASS_PROC, WRACERSE, all under different ship-model
/// subfolders) span multiple unrelated ship categories, and
/// RandomSpaceshipResources (the field that might have held a matching
/// squadron-specific pool) is empty in the decoded globals - most likely
/// pilots draw from the same broad AI-ship spawn tables used for every
/// other NPC ship in the galaxy. Deliberately NOT exposing a Ship Type
/// picker here for that reason - Filename is shown read-only, only its Seed
/// (paint/appearance) is editable, same restrained treatment already used
/// for Ship/Frigate Model Seed.
///
/// PilotRank (yDG, ushort) was 3 for all 4 pilots in the one real save
/// checked - CONFIRMED to be GcInventoryClass.InventoryClassEnum's raw
/// value (C=0, B=1, A=2, S=3): the same save's screenshot showed an "S"
/// badge next to the selected pilot, and PilotRank=3 for every slot in
/// that save's JSON matches exactly. GcPlayerSquadronConfig's own
/// PilotRankAttackDefinitions array (index-matched to this same enum) uses
/// the literal ids "SQUADRON_C"/"SQUADRON_B"/"SQUADRON_A"/"SQUADRON_S",
/// corroborating S as the top rank.
///
/// TraitsSeed (=bJ) is a plain ulong (not wrapped in a GcSeed [hasValue,
/// hex] pair the way the two resource seeds are) - stored as a bare hex
/// string directly in JSON.
///
/// UNCONFIRMED / NOT reproducible here: the pilot's displayed Name (e.g.
/// "Enforcer Sine"), their ship's flavor name (e.g. "The Dance of the
/// Vy'keen"), the 4 qualitative stat words (Intelligence/Mechanical
/// Aptitude/Discipline/Respect for Command - shown as "Excellent"/"Above
/// Average"/etc.), Confirmed Kills, and the flavor Notes line (e.g. "Cannot
/// resist gravitino balls") are NOT stored as literal strings/numbers
/// anywhere in the save. `GcPlayerSquadronConfig.PilotRankTraitRanges`
/// (a min/max Vector2f per rank tier) strongly suggests these are rolled
/// from TraitsSeed + PilotRank at render time, the same "generated from a
/// seed, not stored" pattern as Frigate's auto-generated CustomName - but
/// the exact word-tier thresholds, the Notes flavor-text pool, and the
/// name-generation logic itself weren't found in any decoded globals table
/// (checked GcAISpaceshipGlobals in full). Writing has not been tested
/// in-game at all yet - this is a first pass, reading-only confidence.
/// </summary>
public static class NmsSquadronPaths
{
    public const int PilotSlotCount = 4;

    public static readonly string[] SquadronArrayPath = { "vLc", "6f=", "S5O" };

    public static string[] PilotPath(int slotIndex) =>
        new[] { "vLc", "6f=", "S5O", slotIndex.ToString() };

    public static string[] UnlockedPath(int slotIndex) =>
        new[] { "vLc", "6f=", "7?0", slotIndex.ToString() };

    /// <summary>One of NPCGEK/NPCVYKEEN/NPCKORVAX/NPCFOURTH - see this
    /// class's doc comment. Use <see cref="NpcRaceOptions"/> for the
    /// display-name/filename pairs rather than hand-typing the raw path.</summary>
    public static string[] NpcFilenamePath(int slotIndex) => PilotPath(slotIndex).Append(">r:").Append("93M").ToArray();

    /// <summary>Same "append the 2nd (hex) element, skip the leading bool"
    /// pattern as every other Seed in this app (e.g. Frigate's ModelSeedPath).</summary>
    public static string[] NpcSeedPath(int slotIndex) => PilotPath(slotIndex).Append(">r:").Append("@EL").Append("1").ToArray();

    /// <summary>Read-only in the UI - see this class's doc comment for why
    /// (no confirmed small pool to pick from, unlike NPCResource).</summary>
    public static string[] ShipFilenamePath(int slotIndex) => PilotPath(slotIndex).Append(":dY").Append("93M").ToArray();

    public static string[] ShipSeedPath(int slotIndex) => PilotPath(slotIndex).Append(":dY").Append("@EL").Append("1").ToArray();

    /// <summary>Plain ulong, stored as a bare hex string - NOT a [hasValue,
    /// hex] GcSeed pair like the two resource seeds above.</summary>
    public static string[] TraitsSeedPath(int slotIndex) => PilotPath(slotIndex).Append("=bJ").ToArray();

    /// <summary>GcInventoryClass.InventoryClassEnum raw value (C=0, B=1,
    /// A=2, S=3) - see this class's doc comment for the S-badge confirmation.</summary>
    public static string[] PilotRankPath(int slotIndex) => PilotPath(slotIndex).Append("yDG").ToArray();

    /// <summary>(Display name, raw GcFilename) pairs, sourced from
    /// GcPlayerSquadronConfig.RandomPilotNPCResources (the game's own valid-
    /// pilot pool), not guessed or hand-typed.</summary>
    public static readonly (string DisplayName, string Filename)[] NpcRaceOptions =
    {
        ("Gek", "MODELS/COMMON/PLAYER/PLAYERCHARACTER/NPCGEK.SCENE.MBIN"),
        ("Vy'keen", "MODELS/COMMON/PLAYER/PLAYERCHARACTER/NPCVYKEEN.SCENE.MBIN"),
        ("Korvax", "MODELS/COMMON/PLAYER/PLAYERCHARACTER/NPCKORVAX.SCENE.MBIN"),
        ("Autophage", "MODELS/COMMON/PLAYER/PLAYERCHARACTER/NPCFOURTH.SCENE.MBIN"),
    };
}
