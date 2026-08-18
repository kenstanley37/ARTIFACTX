namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x858E518ECD524D03, NameHash = 0xC402725B)]
    public class GcMoveableObjectComponentData : NMSTemplate
    {
        [NMS(Index = 10)]
        /* 0x00 */ public Vector3f GravGunGrabRotationTarget;
        [NMS(Index = 5)]
        /* 0x10 */ public NMSString0x10 DefaultCollisionEffect;
        [NMS(Index = 17)]
        /* 0x20 */ public GcFilename ReplicationScene;
        [NMS(Index = 6)]
        /* 0x30 */ public NMSString0x10 TerrainCollisionEffect;
        [NMS(Index = 7)]
        /* 0x40 */ public float Cooldown;
        [NMS(Index = 16)]
        /* 0x44 */ public float DroneImpactDamageModifier;
        [NMS(Index = 15)]
        /* 0x48 */ public float DroneImpactStrengthModifier;
        [NMS(Index = 11)]
        /* 0x4C */ public float EnergyRequiredToGrab;
        [NMS(Index = 8)]
        /* 0x50 */ public float GlobalCooldown;
        [NMS(Index = 4)]
        /* 0x54 */ public float MaxImpactScale;
        [NMS(Index = 1)]
        /* 0x58 */ public float MaxImpactStrength;
        [NMS(Index = 3)]
        /* 0x5C */ public float MinImpactScale;
        [NMS(Index = 0)]
        /* 0x60 */ public float MinImpactStrength;
        [NMS(Index = 2)]
        /* 0x64 */ public float MinRelativeVelocity;
        [NMS(Index = 14)]
        /* 0x68 */ public float OnTruckCooldownModifier;
        [NMS(Index = 13)]
        /* 0x6C */ public float OnTruckImpactStrengthModifier;
        [NMS(Index = 12)]
        /* 0x70 */ public float OnTruckMinRelativeVelocityModifier;
        [NMS(Index = 18)]
        /* 0x74 */ public bool NotifyParentEncounterWhenGrabbed;
        [NMS(Index = 9)]
        /* 0x75 */ public bool UseGravGunGrabRotationTarget;
    }
}
