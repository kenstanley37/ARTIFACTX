namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xC458D077F3D56DF4, NameHash = 0xB4AC20F0)]
    public class GcMissionConditionAllMilestonesComplete : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public int ForStage;
        [NMS(Index = 1)]
        /* 0x4 */ public bool UseSeasonOverrideMessage;
    }
}
