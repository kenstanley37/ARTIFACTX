using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x92451E64A3A2A0CD, NameHash = 0x7CA999C6)]
    public class GcRewardCommunicatorMessage : NMSTemplate
    {
        [NMS(Index = 2)]
        /* 0x00 */ public GcPlayerCommunicatorMessage Comms;
        [NMS(Index = 0)]
        /* 0x50 */ public NMSString0x20A FailureMessageBusy;
        [NMS(Index = 1)]
        /* 0x70 */ public NMSString0x20A FailureMessageNotInShip;
    }
}
