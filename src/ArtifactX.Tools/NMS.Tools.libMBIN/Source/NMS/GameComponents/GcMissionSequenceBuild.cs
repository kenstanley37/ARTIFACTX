using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x388E5509CD4504DC, NameHash = 0x3B3192B)]
    public class GcMissionSequenceBuild : NMSTemplate
    {
        [NMS(Index = 4)]
        /* 0x00 */ public GcBuildingPartSearchType Type;
        [NMS(Index = 5)]
        /* 0x18 */ public VariableSizeString DebugText;
        [NMS(Index = 0)]
        /* 0x28 */ public VariableSizeString Message;
        [NMS(Index = 1)]
        /* 0x38 */ public NMSString0x10 Part;
        [NMS(Index = 2)]
        /* 0x48 */ public bool TakePartFromSeasonData;
        [NMS(Index = 3)]
        /* 0x49 */ public bool TeachIfNotKnown;
    }
}
