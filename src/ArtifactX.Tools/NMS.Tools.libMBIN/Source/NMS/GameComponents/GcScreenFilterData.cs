namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xBFEAB1FB5AC3CDB8, NameHash = 0x6706D122)]
    public class GcScreenFilterData : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x00 */ public NMSString0x20A LocText;
        [NMS(Index = 1)]
        /* 0x20 */ public GcFilename Filename;
        [NMS(Index = 2)]
        /* 0x30 */ public float FadeDistance;
        [NMS(Index = 4)]
        /* 0x34 */ public float HdrAreaAdjust;
        [NMS(Index = 3)]
        /* 0x38 */ public bool SelectableInPhotoMode;
    }
}
