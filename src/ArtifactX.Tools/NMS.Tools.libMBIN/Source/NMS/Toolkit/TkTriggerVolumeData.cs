using libMBIN.NMS.Toolkit;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0x707F9153DB1607D0, NameHash = 0x145940EF)]
    public class TkTriggerVolumeData : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public TkVolumeTriggerType TriggerVolumeType;
    }
}
