using libMBIN.NMS.Toolkit;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0xEEC7A14DA5E0F137, NameHash = 0xA1BCFF2E)]
    public class TkModelResourceCameraData : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x00 */ public TkCameraData CameraData;
        [NMS(Index = 3)]
        /* 0x30 */ public NMSString0x20A FocusLocator;
        [NMS(Index = 5)]
        /* 0x50 */ public TkCameraWanderData Wander;
        [NMS(Index = 4)]
        /* 0x5C */ public float FocusInterpTime;
        // size: 0x4
        public enum ResourceFocusTypeEnum : uint {
            ResourceBounds,
            ResourceBoundingHeight,
            NodeBoundingBox,
            World,
        }
        [NMS(Index = 2)]
        /* 0x60 */ public ResourceFocusTypeEnum ResourceFocusType;
        [NMS(Index = 1)]
        /* 0x64 */ public bool UseWorldUp;
    }
}
