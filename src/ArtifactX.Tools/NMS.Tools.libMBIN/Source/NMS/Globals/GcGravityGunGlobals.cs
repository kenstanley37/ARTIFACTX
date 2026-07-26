using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.Globals
{
    [NMS(GUID = 0x47B7CDF4740784D6, NameHash = 0x8BB80F0F)]
    public class GcGravityGunGlobals : NMSTemplate
    {
        [NMS(Index = 7)]
        /* 0x00 */ public NMSString0x10 AttachedMoveableShake;
        [NMS(Index = 42)]
        /* 0x10 */ public NMSString0x10 ImpactDamageType;
        [NMS(Index = 41)]
        /* 0x20 */ public GcImpactCombatEffectData GrabCombatEffectToTarget;
        [NMS(Index = 17)]
        /* 0x30 */ public float AngularEjectionPowerFractionOfPower;
        [NMS(Index = 6)]
        /* 0x34 */ public float AttachedMoveableAltAimLossTimeout;
        [NMS(Index = 3)]
        /* 0x38 */ public float AttachedMoveableAltEnergyRate;
        [NMS(Index = 4)]
        /* 0x3C */ public float AttachedMoveableEnergyDecayGraceTime;
        [NMS(Index = 5)]
        /* 0x40 */ public float AttachedMoveableEnergyDecayRate;
        [NMS(Index = 2)]
        /* 0x44 */ public float AttachedMoveablePrimaryEnergy;
        [NMS(Index = 8)]
        /* 0x48 */ public float AttachedMoveableShakeStrength;
        [NMS(Index = 19)]
        /* 0x4C */ public float EjectMaxPowerup;
        [NMS(Index = 18)]
        /* 0x50 */ public float EjectPowerupMaxTimeSeconds;
        [NMS(Index = 34)]
        /* 0x54 */ public float GrabDragRotationStrength;
        [NMS(Index = 32)]
        /* 0x58 */ public float GrabFixedRotationDampingRatio;
        [NMS(Index = 31)]
        /* 0x5C */ public float GrabFixedRotationSpringConst;
        [NMS(Index = 33)]
        /* 0x60 */ public float GrabFreeRotationDampingFactor;
        [NMS(Index = 28)]
        /* 0x64 */ public float GrabMaxAngularSpeed;
        [NMS(Index = 27)]
        /* 0x68 */ public float GrabMaxLinearSpeed;
        [NMS(Index = 37)]
        /* 0x6C */ public float GrabPositionBobMagnitude;
        [NMS(Index = 36)]
        /* 0x70 */ public float GrabPositionBobSpeed;
        [NMS(Index = 30)]
        /* 0x74 */ public float GrabPositionDampingRatio;
        [NMS(Index = 29)]
        /* 0x78 */ public float GrabPositionSpringConst;
        [NMS(Index = 21)]
        /* 0x7C */ public float GrabPosOffset;
        [NMS(Index = 22)]
        /* 0x80 */ public float GrabRequestTimeoutSeconds;
        [NMS(Index = 40)]
        /* 0x84 */ public float GrabRotationBobTorqueStrength;
        [NMS(Index = 39)]
        /* 0x88 */ public float GrabRotationBobTorqueVariationSpeed;
        [NMS(Index = 49)]
        /* 0x8C */ public float ImpactAggressiveDamageMaxDamage;
        [NMS(Index = 48)]
        /* 0x90 */ public float ImpactAggressiveDamageMaxImpulse;
        [NMS(Index = 47)]
        /* 0x94 */ public float ImpactAggressiveDamageMinImpulse;
        [NMS(Index = 46)]
        /* 0x98 */ public float ImpactDamageMaxDamage;
        [NMS(Index = 45)]
        /* 0x9C */ public float ImpactDamageMaxImpulse;
        [NMS(Index = 44)]
        /* 0xA0 */ public float ImpactDamageMinImpulse;
        [NMS(Index = 50)]
        /* 0xA4 */ public float ImpactDamageModifierOnTruck;
        [NMS(Index = 43)]
        /* 0xA8 */ public float ImpactDamageSpeedThreshold;
        [NMS(Index = 23)]
        /* 0xAC */ public float InitialGrabSpeed;
        [NMS(Index = 25)]
        /* 0xB0 */ public float InitialGrabTimeMinSeconds;
        [NMS(Index = 9)]
        /* 0xB4 */ public float PushForceUpComponent;
        [NMS(Index = 10)]
        /* 0xB8 */ public float PushPower;
        [NMS(Index = 13)]
        /* 0xBC */ public float PushPowerInScrapyard;
        [NMS(Index = 11)]
        /* 0xC0 */ public float PushPowerInScrapyardDistance;
        [NMS(Index = 15)]
        /* 0xC4 */ public float PushPowerSentinel;
        [NMS(Index = 14)]
        /* 0xC8 */ public float PushPowerSentinelEject;
        [NMS(Index = 12)]
        /* 0xCC */ public float PushPowerToxicInScrapyard;
        [NMS(Index = 16)]
        /* 0xD0 */ public float ThresholdForAngularEjectionVelocity;
        [NMS(Index = 1)]
        /* 0xD4 */ public int WeaponChargeGrab;
        [NMS(Index = 0)]
        /* 0xD8 */ public int WeaponChargePush;
        [NMS(Index = 20)]
        /* 0xDC */ public TkCurveType EjectPowerCurve;
        [NMS(Index = 35)]
        /* 0xDD */ public bool GrabPositionBobEnabled;
        [NMS(Index = 38)]
        /* 0xDE */ public bool GrabRotationBobEnabled;
        [NMS(Index = 26)]
        /* 0xDF */ public bool GrabUseDynamicPhysics;
        [NMS(Index = 24)]
        /* 0xE0 */ public TkCurveType InitialGrabCurve;
    }
}
