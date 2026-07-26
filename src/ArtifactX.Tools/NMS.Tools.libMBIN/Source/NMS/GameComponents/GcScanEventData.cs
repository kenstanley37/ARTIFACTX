using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xBF816B43895C7A0D, NameHash = 0x37EE71BB)]
    public class GcScanEventData : NMSTemplate
    {
        [NMS(Index = 46)]
        /* 0x000 */ public GcScanEventSolarSystemLookup SolarSystemAttributes;
        [NMS(Index = 47)]
        /* 0x0C0 */ public GcScanEventSolarSystemLookup SolarSystemAttributesFallback;
        [NMS(Index = 70)]
        /* 0x180 */ public GcResourceElement ResourceOverride;
        [NMS(Index = 1)]
        /* 0x1C8 */ public NMSString0x20A ForceInteraction;
        [NMS(Index = 7)]
        /* 0x1E8 */ public NMSString0x20A MustMatchStoryUtilityPuzzle;
        [NMS(Index = 0)]
        /* 0x208 */ public NMSString0x20A Name;
        [NMS(Index = 51)]
        /* 0x228 */ public NMSString0x20A NextOption;
        [NMS(Index = 72)]
        /* 0x248 */ public NMSString0x20A OverrideGameTableConfig;
        [NMS(Index = 74)]
        /* 0x268 */ public NMSString0x20A OverrideGameTableGameConfig;
        [NMS(Index = 75)]
        /* 0x288 */ public NMSString0x20A OverrideGameTableGameConfigOnCompletion;
        [NMS(Index = 22)]
        /* 0x2A8 */ public NMSString0x20A PlanetLabelText;
        [NMS(Index = 71)]
        /* 0x2C8 */ public NMSString0x20A RequireDefaultGameTableConfig;
        [NMS(Index = 24)]
        /* 0x2E8 */ public NMSString0x20A SurveyDiscoveryOSDMessage;
        [NMS(Index = 25)]
        /* 0x308 */ public NMSString0x20A SurveyHUDName;
        [NMS(Index = 58)]
        /* 0x328 */ public TkTextureResource MarkerIcon;
        [NMS(Index = 52)]
        /* 0x340 */ public GcScanEventTriggers TriggerActions;
        [NMS(Index = 13)]
        /* 0x358 */ public NMSString0x10 ForceOverrideEncounter;
        [NMS(Index = 50)]
        /* 0x368 */ public NMSString0x10 HasReward;
        [NMS(Index = 56)]
        /* 0x378 */ public VariableSizeString InterstellarOSDMessage;
        [NMS(Index = 57)]
        /* 0x388 */ public VariableSizeString MarkerLabel;
        [NMS(Index = 69)]
        /* 0x398 */ public NMSString0x10 MissionMessageOnInteract;
        [NMS(Index = 55)]
        /* 0x3A8 */ public VariableSizeString OSDMessage;
        [NMS(Index = 10)]
        /* 0x3B8 */ public NMSString0x10 ReplacementMaintData;
        [NMS(Index = 68)]
        /* 0x3C8 */ public VariableSizeString TooltipMessage;
        [NMS(Index = 53)]
        /* 0x3D8 */ public List<VariableSizeString> UAsList;
        [NMS(Index = 43)]
        /* 0x3E8 */ public VariableSizeString UseUDAAsSearchPoint;
        // size: 0x8
        public enum BuildingLocationEnum : uint {
            Nearest,
            AllNearest,
            Random,
            RandomOnNearPlanet,
            RandomOnFarPlanet,
            PlanetSearch,
            PlayerSettlement,
            NearestUnmarked,
        }
        [NMS(Index = 34)]
        /* 0x3F8 */ public BuildingLocationEnum BuildingLocation;
        [NMS(Index = 16)]
        /* 0x3FC */ public float BuildingPreventionRadius;
        // size: 0x5
        public enum EventEndTypeEnum : uint {
            None,
            Proximity,
            Interact,
            EnterBuilding,
            TimedInteract,
        }
        [NMS(Index = 28)]
        /* 0x400 */ public EventEndTypeEnum EventEndType;
        // size: 0x2
        public enum EventPriorityEnum : uint {
            Regular,
            High,
        }
        [NMS(Index = 29)]
        /* 0x404 */ public EventPriorityEnum EventPriority;
        // size: 0x6
        public enum EventStartTypeEnum : uint {
            None,
            Special,
            Discovered,
            Timer,
            ObjectScan,
            LeaveBuilding,
        }
        [NMS(Index = 27)]
        /* 0x408 */ public EventStartTypeEnum EventStartType;
        [NMS(Index = 3)]
        /* 0x40C */ public GcInteractionType ForceInteractionType;
        [NMS(Index = 64)]
        /* 0x410 */ public float IconTime;
        [NMS(Index = 63)]
        /* 0x414 */ public GcAudioWwiseEvents MessageAudio;
        [NMS(Index = 62)]
        /* 0x418 */ public float MessageDisplayTime;
        [NMS(Index = 61)]
        /* 0x41C */ public float MessageTime;
        [NMS(Index = 59)]
        /* 0x420 */ public GcScannerIconHighlightTypes MissionMarkerHighlightStyleOverride;
        [NMS(Index = 6)]
        /* 0x424 */ public GcAlienRace OverrideInteractionRace;
        [NMS(Index = 26)]
        /* 0x428 */ public GcStaticTag PlaceMarkerAtTaggedNode;
        [NMS(Index = 5)]
        /* 0x42C */ public GcAlienRace RequireInteractionRace;
        // size: 0x1C
        public enum SearchTypeEnum : uint {
            Any,
            AnyShelter,
            AnyNPC,
            FindBuildingClass,
            SpaceStation,
            SpaceAnomaly,
            Atlas,
            Freighter,
            FreighterBase,
            ExternalPlanetBase,
            PlanetBaseTerminal,
            Expedition,
            ExpeditionLeader,
            TutorialShelter,
            MPMissionFreighter,
            Nexus,
            InitialDistressSignal,
            SpaceMarker,
            NexusEggMachine,
            PhotoTarget,
            NPC_PetBattle,
            SettlementConstruction,
            UnownedSettlement,
            NPC_HideOut,
            FriendlyDrone,
            AnyRobotSite,
            UnownedSettlement_Builders,
            OwnedSettlementHub,
        }
        [NMS(Index = 35)]
        /* 0x430 */ public SearchTypeEnum SearchType;
        // size: 0x8
        public enum SolarSystemLocationEnum : uint {
            Local,
            Near,
            LocalOrNear,
            NearWithNoExpeditions,
            FromList,
            SeasonParty,
            FirstPurpleSystemUA,
            NearSpecificPartyIndex,
        }
        [NMS(Index = 44)]
        /* 0x434 */ public SolarSystemLocationEnum SolarSystemLocation;
        [NMS(Index = 45)]
        /* 0x438 */ public int SpecificPartyIndexToSearchFrom;
        [NMS(Index = 60)]
        /* 0x43C */ public float StartTime;
        [NMS(Index = 23)]
        /* 0x440 */ public float SurveyDistance;
        [NMS(Index = 54)]
        /* 0x444 */ public GcTechnologyCategory TechShopType;
        [NMS(Index = 65)]
        /* 0x448 */ public float TooltipTime;
        [NMS(Index = 37)]
        /* 0x44C */ public bool AllowFriendsBases;
        [NMS(Index = 40)]
        /* 0x44D */ public bool AllowOverriddenBuildings;
        [NMS(Index = 19)]
        /* 0x44E */ public bool AlwaysShow;
        [NMS(Index = 32)]
        /* 0x44F */ public bool BlockStartedOnUseEvents;
        [NMS(Index = 17)]
        /* 0x450 */ public bool BuildingPreventionDisallowBuilding;
        [NMS(Index = 30)]
        /* 0x451 */ public bool CanEndFromOutsideMission;
        [NMS(Index = 15)]
        /* 0x452 */ public bool ClearForcedInteractionOnCompletion;
        [NMS(Index = 73)]
        /* 0x453 */ public bool ClearGameTableConfigOverrideOnCompletion;
        [NMS(Index = 31)]
        /* 0x454 */ public bool DisableMultiplayerSync;
        [NMS(Index = 8)]
        /* 0x455 */ public bool ForceBroken;
        [NMS(Index = 9)]
        /* 0x456 */ public bool ForceFixed;
        [NMS(Index = 12)]
        /* 0x457 */ public bool ForceOverridesAll;
        [NMS(Index = 11)]
        /* 0x458 */ public bool ForceReplaceStoryPortalSeed;
        [NMS(Index = 49)]
        /* 0x459 */ public bool ForceResetPortal;
        [NMS(Index = 48)]
        /* 0x45A */ public bool ForceRestartInteraction;
        [NMS(Index = 38)]
        /* 0x45B */ public bool ForceWideRandom;
        [NMS(Index = 14)]
        /* 0x45C */ public bool IsCommunityPortalOverride;
        [NMS(Index = 39)]
        /* 0x45D */ public bool MustFindSystem;
        [NMS(Index = 20)]
        /* 0x45E */ public bool NeverShow;
        [NMS(Index = 4)]
        /* 0x45F */ public bool NPCReactsToPlayer;
        [NMS(Index = 33)]
        /* 0x460 */ public bool ReplaceEventIfAlreadyActive;
        [NMS(Index = 67)]
        /* 0x461 */ public bool ShowEndTooltip;
        [NMS(Index = 21)]
        /* 0x462 */ public bool ShowOnlyIfSequenceTarget;
        [NMS(Index = 36)]
        /* 0x463 */ public GcBuildingClassification SpecificBuildingClass;
        [NMS(Index = 41)]
        /* 0x464 */ public bool TargetMustMatchMissionSeed;
        [NMS(Index = 66)]
        /* 0x465 */ public bool TooltipRepeats;
        [NMS(Index = 42)]
        /* 0x466 */ public bool UseBuildingFromRendezvousStage;
        [NMS(Index = 18)]
        /* 0x467 */ public bool UseMissionTradingDataOverride;
        [NMS(Index = 2)]
        /* 0x468 */ public bool UseSeasonDataAsInteraction;
    }
}
