using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.Globals
{
    [NMS(GUID = 0x8477657FDD07B843, NameHash = 0xAA861200)]
    public class GcDebugOptions : NMSTemplate
    {
        [NMS(Index = 446)]
        /* 0x0000 */ public GcSeasonTransferInventoryConfig SeasonTransferInventoryConfigOverride;
        [NMS(Index = 209)]
        /* 0x0030 */ public GcFilename CrashDumpPath;
        [NMS(Index = 443)]
        /* 0x0040 */ public NMSString0x10 CreateSeasonContextMaskIdOverride;
        [NMS(Index = 328)]
        /* 0x0050 */ public GcFilename CursorTexture;
        [NMS(Index = 326)]
        /* 0x0060 */ public GcFilename DebugFont;
        [NMS(Index = 327)]
        /* 0x0070 */ public GcFilename DebugFontTexture;
        [NMS(Index = 108)]
        /* 0x0080 */ public GcFilename DebugScene;
        [NMS(Index = 361)]
        /* 0x0090 */ public NMSString0x10 DefaultAirCreatureTable;
        [NMS(Index = 363)]
        /* 0x00A0 */ public NMSString0x10 DefaultCaveCreatureTable;
        [NMS(Index = 360)]
        /* 0x00B0 */ public NMSString0x10 DefaultGroundCreatureTable;
        [NMS(Index = 180)]
        /* 0x00C0 */ public GcFilename DefaultSaveData;
        [NMS(Index = 362)]
        /* 0x00D0 */ public NMSString0x10 DefaultWaterCreatureTable;
        [NMS(Index = 452)]
        /* 0x00E0 */ public NMSString0x10 ForceBuilderMissionBoardMission;
        [NMS(Index = 179)]
        /* 0x00F0 */ public List<NMSString0x20> LocTableList;
        [NMS(Index = 329)]
        /* 0x0100 */ public GcFilename PauseTexture;
        [NMS(Index = 267)]
        /* 0x0110 */ public GcFilename PipelineFile;
        [NMS(Index = 269)]
        /* 0x0120 */ public GcFilename PipelineFileEditor;
        [NMS(Index = 268)]
        /* 0x0130 */ public GcFilename PipelineFileFrontend;
        [NMS(Index = 330)]
        /* 0x0140 */ public GcFilename PlayTexture;
        [NMS(Index = 177)]
        /* 0x0150 */ public GcFilename RealityPresetFile;
        [NMS(Index = 332)]
        /* 0x0160 */ public GcFilename RenderToTexture;
        [NMS(Index = 106)]
        /* 0x0170 */ public GcFilename SceneSettings;
        [NMS(Index = 331)]
        /* 0x0180 */ public GcFilename StepTexture;
        [NMS(Index = 444)]
        /* 0x0190 */ public NMSString0x10 SwitchSeasonContextMaskIdOverride;
        [NMS(Index = 207)]
        /* 0x01A0 */ public ulong ForceTimeToEpoch;
        [NMS(Index = 3)]
        /* 0x01A8 */ public ulong OverrideAbandonedFreighterSeed;
        [NMS(Index = 298)]
        /* 0x01B0 */ public ulong OverrideMatchmakingVersion;
        [NMS(Index = 427)]
        /* 0x01B8 */ public TkGlobals ToolkitGlobals;
        [NMS(Index = 191, MxmlName = "3dTextDistance")]
        /* 0x06AC */ public float _3dTextDistance;
        [NMS(Index = 192, MxmlName = "3dTextMinScale")]
        /* 0x06B0 */ public float _3dTextMinScale;
        [NMS(Index = 417)]
        /* 0x06B4 */ public GcBaseBuildingPartStyle AutomaticPartSpawnStyle;
        [NMS(Index = 412)]
        /* 0x06B8 */ public float BaseDownloadTimeout;
        [NMS(Index = 425)]
        /* 0x06BC */ public uint BasePayloadMultiplier;
        [NMS(Index = 10)]
        /* 0x06C0 */ public int BootDirectlyIntoSaveSlot;
        // size: 0x3
        public enum BootLoadDelayEnum : uint {
            LoadAll,
            WaitForPlanet,
            WaitForNothing,
        }
        [NMS(Index = 115)]
        /* 0x06C4 */ public BootLoadDelayEnum BootLoadDelay;
        [NMS(Index = 186)]
        /* 0x06C8 */ public float BootLogoFadeRate;
        // size: 0x7
        public enum BootModeEnum : uint {
            MinimalSolarSystem,
            SolarSystem,
            GalaxyMap,
            SmokeTest,
            SmokeTestGalaxyMap,
            Scratchpad,
            UnitTest,
        }
        [NMS(Index = 104)]
        /* 0x06CC */ public BootModeEnum BootMode;
        [NMS(Index = 170)]
        /* 0x06D0 */ public TkLanguages DebugLanguage;
        [NMS(Index = 217)]
        /* 0x06D4 */ public float DebugMenuAlpha;
        [NMS(Index = 219)]
        /* 0x06D8 */ public float DebugTextLineHeight;
        [NMS(Index = 218)]
        /* 0x06DC */ public float DebugTextSize;
        [NMS(Index = 319)]
        /* 0x06E0 */ public int DebugTextureSize;
        [NMS(Index = 291)]
        /* 0x06E4 */ public int DiscoveryAutoSyncIntervalSeconds;
        [NMS(Index = 374)]
        /* 0x06E8 */ public GcGalaxyStarAnomaly ForceAnomalyTo;
        [NMS(Index = 392)]
        /* 0x06EC */ public int ForceAsteroidSystemIndex;
        [NMS(Index = 351)]
        /* 0x06F0 */ public GcBiomeSubType ForceBiomeSubTypeTo;
        [NMS(Index = 350)]
        /* 0x06F4 */ public GcBiomeType ForceBiomeTo;
        [NMS(Index = 355)]
        /* 0x06F8 */ public GcAlienRace ForceBuildingRaceTo;
        [NMS(Index = 364)]
        /* 0x06FC */ public GcPlanetLife ForceCreatureLifeLevelTo;
        [NMS(Index = 385)]
        /* 0x0700 */ public int ForceGrassColourIndex;
        [NMS(Index = 119)]
        /* 0x0704 */ public float ForceInitialTimeOfDay;
        [NMS(Index = 165)]
        /* 0x0708 */ public int ForceInteractionIndex;
        [NMS(Index = 173)]
        /* 0x070C */ public GcAlienRace ForceInteractionRaceTo;
        [NMS(Index = 357)]
        /* 0x0710 */ public GcPlanetLife ForceLifeLevelTo;
        [NMS(Index = 166)]
        /* 0x0714 */ public GcAlienPuzzleCategory ForceNPCPuzzleCategory;
        [NMS(Index = 353)]
        /* 0x0718 */ public GcScreenFilters ForceScreenFilterTo;
        [NMS(Index = 377)]
        /* 0x071C */ public float ForceSeaLevel;
        [NMS(Index = 380)]
        /* 0x0720 */ public int ForceSkyColourIndex;
        [NMS(Index = 383)]
        /* 0x0724 */ public uint ForceSkyColourSeed;
        [NMS(Index = 37)]
        /* 0x0728 */ public GcSpaceBattleType ForceSpaceBattleType;
        [NMS(Index = 381)]
        /* 0x072C */ public int ForceSpaceSkyColourIndex;
        [NMS(Index = 348)]
        /* 0x0730 */ public GcGalaxyStarTypes ForceStarTypeTo;
        [NMS(Index = 121)]
        /* 0x0734 */ public float ForceSunAngle;
        [NMS(Index = 365)]
        /* 0x0738 */ public GcPlanetLife ForceTerrainSettings;
        [NMS(Index = 367)]
        /* 0x073C */ public TkVoxelGeneratorSettingsTypes ForceTerrainTypeTo;
        [NMS(Index = 118)]
        /* 0x0740 */ public float ForceTimeOfDay;
        [NMS(Index = 384)]
        /* 0x0744 */ public int ForceWaterColourIndex;
        [NMS(Index = 379)]
        /* 0x0748 */ public TkWaterCondition ForceWaterConditionTo;
        [NMS(Index = 376)]
        /* 0x074C */ public int ForceWaterObjectFileIndex;
        // size: 0x3
        public enum GameStateModeEnum : uint {
            LoadPreset,
            UserStorage,
            FreshStart,
        }
        [NMS(Index = 103)]
        /* 0x0750 */ public GameStateModeEnum GameStateMode;
        [NMS(Index = 401)]
        /* 0x0754 */ public float GenerateCostAngle;
        [NMS(Index = 400)]
        /* 0x0758 */ public float GenerateCostDistance;
        [NMS(Index = 402)]
        /* 0x075C */ public float GenerateCostLOD;
        [NMS(Index = 403)]
        /* 0x0760 */ public float GenerateCostWait;
        [NMS(Index = 398)]
        /* 0x0764 */ public int GenerateFarLodBuildingDist;
        [NMS(Index = 317)]
        /* 0x0768 */ public int MaxNumDebugMessages;
        [NMS(Index = 415)]
        /* 0x076C */ public int MoveBaseIndex;
        [NMS(Index = 439)]
        /* 0x0770 */ public int MultipleFingersSamePressFrameDelta;
        [NMS(Index = 12)]
        /* 0x0774 */ public GcGameMode NewSaveGameMode;
        [NMS(Index = 312)]
        /* 0x0778 */ public int OverrideCommunityMissionNumber;
        [NMS(Index = 308)]
        /* 0x077C */ public int OverrideServerSeasonEndTime;
        [NMS(Index = 307)]
        /* 0x0780 */ public int OverrideServerSeasonNumber;
        [NMS(Index = 442)]
        /* 0x0784 */ public float PanDeadzone;
        // size: 0x7
        public enum PlayerSpawnLocationOverrideEnum : uint {
            None,
            FromSettings,
            Space,
            SpaceStation,
            RandomPlanet,
            GameStartPlanet,
            SpecificLocation,
        }
        [NMS(Index = 105)]
        /* 0x0788 */ public PlayerSpawnLocationOverrideEnum PlayerSpawnLocationOverride;
        [NMS(Index = 322)]
        /* 0x078C */ public int ProceduralModelBatchSize;
        [NMS(Index = 323)]
        /* 0x0790 */ public int ProceduralModelFilterMatchretryCount;
        [NMS(Index = 321)]
        /* 0x0794 */ public int ProceduralModelsShown;
        [NMS(Index = 325)]
        /* 0x0798 */ public int ProceduralModelsThumbnailSize;
        [NMS(Index = 419)]
        /* 0x079C */ public int ProfilerPartIndexPhase;
        [NMS(Index = 420)]
        /* 0x07A0 */ public int ProfilerPartIndexStride;
        [NMS(Index = 421)]
        /* 0x07A4 */ public int ProfilerPartIteration;
        // size: 0x3
        public enum ProxyTypeEnum : uint {
            None,
            ManualURI,
            InetProxy,
        }
        [NMS(Index = 285)]
        /* 0x07A8 */ public ProxyTypeEnum ProxyType;
        // size: 0x2
        public enum RealityModeEnum : uint {
            LoadPreset,
            Generate,
        }
        [NMS(Index = 174)]
        /* 0x07AC */ public RealityModeEnum RealityMode;
        // size: 0x3
        public enum RecordSettingEnum : uint {
            None,
            Record,
            Playback,
        }
        [NMS(Index = 193)]
        /* 0x07B0 */ public RecordSettingEnum RecordSetting;
        [NMS(Index = 206)]
        /* 0x07B4 */ public int RecurrenceTimeOffset;
        [NMS(Index = 438)]
        /* 0x07B8 */ public int ScreenshotForUploadHeight;
        [NMS(Index = 437)]
        /* 0x07BC */ public int ScreenshotForUploadWidth;
        // size: 0x9
        public enum ServerEnvEnum : uint {
            Default,
            dev,
            qa,
            prodqa,
            prod,
            custom,
            pentest,
            merged,
            local,
        }
        [NMS(Index = 287)]
        /* 0x07C0 */ public ServerEnvEnum ServerEnv;
        // size: 0x2
        public enum ShaderPreloadEnum : uint {
            Off,
            Full,
        }
        [NMS(Index = 111)]
        /* 0x07C4 */ public ShaderPreloadEnum ShaderPreload;
        [NMS(Index = 229)]
        /* 0x07C8 */ public int ShowSpecificGraph;
        [NMS(Index = 245)]
        /* 0x07CC */ public int SmokeTestConfigCaptureCycles;
        [NMS(Index = 246)]
        /* 0x07D0 */ public float SmokeTestConfigCaptureDurationInSeconds;
        [NMS(Index = 247)]
        /* 0x07D4 */ public int SmokeTestConfigCaptureFolderNameNumberOffset;
        [NMS(Index = 238)]
        /* 0x07D8 */ public int SmokeTestConfigPlanetPositionCount;
        [NMS(Index = 240)]
        /* 0x07DC */ public float SmokeTestConfigScenarioLength;
        [NMS(Index = 239)]
        /* 0x07E0 */ public float SmokeTestConfigScenarioPreambleLength;
        // size: 0x7
        public enum SmokeTestCycleModeEnum : uint {
            None,
            TourPlanet,
            TourSolarSystem,
            TourGalaxy,
            TourUDAs,
            TourShortUDAs,
            TourRandomWarps,
        }
        [NMS(Index = 235)]
        /* 0x07E4 */ public SmokeTestCycleModeEnum SmokeTestCycleMode;
        // size: 0xA
        public enum SmokeTestScenarioEnum : uint {
            None,
            TerrainSnapShotFromAltitude,
            BelowCloudLayerSnapShot,
            Flying,
            UltraBiomeSnapShot,
            Walking,
            LeakDetector,
            WalkingSnapshot,
            ModelLoading,
            SettlementSnapshot,
        }
        [NMS(Index = 236)]
        /* 0x07E8 */ public SmokeTestScenarioEnum SmokeTestScenario;
        [NMS(Index = 251)]
        /* 0x07EC */ public int SmokeTestSmokeBotTargetWarps;
        // size: 0x2
        public enum SolarSystemBootEnum : uint {
            FromSettings,
            Generate,
        }
        [NMS(Index = 110)]
        /* 0x07F0 */ public SolarSystemBootEnum SolarSystemBoot;
        [NMS(Index = 272)]
        /* 0x07F4 */ public float SunLightScaleGgx;
        [NMS(Index = 440)]
        /* 0x07F8 */ public int SwipeDetectionMaxFrames;
        [NMS(Index = 441)]
        /* 0x07FC */ public float SwipeDetectionNormalizedTravelThreshold;
        [NMS(Index = 315)]
        /* 0x0800 */ public int SynergyPort;
        // size: 0x3
        public enum UseBanksEnum : uint {
            False,
            True,
            Default,
        }
        [NMS(Index = 200)]
        /* 0x0804 */ public UseBanksEnum UseBanks;
        [NMS(Index = 188)]
        /* 0x0808 */ public float WeaponScale3P;
        [NMS(Index = 178)]
        /* 0x080C */ public ushort RealityGenerationIteration;
        [NMS(Index = 294)]
        /* 0x080E */ public NMSString0x800 AutoJoinUserNames;
        [NMS(Index = 79)]
        /* 0x100E */ public NMSString0x400 DebugTwitchRewards;
        [NMS(Index = 299)]
        /* 0x140E */ public NMSString0x200 LoadToBase;
        [NMS(Index = 75)]
        /* 0x160E */ public NMSString0x200 SeasonalDataOverrideFile;
        [NMS(Index = 309)]
        /* 0x180E */ public NMSString0x100 ForceHgAccount;
        [NMS(Index = 100)]
        /* 0x190E */ public NMSString0x100 ForcePlayerPosition;
        [NMS(Index = 99)]
        /* 0x1A0E */ public NMSString0x100 ForceUniverseAddress;
        [NMS(Index = 302)]
        /* 0x1B0E */ public NMSString0x100 GOGLogin;
        [NMS(Index = 225)]
        /* 0x1C0E */ public NMSString0x100 ShowUniverseAddressOnGalaxyMap;
        [NMS(Index = 107)]
        /* 0x1D0E */ public NMSString0x100 WorkingDirectory;
        [NMS(Index = 288)]
        /* 0x1E0E */ public NMSString0x80 AuthBaseUrl;
        [NMS(Index = 286)]
        /* 0x1E8E */ public NMSString0x80 ProxyURI;
        [NMS(Index = 310)]
        /* 0x1F0E */ public NMSString0x40 ForceBaseDownloadUser;
        [NMS(Index = 430)]
        /* 0x1F4E */ public NMSString0x40 OverrideSettlementOwnershipOnlineId;
        [NMS(Index = 429)]
        /* 0x1F8E */ public NMSString0x40 OverrideSettlementOwnershipUsername;
        [NMS(Index = 436)]
        /* 0x1FCE */ public NMSString0x40 ScreenshotForUploadName;
        [NMS(Index = 171)]
        /* 0x200E */ public NMSString0x20 AllowedLanguagesFile;
        [NMS(Index = 416)]
        /* 0x202E */ public NMSString0x20 AutomaticPartSpawnID;
        [NMS(Index = 424)]
        /* 0x204E */ public NMSString0x20 BaseServerPlatform;
        [NMS(Index = 210)]
        /* 0x206E */ public NMSString0x20 CrashDumpIdentifier;
        [NMS(Index = 290)]
        /* 0x208E */ public NMSString0x20 OverrideUsernameForDev;
        [NMS(Index = 199)]
        /* 0x20AE */ public NMSString0x20 SaveTestingCommand;
        [NMS(Index = 253)]
        /* 0x20CE */ public NMSString0x20 SmokeTestForcePlanetDetail;
        [NMS(Index = 254)]
        /* 0x20EE */ public NMSString0x20 SmokeTestRunFolder;
        [NMS(Index = 314)]
        /* 0x210E */ public NMSString0x20 SynergyServer;
        [NMS(Index = 449)]
        /* 0x212E */ public bool ActiveMissionsIgnoreStartCancelConditions;
        [NMS(Index = 125)]
        /* 0x212F */ public bool AllowGalaxyMapRequests;
        [NMS(Index = 411)]
        /* 0x2130 */ public bool AllowGlobalPartSnapping;
        [NMS(Index = 278)]
        /* 0x2131 */ public bool AllowMultiThreadedRenderingOnVulkan;
        [NMS(Index = 341)]
        /* 0x2132 */ public bool AllowNGuiVR;
        [NMS(Index = 428)]
        /* 0x2133 */ public bool AllowOverrideSettlementOwnership;
        [NMS(Index = 80)]
        /* 0x2134 */ public bool AllowPause;
        [NMS(Index = 96)]
        /* 0x2135 */ public bool AllowPetBattlerPvEOnPvPTable;
        [NMS(Index = 145)]
        /* 0x2136 */ public bool AllowRobotBehaviors;
        [NMS(Index = 1)]
        /* 0x2137 */ public bool AllowSavingOnAbandonedFreighters;
        [NMS(Index = 152)]
        /* 0x2138 */ public bool AllSeasonMilestonesShowComplete;
        [NMS(Index = 81)]
        /* 0x2139 */ public bool AllSettlementsAreCompleted;
        [NMS(Index = 66)]
        /* 0x213A */ public bool AlternateControls;
        [NMS(Index = 74)]
        /* 0x213B */ public bool AlwaysAllowFreighterInventoryAccess;
        [NMS(Index = 72)]
        /* 0x213C */ public bool AlwaysAllowShipOperations;
        [NMS(Index = 142)]
        /* 0x213D */ public bool AlwaysAllowSpookFiends;
        [NMS(Index = 73)]
        /* 0x213E */ public bool AlwaysAllowVehicleOperations;
        [NMS(Index = 18)]
        /* 0x213F */ public bool AlwaysHaveFocus;
        [NMS(Index = 283)]
        /* 0x2140 */ public bool AlwaysIncludeLocalPlayerInChatMessage;
        [NMS(Index = 295)]
        /* 0x2141 */ public bool AlwaysSaveGameAsClient;
        [NMS(Index = 227)]
        /* 0x2142 */ public bool AlwaysShowSaveIds;
        [NMS(Index = 67)]
        /* 0x2143 */ public bool AlwaysShowURI;
        [NMS(Index = 36)]
        /* 0x2144 */ public bool AlwaysSpaceBattle;
        [NMS(Index = 450)]
        /* 0x2145 */ public bool AssertIfDiploFound;
        [NMS(Index = 301)]
        /* 0x2146 */ public bool AutoJoinRandomGames;
        [NMS(Index = 293)]
        /* 0x2147 */ public bool AutoJoinUserEnabled;
        [NMS(Index = 418)]
        /* 0x2148 */ public bool AutomaticPartSpawnInactive;
        [NMS(Index = 423)]
        /* 0x2149 */ public bool BaseAdmin;
        [NMS(Index = 85)]
        /* 0x214A */ public bool BlockCommunicatorSignals;
        [NMS(Index = 431)]
        /* 0x214B */ public bool BlockSettlementsNetwork;
        [NMS(Index = 38)]
        /* 0x214C */ public bool BlockSpaceBattle;
        [NMS(Index = 335)]
        /* 0x214D */ public bool BodyTurning;
        [NMS(Index = 11)]
        /* 0x214E */ public bool BootDirectlyIntoLastSave;
        [NMS(Index = 187)]
        /* 0x214F */ public bool BootMusic;
        [NMS(Index = 28)]
        /* 0x2150 */ public bool CanLeaveDialogs;
        [NMS(Index = 289)]
        /* 0x2151 */ public bool CertificateSecurityBypass;
        [NMS(Index = 197)]
        /* 0x2152 */ public bool CheckForMissingLocStrings;
        [NMS(Index = 53)]
        /* 0x2153 */ public bool ClothForceAsyncSimulationOff;
        [NMS(Index = 54)]
        /* 0x2154 */ public bool ClothForceAsyncSimulationOn;
        [NMS(Index = 52)]
        /* 0x2155 */ public bool ClothForcePositionExtrapolationAntiSyncWithFpsLock;
        [NMS(Index = 49)]
        /* 0x2156 */ public bool ClothForcePositionExtrapolationBackOn;
        [NMS(Index = 47)]
        /* 0x2157 */ public bool ClothForcePositionExtrapolationOff;
        [NMS(Index = 48)]
        /* 0x2158 */ public bool ClothForcePositionExtrapolationOn;
        [NMS(Index = 51)]
        /* 0x2159 */ public bool ClothForcePositionExtrapolationSyncWithFpsLock;
        [NMS(Index = 50)]
        /* 0x215A */ public bool ClothForcePositionExtrapolationUpdateOrderDependent;
        [NMS(Index = 259)]
        /* 0x215B */ public bool CompressTextures;
        [NMS(Index = 208)]
        /* 0x215C */ public bool CrashDumpFull;
        [NMS(Index = 91)]
        /* 0x215D */ public bool CrashOnF10;
        [NMS(Index = 255)]
        /* 0x215E */ public bool CreatureChatter;
        [NMS(Index = 257)]
        /* 0x215F */ public bool CreatureDrawVocals;
        [NMS(Index = 256)]
        /* 0x2160 */ public bool CreatureErrors;
        [NMS(Index = 426)]
        /* 0x2161 */ public bool CrossPlatformFeaturedBases;
        [NMS(Index = 405)]
        /* 0x2162 */ public bool DChecksEnabled;
        [NMS(Index = 407)]
        /* 0x2163 */ public bool DChecksOutputBinary;
        [NMS(Index = 408)]
        /* 0x2164 */ public bool DChecksOutputFileLine;
        [NMS(Index = 406)]
        /* 0x2165 */ public bool DChecksOutputJson;
        [NMS(Index = 194)]
        /* 0x2166 */ public bool DebugBuildingSpawns;
        [NMS(Index = 264)]
        /* 0x2167 */ public bool DebugDepthReprojection;
        [NMS(Index = 163)]
        /* 0x2168 */ public bool DebugDrawPlayerInteract;
        [NMS(Index = 20)]
        /* 0x2169 */ public bool DebugGalaxyMapInQuickMenu;
        [NMS(Index = 260)]
        /* 0x216A */ public bool DebugIBL;
        [NMS(Index = 198)]
        /* 0x216B */ public bool DebugNetworkLocks;
        [NMS(Index = 175)]
        /* 0x216C */ public bool DebugPersistentInteractions;
        [NMS(Index = 263)]
        /* 0x216D */ public bool DebugRenderSpaceOffset;
        [NMS(Index = 261)]
        /* 0x216E */ public bool DebugSpotlights;
        [NMS(Index = 262)]
        /* 0x216F */ public bool DebugTerrainTextures;
        [NMS(Index = 211)]
        /* 0x2170 */ public bool DebugThreatLevels;
        [NMS(Index = 399)]
        /* 0x2171 */ public bool DeferRegionBodies;
        [NMS(Index = 2)]
        /* 0x2172 */ public bool DisableAbandonedFreighterRoomsOptimisation;
        [NMS(Index = 409)]
        /* 0x2173 */ public bool DisableBaseBuilding;
        [NMS(Index = 410)]
        /* 0x2174 */ public bool DisableBaseBuildingLimits;
        [NMS(Index = 413)]
        /* 0x2175 */ public bool DisableBasePowerRequirements;
        [NMS(Index = 277)]
        /* 0x2176 */ public bool DisableClouds;
        [NMS(Index = 42)]
        /* 0x2177 */ public bool DisableContinuousSaving;
        [NMS(Index = 422)]
        /* 0x2178 */ public bool DisableCorvetteSwapParts;
        [NMS(Index = 92)]
        /* 0x2179 */ public bool DisableCorvetteValidation;
        [NMS(Index = 158)]
        /* 0x217A */ public bool DisableDebugControls;
        [NMS(Index = 162)]
        /* 0x217B */ public bool DisableDiscoveryNaming;
        [NMS(Index = 205)]
        /* 0x217C */ public bool DisableFileWatcher;
        [NMS(Index = 266)]
        /* 0x217D */ public bool DisableGlowEffect;
        [NMS(Index = 27)]
        /* 0x217E */ public bool DisableHazards;
        [NMS(Index = 339)]
        /* 0x217F */ public bool DisableHeadConstraints;
        [NMS(Index = 43)]
        /* 0x2180 */ public bool DisableInvalidSaveVersion;
        [NMS(Index = 338)]
        /* 0x2181 */ public bool DisableLeftHand;
        [NMS(Index = 393)]
        /* 0x2182 */ public bool DisableLimits;
        [NMS(Index = 451)]
        /* 0x2183 */ public bool DisableMissionShop;
        [NMS(Index = 296)]
        /* 0x2184 */ public bool DisableMonumentDownloads;
        [NMS(Index = 63, MxmlName = "DisableNPCHiddenUntilScanned ")]
        /* 0x2185 */ public bool DisableNPCHiddenUntilScanned;
        [NMS(Index = 62)]
        /* 0x2186 */ public bool DisableNPCs;
        [NMS(Index = 433)]
        /* 0x2187 */ public bool DisablePartialStories;
        [NMS(Index = 97)]
        /* 0x2188 */ public bool DisablePetBattlerSpectatorWelcomeMessageForSwitch2;
        [NMS(Index = 204)]
        /* 0x2189 */ public bool DisableProfanityFilter;
        [NMS(Index = 40)]
        /* 0x218A */ public bool DisableSaveSlotSorting;
        [NMS(Index = 311)]
        /* 0x218B */ public bool DisableSaveUploadRateLimits;
        [NMS(Index = 41)]
        /* 0x218C */ public bool DisableSaving;
        [NMS(Index = 82)]
        /* 0x218D */ public bool DisableSettlements;
        [NMS(Index = 265)]
        /* 0x218E */ public bool DisableShadowSwitching;
        [NMS(Index = 182)]
        /* 0x218F */ public bool DisableShipSaveDataRecovery;
        [NMS(Index = 292)]
        /* 0x2190 */ public bool DisableSpaceStationSpawnOnJoin;
        [NMS(Index = 44)]
        /* 0x2191 */ public bool DisableStorms;
        [NMS(Index = 19)]
        /* 0x2192 */ public bool DisableVibration;
        [NMS(Index = 172)]
        /* 0x2193 */ public bool DoAlienLanguage;
        [NMS(Index = 258)]
        /* 0x2194 */ public bool DrawCreaturesInRoutines;
        [NMS(Index = 212)]
        /* 0x2195 */ public bool DumpManifestContents;
        [NMS(Index = 432)]
        /* 0x2196 */ public bool EnableAccessibleUI;
        [NMS(Index = 155)]
        /* 0x2197 */ public bool EnableBaseBuildingExpandables;
        [NMS(Index = 414)]
        /* 0x2198 */ public bool EnableBaseMovingOption;
        [NMS(Index = 132)]
        /* 0x2199 */ public bool EnableCloudAnimation;
        [NMS(Index = 273)]
        /* 0x219A */ public bool EnableComputePost;
        [NMS(Index = 141)]
        /* 0x219B */ public bool EnableDayNightCycle;
        [NMS(Index = 109)]
        /* 0x219C */ public bool EnableDebugSceneAutoSave;
        [NMS(Index = 123)]
        /* 0x219D */ public bool EnableFrontendPreload;
        [NMS(Index = 153)]
        /* 0x219E */ public bool EnableGalaxyRecolouring;
        [NMS(Index = 156)]
        /* 0x219F */ public bool EnableGameTables;
        [NMS(Index = 271)]
        /* 0x21A0 */ public bool EnableGgx;
        [NMS(Index = 84)]
        /* 0x21A1 */ public bool EnableMemoryPoolAllocPrint;
        [NMS(Index = 94)]
        /* 0x21A2 */ public bool EnablePetBattlerRandomPets;
        [NMS(Index = 95)]
        /* 0x21A3 */ public bool EnablePetBattlerTurnTimeoutInPvE;
        [NMS(Index = 340)]
        /* 0x21A4 */ public bool EnablePhotomodeVR;
        [NMS(Index = 313)]
        /* 0x21A5 */ public bool EnableSynergy;
        [NMS(Index = 87)]
        /* 0x21A6 */ public bool EnableTouchScreenDebugging;
        [NMS(Index = 93)]
        /* 0x21A7 */ public bool EnforceCorvetteComplexityLimit;
        [NMS(Index = 23)]
        /* 0x21A8 */ public bool EverythingIsFree;
        [NMS(Index = 24)]
        /* 0x21A9 */ public bool EverythingIsKnown;
        [NMS(Index = 25)]
        /* 0x21AA */ public bool EverythingIsStar;
        [NMS(Index = 304)]
        /* 0x21AB */ public bool FakeHandsInMultiplayer;
        [NMS(Index = 39)]
        /* 0x21AC */ public bool FastAndFrequentFleetInterventions;
        [NMS(Index = 9)]
        /* 0x21AD */ public bool FastLoad;
        [NMS(Index = 159)]
        /* 0x21AE */ public bool FixedFramerate;
        [NMS(Index = 190)]
        /* 0x21AF */ public bool FleetDirectorAutoMode;
        [NMS(Index = 448)]
        /* 0x21B0 */ public bool ForceAllExhibitsToBeEditable;
        [NMS(Index = 185)]
        /* 0x21B1 */ public bool ForceBasicLoadScreen;
        [NMS(Index = 372)]
        /* 0x21B2 */ public bool ForceBinaryStar;
        [NMS(Index = 349)]
        /* 0x21B3 */ public bool ForceBiome;
        [NMS(Index = 65)]
        /* 0x21B4 */ public bool ForceBuildersAlwaysKnown;
        [NMS(Index = 354)]
        /* 0x21B5 */ public bool ForceBuildingRace;
        [NMS(Index = 345)]
        /* 0x21B6 */ public bool ForceCorruptSentinels;
        [NMS(Index = 358)]
        /* 0x21B7 */ public bool ForceCreatureLifeLevel;
        [NMS(Index = 359)]
        /* 0x21B8 */ public bool ForceDefaultCreatureFile;
        [NMS(Index = 46)]
        /* 0x21B9 */ public bool ForceDisableClothComponent;
        [NMS(Index = 59)]
        /* 0x21BA */ public bool ForceDisableNonPlayerRagdollComponents;
        [NMS(Index = 58)]
        /* 0x21BB */ public bool ForceDisableRagdollComponent;
        [NMS(Index = 61)]
        /* 0x21BC */ public bool ForceDisableSeparatePhysicsWorlds;
        [NMS(Index = 60)]
        /* 0x21BD */ public bool ForceDisableSplitIkOptimisation;
        [NMS(Index = 56)]
        /* 0x21BE */ public bool ForceDisableSpringComponent;
        [NMS(Index = 45)]
        /* 0x21BF */ public bool ForceEnableClothComponent;
        [NMS(Index = 57)]
        /* 0x21C0 */ public bool ForceEnableRagdollComponent;
        [NMS(Index = 55)]
        /* 0x21C1 */ public bool ForceEnableSpringComponent;
        [NMS(Index = 344)]
        /* 0x21C2 */ public bool ForceExtremeSentinels;
        [NMS(Index = 343)]
        /* 0x21C3 */ public bool ForceExtremeWeather;
        [NMS(Index = 117)]
        /* 0x21C4 */ public bool ForceFrontendPersonalityTest;
        [NMS(Index = 90)]
        /* 0x21C5 */ public bool ForceFullFeatureMode;
        [NMS(Index = 370)]
        /* 0x21C6 */ public bool ForceGasGiantSystem;
        [NMS(Index = 369)]
        /* 0x21C7 */ public bool ForceGiantSystem;
        [NMS(Index = 101)]
        /* 0x21C8 */ public bool ForceInitialShip;
        [NMS(Index = 102)]
        /* 0x21C9 */ public bool ForceInitialWeapon;
        [NMS(Index = 164)]
        /* 0x21CA */ public bool ForceInteractionToSettings;
        [NMS(Index = 356)]
        /* 0x21CB */ public bool ForceLifeLevel;
        [NMS(Index = 347)]
        /* 0x21CC */ public bool ForceLoadAllWeather;
        [NMS(Index = 151)]
        /* 0x21CD */ public bool ForceNexusInQuickMenu;
        [NMS(Index = 368)]
        /* 0x21CE */ public bool ForcePirateSystem;
        [NMS(Index = 388)]
        /* 0x21CF */ public bool ForcePlanetsToHaveNoCaves;
        [NMS(Index = 389)]
        /* 0x21D0 */ public bool ForcePlanetsToHaveNoNoiseLayers;
        [NMS(Index = 390)]
        /* 0x21D1 */ public bool ForcePlanetsToHaveNoTerrainFeatures;
        [NMS(Index = 387)]
        /* 0x21D2 */ public bool ForcePlanetsToHaveNoWater;
        [NMS(Index = 375)]
        /* 0x21D3 */ public bool ForcePlanetsToHaveWater;
        [NMS(Index = 371)]
        /* 0x21D4 */ public bool ForcePrimeTerrain;
        [NMS(Index = 184)]
        /* 0x21D5 */ public bool ForcePurpleSystemsToAlwaysBirth;
        [NMS(Index = 183)]
        /* 0x21D6 */ public bool ForcePurpleSystemsVisibleOnLoad;
        [NMS(Index = 391)]
        /* 0x21D7 */ public bool ForceRareAsteroidSystem;
        [NMS(Index = 453)]
        /* 0x21D8 */ public bool ForceScanEventsToGoPrime;
        [NMS(Index = 454)]
        /* 0x21D9 */ public bool ForceScanEventsToSpecificGrassColour;
        [NMS(Index = 342)]
        /* 0x21DA */ public bool ForceScrapWorlds;
        [NMS(Index = 352)]
        /* 0x21DB */ public bool ForceScreenFilter;
        [NMS(Index = 305)]
        /* 0x21DC */ public bool ForceSmallLobby;
        [NMS(Index = 382)]
        /* 0x21DD */ public bool ForceSpaceSkyColourRare;
        [NMS(Index = 346)]
        /* 0x21DE */ public bool ForceStarType;
        [NMS(Index = 120)]
        /* 0x21DF */ public bool ForceSunDirectionFromPhotoMode;
        [NMS(Index = 373)]
        /* 0x21E0 */ public bool ForceTernaryStar;
        [NMS(Index = 366)]
        /* 0x21E1 */ public bool ForceTerrainType;
        [NMS(Index = 89)]
        /* 0x21E2 */ public bool ForceTgaDlc;
        [NMS(Index = 306)]
        /* 0x21E3 */ public bool ForceTinyLobby;
        [NMS(Index = 33)]
        /* 0x21E4 */ public bool ForceTranslateAllAlienText;
        [NMS(Index = 378)]
        /* 0x21E5 */ public bool ForceWaterCondition;
        [NMS(Index = 181)]
        /* 0x21E6 */ public bool FormatDownloadStorageAreaOnBoot;
        [NMS(Index = 15)]
        /* 0x21E7 */ public bool GodMode;
        [NMS(Index = 230)]
        /* 0x21E8 */ public bool GraphCommandBuffer;
        [NMS(Index = 232)]
        /* 0x21E9 */ public bool GraphFPS;
        [NMS(Index = 231)]
        /* 0x21EA */ public bool GraphGeneration;
        [NMS(Index = 233)]
        /* 0x21EB */ public bool GraphTexStreaming;
        [NMS(Index = 201)]
        /* 0x21EC */ public bool HangOnCrash;
        [NMS(Index = 333)]
        /* 0x21ED */ public bool HmdFrameShiftEnabled;
        [NMS(Index = 334)]
        /* 0x21EE */ public bool HmdUseSolidGuiPointer;
        [NMS(Index = 202)]
        /* 0x21EF */ public bool HotReloadModGlobals;
        [NMS(Index = 34)]
        /* 0x21F0 */ public bool IgnoreFreighterSpawnWarpRequirement;
        [NMS(Index = 455)]
        /* 0x21F1 */ public bool IgnoreMissionRank;
        [NMS(Index = 154)]
        /* 0x21F2 */ public bool IgnoreSteamDev;
        [NMS(Index = 300)]
        /* 0x21F3 */ public bool IgnoreTransactionTimeouts;
        [NMS(Index = 168)]
        /* 0x21F4 */ public bool InfiniteInteractions;
        [NMS(Index = 26)]
        /* 0x21F5 */ public bool InfiniteStamina;
        [NMS(Index = 148)]
        /* 0x21F6 */ public bool InstanceCollision;
        [NMS(Index = 167)]
        /* 0x21F7 */ public bool InteractionsAllwaysGivesTech;
        [NMS(Index = 397)]
        /* 0x21F8 */ public bool LimitGlobalBodies;
        [NMS(Index = 396)]
        /* 0x21F9 */ public bool LimitGlobalInstances;
        [NMS(Index = 395)]
        /* 0x21FA */ public bool LimitPerRegionBodies;
        [NMS(Index = 394)]
        /* 0x21FB */ public bool LimitPerRegionInstances;
        [NMS(Index = 276)]
        /* 0x21FC */ public bool LoadShaderSourceIfRenderdocEnabled;
        [NMS(Index = 71)]
        /* 0x21FD */ public bool LockAllTitles;
        [NMS(Index = 189)]
        /* 0x21FE */ public bool LogMissingLocalisedText;
        [NMS(Index = 22)]
        /* 0x21FF */ public bool MapWarpCheckIgnoreDrive;
        [NMS(Index = 21)]
        /* 0x2200 */ public bool MapWarpCheckIgnoreFuel;
        [NMS(Index = 35)]
        /* 0x2201 */ public bool MaximumFreighterSpawns;
        [NMS(Index = 116)]
        /* 0x2202 */ public bool MemCsv;
        [NMS(Index = 456)]
        /* 0x2203 */ public bool MissionMessageLoggingEnabled;
        [NMS(Index = 457)]
        /* 0x2204 */ public bool MissionNGUIShowsConditionResults;
        [NMS(Index = 458)]
        /* 0x2205 */ public bool MissionNGUIShowsTableNames;
        [NMS(Index = 459)]
        /* 0x2206 */ public bool MissionSurveyEnabled;
        [NMS(Index = 386)]
        /* 0x2207 */ public bool ModifyPlanetsInInitialSystems;
        [NMS(Index = 460)]
        /* 0x2208 */ public bool MPMissions;
        [NMS(Index = 461)]
        /* 0x2209 */ public bool MPMissionsAlwaysEPIC;
        [NMS(Index = 150)]
        /* 0x220A */ public bool MultiplePlayerFreightersInASystem;
        [NMS(Index = 157)]
        /* 0x220B */ public bool NexusBots;
        [NMS(Index = 83)]
        /* 0x220C */ public bool PlaceOnGroundWhenLeavingDebugCamera;
        [NMS(Index = 318)]
        /* 0x220D */ public bool PreloadToolbox;
        [NMS(Index = 220)]
        /* 0x220E */ public bool PrintAvgFrameTimes;
        [NMS(Index = 324)]
        /* 0x220F */ public bool ProceduralModelsDeterministicSequence;
        [NMS(Index = 274)]
        /* 0x2210 */ public bool Proto2DevKit;
        [NMS(Index = 303)]
        /* 0x2211 */ public bool RecordNetworkStatsOnBoot;
        [NMS(Index = 31)]
        /* 0x2212 */ public bool RenderCreatureDetails;
        [NMS(Index = 161)]
        /* 0x2213 */ public bool RenderHud;
        [NMS(Index = 270)]
        /* 0x2214 */ public bool RenderLowFramerate;
        [NMS(Index = 122)]
        /* 0x2215 */ public bool ResetForcedSaveSlotOnLoad;
        [NMS(Index = 98)]
        /* 0x2216 */ public bool ResetToSupportedResolution;
        [NMS(Index = 69)]
        /* 0x2217 */ public bool RevealAllTitles;
        [NMS(Index = 203)]
        /* 0x2218 */ public bool SaveOutModdedMetadata;
        [NMS(Index = 447)]
        /* 0x2219 */ public bool ScratchpadPlanetEnvironment;
        [NMS(Index = 160)]
        /* 0x221A */ public bool ScreenshotMode;
        [NMS(Index = 114)]
        /* 0x221B */ public bool ShaderCaching;
        [NMS(Index = 112)]
        /* 0x221C */ public bool ShaderPreloadListExport;
        [NMS(Index = 113)]
        /* 0x221D */ public bool ShaderPreloadListImport;
        [NMS(Index = 64)]
        /* 0x221E */ public bool ShipSalvageGivesAllParts;
        [NMS(Index = 213)]
        /* 0x221F */ public bool ShowDebugMessages;
        [NMS(Index = 216)]
        /* 0x2220 */ public bool ShowDynamicResScale;
        [NMS(Index = 316)]
        /* 0x2221 */ public bool ShowEditorPlacementPreview;
        [NMS(Index = 68)]
        /* 0x2222 */ public bool ShowFireteamMembersUA;
        [NMS(Index = 214)]
        /* 0x2223 */ public bool ShowFramerate;
        [NMS(Index = 222)]
        /* 0x2224 */ public bool ShowGPUMemory;
        [NMS(Index = 215)]
        /* 0x2225 */ public bool ShowGPURenderTime;
        [NMS(Index = 228)]
        /* 0x2226 */ public bool ShowGraphs;
        [NMS(Index = 336)]
        /* 0x2227 */ public bool ShowHmdHandControllers;
        [NMS(Index = 196)]
        /* 0x2228 */ public bool ShowLongestStrings;
        [NMS(Index = 223)]
        /* 0x2229 */ public bool ShowMempoolOverlay;
        [NMS(Index = 462)]
        /* 0x222A */ public bool ShowMissionIdInTitle;
        [NMS(Index = 224)]
        /* 0x222B */ public bool ShowMouseSmoothing;
        [NMS(Index = 221)]
        /* 0x222C */ public bool ShowPositionDebug;
        [NMS(Index = 226)]
        /* 0x222D */ public bool ShowRenderStatsDisplay;
        [NMS(Index = 337)]
        /* 0x222E */ public bool ShowTeleportEffectLocally;
        [NMS(Index = 275)]
        /* 0x222F */ public bool SimulateDisabledParticleRefractions;
        [NMS(Index = 284)]
        /* 0x2230 */ public bool SimulateNoNetworkConnection;
        [NMS(Index = 0)]
        /* 0x2231 */ public bool SkipAbandonedFreighterUnlocking;
        [NMS(Index = 6)]
        /* 0x2232 */ public bool SkipIntro;
        [NMS(Index = 7)]
        /* 0x2233 */ public bool SkipLogos;
        [NMS(Index = 13)]
        /* 0x2234 */ public bool SkipPlanetDiscoverOnBoot;
        [NMS(Index = 32)]
        /* 0x2235 */ public bool SkipTutorial;
        [NMS(Index = 8)]
        /* 0x2236 */ public bool SkipUITimers;
        [NMS(Index = 242)]
        /* 0x2237 */ public bool SmokeTestCameraFly;
        [NMS(Index = 248)]
        /* 0x2238 */ public bool SmokeTestConfigRandomizePlanetSeed;
        [NMS(Index = 234)]
        /* 0x2239 */ public bool SmokeTestDumpStatsMode;
        [NMS(Index = 241)]
        /* 0x223A */ public bool SmokeTestFastExit;
        [NMS(Index = 237)]
        /* 0x223B */ public bool SmokeTestLegacyOutput;
        [NMS(Index = 243)]
        /* 0x223C */ public bool SmokeTestOutputOnly;
        [NMS(Index = 252)]
        /* 0x223D */ public bool SmokeTestPostBandwidthStats;
        [NMS(Index = 244)]
        /* 0x223E */ public bool SmokeTestPureFlight;
        [NMS(Index = 250)]
        /* 0x223F */ public bool SmokeTestSmokeBotAutoStart;
        [NMS(Index = 249)]
        /* 0x2240 */ public bool SmokeTestSmokeBotEnabled;
        [NMS(Index = 143)]
        /* 0x2241 */ public bool SpawnPirates;
        [NMS(Index = 147)]
        /* 0x2242 */ public bool SpawnPulseEncounters;
        [NMS(Index = 144)]
        /* 0x2243 */ public bool SpawnRobots;
        [NMS(Index = 146)]
        /* 0x2244 */ public bool SpawnShips;
        [NMS(Index = 149)]
        /* 0x2245 */ public bool SpecialsShop;
        [NMS(Index = 281)]
        /* 0x2246 */ public bool SpotlightsTiledBins;
        [NMS(Index = 280)]
        /* 0x2247 */ public bool SpotlightsTiledOn;
        [NMS(Index = 279)]
        /* 0x2248 */ public bool SpotlightsTiledSettings;
        [NMS(Index = 282)]
        /* 0x2249 */ public bool SpotlightsTiledVisualise;
        [NMS(Index = 169)]
        /* 0x224A */ public bool StopSwitchingToSecondaryInteractions;
        [NMS(Index = 195)]
        /* 0x224B */ public bool StressTestLongNameDisplay;
        [NMS(Index = 17)]
        /* 0x224C */ public bool SuperKillGuns;
        [NMS(Index = 126)]
        /* 0x224D */ public bool SuppressSeasonalRewardReminders;
        [NMS(Index = 16)]
        /* 0x224E */ public bool TakeNoDamage;
        [NMS(Index = 4)]
        /* 0x224F */ public bool ThirdPersonIsDefaultCameraForPlayer;
        [NMS(Index = 5)]
        /* 0x2250 */ public bool ThirdPersonIsDefaultCameraForShipAndVehicles;
        [NMS(Index = 78)]
        /* 0x2251 */ public bool UnlockAllPlatformRewards;
        [NMS(Index = 76)]
        /* 0x2252 */ public bool UnlockAllSeasonRewards;
        [NMS(Index = 434)]
        /* 0x2253 */ public bool UnlockAllStories;
        [NMS(Index = 70)]
        /* 0x2254 */ public bool UnlockAllTitles;
        [NMS(Index = 77)]
        /* 0x2255 */ public bool UnlockAllTwitchRewards;
        [NMS(Index = 435)]
        /* 0x2256 */ public bool UnlockAllWords;
        [NMS(Index = 129)]
        /* 0x2257 */ public bool UseBloom;
        [NMS(Index = 136)]
        /* 0x2258 */ public bool UseBuildings;
        [NMS(Index = 130)]
        /* 0x2259 */ public bool UseClouds;
        [NMS(Index = 137)]
        /* 0x225A */ public bool UseCreatures;
        [NMS(Index = 138)]
        /* 0x225B */ public bool UseElevation;
        [NMS(Index = 131)]
        /* 0x225C */ public bool UseGTAO;
        [NMS(Index = 30)]
        /* 0x225D */ public bool UseGunImpactEffect;
        [NMS(Index = 176)]
        /* 0x225E */ public bool UseHighlightedOptionStyle;
        [NMS(Index = 88)]
        /* 0x225F */ public bool UseImmediateModeFrontend;
        [NMS(Index = 134)]
        /* 0x2260 */ public bool UseInstances;
        [NMS(Index = 140)]
        /* 0x2261 */ public bool UseLegacyBuildingTable;
        [NMS(Index = 139)]
        /* 0x2262 */ public bool UseLegacyFreighters;
        [NMS(Index = 86)]
        /* 0x2263 */ public bool UseMovementStickForRun;
        [NMS(Index = 135)]
        /* 0x2264 */ public bool UseObjects;
        [NMS(Index = 404)]
        /* 0x2265 */ public bool UseOldTerrainMeshing;
        [NMS(Index = 297)]
        /* 0x2266 */ public bool UsePadOnUnfocusedWindow;
        [NMS(Index = 127)]
        /* 0x2267 */ public bool UseParticles;
        [NMS(Index = 320)]
        /* 0x2268 */ public bool UseProcTextureDebugger;
        [NMS(Index = 124)]
        /* 0x2269 */ public bool UseSceneInfoWindow;
        [NMS(Index = 29)]
        /* 0x226A */ public bool UseScreenEffects;
        [NMS(Index = 445)]
        /* 0x226B */ public bool UseSeasonTransferInventoryConfigOverride;
        [NMS(Index = 133)]
        /* 0x226C */ public bool UseTerrain;
        [NMS(Index = 128)]
        /* 0x226D */ public bool UseVolumetrics;
        [NMS(Index = 14)]
        /* 0x226E */ public bool VideoCaptureMode;
    }
}
