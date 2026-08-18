using libMBIN.NMS.Toolkit;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0xF77C2CB78BB9015A, NameHash = 0x6BE3C671)]
    public class TkModelResourceData : NMSTemplate
    {
        [NMS(Index = 1)]
        /* 0x00 */ public TkModelResourceCameraData Camera;
        [NMS(Index = 8)]
        /* 0x70 */ public NMSString0x20A Anim;
        [NMS(Index = 0)]
        /* 0x90 */ public NMSString0x10 Id;
        [NMS(Index = 2)]
        /* 0xA0 */ public float AspectRatio;
        [NMS(Index = 7)]
        /* 0xA4 */ public float BlendInOffset;
        [NMS(Index = 6)]
        /* 0xA8 */ public float BlendInTime;
        [NMS(Index = 9)]
        /* 0xAC */ public float HeightOffset;
        [NMS(Index = 4)]
        /* 0xB0 */ public float LightPitch;
        [NMS(Index = 5)]
        /* 0xB4 */ public float LightRotate;
        // size: 0x3
        public enum ResourceThumbnailModeEnum : uint {
            None,
            HUD,
            GUI,
        }
        [NMS(Index = 3)]
        /* 0xB8 */ public ResourceThumbnailModeEnum ResourceThumbnailMode;
        [NMS(Index = 10)]
        /* 0xBC */ public bool CanRotateWithInput;
    }
}
