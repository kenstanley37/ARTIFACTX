using libMBIN.NMS.Toolkit;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0x2AE58300EFA3659C, NameHash = 0xB06E660D)]
    public class TkEmitterRotation : NMSTemplate
    {
        [NMS(Index = 3)]
        /* 0x00 */ public Vector3f RotationAxis;
        [NMS(Index = 2)]
        /* 0x10 */ public TkEmitterFloatProperty Rotation;
        // size: 0x3
        public enum AlignmentAxisEnum : uint {
            Rotation,
            Velocity,
            VelocityScreenSpace,
        }
        [NMS(Index = 0)]
        /* 0x48 */ public AlignmentAxisEnum AlignmentAxis;
        [NMS(Index = 1)]
        /* 0x4C */ public float StartRotationVariation;
    }
}
