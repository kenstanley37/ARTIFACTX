using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Toolkit;

// Synthetic assets only. Does not require a game install or write a catalog/save.
try
{
    const string tablePath = "METADATA/SIMULATION/ENVIRONMENT/PLANETBUILDINGTABLE.MBIN";
    var files = new GcBuildingFilename[(int)GcBuildingClassification.BuildingClassEnum.Shop + 1];
    files[(int)GcBuildingClassification.BuildingClassEnum.Shop] = new() { LSystem = ["RULES.LSYSTEM.MBIN"] };
    var table = new GcBuildingDefinitionTable
    {
        BuildingFiles = [new() { BuildingFiles = files }]
    };
    var rules = new TkLSystemRulesData
    {
        Rules = [new() { Name = "shop", Model = "ENTRY.SCENE.MBIN", Rules = [new()
        {
            LocatorType = "WEAPONSCRATE_",
            Entries = [new() { Model = "ENTRY.SCENE.MBIN", Probability = 40, Restrictions = [] }]
        }] }]
    };
    var entry = Node("entry", 10, 0, 0);
    entry.Transform.ScaleX = entry.Transform.ScaleY = entry.Transform.ScaleZ = 2;
    entry.Transform.RotZ = 90;
    var reference = Node("reference", 1, 0, 0);
    reference.Attributes = [new() { Name = "SCENEGRAPH", Value = "crate.scene.mbin" }];
    entry.Children = [reference];
    var crate = Node("crate", 0, 1, 0);
    var terminal = Node("terminal", 0, 0, 3);
    terminal.Attributes = [new() { Name = "ATTACHMENT", Value = "weapon.entity.mbin" }];
    crate.Children = [terminal];
    var interaction = new GcInteractionComponentData
    {
        InteractionType = new() { InteractionType = GcInteractionType.InteractionTypeEnum.WeaponTerminal },
        ReseedAfterRewardSuccess = true,
        InteractionSpecificData = new GcWeaponTerminalInteractionData { RespawnPeriodInSeconds = 3600, UseSentinelWeapon = true }
    };
    var attachment = new TkAttachmentData { Components = [new() { Template = interaction }] };
    var assets = new Dictionary<string, NMSTemplate>(StringComparer.OrdinalIgnoreCase)
    {
        [tablePath] = table, ["RULES.LSYSTEM.MBIN"] = rules, ["ENTRY.SCENE.MBIN"] = entry,
        ["CRATE.SCENE.MBIN"] = crate, ["WEAPON.ENTITY.MBIN"] = attachment
    };
    var reads = new Dictionary<string, int>();
    NMSTemplate? Decode(string path)
    {
        reads[path] = reads.GetValueOrDefault(path) + 1;
        return assets.GetValueOrDefault(path);
    }
    var survey = PlanetShopWeaponSurveyService.Read(Decode);
    Require(survey.References.Length == 1 && survey.UnresolvedPaths.Length == 0, "Terminal chain was not resolved");
    var row = survey.References[0];
    var position = row.Terminal.PositionInEntryModel;
    Require(Math.Abs(position[0] - 8) < .00001f && Math.Abs(position[1] - 2) < .00001f
        && Math.Abs(position[2] - 6) < .00001f, "Nested scale/rotation/translation composition differs");
    Require(row.Probability == 40 && row.Race == "Traders" && row.SystemType == "Normal"
        && row.Terminal.ReseedAfterRewardSuccess && row.Terminal.UseSentinelWeapon == true
        && row.Terminal.RespawnPeriodInSeconds == 3600, "Extracted metadata was lost or guessed");
    Require(reads.Values.All(count => count == 1), "An asset was decoded repeatedly");

    assets.Remove("CRATE.SCENE.MBIN");
    survey = PlanetShopWeaponSurveyService.Read(Decode);
    Require(survey.References.Length == 0 && survey.UnresolvedPaths.Contains("CRATE.SCENE.MBIN"),
        "Missing reference was silently accepted");
    assets["CRATE.SCENE.MBIN"] = crate;
    var cycle = Node("cycle", 0, 0, 0);
    cycle.Attributes = [new() { Name = "SCENEGRAPH", Value = "ENTRY.SCENE.MBIN" }];
    crate.Children.Add(cycle);
    survey = PlanetShopWeaponSurveyService.Read(Decode);
    Require(survey.References.Length == 1 && survey.UnresolvedPaths.Contains("ENTRY.SCENE.MBIN"),
        "Recursive reference was not reported");
    crate.Children.Remove(cycle);
    interaction.InteractionType.InteractionType = GcInteractionType.InteractionTypeEnum.None;
    Require(PlanetShopWeaponSurveyService.Read(Decode).References.Length == 0, "Non-weapon interaction accepted");
    Require(PlanetShopWeaponSurveyService.Read(_ => null).UnresolvedPaths.Contains(tablePath), "Missing building table accepted");
    Console.WriteLine("PASS: reference traversal, nested transforms, metadata, decode cache, missing assets, cycles, and non-weapon rejection.");
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
}

static TkSceneNodeData Node(string name, float x, float y, float z) => new()
{
    Name = new(name), Children = [], Attributes = [],
    Transform = new() { TransX = x, TransY = y, TransZ = z, ScaleX = 1, ScaleY = 1, ScaleZ = 1 }
};

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
