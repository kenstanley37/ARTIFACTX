namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0x856E390F47759B5C, NameHash = 0x98C0D964)]
    public class TkGraphicsDetailTypes : NMSTemplate
    {
        // size: 0x4
        public enum GraphicDetailEnum : uint {
            Low,
            Medium,
            High,
            Ultra,
        }
        [NMS(Index = 0)]
        /* 0x0 */ public GraphicDetailEnum GraphicDetail;
    }
}
