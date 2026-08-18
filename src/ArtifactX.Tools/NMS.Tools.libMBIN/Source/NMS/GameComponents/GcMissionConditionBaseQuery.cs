using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xF8927C9BB1FC02F6, NameHash = 0x67438A8B)]
    public class GcMissionConditionBaseQuery : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x00 */ public GcBaseSearchFilter BaseSearchFilter;
        [NMS(Index = 2)]
        /* 0xC0 */ public int MaxBasesFound;
        [NMS(Index = 1)]
        /* 0xC4 */ public int MinBasesFound;
        [NMS(Index = 3)]
        /* 0xC8 */ public float SearchDistanceLimit;
        [NMS(Index = 4)]
        /* 0xCC */ public bool TakeSpecificPartIdFromSeasonData;
    }
}
