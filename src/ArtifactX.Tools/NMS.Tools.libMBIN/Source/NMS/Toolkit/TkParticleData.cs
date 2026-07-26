using libMBIN.NMS.Toolkit;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0xB5A4C9D115ECEE5D, NameHash = 0xFA1C5236)]
    public class TkParticleData : NMSTemplate
    {
        [NMS(Index = 39)]
        /* 0x000 */ public TkEmitterRotation SecondRotationInfo;
        [NMS(Index = 57)]
        /* 0x050 */ public Colour ColourEnd;
        [NMS(Index = 56)]
        /* 0x060 */ public Colour ColourMiddle;
        [NMS(Index = 55)]
        /* 0x070 */ public Colour ColourStart;
        [NMS(Index = 21)]
        /* 0x080 */ public Vector3f EmitterDirection;
        [NMS(Index = 49)]
        /* 0x090 */ public Vector3f RotateAroundEmitterAxis;
        [NMS(Index = 37)]
        /* 0x0A0 */ public Vector3f RotationAxis;
        [NMS(Index = 42)]
        /* 0x0B0 */ public Vector3f RotationPivot;
        [NMS(Index = 30)]
        /* 0x0C0 */ public Vector3f SpawnOffsetParams;
        [NMS(Index = 31)]
        /* 0x0D0 */ public TkParticleSize ParticleSize;
        [NMS(Index = 10)]
        /* 0x1E0 */ public TkParticleBurstData BurstData;
        [NMS(Index = 58)]
        /* 0x258 */ public TkEmitterFloatProperty AlphaThreshold;
        [NMS(Index = 11)]
        /* 0x290 */ public TkEmitterFloatProperty EmissionRate;
        [NMS(Index = 15)]
        /* 0x2C8 */ public TkEmitterFloatProperty EmitterLife;
        [NMS(Index = 24)]
        /* 0x300 */ public TkEmitterFloatProperty ParticleDamping;
        [NMS(Index = 25)]
        /* 0x338 */ public TkEmitterFloatProperty ParticleDrag;
        [NMS(Index = 23)]
        /* 0x370 */ public TkEmitterFloatProperty ParticleGravity;
        [NMS(Index = 14)]
        /* 0x3A8 */ public TkEmitterFloatProperty ParticleLife;
        [NMS(Index = 33)]
        /* 0x3E0 */ public TkEmitterFloatProperty ParticleSizeY;
        [NMS(Index = 22)]
        /* 0x418 */ public TkEmitterFloatProperty ParticleSpeedMultiplier;
        [NMS(Index = 36)]
        /* 0x450 */ public TkEmitterFloatProperty Rotation;
        [NMS(Index = 46)]
        /* 0x488 */ public TkEmitterFloatProperty TrackEmitterPosition;
        [NMS(Index = 1, MxmlName = "3DGeom")]
        /* 0x4C0 */ public GcFilename _3DGeom;
        [NMS(Index = 3)]
        /* 0x4D0 */ public GcFilename TrailPath;
        [NMS(Index = 61)]
        /* 0x4E0 */ public NMSString0x10 UserColour;
        [NMS(Index = 69)]
        /* 0x4F0 */ public TkEmitterWindDrift WindDrift;
        [NMS(Index = 40, MxmlName = "Billboard Alignment")]
        /* 0x50C */ public TkEmitterBillboardAlignment BillboardAlignment;
        [NMS(Index = 67)]
        /* 0x514 */ public TkFloatRange CameraDistanceFade;
        [NMS(Index = 13)]
        /* 0x51C */ public TkEmitFromParticleInfo EmitFromParticleInfo;
        // size: 0x3
        public enum AlignmentEnum : uint {
            Rotation,
            Velocity,
            VelocityScreenSpace,
        }
        [NMS(Index = 34)]
        /* 0x524 */ public AlignmentEnum Alignment;
        [NMS(Index = 54)]
        /* 0x528 */ public float AlphaVariance;
        [NMS(Index = 6)]
        /* 0x52C */ public uint AudioEvent;
        [NMS(Index = 41)]
        /* 0x530 */ public float BillboardAngleFadeThreshold;
        [NMS(Index = 12)]
        /* 0x534 */ public float Delay;
        // size: 0x3
        public enum DisableDaytimeEnum : uint {
            None,
            Day,
            Night,
        }
        [NMS(Index = 70)]
        /* 0x538 */ public DisableDaytimeEnum DisableDaytime;
        // size: 0x3
        public enum DragTypeEnum : uint {
            IgnoreGravity,
            PhysicallyBased,
            ApplyWind,
        }
        [NMS(Index = 26)]
        /* 0x53C */ public DragTypeEnum DragType;
        [NMS(Index = 16)]
        /* 0x540 */ public float EmitterMidLifeRatio;
        // size: 0x3
        public enum EmitterQualityLevelEnum : uint {
            All,
            Low,
            High,
        }
        [NMS(Index = 0)]
        /* 0x544 */ public EmitterQualityLevelEnum EmitterQualityLevel;
        [NMS(Index = 19)]
        /* 0x548 */ public float EmitterSpreadAngle;
        [NMS(Index = 20)]
        /* 0x54C */ public float EmitterSpreadAngleMin;
        // size: 0x4
        public enum FlipbookPlaybackRateEnum : uint {
            Absolute,
            RelativeToMax,
            OnceToCompletion,
            Random,
        }
        [NMS(Index = 50)]
        /* 0x550 */ public FlipbookPlaybackRateEnum FlipbookPlaybackRate;
        [NMS(Index = 51)]
        /* 0x554 */ public float HueVariance;
        [NMS(Index = 53)]
        /* 0x558 */ public float LightnessVariance;
        [NMS(Index = 68)]
        /* 0x55C */ public float LimitLifetimeOnMove;
        [NMS(Index = 9)]
        /* 0x560 */ public int MaxCount;
        [NMS(Index = 63)]
        /* 0x564 */ public float MaxRenderCameraHeight;
        [NMS(Index = 62)]
        /* 0x568 */ public float MaxRenderDistance;
        [NMS(Index = 64)]
        /* 0x56C */ public float MaxSpawnDistance;
        // size: 0x2
        public enum OnRefractionsDisabledEnum : uint {
            Hide,
            AlphaBlend,
        }
        [NMS(Index = 59)]
        /* 0x570 */ public OnRefractionsDisabledEnum OnRefractionsDisabled;
        [NMS(Index = 32)]
        /* 0x574 */ public float ParticleSizeCurveVariation;
        [NMS(Index = 48)]
        /* 0x578 */ public float RotateAroundEmitter;
        [NMS(Index = 52)]
        /* 0x57C */ public float SaturationVariance;
        [NMS(Index = 65)]
        /* 0x580 */ public float SoftFadeStrength;
        // size: 0x6
        public enum SpawnOffsetTypeEnum : uint {
            Sphere,
            Box,
            Disc,
            Cone,
            Donut,
            Point,
        }
        [NMS(Index = 29)]
        /* 0x584 */ public SpawnOffsetTypeEnum SpawnOffsetType;
        [NMS(Index = 28)]
        /* 0x588 */ public float StartOffset;
        [NMS(Index = 35)]
        /* 0x58C */ public float StartRotationVariation;
        [NMS(Index = 66)]
        /* 0x590 */ public float SurfaceDistanceFadeStrength;
        [NMS(Index = 4)]
        /* 0x594 */ public float TrailRatio;
        [NMS(Index = 43, MxmlName = "U Coordinate")]
        /* 0x598 */ public TkCoordinateOrientation UCoordinate;
        [NMS(Index = 44, MxmlName = "V Coordinate")]
        /* 0x59C */ public TkCoordinateOrientation VCoordinate;
        [NMS(Index = 27)]
        /* 0x5A0 */ public float Variation;
        [NMS(Index = 45)]
        /* 0x5A4 */ public float VelocityInheritance;
        [NMS(Index = 17)]
        /* 0x5A8 */ public TkCurveType EmitterLifeCurve1;
        [NMS(Index = 18)]
        /* 0x5A9 */ public TkCurveType EmitterLifeCurve2;
        [NMS(Index = 38)]
        /* 0x5AA */ public bool EnableSecondRotation;
        [NMS(Index = 60)]
        /* 0x5AB */ public bool FadeRefractionsAtScreenEdge;
        [NMS(Index = 2)]
        /* 0x5AC */ public bool GPURender;
        [NMS(Index = 8)]
        /* 0x5AD */ public bool Oneshot;
        [NMS(Index = 7)]
        /* 0x5AE */ public bool StartEnabled;
        [NMS(Index = 47)]
        /* 0x5AF */ public bool TrackEmitterRotation;
        [NMS(Index = 5)]
        /* 0x5B0 */ public bool TrailIsRibbon;
    }
}
