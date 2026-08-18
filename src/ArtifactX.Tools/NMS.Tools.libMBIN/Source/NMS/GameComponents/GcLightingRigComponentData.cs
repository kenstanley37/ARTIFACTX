using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x71D379762134139E, NameHash = 0x20C96CD2)]
    public class GcLightingRigComponentData : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x00 */ public List<GcHeroLightData> LightData;
        [NMS(Index = 4)]
        /* 0x10 */ public float BlendTime;
        // size: 0x3
        public enum LightRigTypeEnum : uint {
            Default,
            ThirdPerson,
            FirstPerson,
        }
        [NMS(Index = 1)]
        /* 0x14 */ public LightRigTypeEnum LightRigType;
        [NMS(Index = 3)]
        /* 0x18 */ public float PitchAngleMax;
        [NMS(Index = 2)]
        /* 0x1C */ public float PitchAngleMin;
    }
}
