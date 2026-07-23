using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x52A9CF0D1435B969, NameHash = 0xC7D918F3)]
    public class GcPlayerCommonStateData : NMSTemplate
    {
        [NMS(Index = 5)]
        /* 0x0000 */ public GcPhotoModeSettings PhotoModeSettings;
        [NMS(Index = 7)]
        /* 0x0050 */ public GcSeasonalGameModeData SeasonData;
        [NMS(Index = 6)]
        /* 0x6B88 */ public GcByteBeatLibraryData ByteBeatLibrary;
        [NMS(Index = 8)]
        /* 0x8590 */ public GcSeasonStateData SeasonState;
        [NMS(Index = 9)]
        /* 0x8768 */ public GcSeasonTransferInventoryData SeasonTransferInventoryData;
        [NMS(Index = 10)]
        /* 0x88E8 */ public List<NMSString0x10> EarnedSeasonSpecialRewards;
        [NMS(Index = 13)]
        /* 0x88F8 */ public List<GcDiscoveryOwner> UsedDiscoveryOwnersV2;
        [NMS(Index = 12)]
        /* 0x8908 */ public List<NMSString0x20> UsedPlatforms;
        [NMS(Index = 11)]
        /* 0x8918 */ public ulong SaveUniversalId;
        [NMS(Index = 1)]
        /* 0x8920 */ public ulong TotalPlayTime;
        [NMS(Index = 0)]
        /* 0x8928 */ public NMSString0x80 SaveName;
        [NMS(Index = 2)]
        /* 0x89A8 */ public bool UsesThirdPersonCharacterCam;
        [NMS(Index = 4)]
        /* 0x89A9 */ public bool UsesThirdPersonShipCam;
        [NMS(Index = 3)]
        /* 0x89AA */ public bool UsesThirdPersonVehicleCam;
    }
}
