using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x6EE9FF0520C0DBEE, NameHash = 0xBF805707)]
    public class GcMissionSequenceFish : NMSTemplate
    {
        [NMS(Index = 7)]
        /* 0x00 */ public GcMissionFishData TargetFishInfo;
        [NMS(Index = 17)]
        /* 0x30 */ public VariableSizeString DebugText;
        [NMS(Index = 15)]
        /* 0x40 */ public NMSString0x10 FormatStatIntoText;
        [NMS(Index = 0)]
        /* 0x50 */ public VariableSizeString Message;
        [NMS(Index = 1)]
        /* 0x60 */ public VariableSizeString MessageAvailableNearby;
        [NMS(Index = 4)]
        /* 0x70 */ public VariableSizeString MessageNoFishLaserEquipped;
        [NMS(Index = 3)]
        /* 0x80 */ public VariableSizeString MessageNoFishLaserInstalled;
        [NMS(Index = 2)]
        /* 0x90 */ public VariableSizeString MessageNoneInSystem;
        [NMS(Index = 5)]
        /* 0xA0 */ public int Amount;
        [NMS(Index = 12)]
        /* 0xA4 */ public float DepthToFormatIntoText;
        [NMS(Index = 6)]
        /* 0xA8 */ public bool FromNow;
        [NMS(Index = 16)]
        /* 0xA9 */ public bool Multiplayer;
        [NMS(Index = 14)]
        /* 0xAA */ public bool NeverCompleteSequence;
        [NMS(Index = 8)]
        /* 0xAB */ public bool QualityTestIsEqualOrGreater;
        [NMS(Index = 9)]
        /* 0xAC */ public bool SizeTestIsEqualOrGreater;
        [NMS(Index = 10)]
        /* 0xAD */ public bool TakeAmountFromDefaultNumber;
        [NMS(Index = 11)]
        /* 0xAE */ public bool TakeAmountFromSeasonData;
        [NMS(Index = 13)]
        /* 0xAF */ public bool TakeDepthFromSeasonData;
    }
}
