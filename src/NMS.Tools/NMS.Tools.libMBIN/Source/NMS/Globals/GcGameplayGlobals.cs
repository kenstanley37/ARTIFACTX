using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.Globals
{
    [NMS(GUID = 0xD1AD0CDC2E78853E, NameHash = 0x2524086B)]
    public class GcGameplayGlobals : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0000 */ public GcDiscoveryTrimSettings DiscoveryTrimSettings;
        [NMS(Index = 484)]
        /* 0x0150 */ public GcShipHUDTargetData HUDTarget;
        [NMS(Index = 428)]
        /* 0x0260 */ public GcScanEffectData BaseBuildingDeleteScanEffect;
        [NMS(Index = 436)]
        /* 0x02B0 */ public GcScanEffectData BaseBuildingErrorAboveWaterScanEffect;
        [NMS(Index = 437)]
        /* 0x0300 */ public GcScanEffectData BaseBuildingErrorBelowWaterScanEffect;
        [NMS(Index = 433)]
        /* 0x0350 */ public GcScanEffectData BaseBuildingErrorCollisionScanEffect;
        [NMS(Index = 434)]
        /* 0x03A0 */ public GcScanEffectData BaseBuildingErrorImpossibleScanEffect;
        [NMS(Index = 432)]
        /* 0x03F0 */ public GcScanEffectData BaseBuildingErrorInsideScanEffect;
        [NMS(Index = 435)]
        /* 0x0440 */ public GcScanEffectData BaseBuildingErrorUnaffordableScanEffect;
        [NMS(Index = 429)]
        /* 0x0490 */ public GcScanEffectData BaseBuildingGhostScanEffect;
        [NMS(Index = 426)]
        /* 0x04E0 */ public GcScanEffectData BaseBuildingPreviewSelectionScanEffect;
        [NMS(Index = 439)]
        /* 0x0530 */ public GcScanEffectData BaseBuildingRemotePlayerSelectionScanEffect;
        [NMS(Index = 438)]
        /* 0x0580 */ public GcScanEffectData BaseBuildingRemotePlayerValidScanEffect;
        [NMS(Index = 427)]
        /* 0x05D0 */ public GcScanEffectData BaseBuildingSelectionScanEffect;
        [NMS(Index = 430)]
        /* 0x0620 */ public GcScanEffectData BaseBuildingSpawnStaticEffect;
        [NMS(Index = 425)]
        /* 0x0670 */ public GcScanEffectData BaseBuildingValidScanEffect;
        [NMS(Index = 431)]
        /* 0x06C0 */ public GcScanEffectData BaseBuildingVisionScanEffect;
        [NMS(Index = 232)]
        /* 0x0710 */ public GcScanEffectData BinocularScanEffect;
        [NMS(Index = 121)]
        /* 0x0760 */ public GcScanEffectData BinocularSelectedEffect;
        [NMS(Index = 424)]
        /* 0x07B0 */ public GcScanEffectData BlankScanEffect;
        [NMS(Index = 418)]
        /* 0x0800 */ public GcScanEffectData BuildingScanEffect;
        [NMS(Index = 421)]
        /* 0x0850 */ public GcScanEffectData CreatureScanEffect;
        [NMS(Index = 246)]
        /* 0x08A0 */ public GcScanEffectData DeconstructEffect;
        [NMS(Index = 247)]
        /* 0x08F0 */ public GcScanEffectData HitEffect;
        [NMS(Index = 248)]
        /* 0x0940 */ public GcScanEffectData HitEffectCritical;
        [NMS(Index = 420)]
        /* 0x0990 */ public GcScanEffectData HUDShipScanEffect;
        [NMS(Index = 422)]
        /* 0x09E0 */ public GcScanEffectData ObjectsScanEffect;
        [NMS(Index = 417)]
        /* 0x0A30 */ public GcScanEffectData PassiveScanEffect;
        [NMS(Index = 626)]
        /* 0x0A80 */ public GcScanEffectData ShipBuilderNullScanEffect;
        [NMS(Index = 625)]
        /* 0x0AD0 */ public GcScanEffectData ShipBuilderScanEffect;
        [NMS(Index = 624)]
        /* 0x0B20 */ public GcScanEffectData ShipSalvageScanEffect;
        [NMS(Index = 419)]
        /* 0x0B70 */ public GcScanEffectData TargetShipScanEffect;
        [NMS(Index = 423)]
        /* 0x0BC0 */ public GcScanEffectData VRGhostScanEffect;
        [NMS(Index = 231)]
        /* 0x0C10 */ public Colour BinocularScanTargetColour;
        [NMS(Index = 122)]
        /* 0x0C20 */ public Colour BinocularSelectedColour;
        [NMS(Index = 123)]
        /* 0x0C30 */ public Colour BinocularSelectedUnknownColour;
        [NMS(Index = 96)]
        /* 0x0C40 */ public Colour BulletTraceLineColour;
        [NMS(Index = 251)]
        /* 0x0C50 */ public Vector3f CommunicatorShipSpawnOffset;
        [NMS(Index = 480)]
        /* 0x0C60 */ public Colour HUDDamageSplatPainColour;
        [NMS(Index = 479)]
        /* 0x0C70 */ public Colour HUDDamageSplatShieldColour;
        [NMS(Index = 179)]
        /* 0x0C80 */ public Colour OverHeatBarColour;
        [NMS(Index = 178)]
        /* 0x0C90 */ public Colour OverHeatColour;
        [NMS(Index = 517)]
        /* 0x0CA0 */ public Colour PainColour;
        [NMS(Index = 567)]
        /* 0x0CB0 */ public Colour PlayerAmbientSpotLightColour;
        [NMS(Index = 204)]
        /* 0x0CC0 */ public Colour RefinerColour1A;
        [NMS(Index = 205)]
        /* 0x0CD0 */ public Colour RefinerColour1B;
        [NMS(Index = 206)]
        /* 0x0CE0 */ public Colour RefinerColour1C;
        [NMS(Index = 207)]
        /* 0x0CF0 */ public Colour RefinerColour1D;
        [NMS(Index = 463)]
        /* 0x0D00 */ public Colour ScannerColour1;
        [NMS(Index = 464)]
        /* 0x0D10 */ public Colour ScannerColour2;
        [NMS(Index = 565)]
        /* 0x0D20 */ public Colour TorchColour;
        [NMS(Index = 1)]
        /* 0x0D30 */ public GcScanToRevealComponentData CaveGraveRevealData;
        [NMS(Index = 110, Size = 0x5)]
        /* 0x0D80 */ public NMSString0x10[] SalvageTechList;
        [NMS(Index = 111, Size = 0x5)]
        /* 0x0DD0 */ public NMSString0x10[] SalvageTechListMultiTool;
        [NMS(Index = 107, Size = 0x4, EnumType = typeof(GcInventoryClass.InventoryClassEnum))]
        /* 0x0E20 */ public NMSString0x10[] SalvageRewardsDropship;
        [NMS(Index = 108, Size = 0x4, EnumType = typeof(GcInventoryClass.InventoryClassEnum))]
        /* 0x0E60 */ public NMSString0x10[] SalvageRewardsFighter;
        [NMS(Index = 109, Size = 0x4, EnumType = typeof(GcInventoryClass.InventoryClassEnum))]
        /* 0x0EA0 */ public NMSString0x10[] SalvageRewardsScience;
        [NMS(Index = 106, Size = 0x4, EnumType = typeof(GcInventoryClass.InventoryClassEnum))]
        /* 0x0EE0 */ public NMSString0x10[] SalvageRewardsShuttle;
        [NMS(Index = 639)]
        /* 0x0F20 */ public NMSString0x20A CommunicatorFireteamNexusExitDialog;
        [NMS(Index = 640)]
        /* 0x0F40 */ public NMSString0x20A CommunicatorNexusExitAbandonWarningDialog;
        [NMS(Index = 654)]
        /* 0x0F60 */ public TkModelResource WeaponSpawnAltObject;
        [NMS(Index = 653)]
        /* 0x0F80 */ public TkModelResource WeaponSpawnObject;
        [NMS(Index = 594)]
        /* 0x0FA0 */ public GcFilename BaseBuildingTable;
        [NMS(Index = 403)]
        /* 0x0FB0 */ public NMSString0x10 BeaconScan;
        [NMS(Index = 392)]
        /* 0x0FC0 */ public NMSString0x10 BioShipTerrainResource;
        [NMS(Index = 34)]
        /* 0x0FD0 */ public NMSString0x10 BuildersWordReward;
        [NMS(Index = 263)]
        /* 0x0FE0 */ public List<GcScanEffectData> CommunicatorEffectTable;
        [NMS(Index = 407)]
        /* 0x0FF0 */ public NMSString0x10 DebugScanPlanet;
        [NMS(Index = 408)]
        /* 0x1000 */ public NMSString0x10 DebugScanSpace;
        [NMS(Index = 530)]
        /* 0x1010 */ public NMSString0x10 DefaultAnim;
        [NMS(Index = 406)]
        /* 0x1020 */ public NMSString0x10 DistressSignalScan;
        [NMS(Index = 75)]
        /* 0x1030 */ public NMSString0x10 FreeShipSlotID;
        [NMS(Index = 74)]
        /* 0x1040 */ public NMSString0x10 FreeSuitSlotID;
        [NMS(Index = 76)]
        /* 0x1050 */ public NMSString0x10 FreeWeapSlotID;
        [NMS(Index = 187)]
        /* 0x1060 */ public List<NMSString0x10> FreighterHyperdriveAccess;
        [NMS(Index = 186)]
        /* 0x1070 */ public List<NMSString0x10> HyperdriveAccess;
        [NMS(Index = 581)]
        /* 0x1080 */ public GcFilename InitialDebrisScene;
        [NMS(Index = 593)]
        /* 0x1090 */ public GcFilename LegacyBaseBuildingTable;
        [NMS(Index = 133)]
        /* 0x10A0 */ public NMSString0x10 MissionSurveyBugsID;
        [NMS(Index = 132)]
        /* 0x10B0 */ public NMSString0x10 MissionSurveyWormID;
        [NMS(Index = 70)]
        /* 0x10C0 */ public NMSString0x10 NexusRecipeCostType;
        [NMS(Index = 405)]
        /* 0x10D0 */ public NMSString0x10 ObservatoryScan;
        [NMS(Index = 358)]
        /* 0x10E0 */ public List<float> PirateEarlySpawnChances;
        [NMS(Index = 32)]
        /* 0x10F0 */ public NMSString0x10 PirateMissionPrimaryReward;
        [NMS(Index = 33)]
        /* 0x1100 */ public NMSString0x10 PirateMissionSecondReward;
        [NMS(Index = 404)]
        /* 0x1110 */ public NMSString0x10 RadioTowerScan;
        [NMS(Index = 389)]
        /* 0x1120 */ public NMSString0x10 ResourceDirtSubstanceID;
        [NMS(Index = 494)]
        /* 0x1130 */ public GcFilename ResourceParticleFile;
        [NMS(Index = 390)]
        /* 0x1140 */ public NMSString0x10 ResourceUnderwaterSubstanceID;
        [NMS(Index = 112)]
        /* 0x1150 */ public List<NMSString0x10> SalvageProdsList;
        [NMS(Index = 113)]
        /* 0x1160 */ public List<NMSString0x10> SalvageSubstancesList;
        [NMS(Index = 391)]
        /* 0x1170 */ public NMSString0x10 SandwormTerrainResource;
        [NMS(Index = 10)]
        /* 0x1180 */ public NMSString0x10 SeaTrashDeployID;
        [NMS(Index = 409)]
        /* 0x1190 */ public NMSString0x10 ShipScan;
        [NMS(Index = 400)]
        /* 0x11A0 */ public NMSString0x10 ToolScan;
        [NMS(Index = 401)]
        /* 0x11B0 */ public NMSString0x10 ToolScanHardMode;
        [NMS(Index = 410)]
        /* 0x11C0 */ public NMSString0x10 VisualScan;
        [NMS(Index = 402)]
        /* 0x11D0 */ public NMSString0x10 WaypointScan;
        [NMS(Index = 416)]
        /* 0x11E0 */ public List<GcZoomData> ZoomLevels;
        [NMS(Index = 7)]
        /* 0x11F0 */ public ulong CookingRefreshTimeInSeconds;
        [NMS(Index = 596)]
        /* 0x11F8 */ public ulong NPCWorkerInteractionCooldown;
        [NMS(Index = 398)]
        /* 0x1200 */ public GcResourceCollectEffect AsteroidResourceCollect;
        [NMS(Index = 651, Size = 0x8)]
        /* 0x1234 */ public int[] AlienShipEvolveCosts;
        [NMS(Index = 360)]
        /* 0x1254 */ public GcExperienceTimers FlybyTimers;
        [NMS(Index = 363, Size = 0x8, EnumType = typeof(GcProceduralTechnologyData.QualityEnum))]
        /* 0x1274 */ public int[] FreighterTechQualityWeightings;
        [NMS(Index = 355)]
        /* 0x1294 */ public GcExperienceTimers FreighterTimers;
        [NMS(Index = 356)]
        /* 0x12B4 */ public GcExperienceTimers PlanetPirateTimers;
        [NMS(Index = 362)]
        /* 0x12D4 */ public GcExperienceTimers PoliceTimers;
        [NMS(Index = 352)]
        /* 0x12F4 */ public GcExperienceTimers SentinelTimersLow;
        [NMS(Index = 353)]
        /* 0x1314 */ public GcExperienceTimers SentinelTimersNormal;
        [NMS(Index = 361)]
        /* 0x1334 */ public GcExperienceTimers SpaceFlybyTimers;
        [NMS(Index = 357)]
        /* 0x1354 */ public GcExperienceTimers SpacePirateTimers;
        [NMS(Index = 528)]
        /* 0x1374 */ public GcTracerData BulletTracer;
        [NMS(Index = 529)]
        /* 0x138C */ public GcTracerData ShipTracer;
        [NMS(Index = 305, Size = 0x4, EnumType = typeof(GcPlayerConflictData.ConflictLevelEnum))]
        /* 0x13A4 */ public float[] ChanceOfPirateFreighterBattleOnWarpToSystem;
        [NMS(Index = 35, Size = 0x3, EnumType = typeof(GcMissionDifficulty.MissionDifficultyEnum))]
        /* 0x13B4 */ public float[] MissionSubstanceDifficultyMultipliers;
        [NMS(Index = 288, Size = 0x3)]
        /* 0x13C0 */ public int[] PirateInterestLevels;
        [NMS(Index = 289, Size = 0x3)]
        /* 0x13CC */ public int[] PirateInterestLevelsSurvival;
        [NMS(Index = 293, Size = 0x3)]
        /* 0x13D8 */ public int[] PoliceInterestLevels;
        [NMS(Index = 294, Size = 0x3)]
        /* 0x13E4 */ public int[] PoliceInterestLevelsSurvival;
        [NMS(Index = 656)]
        /* 0x13F0 */ public Vector2f FrigateFlybyTimer;
        [NMS(Index = 477)]
        /* 0x13F8 */ public Vector2f HUDDamageSize;
        [NMS(Index = 478)]
        /* 0x1400 */ public Vector2f HUDDamageSplatSize;
        [NMS(Index = 612)]
        /* 0x1408 */ public GcPortalData Portal;
        [NMS(Index = 17)]
        /* 0x1410 */ public Vector2f SaveContextTransferShipCostScaleNanites;
        [NMS(Index = 16)]
        /* 0x1418 */ public Vector2f SaveContextTransferShipCostScaleUnits;
        [NMS(Index = 15)]
        /* 0x1420 */ public Vector2f SaveContextTransferWeaponCostScaleNanites;
        [NMS(Index = 14)]
        /* 0x1428 */ public Vector2f SaveContextTransferWeaponCostScaleUnits;
        [NMS(Index = 341)]
        /* 0x1430 */ public float AggressiveSentinelProbability;
        [NMS(Index = 342)]
        /* 0x1434 */ public float AggressiveSentinelProbabilitySurvival;
        [NMS(Index = 598)]
        /* 0x1438 */ public float AirLockDoorRange;
        [NMS(Index = 95)]
        /* 0x143C */ public float AlienPodThreshold1;
        [NMS(Index = 94)]
        /* 0x1440 */ public float AlienPodThreshold2;
        [NMS(Index = 238)]
        /* 0x1444 */ public float AllRecipesKnownNanitesMax;
        [NMS(Index = 237)]
        /* 0x1448 */ public float AllRecipesKnownNanitesMin;
        [NMS(Index = 126)]
        /* 0x144C */ public float AngleFromBaseComputerWhenTeleporting;
        [NMS(Index = 368)]
        /* 0x1450 */ public float AsteroidCarveRadius;
        [NMS(Index = 366)]
        /* 0x1454 */ public float AsteroidHitForce;
        [NMS(Index = 376)]
        /* 0x1458 */ public int AsteroidResourceReducer;
        [NMS(Index = 213)]
        /* 0x145C */ public int AsteroidScanMaxShown;
        [NMS(Index = 215)]
        /* 0x1460 */ public float AsteroidScanRange;
        [NMS(Index = 214)]
        /* 0x1464 */ public float AsteroidScanTime;
        [NMS(Index = 367)]
        /* 0x1468 */ public float AsteroidTorqueForce;
        [NMS(Index = 669)]
        /* 0x146C */ public float AtlasGunCurrencyConversion;
        [NMS(Index = 212)]
        /* 0x1470 */ public float AtmosphereEntryErrorTime;
        [NMS(Index = 209)]
        /* 0x1474 */ public float AtmosphereEntryFadeTimeFactor;
        [NMS(Index = 210)]
        /* 0x1478 */ public float AtmosphereEntryOffset;
        [NMS(Index = 208)]
        /* 0x147C */ public float AtmosphereEntryTime;
        [NMS(Index = 128)]
        /* 0x1480 */ public float AutoTranslateWordChance;
        [NMS(Index = 641)]
        /* 0x1484 */ public float BackgroundSpaceEncounterCheckTimer;
        [NMS(Index = 526)]
        /* 0x1488 */ public float BarrelOffset;
        [NMS(Index = 595)]
        /* 0x148C */ public float BaseBuildingHoverTimeToKnowPart;
        [NMS(Index = 440)]
        /* 0x1490 */ public float BaseBuildingRemotePlayerErrorAlphaScanEffect;
        [NMS(Index = 9)]
        /* 0x1494 */ public float BaseFoodDuration;
        [NMS(Index = 168)]
        /* 0x1498 */ public float BaseLaserHeatTime;
        [NMS(Index = 396)]
        /* 0x149C */ public float BeaconInteractRange;
        [NMS(Index = 225)]
        /* 0x14A0 */ public float BinocCreatureScanTime;
        [NMS(Index = 223)]
        /* 0x14A4 */ public float BinocMinScanTime;
        [NMS(Index = 224)]
        /* 0x14A8 */ public float BinocScanTime;
        [NMS(Index = 222)]
        /* 0x14AC */ public float BinocTimeBeforeScan;
        [NMS(Index = 230)]
        /* 0x14B0 */ public float BinocularScanTargetFadeTime;
        [NMS(Index = 221)]
        /* 0x14B4 */ public float BinocularScanTargetHeightRange;
        [NMS(Index = 227)]
        /* 0x14B8 */ public float BinocularScanTargetInitialFadeTime;
        [NMS(Index = 229)]
        /* 0x14BC */ public float BinocularScanTargetLineWidth;
        [NMS(Index = 220)]
        /* 0x14C0 */ public float BinocularScanTargetMinHeight;
        [NMS(Index = 228)]
        /* 0x14C4 */ public float BinocularScanTargetScaler;
        [NMS(Index = 588)]
        /* 0x14C8 */ public float BlackHoleJumpVoxelDist;
        [NMS(Index = 589)]
        /* 0x14CC */ public float BlackHoleJumpVoxelVariation;
        [NMS(Index = 584)]
        /* 0x14D0 */ public float BonusChildTypeElementsAdd;
        [NMS(Index = 585)]
        /* 0x14D4 */ public float BonusChildTypeElementsMultiply;
        [NMS(Index = 582)]
        /* 0x14D8 */ public float BonusSameTypeElementsAdd;
        [NMS(Index = 583)]
        /* 0x14DC */ public float BonusSameTypeElementsMultiply;
        [NMS(Index = 11)]
        /* 0x14E0 */ public int BugFiendCap;
        [NMS(Index = 371)]
        /* 0x14E4 */ public float BuildingBeamDistance;
        [NMS(Index = 240)]
        /* 0x14E8 */ public float BuildingPlayerBaseIconOffset;
        [NMS(Index = 97)]
        /* 0x14EC */ public float BulletTraceLineWidth;
        [NMS(Index = 42)]
        /* 0x14F0 */ public float CargoShieldStrength;
        [NMS(Index = 124)]
        /* 0x14F4 */ public float CockpitScale;
        [NMS(Index = 63)]
        /* 0x14F8 */ public float CombatLevelBaseValueBoltcaster;
        [NMS(Index = 67)]
        /* 0x14FC */ public float CombatLevelBaseValueCannon;
        [NMS(Index = 68)]
        /* 0x1500 */ public float CombatLevelBaseValueGrenade;
        [NMS(Index = 65)]
        /* 0x1504 */ public float CombatLevelBaseValueRailgun;
        [NMS(Index = 64)]
        /* 0x1508 */ public float CombatLevelBaseValueShotgun;
        [NMS(Index = 66)]
        /* 0x150C */ public float CombatLevelBaseValueSMG;
        [NMS(Index = 62)]
        /* 0x1510 */ public float CombatLevelBestWeaponBias;
        [NMS(Index = 69)]
        /* 0x1514 */ public float CombatLevelSClassValue;
        [NMS(Index = 534)]
        /* 0x1518 */ public float CombatSpeedReadoutMultiplier;
        [NMS(Index = 374)]
        /* 0x151C */ public int CommonProductEffectiveness;
        [NMS(Index = 258)]
        /* 0x1520 */ public float CommunicatorCargoScanDroneAtOffsetFallback;
        [NMS(Index = 259)]
        /* 0x1524 */ public float CommunicatorCargoScanDronePitch;
        [NMS(Index = 260)]
        /* 0x1528 */ public float CommunicatorCargoScanDroneRoll;
        [NMS(Index = 254)]
        /* 0x152C */ public float CommunicatorCargoScanDroneScale;
        [NMS(Index = 255)]
        /* 0x1530 */ public float CommunicatorCargoScanDroneScaleFallback;
        [NMS(Index = 256)]
        /* 0x1534 */ public float CommunicatorCargoScanDroneUpOffset;
        [NMS(Index = 257)]
        /* 0x1538 */ public float CommunicatorCargoScanDroneUpOffsetFallback;
        [NMS(Index = 261)]
        /* 0x153C */ public float CommunicatorCargoScanDroneYaw;
        [NMS(Index = 262)]
        /* 0x1540 */ public float CommunicatorFadeTime;
        [NMS(Index = 250)]
        /* 0x1544 */ public float CommunicatorSpawnHeightOffset;
        [NMS(Index = 249)]
        /* 0x1548 */ public float CommunicatorSpawnOffset;
        [NMS(Index = 253)]
        /* 0x154C */ public float CommunicatorSpawnRotation;
        [NMS(Index = 252)]
        /* 0x1550 */ public float CommunicatorSpawnScale;
        [NMS(Index = 616)]
        /* 0x1554 */ public float CorruptSentinelProbability;
        [NMS(Index = 329)]
        /* 0x1558 */ public float CreatureMinScanTime;
        [NMS(Index = 328)]
        /* 0x155C */ public float CreatureScanAngle;
        [NMS(Index = 327)]
        /* 0x1560 */ public float CreatureScanStickyDecay;
        [NMS(Index = 160)]
        /* 0x1564 */ public int CreaturesToBaitForTutorial;
        [NMS(Index = 161)]
        /* 0x1568 */ public int CreaturesToFeedForTutorial;
        [NMS(Index = 335)]
        /* 0x156C */ public int CritDamage;
        [NMS(Index = 241)]
        /* 0x1570 */ public float CriticalHitMultiplier;
        [NMS(Index = 336)]
        /* 0x1574 */ public float CritScale;
        [NMS(Index = 8)]
        /* 0x1578 */ public int CronusNaniteDonationMul;
        [NMS(Index = 622)]
        /* 0x157C */ public float DeadPlanetGravityFactor;
        [NMS(Index = 369)]
        /* 0x1580 */ public int DeathMoneyPenalty;
        [NMS(Index = 334)]
        /* 0x1584 */ public float DeepWaterJellyCrimeModMax;
        [NMS(Index = 385)]
        /* 0x1588 */ public float DeepWaterResourceMulMax;
        [NMS(Index = 650)]
        /* 0x158C */ public float DerelictPulseEncounterAtmosphericEffectChance;
        [NMS(Index = 275)]
        /* 0x1590 */ public float DestroyEffectHeight;
        [NMS(Index = 276)]
        /* 0x1594 */ public float DestroyEffectMaxScale;
        [NMS(Index = 127)]
        /* 0x1598 */ public float DistanceFromBaseComputerWhenTeleporting;
        [NMS(Index = 395)]
        /* 0x159C */ public float DockNodeBuyRadius;
        [NMS(Index = 283)]
        /* 0x15A0 */ public int EarlyPiratesToBeKilled;
        [NMS(Index = 338)]
        /* 0x15A4 */ public float EmergencyBuildingScanEventDistance;
        [NMS(Index = 339)]
        /* 0x15A8 */ public float EmergencyBuildingScanEventTime;
        [NMS(Index = 277)]
        /* 0x15AC */ public float EventTargetSpeed;
        [NMS(Index = 468)]
        /* 0x15B0 */ public float ExcitingNearRange;
        [NMS(Index = 466)]
        /* 0x15B4 */ public float ExcitingViewAngle;
        [NMS(Index = 467)]
        /* 0x15B8 */ public float ExcitingViewRange;
        [NMS(Index = 576)]
        /* 0x15BC */ public float ExplosionForce;
        [NMS(Index = 577)]
        /* 0x15C0 */ public float ExplosionForceMaxForceRadius;
        [NMS(Index = 575)]
        /* 0x15C4 */ public float ExplosionRadiusMultiplier;
        [NMS(Index = 613)]
        /* 0x15C8 */ public int FourthRaceSpawnPercentage;
        [NMS(Index = 300)]
        /* 0x15CC */ public float FreighterBattleRadius;
        [NMS(Index = 304)]
        /* 0x15D0 */ public float FreighterCargoPodHealthFraction;
        [NMS(Index = 303)]
        /* 0x15D4 */ public float FreighterFuelRodHealthFraction;
        [NMS(Index = 354)]
        /* 0x15D8 */ public int FreighterStartPecent;
        [NMS(Index = 623)]
        /* 0x15DC */ public float GasGiantGravityFactor;
        [NMS(Index = 587)]
        /* 0x15E0 */ public float GoalGapVoxelDist;
        [NMS(Index = 103)]
        /* 0x15E4 */ public int GPSRandomChangeTimeBuilderCorruption;
        [NMS(Index = 101)]
        /* 0x15E8 */ public int GPSRandomChangeTimeMid;
        [NMS(Index = 102)]
        /* 0x15EC */ public int GPSRandomChangeTimeNarrow;
        [NMS(Index = 100)]
        /* 0x15F0 */ public int GPSRandomChangeTimeWide;
        [NMS(Index = 243)]
        /* 0x15F4 */ public float GrabPlayerCooldownTime;
        [NMS(Index = 610)]
        /* 0x15F8 */ public int GuildStockReplenishTime;
        [NMS(Index = 611)]
        /* 0x15FC */ public int GuildStockReplenishTimeSubstance;
        [NMS(Index = 524)]
        /* 0x1600 */ public float GunDownAngle;
        [NMS(Index = 521)]
        /* 0x1604 */ public float GunOffsetAggressiveX;
        [NMS(Index = 522)]
        /* 0x1608 */ public float GunOffsetAggressiveY;
        [NMS(Index = 523)]
        /* 0x160C */ public float GunOffsetAggressiveZ;
        [NMS(Index = 518)]
        /* 0x1610 */ public float GunOffsetX;
        [NMS(Index = 519)]
        /* 0x1614 */ public float GunOffsetY;
        [NMS(Index = 520)]
        /* 0x1618 */ public float GunOffsetZ;
        [NMS(Index = 525)]
        /* 0x161C */ public float GunUpAngle;
        [NMS(Index = 282)]
        /* 0x1620 */ public int HardModeTechDamageHighPercent;
        [NMS(Index = 280)]
        /* 0x1624 */ public int HardModeTechDamageMidNum;
        [NMS(Index = 281)]
        /* 0x1628 */ public int HardModeTechDamageMidPercent;
        [NMS(Index = 169)]
        /* 0x162C */ public float HeatAlertTime;
        [NMS(Index = 170)]
        /* 0x1630 */ public float HeatDamageBoost;
        [NMS(Index = 174)]
        /* 0x1634 */ public float HeatMaxPowerPoint;
        [NMS(Index = 171)]
        /* 0x1638 */ public float HeatWidthBoost;
        [NMS(Index = 172)]
        /* 0x163C */ public float HeatWidthBoostAlt;
        [NMS(Index = 242)]
        /* 0x1640 */ public float HitEffectMinTime;
        [NMS(Index = 527)]
        /* 0x1644 */ public float HitOffset;
        [NMS(Index = 190)]
        /* 0x1648 */ public float HmdInteractionRangeMinDistance;
        [NMS(Index = 189)]
        /* 0x164C */ public float HmdInteractionRangeMinRange;
        [NMS(Index = 191)]
        /* 0x1650 */ public float HmdInteractionRangeMultiplier;
        [NMS(Index = 297)]
        /* 0x1654 */ public float HoursBetweenBattles;
        [NMS(Index = 592)]
        /* 0x1658 */ public float HoursBetweenMiniStation;
        [NMS(Index = 413)]
        /* 0x165C */ public float HoverTimeToScan;
        [NMS(Index = 414)]
        /* 0x1660 */ public float HoverTimeToScanCreatures;
        [NMS(Index = 415)]
        /* 0x1664 */ public int HoverVisiblePointsNeeded;
        [NMS(Index = 476)]
        /* 0x1668 */ public float HUDArrowPrimaryScaler;
        [NMS(Index = 481)]
        /* 0x166C */ public float HUDDamageOffset;
        [NMS(Index = 483)]
        /* 0x1670 */ public float HUDDamageRadius;
        [NMS(Index = 482)]
        /* 0x1674 */ public float HUDDamageScaler;
        [NMS(Index = 486)]
        /* 0x1678 */ public float HUDLabelAngle;
        [NMS(Index = 487)]
        /* 0x167C */ public float HUDLabelAngleOffset;
        [NMS(Index = 488)]
        /* 0x1680 */ public float HUDLabelOffsetX;
        [NMS(Index = 489)]
        /* 0x1684 */ public float HUDLabelOffsetY;
        [NMS(Index = 490)]
        /* 0x1688 */ public float HUDLabelOffsetZ;
        [NMS(Index = 491)]
        /* 0x168C */ public float HUDLabelScale;
        [NMS(Index = 485)]
        /* 0x1690 */ public float HUDLabelTime;
        [NMS(Index = 475)]
        /* 0x1694 */ public float HUDLockOnAudioTime;
        [NMS(Index = 511)]
        /* 0x1698 */ public float HUDReticuleMoveAim;
        [NMS(Index = 510)]
        /* 0x169C */ public float HUDReticuleMoveBack;
        [NMS(Index = 504)]
        /* 0x16A0 */ public float HUDScaleAdd;
        [NMS(Index = 509)]
        /* 0x16A4 */ public float HUDScaleFarTarget;
        [NMS(Index = 508)]
        /* 0x16A8 */ public float HUDScaleMax;
        [NMS(Index = 506)]
        /* 0x16AC */ public float HUDScaleMin;
        [NMS(Index = 507)]
        /* 0x16B0 */ public float HUDScaleMinAim;
        [NMS(Index = 505)]
        /* 0x16B4 */ public float HUDScaleRadius;
        [NMS(Index = 465)]
        /* 0x16B8 */ public float HUDStoreFlashTime;
        [NMS(Index = 61)]
        /* 0x16BC */ public float IllegalTechProductTradingMod;
        [NMS(Index = 58)]
        /* 0x16C0 */ public float InfestedPlanetDeepSpaceFlybyMultiplier;
        [NMS(Index = 77)]
        /* 0x16C4 */ public float InitialLocationProtectionRadiusMultiplier;
        [NMS(Index = 269)]
        /* 0x16C8 */ public float InteractionAttractTime;
        [NMS(Index = 586)]
        /* 0x16CC */ public float InteractionComponentUpdateDistance;
        [NMS(Index = 270)]
        /* 0x16D0 */ public float InteractionDescriptionTextTime;
        [NMS(Index = 268)]
        /* 0x16D4 */ public float InteractionMessageModuleDisplayTime;
        [NMS(Index = 265)]
        /* 0x16D8 */ public float InteractionMinTimeBeforeHighlightedOptionSelection;
        [NMS(Index = 266)]
        /* 0x16DC */ public float InteractionMinTimeBetweenStickOptionHighlights;
        [NMS(Index = 271)]
        /* 0x16E0 */ public float InteractionMinTimeForResponseText;
        [NMS(Index = 273)]
        /* 0x16E4 */ public GcInteractionType InteractionOverrideFrom;
        [NMS(Index = 274)]
        /* 0x16E8 */ public GcInteractionType InteractionOverrideTo;
        [NMS(Index = 573)]
        /* 0x16EC */ public float InteractionPickupAngle;
        [NMS(Index = 574)]
        /* 0x16F0 */ public float InteractionPickupRadius;
        [NMS(Index = 267)]
        /* 0x16F4 */ public float InteractionStickOptionStrength;
        [NMS(Index = 541)]
        /* 0x16F8 */ public float InteractionTorchFoV;
        [NMS(Index = 542)]
        /* 0x16FC */ public float InteractionTorchStrength;
        [NMS(Index = 590)]
        /* 0x1700 */ public float JumpToCenterFuelCost;
        [NMS(Index = 579)]
        /* 0x1704 */ public float LaserExplosionForce;
        [NMS(Index = 580)]
        /* 0x1708 */ public float LaserExplosionForceMaxForceRadius;
        [NMS(Index = 578)]
        /* 0x170C */ public float LaserExplosionForceRadius;
        [NMS(Index = 18)]
        /* 0x1710 */ public int LaserRechargeHintThresholdEarly;
        [NMS(Index = 19)]
        /* 0x1714 */ public int LaserRechargeHintThresholdLate;
        [NMS(Index = 597)]
        /* 0x1718 */ public float LightStrength;
        [NMS(Index = 470)]
        /* 0x171C */ public float LineWidth;
        [NMS(Index = 471)]
        /* 0x1720 */ public float LockOffFactor;
        [NMS(Index = 343)]
        /* 0x1724 */ public float LowSentinelProbability;
        [NMS(Index = 344)]
        /* 0x1728 */ public float LowSentinelProbabilitySurvival;
        [NMS(Index = 502)]
        /* 0x172C */ public float MapFOV;
        [NMS(Index = 499)]
        /* 0x1730 */ public float MapHeight;
        [NMS(Index = 500)]
        /* 0x1734 */ public float MapOffset;
        [NMS(Index = 503)]
        /* 0x1738 */ public float MapRadius;
        [NMS(Index = 501)]
        /* 0x173C */ public float MapTargetOffset;
        [NMS(Index = 80)]
        /* 0x1740 */ public int MaxActiveWeatherHazardGifts;
        [NMS(Index = 5)]
        /* 0x1744 */ public int MaxCookingRank;
        [NMS(Index = 125)]
        /* 0x1748 */ public float MaxDistanceForLadderTeleport;
        [NMS(Index = 350)]
        /* 0x174C */ public int MaxDronesAggressive;
        [NMS(Index = 351)]
        /* 0x1750 */ public int MaxDronesAggressiveSurvival;
        [NMS(Index = 346)]
        /* 0x1754 */ public int MaxDronesLow;
        [NMS(Index = 347)]
        /* 0x1758 */ public int MaxDronesLowSurvival;
        [NMS(Index = 348)]
        /* 0x175C */ public int MaxDronesNormal;
        [NMS(Index = 349)]
        /* 0x1760 */ public int MaxDronesNormalSurvival;
        [NMS(Index = 84)]
        /* 0x1764 */ public float MaxMeteorSurfaceAngle;
        [NMS(Index = 264)]
        /* 0x1768 */ public int MaxNumMissionsFromMissionGiver;
        [NMS(Index = 200)]
        /* 0x176C */ public int MaxNumSameGroupTech;
        [NMS(Index = 239)]
        /* 0x1770 */ public float MaxPossibleShipDamagePotential;
        [NMS(Index = 370)]
        /* 0x1774 */ public int MaxResourceCrystalBlobSize;
        [NMS(Index = 621)]
        /* 0x1778 */ public float MaxRespawnOnGroundHeight;
        [NMS(Index = 493)]
        /* 0x177C */ public float MaxSpawnTime;
        [NMS(Index = 461)]
        /* 0x1780 */ public float MaxStandingTechDiscount;
        [NMS(Index = 115)]
        /* 0x1784 */ public int MaxTorpedoesInFlight;
        [NMS(Index = 20)]
        /* 0x1788 */ public int MeleeHintThresholdEarly;
        [NMS(Index = 21)]
        /* 0x178C */ public int MeleeHintThresholdLate;
        [NMS(Index = 6)]
        /* 0x1790 */ public int MinCookingRank;
        [NMS(Index = 321)]
        /* 0x1794 */ public float MissileApproachTime;
        [NMS(Index = 315)]
        /* 0x1798 */ public float MissileBrakeForceMax;
        [NMS(Index = 314)]
        /* 0x179C */ public float MissileBrakeForceMin;
        [NMS(Index = 317)]
        /* 0x17A0 */ public float MissileBrakeTime;
        [NMS(Index = 313)]
        /* 0x17A4 */ public float MissileForceMax;
        [NMS(Index = 312)]
        /* 0x17A8 */ public float MissileForceMin;
        [NMS(Index = 318)]
        /* 0x17AC */ public float MissileHitRadius;
        [NMS(Index = 307)]
        /* 0x17B0 */ public float MissileLockTime;
        [NMS(Index = 311)]
        /* 0x17B4 */ public float MissileMaxSpeed;
        [NMS(Index = 306)]
        /* 0x17B8 */ public float MissileMaxTurnAngle;
        [NMS(Index = 320)]
        /* 0x17BC */ public float MissileMinCircleTime;
        [NMS(Index = 319)]
        /* 0x17C0 */ public float MissileOffset;
        [NMS(Index = 308)]
        /* 0x17C4 */ public float MissileReloadTime;
        [NMS(Index = 310)]
        /* 0x17C8 */ public float MissileRotateMax;
        [NMS(Index = 309)]
        /* 0x17CC */ public float MissileRotateMin;
        [NMS(Index = 322)]
        /* 0x17D0 */ public float MissileRotateSpeed;
        [NMS(Index = 316)]
        /* 0x17D4 */ public float MissileThreatTime;
        [NMS(Index = 236)]
        /* 0x17D8 */ public float MissionEventAllowResetDistance;
        [NMS(Index = 143)]
        /* 0x17DC */ public float MissionSurveyAutoScanDistance;
        [NMS(Index = 144)]
        /* 0x17E0 */ public float MissionSurveyAutoScanFlattenRadiusMul;
        [NMS(Index = 139)]
        /* 0x17E4 */ public float MissionSurveyBeginScanPercentageEvent;
        [NMS(Index = 138)]
        /* 0x17E8 */ public float MissionSurveyBeginScanPercentageScannable;
        [NMS(Index = 150)]
        /* 0x17EC */ public float MissionSurveyInShipArrivedDistance;
        [NMS(Index = 134)]
        /* 0x17F0 */ public float MissionSurveyMarkerAutoHideTime;
        [NMS(Index = 148)]
        /* 0x17F4 */ public float MissionSurveyMaxAngleOffset;
        [NMS(Index = 130)]
        /* 0x17F8 */ public float MissionSurveyMaxGuidanceDistance;
        [NMS(Index = 131)]
        /* 0x17FC */ public float MissionSurveyMaxGuidanceDistanceWorms;
        [NMS(Index = 136)]
        /* 0x1800 */ public float MissionSurveyMaxScanAngle;
        [NMS(Index = 137)]
        /* 0x1804 */ public float MissionSurveyMaxScanAngleScannableComp;
        [NMS(Index = 149)]
        /* 0x1808 */ public float MissionSurveyMaxSignalConeAngle;
        [NMS(Index = 142)]
        /* 0x180C */ public float MissionSurveyRevealWithoutScanDistance;
        [NMS(Index = 135)]
        /* 0x1810 */ public float MissionSurveyScanRangeMultiplier;
        [NMS(Index = 140)]
        /* 0x1814 */ public float MissionSurveyScanSpeed;
        [NMS(Index = 141)]
        /* 0x1818 */ public float MissionSurveyScanSpeedScannableComp;
        [NMS(Index = 146)]
        /* 0x181C */ public float MissionSurveySonarMaxPulseSpeed;
        [NMS(Index = 145)]
        /* 0x1820 */ public float MissionSurveySonarMinPulseSpeed;
        [NMS(Index = 147)]
        /* 0x1824 */ public float MissionSurveySonarPulseSpeedBandCount;
        [NMS(Index = 512)]
        /* 0x1828 */ public float MouseSensitivity;
        [NMS(Index = 513)]
        /* 0x182C */ public float MouseSensitivityShip;
        [NMS(Index = 514)]
        /* 0x1830 */ public float MouseSensitivityVehicle;
        [NMS(Index = 119)]
        /* 0x1834 */ public float MultidimensionalFadeTime;
        [NMS(Index = 331)]
        /* 0x1838 */ public float NewDiscoveryDisplayTime;
        [NMS(Index = 670)]
        /* 0x183C */ public int NexusDespawnSquaredDistance;
        [NMS(Index = 71)]
        /* 0x1840 */ public int NexusRecipeCostNaniteMultiplier;
        [NMS(Index = 345)]
        /* 0x1844 */ public float NonAggressiveLushSurvivalProbabability;
        [NMS(Index = 614)]
        /* 0x1848 */ public int NonDominantRaceSpawnPercentage;
        [NMS(Index = 615)]
        /* 0x184C */ public int NonDominantRaceSpawnPercentagePirate;
        [NMS(Index = 462)]
        /* 0x1850 */ public float NonNaturalCostMul;
        [NMS(Index = 167)]
        /* 0x1854 */ public float NormalModeHeatBonus;
        [NMS(Index = 12)]
        /* 0x1858 */ public float NPCNavNodeSearchUpDistScaling;
        [NMS(Index = 30)]
        /* 0x185C */ public float NPCWaitWhenRevealedTime;
        [NMS(Index = 337)]
        /* 0x1860 */ public int NumMissionsPerSolarSystem;
        [NMS(Index = 472)]
        /* 0x1864 */ public float OffscreenFadeDistance;
        [NMS(Index = 473)]
        /* 0x1868 */ public float OffscreenMinDot;
        [NMS(Index = 175)]
        /* 0x186C */ public float OverheatDecay;
        [NMS(Index = 173)]
        /* 0x1870 */ public float OverheatGenerosity;
        [NMS(Index = 515)]
        /* 0x1874 */ public float PainTime;
        [NMS(Index = 78)]
        /* 0x1878 */ public float PartyPlanetSearchDistance;
        [NMS(Index = 658)]
        /* 0x187C */ public int PersonalInventoryMinHeightMax;
        [NMS(Index = 657)]
        /* 0x1880 */ public int PersonalInventoryMinWidthMax;
        [NMS(Index = 660)]
        /* 0x1884 */ public int PersonalTechInventoryMinHeightMax;
        [NMS(Index = 659)]
        /* 0x1888 */ public int PersonalTechInventoryMinWidthMax;
        [NMS(Index = 359)]
        /* 0x188C */ public float PirateEarlySpawnTime;
        [NMS(Index = 48)]
        /* 0x1890 */ public float PirateGroundAccelerator;
        [NMS(Index = 49)]
        /* 0x1894 */ public float PirateGroundInventoryAccelerator;
        [NMS(Index = 286)]
        /* 0x1898 */ public float PirateInterestDistance;
        [NMS(Index = 287)]
        /* 0x189C */ public float PirateInterestDistanceMPMission;
        [NMS(Index = 285)]
        /* 0x18A0 */ public int PirateInterestOverride;
        [NMS(Index = 47)]
        /* 0x18A4 */ public float PirateRaidAccelerator;
        [NMS(Index = 284)]
        /* 0x18A8 */ public int PlanetPirateFlybyMaxRepeatCounter;
        [NMS(Index = 566)]
        /* 0x18AC */ public float PlayerAmbientSpotLightIntensity;
        [NMS(Index = 568)]
        /* 0x18B0 */ public float PlayerAmbientSpotLightOffsetY;
        [NMS(Index = 569)]
        /* 0x18B4 */ public float PlayerAmbientSpotLightOffsetZ;
        [NMS(Index = 55)]
        /* 0x18B8 */ public float PoliceAcceleratorNoCargoNormal;
        [NMS(Index = 56)]
        /* 0x18BC */ public float PoliceAcceleratorNoCargoSkipScanOdds;
        [NMS(Index = 54)]
        /* 0x18C0 */ public float PoliceAcceleratorNoCargoWealthy;
        [NMS(Index = 51)]
        /* 0x18C4 */ public float PoliceAcceleratorPulseExtra;
        [NMS(Index = 52)]
        /* 0x18C8 */ public int PoliceAcceleratorSmugglingItemsMax;
        [NMS(Index = 53)]
        /* 0x18CC */ public float PoliceAcceleratorSmugglingMulMax;
        [NMS(Index = 50)]
        /* 0x18D0 */ public float PoliceAcceleratorStationLockOn;
        [NMS(Index = 291)]
        /* 0x18D4 */ public float PoliceInterestDistance;
        [NMS(Index = 292)]
        /* 0x18D8 */ public float PoliceInterestDistanceMPMission;
        [NMS(Index = 290)]
        /* 0x18DC */ public int PoliceInterestOverride;
        [NMS(Index = 606)]
        /* 0x18E0 */ public int ProductItemStockDecayTime;
        [NMS(Index = 607)]
        /* 0x18E4 */ public int ProductItemStockReplenishTime;
        [NMS(Index = 340)]
        /* 0x18E8 */ public float ProjectileLightIntensity;
        [NMS(Index = 469)]
        /* 0x18EC */ public float ProjectileSpeedBoost;
        [NMS(Index = 643)]
        /* 0x18F0 */ public float PulseEncounterChance;
        [NMS(Index = 647)]
        /* 0x18F4 */ public float PulseEncounterChanceBlue;
        [NMS(Index = 646)]
        /* 0x18F8 */ public float PulseEncounterChanceGreen;
        [NMS(Index = 645)]
        /* 0x18FC */ public float PulseEncounterChanceRed;
        [NMS(Index = 644)]
        /* 0x1900 */ public float PulseEncounterChanceStandard;
        [NMS(Index = 642)]
        /* 0x1904 */ public float PulseEncounterCheckTimer;
        [NMS(Index = 648)]
        /* 0x1908 */ public float PulseEncounterStopPower;
        [NMS(Index = 649)]
        /* 0x190C */ public float PulseEncounterStopSpeed;
        [NMS(Index = 375)]
        /* 0x1910 */ public int RareProductEffectiveness;
        [NMS(Index = 373)]
        /* 0x1914 */ public int RareSubstanceEffectiveness;
        [NMS(Index = 182)]
        /* 0x1918 */ public int RefinerProductsMadeInTime;
        [NMS(Index = 184)]
        /* 0x191C */ public int RefinerProductsMadeInTimeSurvival;
        [NMS(Index = 183)]
        /* 0x1920 */ public int RefinerSubsMadeInTime;
        [NMS(Index = 185)]
        /* 0x1924 */ public int RefinerSubsMadeInTimeSurvival;
        [NMS(Index = 384)]
        /* 0x1928 */ public int ResourceCommonMaxAmount;
        [NMS(Index = 383)]
        /* 0x192C */ public int ResourceCommonMinAmount;
        [NMS(Index = 382)]
        /* 0x1930 */ public int ResourceCommonReducer;
        [NMS(Index = 388)]
        /* 0x1934 */ public int ResourceDirtMaxAmount;
        [NMS(Index = 387)]
        /* 0x1938 */ public int ResourceDirtMinAmount;
        [NMS(Index = 386)]
        /* 0x193C */ public int ResourceDirtReducer;
        [NMS(Index = 381)]
        /* 0x1940 */ public int ResourceMaxAmount;
        [NMS(Index = 380)]
        /* 0x1944 */ public int ResourceMinAmount;
        [NMS(Index = 497)]
        /* 0x1948 */ public int ResourceParticleMaxNum;
        [NMS(Index = 498)]
        /* 0x194C */ public int ResourceParticleMinNum;
        [NMS(Index = 495)]
        /* 0x1950 */ public float ResourceParticleSpeed;
        [NMS(Index = 496)]
        /* 0x1954 */ public float ResourceParticleSpread;
        [NMS(Index = 379)]
        /* 0x1958 */ public int ResourceReducer;
        [NMS(Index = 40)]
        /* 0x195C */ public int RocketLockerFuelUse;
        [NMS(Index = 633)]
        /* 0x1960 */ public float SalvageShipExplodeShipDistance;
        [NMS(Index = 629)]
        /* 0x1964 */ public float SalvageShipExplodeSpeed;
        [NMS(Index = 628)]
        /* 0x1968 */ public float SalvageShipHeightOffset;
        [NMS(Index = 634)]
        /* 0x196C */ public float SalvageShipOneRevolutionTime;
        [NMS(Index = 627)]
        /* 0x1970 */ public float SalvageShipPitch;
        [NMS(Index = 632)]
        /* 0x1974 */ public float SalvageShipShowShipDistance;
        [NMS(Index = 630)]
        /* 0x1978 */ public float SalvageShipShowShipSpeed;
        [NMS(Index = 631)]
        /* 0x197C */ public float SalvageShipShowShipSpeedSwitching;
        [NMS(Index = 105)]
        /* 0x1980 */ public int SalvageSubstanceValueThreshold;
        [NMS(Index = 104)]
        /* 0x1984 */ public float SalvageValueAsProds;
        [NMS(Index = 636)]
        /* 0x1988 */ public float SalvageWeaponHeightOffset;
        [NMS(Index = 638)]
        /* 0x198C */ public float SalvageWeaponMaxSize;
        [NMS(Index = 635)]
        /* 0x1990 */ public float SalvageWeaponPitch;
        [NMS(Index = 637)]
        /* 0x1994 */ public float SalvageWeaponScale;
        [NMS(Index = 330)]
        /* 0x1998 */ public float ScanAngle;
        [NMS(Index = 399)]
        /* 0x199C */ public float ScanAngleCutoff;
        [NMS(Index = 234)]
        /* 0x19A0 */ public float ScannableComponentMarkerOffset;
        [NMS(Index = 235)]
        /* 0x19A4 */ public float ScannableComponentPlaceMarkerOffset;
        [NMS(Index = 450)]
        /* 0x19A8 */ public float ScannerAnalyzeFXRate;
        [NMS(Index = 233)]
        /* 0x19AC */ public float ScannerIconsClumpRadius;
        [NMS(Index = 447)]
        /* 0x19B0 */ public float ScannerMarkerFadeTime;
        [NMS(Index = 449)]
        /* 0x19B4 */ public float ScannerPhysicsProbeAABBSize;
        [NMS(Index = 448)]
        /* 0x19B8 */ public float ScannerSmallMarkerFadeTime;
        [NMS(Index = 451)]
        /* 0x19BC */ public float ScannerSubstanceTimePerTrigger;
        [NMS(Index = 452)]
        /* 0x19C0 */ public float ScannerTuningA;
        [NMS(Index = 453)]
        /* 0x19C4 */ public float ScannerTuningB;
        [NMS(Index = 454)]
        /* 0x19C8 */ public float ScannerTuningC;
        [NMS(Index = 455)]
        /* 0x19CC */ public float ScannerTuningD;
        [NMS(Index = 456)]
        /* 0x19D0 */ public float ScannerTuningResourceOffset1;
        [NMS(Index = 457)]
        /* 0x19D4 */ public float ScannerTuningResourceOffset2;
        [NMS(Index = 458)]
        /* 0x19D8 */ public float ScannerTuningSmoothStepAmount;
        [NMS(Index = 459)]
        /* 0x19DC */ public float ScannerTuningTrailingEdge;
        [NMS(Index = 323)]
        /* 0x19E0 */ public float ScanStartTimeDelayMinDist;
        [NMS(Index = 324)]
        /* 0x19E4 */ public float ScanStartTimeDelayRange;
        [NMS(Index = 325)]
        /* 0x19E8 */ public float ScanStartTimeDistanceDelayTime;
        [NMS(Index = 326)]
        /* 0x19EC */ public float ScanStickyDecay;
        [NMS(Index = 27)]
        /* 0x19F0 */ public float ScanToRevealDelayOverride;
        [NMS(Index = 31)]
        /* 0x19F4 */ public float ScanToRevealVFXBaseScalingSize;
        [NMS(Index = 620)]
        /* 0x19F8 */ public float ScrapDroneProbability;
        [NMS(Index = 188)]
        /* 0x19FC */ public float SeasonalMessageModuleAttractDistanceMul;
        [NMS(Index = 364)]
        /* 0x1A00 */ public int SentinelsHigh;
        [NMS(Index = 365)]
        /* 0x1A04 */ public int SentinelsLow;
        [NMS(Index = 98)]
        /* 0x1A08 */ public float ShipBulletTraceLineWidth;
        [NMS(Index = 602)]
        /* 0x1A0C */ public float ShipDisplayDamageMultiplier;
        [NMS(Index = 397)]
        /* 0x1A10 */ public float ShipInteractRadius;
        [NMS(Index = 57)]
        /* 0x1A14 */ public float ShipInterestAcceleratorInAsteroidField;
        [NMS(Index = 662)]
        /* 0x1A18 */ public int ShipInventoryMinHeightMax;
        [NMS(Index = 661)]
        /* 0x1A1C */ public int ShipInventoryMinWidthMax;
        [NMS(Index = 59)]
        /* 0x1A20 */ public float ShipMiningMul;
        [NMS(Index = 444)]
        /* 0x1A24 */ public float ShipScanOffset;
        [NMS(Index = 445)]
        /* 0x1A28 */ public float ShipScanOffsetGap;
        [NMS(Index = 72)]
        /* 0x1A2C */ public float ShipScanPlanetRechargeMultiplier;
        [NMS(Index = 73)]
        /* 0x1A30 */ public float ShipScanSpaceRechargeMultiplier;
        [NMS(Index = 664)]
        /* 0x1A34 */ public int ShipTechInventoryMinHeightMax;
        [NMS(Index = 663)]
        /* 0x1A38 */ public int ShipTechInventoryMinWidthMax;
        [NMS(Index = 203)]
        /* 0x1A3C */ public float ShootableAggroForgetTime;
        [NMS(Index = 202)]
        /* 0x1A40 */ public float ShootableAggroMinStartTime;
        [NMS(Index = 201)]
        /* 0x1A44 */ public float ShootableArmourHighlightHitTime;
        [NMS(Index = 572)]
        /* 0x1A48 */ public float SimpleInteractionMessageTime;
        [NMS(Index = 570)]
        /* 0x1A4C */ public float SimpleInteractionPickupAngle;
        [NMS(Index = 571)]
        /* 0x1A50 */ public float SimpleInteractionPickupRadius;
        [NMS(Index = 60)]
        /* 0x1A54 */ public float SmugglerSellingMarkup;
        [NMS(Index = 299)]
        /* 0x1A58 */ public float SpaceBattleAnyHostileShipsRadius;
        [NMS(Index = 301)]
        /* 0x1A5C */ public int SpaceBattleMaxTorpedosTargetingLeaderShip;
        [NMS(Index = 302)]
        /* 0x1A60 */ public int SpaceBattleMaxTorpedosTargetingNonLeaderShip;
        [NMS(Index = 298)]
        /* 0x1A64 */ public float SpaceBattleRadius;
        [NMS(Index = 295)]
        /* 0x1A68 */ public float SpaceCombatRadius;
        [NMS(Index = 533)]
        /* 0x1A6C */ public float SpaceSpeedReadoutMultiplier;
        [NMS(Index = 652)]
        /* 0x1A70 */ public float SpawnerWeaponRange;
        [NMS(Index = 278)]
        /* 0x1A74 */ public int SpecialMiningAmount;
        [NMS(Index = 279)]
        /* 0x1A78 */ public float SpecialMiningTime;
        [NMS(Index = 39)]
        /* 0x1A7C */ public float SpecialTechSlotBonus;
        [NMS(Index = 532)]
        /* 0x1A80 */ public float SpeedReadoutMultiplier;
        [NMS(Index = 3)]
        /* 0x1A84 */ public float SpookMessageChance;
        [NMS(Index = 4)]
        /* 0x1A88 */ public float SpookMessageCooldown;
        [NMS(Index = 2)]
        /* 0x1A8C */ public float SpookMessageThreshold;
        [NMS(Index = 86)]
        /* 0x1A90 */ public float StormBonusJetpackMod;
        [NMS(Index = 90)]
        /* 0x1A94 */ public GcPlayerHazardType StormBonusJetpackWeather;
        [NMS(Index = 89)]
        /* 0x1A98 */ public float StormBonusMiningAmountMod;
        [NMS(Index = 93)]
        /* 0x1A9C */ public GcPlayerHazardType StormBonusMiningAmountWeather;
        [NMS(Index = 88)]
        /* 0x1AA0 */ public float StormBonusMiningHeatMod;
        [NMS(Index = 92)]
        /* 0x1AA4 */ public GcPlayerHazardType StormBonusMiningHeatWeather;
        [NMS(Index = 87)]
        /* 0x1AA8 */ public float StormBonusSprintMod;
        [NMS(Index = 91)]
        /* 0x1AAC */ public GcPlayerHazardType StormBonusSprintWeather;
        [NMS(Index = 85)]
        /* 0x1AB0 */ public float StormGravityChangeTime;
        [NMS(Index = 608)]
        /* 0x1AB4 */ public int SubstanceItemStockDecayTime;
        [NMS(Index = 609)]
        /* 0x1AB8 */ public int SubstanceItemStockReplenishTime;
        [NMS(Index = 151)]
        /* 0x1ABC */ public float SurveyBeginScanPercentage;
        [NMS(Index = 159)]
        /* 0x1AC0 */ public float SurveyMaxDistance;
        [NMS(Index = 153)]
        /* 0x1AC4 */ public float SurveyScanMaxSpeed;
        [NMS(Index = 152)]
        /* 0x1AC8 */ public float SurveyScanMinSpeed;
        [NMS(Index = 156)]
        /* 0x1ACC */ public float SurveySonarMaxPulseSpeed;
        [NMS(Index = 158)]
        /* 0x1AD0 */ public float SurveySonarMiddleRangeMax;
        [NMS(Index = 157)]
        /* 0x1AD4 */ public float SurveySonarMiddleRangeMin;
        [NMS(Index = 155)]
        /* 0x1AD8 */ public float SurveySonarMinPulseSpeed;
        [NMS(Index = 154)]
        /* 0x1ADC */ public float SurveySonarPulseSpeedBandCount;
        [NMS(Index = 245)]
        /* 0x1AE0 */ public float TapInteractionDecay;
        [NMS(Index = 244)]
        /* 0x1AE4 */ public float TapInteractionIncreaseAmount;
        [NMS(Index = 43)]
        /* 0x1AE8 */ public float TechDamageChanceShieldedMax;
        [NMS(Index = 45)]
        /* 0x1AEC */ public float TechDamageChanceShieldLevelMax;
        [NMS(Index = 44)]
        /* 0x1AF0 */ public float TechDamageChanceShieldLevelMin;
        [NMS(Index = 46)]
        /* 0x1AF4 */ public float TechDamageChanceToSelectPrimary;
        [NMS(Index = 83)]
        /* 0x1AF8 */ public GcTechnologyCategory TechnologyOverrideForSmuggler;
        [NMS(Index = 120)]
        /* 0x1AFC */ public float TeleporterDistanceTravelledMultiplier;
        [NMS(Index = 218)]
        /* 0x1B00 */ public float TerrainResourceCompassRange;
        [NMS(Index = 217)]
        /* 0x1B04 */ public float TerrainResourceScanRange;
        [NMS(Index = 216)]
        /* 0x1B08 */ public float TerrainResourceScanTime;
        [NMS(Index = 412)]
        /* 0x1B0C */ public float TimeToDisplayScanResults;
        [NMS(Index = 535)]
        /* 0x1B10 */ public int TorchCookieIndex;
        [NMS(Index = 538)]
        /* 0x1B14 */ public int TorchDimCookieIndex;
        [NMS(Index = 539)]
        /* 0x1B18 */ public float TorchDimFoV;
        [NMS(Index = 540)]
        /* 0x1B1C */ public float TorchDimStrength;
        [NMS(Index = 563)]
        /* 0x1B20 */ public float TorchFollowCameraPitch;
        [NMS(Index = 564)]
        /* 0x1B24 */ public float TorchFollowCameraTime;
        [NMS(Index = 536)]
        /* 0x1B28 */ public float TorchFoV;
        [NMS(Index = 558)]
        /* 0x1B2C */ public float TorchHandOffsetX;
        [NMS(Index = 559)]
        /* 0x1B30 */ public float TorchHandOffsetY;
        [NMS(Index = 560)]
        /* 0x1B34 */ public float TorchHandOffsetZ;
        [NMS(Index = 549)]
        /* 0x1B38 */ public float TorchOffsetX;
        [NMS(Index = 550)]
        /* 0x1B3C */ public float TorchOffsetY;
        [NMS(Index = 552)]
        /* 0x1B40 */ public float TorchOffsetZ;
        [NMS(Index = 551)]
        /* 0x1B44 */ public float TorchOffsetZMin;
        [NMS(Index = 557)]
        /* 0x1B48 */ public float TorchOffsetZMinSpeed;
        [NMS(Index = 556)]
        /* 0x1B4C */ public float TorchOffsetZSpeedRange;
        [NMS(Index = 562)]
        /* 0x1B50 */ public float TorchRotation;
        [NMS(Index = 553)]
        /* 0x1B54 */ public float TorchStaffOffsetY;
        [NMS(Index = 555)]
        /* 0x1B58 */ public float TorchStaffOffsetZ;
        [NMS(Index = 554)]
        /* 0x1B5C */ public float TorchStaffOffsetZMin;
        [NMS(Index = 537)]
        /* 0x1B60 */ public float TorchStrength;
        [NMS(Index = 114)]
        /* 0x1B64 */ public float TorpedoCooldown;
        [NMS(Index = 117)]
        /* 0x1B68 */ public float TorpedoCooldownRandomExtra;
        [NMS(Index = 116)]
        /* 0x1B6C */ public float TorpedoInitialWait;
        [NMS(Index = 118)]
        /* 0x1B70 */ public float TorpedoLaunchWarning;
        [NMS(Index = 377)]
        /* 0x1B74 */ public int TunnelStatReducer;
        [NMS(Index = 22)]
        /* 0x1B78 */ public float TurretAITargetingDistanceFactor;
        [NMS(Index = 26)]
        /* 0x1B7C */ public float TurretAITargetingFocusPlayerFactor;
        [NMS(Index = 25)]
        /* 0x1B80 */ public float TurretAITargetingLastHitFactor;
        [NMS(Index = 23)]
        /* 0x1B84 */ public float TurretAITargetingPitchAngleFactor;
        [NMS(Index = 24)]
        /* 0x1B88 */ public float TurretAITargetingYawAngleFactor;
        [NMS(Index = 543)]
        /* 0x1B8C */ public float UndergroundTorchFoV;
        [NMS(Index = 544)]
        /* 0x1B90 */ public float UndergroundTorchStrength;
        [NMS(Index = 547)]
        /* 0x1B94 */ public float UnderwaterAmbientLightStrength;
        [NMS(Index = 79)]
        /* 0x1B98 */ public float UnderwaterLightningDamageDistanceMultiplier;
        [NMS(Index = 545)]
        /* 0x1B9C */ public float UnderwaterTorchFoV;
        [NMS(Index = 546)]
        /* 0x1BA0 */ public float UnderwaterTorchStrengthMax;
        [NMS(Index = 548)]
        /* 0x1BA4 */ public float UnderwaterTorchVolumetricStrength;
        [NMS(Index = 99)]
        /* 0x1BA8 */ public float VehicleAllIconScanTime;
        [NMS(Index = 666)]
        /* 0x1BAC */ public int VehicleInventoryMinHeightMax;
        [NMS(Index = 665)]
        /* 0x1BB0 */ public int VehicleInventoryMinWidthMax;
        [NMS(Index = 617)]
        /* 0x1BB4 */ public float ViciousSentinelProbability;
        [NMS(Index = 618)]
        /* 0x1BB8 */ public float ViciousStormProbability;
        [NMS(Index = 619)]
        /* 0x1BBC */ public float ViciousWeatherProbability;
        [NMS(Index = 378)]
        /* 0x1BC0 */ public float VoxelAsteroidResourceChance;
        [NMS(Index = 296)]
        /* 0x1BC4 */ public int WarpsBetweenBattles;
        [NMS(Index = 591)]
        /* 0x1BC8 */ public int WarpsBetweenMiniStation;
        [NMS(Index = 333)]
        /* 0x1BCC */ public float WaterLandingDamageMultiplier;
        [NMS(Index = 372)]
        /* 0x1BD0 */ public float WaypointScanTime;
        [NMS(Index = 668)]
        /* 0x1BD4 */ public int WeaponInventoryMinHeightMax;
        [NMS(Index = 667)]
        /* 0x1BD8 */ public int WeaponInventoryMinWidthMax;
        [NMS(Index = 82)]
        /* 0x1BDC */ public float WeatherHazardGiftLifeTime;
        [NMS(Index = 81)]
        /* 0x1BE0 */ public float WeatherHazardGiftMaxDistance;
        [NMS(Index = 411)]
        /* 0x1BE4 */ public float ZoomFindBuildingRange;
        [NMS(Index = 394)]
        /* 0x1BE8 */ public NMSString0x20 DepotResource;
        [NMS(Index = 393)]
        /* 0x1C08 */ public NMSString0x20 RobotResource;
        [NMS(Index = 196)]
        /* 0x1C28 */ public bool AllowBinocularMarker;
        [NMS(Index = 195)]
        /* 0x1C29 */ public bool AllowDeconstruct;
        [NMS(Index = 165)]
        /* 0x1C2A */ public bool AllowSpaceScanEvents;
        [NMS(Index = 211)]
        /* 0x1C2B */ public TkCurveType AtmosphereEntryCurve;
        [NMS(Index = 460)]
        /* 0x1C2C */ public bool Autolevel;
        [NMS(Index = 219)]
        /* 0x1C2D */ public bool BinocsDisplayUnknownCreatures;
        [NMS(Index = 226)]
        /* 0x1C2E */ public TkCurveType BinocularScanTargetHeightCurve;
        [NMS(Index = 166)]
        /* 0x1C2F */ public bool CanMoveTechnology;
        [NMS(Index = 604)]
        /* 0x1C30 */ public bool ChangePricesLocally;
        [NMS(Index = 531)]
        /* 0x1C31 */ public bool DebugDrawTriggerBoxes;
        [NMS(Index = 38)]
        /* 0x1C32 */ public bool DebugPrintShootables;
        [NMS(Index = 442)]
        /* 0x1C33 */ public bool DebugScanForceBuilding;
        [NMS(Index = 443)]
        /* 0x1C34 */ public GcBuildingClassification DebugScanForceBuildingType;
        [NMS(Index = 194)]
        /* 0x1C35 */ public bool DisableAnomalyAmbientSpawn;
        [NMS(Index = 599)]
        /* 0x1C36 */ public bool DisableBasePowerHUDOverlay;
        [NMS(Index = 162)]
        /* 0x1C37 */ public bool DisableGalaxyMapInQuickMenu;
        [NMS(Index = 163)]
        /* 0x1C38 */ public bool DisableNexusInQuickMenu;
        [NMS(Index = 199)]
        /* 0x1C39 */ public bool EnableShootHitEffect;
        [NMS(Index = 41)]
        /* 0x1C3A */ public bool EnableTechPackaging;
        [NMS(Index = 164)]
        /* 0x1C3B */ public bool EnableTeleporters;
        [NMS(Index = 197)]
        /* 0x1C3C */ public bool EveryMaintenanceIsBroken;
        [NMS(Index = 332)]
        /* 0x1C3D */ public bool FailToFindBuildings;
        [NMS(Index = 193)]
        /* 0x1C3E */ public bool FreighterRequiresRGBHyperdriveUpgrades;
        [NMS(Index = 474)]
        /* 0x1C3F */ public bool HUDUpdateFromData;
        [NMS(Index = 192)]
        /* 0x1C40 */ public bool InstallTechWithInteraction;
        [NMS(Index = 272)]
        /* 0x1C41 */ public bool InteractionOverride;
        [NMS(Index = 600)]
        /* 0x1C42 */ public bool KeepAttackingMaxWantedLevel;
        [NMS(Index = 129)]
        /* 0x1C43 */ public bool MissionSurveySuggestSurveyForNearScannables;
        [NMS(Index = 181)]
        /* 0x1C44 */ public TkCurveType OverheatColourCurve;
        [NMS(Index = 180)]
        /* 0x1C45 */ public TkCurveType OverheatColourCurveShip;
        [NMS(Index = 176)]
        /* 0x1C46 */ public TkCurveType OverheatCurve;
        [NMS(Index = 516)]
        /* 0x1C47 */ public TkCurveType PainCurve;
        [NMS(Index = 492)]
        /* 0x1C48 */ public bool PlayHitEffectsOnZeroDamage;
        [NMS(Index = 13)]
        /* 0x1C49 */ public bool RewardInventoryClassItemFromShipSalvage;
        [NMS(Index = 28)]
        /* 0x1C4A */ public bool ScanToRevealIgnoreRequirements;
        [NMS(Index = 29)]
        /* 0x1C4B */ public bool ScanToRevealNPCsAlwaysTriggersCameraEvent;
        [NMS(Index = 446)]
        /* 0x1C4C */ public TkCurveType ShipScanCurve;
        [NMS(Index = 605)]
        /* 0x1C4D */ public bool ShowPriceMarkupsAsPercentages;
        [NMS(Index = 198)]
        /* 0x1C4E */ public bool SpawnResourceBlobsOnPartDelete;
        [NMS(Index = 37)]
        /* 0x1C4F */ public bool TechMustBeInTechInventory;
        [NMS(Index = 561)]
        /* 0x1C50 */ public bool TorchDisabledInTheDay;
        [NMS(Index = 655)]
        /* 0x1C51 */ public bool UseCustomSaveDirectory;
        [NMS(Index = 441)]
        /* 0x1C52 */ public bool UseDebugScan;
        [NMS(Index = 601)]
        /* 0x1C53 */ public bool UseMultiplierBonus;
        [NMS(Index = 177)]
        /* 0x1C54 */ public bool UseOverheatColour;
        [NMS(Index = 36)]
        /* 0x1C55 */ public bool UseSecondaryBiomeSubstances;
        [NMS(Index = 603)]
        /* 0x1C56 */ public bool UseTradingCostTable;
    }
}
