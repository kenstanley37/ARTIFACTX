using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xE05E9E8A7C4326D3, NameHash = 0x7FD50C85)]
    public class GcGenericMissionSequence : NMSTemplate
    {
        [NMS(Index = 24)]
        /* 0x000 */ public Colour MissionColourOverride;
        [NMS(Index = 40)]
        /* 0x010 */ public GcTradeData TradingDataOverride;
        [NMS(Index = 32)]
        /* 0x0F8 */ public GcMissionBoardOptions MissionBoardOptions;
        [NMS(Index = 7)]
        /* 0x178 */ public GcSeasonalLogOverrides SeasonalLogTextOverrides;
        [NMS(Index = 27)]
        /* 0x1E8 */ public GcDefaultMissionItemsTable DefaultItems;
        [NMS(Index = 21)]
        /* 0x238 */ public NMSString0x20A MissionPageLocID;
        [NMS(Index = 59)]
        /* 0x258 */ public NMSString0x20A SettlementAbandonOSD;
        [NMS(Index = 6)]
        /* 0x278 */ public GcNumberedTextList MissionDescriptions;
        [NMS(Index = 15)]
        /* 0x290 */ public TkTextureResource MissionIcon;
        [NMS(Index = 17)]
        /* 0x2A8 */ public TkTextureResource MissionIconNotSelected;
        [NMS(Index = 16)]
        /* 0x2C0 */ public TkTextureResource MissionIconSelected;
        [NMS(Index = 10)]
        /* 0x2D8 */ public GcNumberedTextList MissionProcDescriptionA;
        [NMS(Index = 11)]
        /* 0x2F0 */ public GcNumberedTextList MissionProcDescriptionB;
        [NMS(Index = 12)]
        /* 0x308 */ public GcNumberedTextList MissionProcDescriptionC;
        [NMS(Index = 9)]
        /* 0x320 */ public GcNumberedTextList MissionProcDescriptionHeader;
        [NMS(Index = 5)]
        /* 0x338 */ public GcNumberedTextList MissionSubtitles;
        [NMS(Index = 4)]
        /* 0x350 */ public GcNumberedTextList MissionTitles;
        [NMS(Index = 45)]
        /* 0x368 */ public List<NMSTemplate> CancelingConditions;
        [NMS(Index = 39)]
        /* 0x378 */ public List<GcCostTableEntry> Costs;
        [NMS(Index = 36)]
        /* 0x388 */ public GcAlienPuzzleTable Dialog;
        [NMS(Index = 46)]
        /* 0x398 */ public List<GcGenericMissionVersionProgress> FinalStageVersions;
        [NMS(Index = 22)]
        /* 0x3A8 */ public NMSString0x10 MissionBuildMenuHint;
        [NMS(Index = 0)]
        /* 0x3B8 */ public NMSString0x10 MissionID;
        [NMS(Index = 29)]
        /* 0x3C8 */ public NMSString0x10 NextMissionHint;
        [NMS(Index = 38)]
        /* 0x3D8 */ public List<GcGenericRewardTableEntry> Rewards;
        [NMS(Index = 37)]
        /* 0x3E8 */ public List<GcScanEventData> ScanEvents;
        [NMS(Index = 47)]
        /* 0x3F8 */ public List<GcGenericMissionStage> Stages;
        [NMS(Index = 44)]
        /* 0x408 */ public List<NMSTemplate> StartingConditions;
        [NMS(Index = 54)]
        /* 0x418 */ public NMSString0x10 UseCommunityMissionForLog;
        [NMS(Index = 26)]
        /* 0x428 */ public List<int> WikiMissionBlockedBySeasons;
        // size: 0x4
        public enum AutoStartEnum : uint {
            None,
            AllModes,
            Seasonal,
            OnSelected,
        }
        [NMS(Index = 33)]
        /* 0x438 */ public AutoStartEnum AutoStart;
        [NMS(Index = 25)]
        /* 0x43C */ public int BeginCheckFrequency;
        [NMS(Index = 42)]
        /* 0x440 */ public GcMissionConditionTest CancelConditionTest;
        // size: 0x3
        public enum MessageCompleteEnum : uint {
            Default,
            Always,
            Never,
        }
        [NMS(Index = 30)]
        /* 0x444 */ public MessageCompleteEnum MessageComplete;
        // size: 0x3
        public enum MessageStartEnum : uint {
            Default,
            Always,
            Never,
        }
        [NMS(Index = 31)]
        /* 0x448 */ public MessageStartEnum MessageStart;
        [NMS(Index = 19)]
        /* 0x44C */ public GcMissionCategory MissionCategory;
        // size: 0xC
        public enum MissionClassEnum : uint {
            Primary,
            Secondary,
            ChainedSecondary,
            Guide,
            Wiki,
            Seasonal,
            Milestone,
            Atlas,
            BlackHole,
            FleetSupport,
            Settlement,
            SecondaryTempMaxPriority,
        }
        [NMS(Index = 1)]
        /* 0x450 */ public MissionClassEnum MissionClass;
        [NMS(Index = 20)]
        /* 0x454 */ public GcMissionPageHint MissionPageHint;
        [NMS(Index = 18)]
        /* 0x458 */ public int MissionPriority;
        [NMS(Index = 41)]
        /* 0x45C */ public GcMissionConditionTest StartConditionTest;
        [NMS(Index = 8)]
        /* 0x460 */ public NMSString0x20 MissionDescSwitchOverride;
        [NMS(Index = 3)]
        /* 0x480 */ public NMSString0x20 MissionObjective;
        [NMS(Index = 52)]
        /* 0x4A0 */ public bool BlocksPinning;
        [NMS(Index = 35)]
        /* 0x4A1 */ public bool CancelSetsComplete;
        [NMS(Index = 53)]
        /* 0x4A2 */ public bool CanRenounce;
        [NMS(Index = 48)]
        /* 0x4A3 */ public bool ForcesBuildMenuHint;
        [NMS(Index = 51)]
        /* 0x4A4 */ public bool IsLegacy;
        [NMS(Index = 49)]
        /* 0x4A5 */ public bool IsProceduralAllowed;
        [NMS(Index = 50)]
        /* 0x4A6 */ public bool IsRecurring;
        [NMS(Index = 23)]
        /* 0x4A7 */ public bool MissionHasColourOverride;
        [NMS(Index = 2)]
        /* 0x4A8 */ public bool MissionIsCritical;
        [NMS(Index = 28)]
        /* 0x4A9 */ public bool PrefixTitle;
        [NMS(Index = 58)]
        /* 0x4AA */ public bool RequiresSettlement;
        [NMS(Index = 34)]
        /* 0x4AB */ public bool RestartOnCompletion;
        [NMS(Index = 43)]
        /* 0x4AC */ public bool StartIsCancel;
        [NMS(Index = 55)]
        /* 0x4AD */ public bool TakeCommunityMissionIDFromSeasonData;
        [NMS(Index = 56)]
        /* 0x4AE */ public bool TelemetryUpload;
        [NMS(Index = 14)]
        /* 0x4AF */ public bool UseFirstPurpleSystemDetailsInLogInfo;
        [NMS(Index = 13)]
        /* 0x4B0 */ public bool UseScanEventDetailsInLogInfo;
        [NMS(Index = 57)]
        /* 0x4B1 */ public bool UseSeasonTitleOverride;
    }
}
