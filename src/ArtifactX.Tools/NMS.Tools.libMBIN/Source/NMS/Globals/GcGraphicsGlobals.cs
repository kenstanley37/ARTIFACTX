using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.Globals
{
    [NMS(GUID = 0xE05784405FF244C6, NameHash = 0xEADD2E75)]
    public class GcGraphicsGlobals : NMSTemplate
    {
        [NMS(Index = 52)]
        /* 0x000 */ public TkImGuiSettings ImGui;
        [NMS(Index = 254, Size = 0x4, EnumType = typeof(TkGraphicsDetailTypes.GraphicDetailEnum))]
        /* 0x190 */ public Vector4f[] ShellsSettings;
        [NMS(Index = 249, Size = 0x4, EnumType = typeof(TkGraphicsDetailTypes.GraphicDetailEnum))]
        /* 0x1D0 */ public Vector4f[] TessSettings;
        [NMS(Index = 6)]
        /* 0x210 */ public GcLightShaftProperties LightShaftProperties;
        [NMS(Index = 7)]
        /* 0x240 */ public GcLightShaftProperties StormLightShaftProperties;
        [NMS(Index = 176)]
        /* 0x270 */ public Vector4f LensParams;
        [NMS(Index = 175)]
        /* 0x280 */ public Vector4f MipLevelDebug;
        [NMS(Index = 49)]
        /* 0x290 */ public Colour ScanColour;
        [NMS(Index = 84)]
        /* 0x2A0 */ public Vector4f ShadowBias;
        [NMS(Index = 79)]
        /* 0x2B0 */ public Vector4f ShadowSplit;
        [NMS(Index = 83)]
        /* 0x2C0 */ public Vector4f ShadowSplitCameraView;
        [NMS(Index = 80)]
        /* 0x2D0 */ public Vector4f ShadowSplitShip;
        [NMS(Index = 81)]
        /* 0x2E0 */ public Vector4f ShadowSplitSpace;
        [NMS(Index = 82)]
        /* 0x2F0 */ public Vector4f ShadowSplitStation;
        [NMS(Index = 248)]
        /* 0x300 */ public Vector4f TaaSettings;
        [NMS(Index = 252)]
        /* 0x310 */ public Vector4f TerrainMipDistanceHigh;
        [NMS(Index = 250)]
        /* 0x320 */ public Vector4f TerrainMipDistanceLow;
        [NMS(Index = 251)]
        /* 0x330 */ public Vector4f TerrainMipDistanceMed;
        [NMS(Index = 253)]
        /* 0x340 */ public Vector4f TerrainMipDistanceUlt;
        [NMS(Index = 53)]
        /* 0x350 */ public Colour UIColour;
        [NMS(Index = 54)]
        /* 0x360 */ public Colour UIShipColour;
        [NMS(Index = 178)]
        /* 0x370 */ public Colour VerticalColourBottom;
        [NMS(Index = 177)]
        /* 0x380 */ public Colour VerticalColourTop;
        [NMS(Index = 179)]
        /* 0x390 */ public Vector4f VerticalGradient;
        [NMS(Index = 285)]
        /* 0x3A0 */ public List<int> CascadeRenderSequence;
        [NMS(Index = 304, Size = 0x4, EnumType = typeof(TkGraphicsDetailTypes.GraphicDetailEnum))]
        /* 0x3B0 */ public TkGraphicsDetailPreset[] GraphicsDetailPresetsPC;
        [NMS(Index = 319)]
        /* 0x540 */ public TkGraphicsDetailPreset GraphicsDetailPresetiOS;
        [NMS(Index = 318)]
        /* 0x5A4 */ public TkGraphicsDetailPreset GraphicsDetailPresetMacOS;
        [NMS(Index = 312)]
        /* 0x608 */ public TkGraphicsDetailPreset GraphicsDetailPresetNX64Handheld;
        [NMS(Index = 311)]
        /* 0x66C */ public TkGraphicsDetailPreset GraphicsDetailPresetOberon;
        [NMS(Index = 305)]
        /* 0x6D0 */ public TkGraphicsDetailPreset GraphicsDetailPresetPS4;
        [NMS(Index = 307)]
        /* 0x734 */ public TkGraphicsDetailPreset GraphicsDetailPresetPS4Pro;
        [NMS(Index = 308)]
        /* 0x798 */ public TkGraphicsDetailPreset GraphicsDetailPresetPS4ProVR;
        [NMS(Index = 306)]
        /* 0x7FC */ public TkGraphicsDetailPreset GraphicsDetailPresetPS4VR;
        [NMS(Index = 314)]
        /* 0x860 */ public TkGraphicsDetailPreset GraphicsDetailPresetPS5;
        [NMS(Index = 316)]
        /* 0x8C4 */ public TkGraphicsDetailPreset GraphicsDetailPresetPS5VR;
        [NMS(Index = 313)]
        /* 0x928 */ public TkGraphicsDetailPreset GraphicsDetailPresetSwitch2Handheld;
        [NMS(Index = 315)]
        /* 0x98C */ public TkGraphicsDetailPreset GraphicsDetailPresetTrinity;
        [NMS(Index = 317)]
        /* 0x9F0 */ public TkGraphicsDetailPreset GraphicsDetailPresetTrinityVR;
        [NMS(Index = 309)]
        /* 0xA54 */ public TkGraphicsDetailPreset GraphicsDetailPresetXB1;
        [NMS(Index = 310)]
        /* 0xAB8 */ public TkGraphicsDetailPreset GraphicsDetailPresetXB1X;
        [NMS(Index = 321, Size = 0x4, EnumType = typeof(TkGraphicsDetailTypes.GraphicDetailEnum))]
        /* 0xB1C */ public float[] VariableUpdatePeriodModifers;
        [NMS(Index = 201)]
        /* 0xB2C */ public Vector2f WindDir1;
        [NMS(Index = 202)]
        /* 0xB34 */ public Vector2f WindDir2;
        [NMS(Index = 228)]
        /* 0xB3C */ public float AlphaCutoutMax;
        [NMS(Index = 227)]
        /* 0xB40 */ public float AlphaCutoutMin;
        [NMS(Index = 203)]
        /* 0xB44 */ public float AtmosphereSize;
        [NMS(Index = 12)]
        /* 0xB48 */ public float Brightness;
        [NMS(Index = 13)]
        /* 0xB4C */ public float Contrast;
        [NMS(Index = 70)]
        /* 0xB50 */ public float DirectionLightFOV;
        [NMS(Index = 69)]
        /* 0xB54 */ public float DirectionLightRadius;
        [NMS(Index = 71)]
        /* 0xB58 */ public float DirectionLightShadowBias;
        [NMS(Index = 113)]
        /* 0xB5C */ public float DOFAmountManual;
        [NMS(Index = 117)]
        /* 0xB60 */ public float DOFAmountManualFull;
        [NMS(Index = 115)]
        /* 0xB64 */ public float DOFAmountManualFullIndoor;
        [NMS(Index = 116)]
        /* 0xB68 */ public float DOFAmountManualLight;
        [NMS(Index = 114)]
        /* 0xB6C */ public float DOFAmountManualLightIndoor;
        [NMS(Index = 131)]
        /* 0xB70 */ public float DOFAutoFarAmount;
        [NMS(Index = 133)]
        /* 0xB74 */ public float DOFAutoFarFarPlane;
        [NMS(Index = 132)]
        /* 0xB78 */ public float DOFAutoFarFarPlaneFade;
        [NMS(Index = 134)]
        /* 0xB7C */ public float DOFAutoFarNearPlane;
        [NMS(Index = 120)]
        /* 0xB80 */ public float DOFFarFadeDistance;
        [NMS(Index = 122)]
        /* 0xB84 */ public float DOFFarFadeDistanceCave;
        [NMS(Index = 138)]
        /* 0xB88 */ public float DOFFarFadeDistanceInteraction;
        [NMS(Index = 130)]
        /* 0xB8C */ public float DOFFarFadeDistanceManual;
        [NMS(Index = 129)]
        /* 0xB90 */ public float DOFFarFadeDistanceManualIndoor;
        [NMS(Index = 124)]
        /* 0xB94 */ public float DOFFarFadeDistanceSpace;
        [NMS(Index = 127)]
        /* 0xB98 */ public float DOFFarFadeDistanceWater;
        [NMS(Index = 112)]
        /* 0xB9C */ public float DOFFarPlane;
        [NMS(Index = 121)]
        /* 0xBA0 */ public float DOFFarPlaneCave;
        [NMS(Index = 137)]
        /* 0xBA4 */ public float DOFFarPlaneInteraction;
        [NMS(Index = 128)]
        /* 0xBA8 */ public float DOFFarPlaneManual;
        [NMS(Index = 123)]
        /* 0xBAC */ public float DOFFarPlaneSpace;
        [NMS(Index = 126)]
        /* 0xBB0 */ public float DOFFarPlaneWater;
        [NMS(Index = 125)]
        /* 0xBB4 */ public float DOFFarStrengthWater;
        [NMS(Index = 136)]
        /* 0xBB8 */ public float DOFNearAdjustInteraction;
        [NMS(Index = 118)]
        /* 0xBBC */ public float DOFNearFadeDistance;
        [NMS(Index = 119)]
        /* 0xBC0 */ public float DOFNearFadeDistanceManual;
        [NMS(Index = 135)]
        /* 0xBC4 */ public float DOFNearMinInteraction;
        [NMS(Index = 111)]
        /* 0xBC8 */ public float DOFNearPlane;
        [NMS(Index = 63)]
        /* 0xBCC */ public float FarClipDistance;
        [NMS(Index = 211)]
        /* 0xBD0 */ public float FoliageSaturationMax;
        [NMS(Index = 210)]
        /* 0xBD4 */ public float FoliageSaturationMin;
        [NMS(Index = 213)]
        /* 0xBD8 */ public float FoliageValueMax;
        [NMS(Index = 212)]
        /* 0xBDC */ public float FoliageValueMin;
        [NMS(Index = 239)]
        /* 0xBE0 */ public float FrustumJitterAmount;
        [NMS(Index = 240)]
        /* 0xBE4 */ public float FrustumJitterAmountDLSS;
        [NMS(Index = 215)]
        /* 0xBE8 */ public float GrassSaturationMax;
        [NMS(Index = 214)]
        /* 0xBEC */ public float GrassSaturationMin;
        [NMS(Index = 217)]
        /* 0xBF0 */ public float GrassValueMax;
        [NMS(Index = 216)]
        /* 0xBF4 */ public float GrassValueMin;
        [NMS(Index = 3, MxmlName = "HBAO Bias")]
        /* 0xBF8 */ public float HBAOBias;
        [NMS(Index = 5, MxmlName = "HBAO Intensity")]
        /* 0xBFC */ public float HBAOIntensity;
        [NMS(Index = 4, MxmlName = "HBAO Radius")]
        /* 0xC00 */ public float HBAORadius;
        [NMS(Index = 92)]
        /* 0xC04 */ public float HDRExposure;
        [NMS(Index = 104)]
        /* 0xC08 */ public float HDRExposureCave;
        [NMS(Index = 93)]
        /* 0xC0C */ public float HDRGamma;
        [NMS(Index = 94)]
        /* 0xC10 */ public float HDRLutExposure;
        [NMS(Index = 95)]
        /* 0xC14 */ public float HDRLutGamma;
        [NMS(Index = 96)]
        /* 0xC18 */ public float HDRLutToe;
        [NMS(Index = 98)]
        /* 0xC1C */ public float HDROffset;
        [NMS(Index = 106)]
        /* 0xC20 */ public float HDROffsetCave;
        [NMS(Index = 97)]
        /* 0xC24 */ public float HDRThreshold;
        [NMS(Index = 105)]
        /* 0xC28 */ public float HDRThresholdCave;
        [NMS(Index = 61)]
        /* 0xC2C */ public float HUDDistance;
        [NMS(Index = 60)]
        /* 0xC30 */ public float HUDMotionPos;
        [NMS(Index = 57)]
        /* 0xC34 */ public float HUDMotionPosSpring;
        [NMS(Index = 58)]
        /* 0xC38 */ public float HUDMotionX;
        [NMS(Index = 55)]
        /* 0xC3C */ public float HUDMotionXSpring;
        [NMS(Index = 59)]
        /* 0xC40 */ public float HUDMotionY;
        [NMS(Index = 56)]
        /* 0xC44 */ public float HUDMotionYSpring;
        [NMS(Index = 207)]
        /* 0xC48 */ public float HueVariance;
        [NMS(Index = 102)]
        /* 0xC4C */ public float LensDirt;
        [NMS(Index = 110)]
        /* 0xC50 */ public float LensDirtCave;
        [NMS(Index = 100)]
        /* 0xC54 */ public float LensOffset;
        [NMS(Index = 108)]
        /* 0xC58 */ public float LensOffsetCave;
        [NMS(Index = 101)]
        /* 0xC5C */ public float LensScale;
        [NMS(Index = 109)]
        /* 0xC60 */ public float LensScaleCave;
        [NMS(Index = 99)]
        /* 0xC64 */ public float LensThreshold;
        [NMS(Index = 107)]
        /* 0xC68 */ public float LensThresholdCave;
        [NMS(Index = 163)]
        /* 0xC6C */ public float LowHealthDesaturationIntensityMax;
        [NMS(Index = 162)]
        /* 0xC70 */ public float LowHealthDesaturationIntensityMin;
        [NMS(Index = 164)]
        /* 0xC74 */ public float LowHealthDesaturationIntensityTimeSinceHit;
        [NMS(Index = 166)]
        /* 0xC78 */ public float LowHealthFadeInTime;
        [NMS(Index = 167)]
        /* 0xC7C */ public float LowHealthFadeOutTime;
        [NMS(Index = 165)]
        /* 0xC80 */ public float LowHealthOverlayIntensity;
        [NMS(Index = 169)]
        /* 0xC84 */ public float LowHealthPulseRateFullShield;
        [NMS(Index = 168)]
        /* 0xC88 */ public float LowHealthPulseRateLowShield;
        [NMS(Index = 171)]
        /* 0xC8C */ public float LowHealthStrengthFullShield;
        [NMS(Index = 170)]
        /* 0xC90 */ public float LowHealthStrengthLowShield;
        [NMS(Index = 161)]
        /* 0xC94 */ public float LowHealthVignetteEnd;
        [NMS(Index = 160)]
        /* 0xC98 */ public float LowHealthVignetteStart;
        [NMS(Index = 0)]
        /* 0xC9C */ public float LUTDistanceFlightMultiplier;
        [NMS(Index = 232)]
        /* 0xCA0 */ public float MaxParticleRenderRange;
        [NMS(Index = 233)]
        /* 0xCA4 */ public float MaxParticleRenderRangeSpace;
        [NMS(Index = 225)]
        /* 0xCA8 */ public float MaxSpaceFogStrength;
        [NMS(Index = 283)]
        /* 0xCAC */ public float MinPixelSizeOfObjectsInShadowsCockpitOnPlanet;
        [NMS(Index = 282)]
        /* 0xCB0 */ public float MinPixelSizeOfObjectsInShadowsPlanet;
        [NMS(Index = 281)]
        /* 0xCB4 */ public float MinPixelSizeOfObjectsInShadowsSpace;
        [NMS(Index = 43)]
        /* 0xCB8 */ public float ModelRendererLightIntensity;
        [NMS(Index = 235)]
        /* 0xCBC */ public float MotionBlurShutterAngle;
        [NMS(Index = 234)]
        /* 0xCC0 */ public float MotionBlurShutterSpeed;
        [NMS(Index = 294)]
        /* 0xCC4 */ public float MotionBlurThresholdDefault;
        [NMS(Index = 292)]
        /* 0xCC8 */ public float MotionBlurThresholdInVehicle;
        [NMS(Index = 291)]
        /* 0xCCC */ public float MotionBlurThresholdOnFoot;
        [NMS(Index = 293)]
        /* 0xCD0 */ public float MotionBlurThresholdSpace;
        [NMS(Index = 62)]
        /* 0xCD4 */ public float NearClipDistance;
        [NMS(Index = 21)]
        /* 0xCD8 */ public float New_BounceLightIntensity;
        [NMS(Index = 20)]
        /* 0xCDC */ public float New_BounceLightPower;
        [NMS(Index = 19)]
        /* 0xCE0 */ public float New_BounceLightWarp;
        [NMS(Index = 26)]
        /* 0xCE4 */ public float New_SideRimColourMixer;
        [NMS(Index = 25)]
        /* 0xCE8 */ public float New_SideRimWarp;
        [NMS(Index = 24)]
        /* 0xCEC */ public float New_SkyLightIntensity;
        [NMS(Index = 23)]
        /* 0xCF0 */ public float New_SkyLightPower;
        [NMS(Index = 22)]
        /* 0xCF4 */ public float New_SkyLightWarp;
        [NMS(Index = 28)]
        /* 0xCF8 */ public float New_TopRimColourMixer;
        [NMS(Index = 30)]
        /* 0xCFC */ public float New_TopRimIntensity;
        [NMS(Index = 29)]
        /* 0xD00 */ public float New_TopRimPower;
        [NMS(Index = 27)]
        /* 0xD04 */ public float New_TopRimWarp;
        [NMS(Index = 297)]
        /* 0xD08 */ public float NoFocusMaxFPS;
        [NMS(Index = 33)]
        /* 0xD0C */ public float Old_BounceLightIntensity;
        [NMS(Index = 32)]
        /* 0xD10 */ public float Old_BounceLightPower;
        [NMS(Index = 31)]
        /* 0xD14 */ public float Old_BounceLightWarp;
        [NMS(Index = 38)]
        /* 0xD18 */ public float Old_SideRimColourMixer;
        [NMS(Index = 37)]
        /* 0xD1C */ public float Old_SideRimWarp;
        [NMS(Index = 36)]
        /* 0xD20 */ public float Old_SkyLightIntensity;
        [NMS(Index = 35)]
        /* 0xD24 */ public float Old_SkyLightPower;
        [NMS(Index = 34)]
        /* 0xD28 */ public float Old_SkyLightWarp;
        [NMS(Index = 40)]
        /* 0xD2C */ public float Old_TopRimColourMixer;
        [NMS(Index = 42)]
        /* 0xD30 */ public float Old_TopRimIntensity;
        [NMS(Index = 41)]
        /* 0xD34 */ public float Old_TopRimPower;
        [NMS(Index = 39)]
        /* 0xD38 */ public float Old_TopRimWarp;
        [NMS(Index = 44)]
        /* 0xD3C */ public float PetModelRendererLightIntensity;
        [NMS(Index = 328)]
        /* 0xD40 */ public float PhotoModeBloomGainMax;
        [NMS(Index = 326)]
        /* 0xD44 */ public float PhotoModeBloomGainMedium;
        [NMS(Index = 322)]
        /* 0xD48 */ public float PhotoModeBloomGainMin;
        [NMS(Index = 329)]
        /* 0xD4C */ public float PhotoModeBloomThresholdMax;
        [NMS(Index = 327)]
        /* 0xD50 */ public float PhotoModeBloomThresholdMedium;
        [NMS(Index = 323)]
        /* 0xD54 */ public float PhotoModeBloomThresholdMin;
        [NMS(Index = 324)]
        /* 0xD58 */ public float PhotoModeDefaultBloomValue;
        [NMS(Index = 325)]
        /* 0xD5C */ public float PhotoModeMediumValue;
        [NMS(Index = 87)]
        /* 0xD60 */ public float QuantizeTime;
        [NMS(Index = 90)]
        /* 0xD64 */ public float QuantizeTimeCameraView;
        [NMS(Index = 88)]
        /* 0xD68 */ public float QuantizeTimeShip;
        [NMS(Index = 89)]
        /* 0xD6C */ public float QuantizeTimeSpace;
        [NMS(Index = 18)]
        /* 0xD70 */ public float Redo_BounceIntensity;
        [NMS(Index = 16)]
        /* 0xD74 */ public float Redo_LightIntensity;
        [NMS(Index = 17)]
        /* 0xD78 */ public float Redo_SkyIntensity;
        [NMS(Index = 226)]
        /* 0xD7C */ public float ReflectionStrength;
        [NMS(Index = 206)]
        /* 0xD80 */ public float RingAvoidanceSphereInterpTime;
        [NMS(Index = 205)]
        /* 0xD84 */ public float RingRadius;
        [NMS(Index = 204)]
        /* 0xD88 */ public float RingSize;
        [NMS(Index = 14)]
        /* 0xD8C */ public float Saturation;
        [NMS(Index = 208)]
        /* 0xD90 */ public float SaturationVariance;
        [NMS(Index = 48)]
        /* 0xD94 */ public float ScanAlpha;
        [NMS(Index = 184)]
        /* 0xD98 */ public float ScanBandWidth;
        [NMS(Index = 47)]
        /* 0xD9C */ public float ScanClamp;
        [NMS(Index = 186)]
        /* 0xDA0 */ public float ScanDistance;
        [NMS(Index = 182)]
        /* 0xDA4 */ public float ScanEffectSpeed;
        [NMS(Index = 180)]
        /* 0xDA8 */ public float ScanFadeInTime;
        [NMS(Index = 181)]
        /* 0xDAC */ public float ScanFadeOutTime;
        [NMS(Index = 46)]
        /* 0xDB0 */ public float ScanFresnel;
        [NMS(Index = 185)]
        /* 0xDB4 */ public float ScanHeightScale;
        [NMS(Index = 187)]
        /* 0xDB8 */ public float ScanHorizontalScale;
        [NMS(Index = 183)]
        /* 0xDBC */ public float ScanObjectFade;
        [NMS(Index = 86)]
        /* 0xDC0 */ public float ShadowBillboardOffset;
        [NMS(Index = 72)]
        /* 0xDC4 */ public float ShadowLength;
        [NMS(Index = 78)]
        /* 0xDC8 */ public float ShadowLengthCameraView;
        [NMS(Index = 76)]
        /* 0xDCC */ public float ShadowLengthFreighter;
        [NMS(Index = 77)]
        /* 0xDD0 */ public float ShadowLengthFreighterAbandoned;
        [NMS(Index = 73)]
        /* 0xDD4 */ public float ShadowLengthShip;
        [NMS(Index = 74)]
        /* 0xDD8 */ public float ShadowLengthSpace;
        [NMS(Index = 75)]
        /* 0xDDC */ public float ShadowLengthStation;
        [NMS(Index = 68)]
        /* 0xDE0 */ public int ShadowMapSize;
        [NMS(Index = 301)]
        /* 0xDE4 */ public float SharpenFilterAmount;
        [NMS(Index = 303)]
        /* 0xDE8 */ public float SharpenFilterDepthFactorEnd;
        [NMS(Index = 302)]
        /* 0xDEC */ public float SharpenFilterDepthFactorStart;
        [NMS(Index = 172)]
        /* 0xDF0 */ public float ShieldDownScanlineTime;
        [NMS(Index = 190)]
        /* 0xDF4 */ public float Single1ScanBandWidth;
        [NMS(Index = 191)]
        /* 0xDF8 */ public float Single1ScanEffectSpeed;
        [NMS(Index = 189)]
        /* 0xDFC */ public float Single1ScanHeightScale;
        [NMS(Index = 193)]
        /* 0xE00 */ public float Single1ScanHorizontalScale;
        [NMS(Index = 192)]
        /* 0xE04 */ public float Single1ScanObjectFade;
        [NMS(Index = 188)]
        /* 0xE08 */ public float Single1ScanTime;
        [NMS(Index = 196)]
        /* 0xE0C */ public float Single2ScanBandWidth;
        [NMS(Index = 197)]
        /* 0xE10 */ public float Single2ScanEffectSpeed;
        [NMS(Index = 195)]
        /* 0xE14 */ public float Single2ScanHeightScale;
        [NMS(Index = 199)]
        /* 0xE18 */ public float Single2ScanHorizontalScale;
        [NMS(Index = 198)]
        /* 0xE1C */ public float Single2ScanObjectFade;
        [NMS(Index = 194)]
        /* 0xE20 */ public float Single2ScanTime;
        [NMS(Index = 219)]
        /* 0xE24 */ public float SkySaturationMax;
        [NMS(Index = 218)]
        /* 0xE28 */ public float SkySaturationMin;
        [NMS(Index = 221)]
        /* 0xE2C */ public float SkyValueMax;
        [NMS(Index = 220)]
        /* 0xE30 */ public float SkyValueMin;
        [NMS(Index = 296)]
        /* 0xE34 */ public float SpaceIBLBlendDistance;
        [NMS(Index = 295)]
        /* 0xE38 */ public float SpaceIBLBlendStart;
        [NMS(Index = 223)]
        /* 0xE3C */ public float SpaceMieFactor;
        [NMS(Index = 222)]
        /* 0xE40 */ public float SpaceScale;
        [NMS(Index = 224)]
        /* 0xE44 */ public float SpaceSunFactor;
        [NMS(Index = 2, MxmlName = "Sun Light Blend Time")]
        /* 0xE48 */ public float SunLightBlendTime;
        [NMS(Index = 1, MxmlName = "Sun Light Intensity")]
        /* 0xE4C */ public float SunLightIntensity;
        [NMS(Index = 9)]
        /* 0xE50 */ public float SunRayDecay;
        [NMS(Index = 8)]
        /* 0xE54 */ public float SunRayDensity;
        [NMS(Index = 10)]
        /* 0xE58 */ public float SunRayExposure;
        [NMS(Index = 11)]
        /* 0xE5C */ public float SunRayWeight;
        [NMS(Index = 286)]
        /* 0xE60 */ public int SupersamplingLevel;
        [NMS(Index = 238)]
        /* 0xE64 */ public float TaaAccumDelay;
        [NMS(Index = 237)]
        /* 0xE68 */ public float TaaHighFreqConstant;
        [NMS(Index = 236)]
        /* 0xE6C */ public float TaaLowFreqConstant;
        [NMS(Index = 290)]
        /* 0xE70 */ public int TargetTextureMemUsageMB;
        [NMS(Index = 200)]
        /* 0xE74 */ public float TeleportFlashTime;
        [NMS(Index = 257)]
        /* 0xE78 */ public int TerrainAnisoHi;
        [NMS(Index = 255)]
        /* 0xE7C */ public int TerrainAnisoLow;
        [NMS(Index = 256)]
        /* 0xE80 */ public int TerrainAnisoMed;
        [NMS(Index = 258)]
        /* 0xE84 */ public int TerrainAnisoUlt;
        [NMS(Index = 265)]
        /* 0xE88 */ public int TerrainBlocksPerFrameHi;
        [NMS(Index = 263)]
        /* 0xE8C */ public int TerrainBlocksPerFrameLow;
        [NMS(Index = 264)]
        /* 0xE90 */ public int TerrainBlocksPerFrameMed;
        [NMS(Index = 271)]
        /* 0xE94 */ public int TerrainBlocksPerFrameOberon;
        [NMS(Index = 267)]
        /* 0xE98 */ public int TerrainBlocksPerFramePs430;
        [NMS(Index = 268)]
        /* 0xE9C */ public int TerrainBlocksPerFramePs460;
        [NMS(Index = 266)]
        /* 0xEA0 */ public int TerrainBlocksPerFrameUlt;
        [NMS(Index = 269)]
        /* 0xEA4 */ public int TerrainBlocksPerFrameXb130;
        [NMS(Index = 270)]
        /* 0xEA8 */ public int TerrainBlocksPerFrameXb160;
        [NMS(Index = 259)]
        /* 0xEAC */ public int TerrainDroppedMipsLow;
        [NMS(Index = 260)]
        /* 0xEB0 */ public int TerrainDroppedMipsMed;
        [NMS(Index = 261)]
        /* 0xEB4 */ public float TerrainMipBiasLow;
        [NMS(Index = 262)]
        /* 0xEB8 */ public float TerrainMipBiasMed;
        [NMS(Index = 91)]
        /* 0xEBC */ public float ToneMapExposure;
        [NMS(Index = 103)]
        /* 0xEC0 */ public float ToneMapExposureCave;
        [NMS(Index = 209)]
        /* 0xEC4 */ public float ValueVariance;
        [NMS(Index = 141)]
        /* 0xEC8 */ public float VignetteEnd;
        [NMS(Index = 145)]
        /* 0xECC */ public float VignetteEndMoveVR;
        [NMS(Index = 149)]
        /* 0xED0 */ public float VignetteEndMoveVRShip;
        [NMS(Index = 158)]
        /* 0xED4 */ public float VignetteEndRidingVR;
        [NMS(Index = 155)]
        /* 0xED8 */ public float VignetteEndTurnRidingVR;
        [NMS(Index = 143)]
        /* 0xEDC */ public float VignetteEndTurnVR;
        [NMS(Index = 152)]
        /* 0xEE0 */ public float VignetteEndTurnVRShip;
        [NMS(Index = 140)]
        /* 0xEE4 */ public float VignetteStart;
        [NMS(Index = 144)]
        /* 0xEE8 */ public float VignetteStartMoveVR;
        [NMS(Index = 148)]
        /* 0xEEC */ public float VignetteStartMoveVRShip;
        [NMS(Index = 157)]
        /* 0xEF0 */ public float VignetteStartRidingVR;
        [NMS(Index = 154)]
        /* 0xEF4 */ public float VignetteStartTurnRidingVR;
        [NMS(Index = 142)]
        /* 0xEF8 */ public float VignetteStartTurnVR;
        [NMS(Index = 151)]
        /* 0xEFC */ public float VignetteStartTurnVRShip;
        [NMS(Index = 147)]
        /* 0xF00 */ public float VignetteVRMoveInterpTime;
        [NMS(Index = 150)]
        /* 0xF04 */ public float VignetteVRMoveInterpTimeShip;
        [NMS(Index = 159)]
        /* 0xF08 */ public float VignetteVRRidingInterpTime;
        [NMS(Index = 146)]
        /* 0xF0C */ public float VignetteVRTurnInterpTime;
        [NMS(Index = 153)]
        /* 0xF10 */ public float VignetteVRTurnInterpTimeShip;
        [NMS(Index = 156)]
        /* 0xF14 */ public float VignetteVRTurnRidingInterpTime;
        [NMS(Index = 64)]
        /* 0xF18 */ public float WarpK;
        [NMS(Index = 65)]
        /* 0xF1C */ public float WarpKCube;
        [NMS(Index = 67)]
        /* 0xF20 */ public float WarpKDispersion;
        [NMS(Index = 66)]
        /* 0xF24 */ public float WarpScale;
        [NMS(Index = 229)]
        /* 0xF28 */ public float WaterHueShift;
        [NMS(Index = 230)]
        /* 0xF2C */ public float WaterSaturation;
        [NMS(Index = 231)]
        /* 0xF30 */ public float WaterValue;
        [NMS(Index = 45)]
        /* 0xF34 */ public float WonderModelRendererLightIntensity;
        [NMS(Index = 284)]
        /* 0xF38 */ public bool AllowPartialCascadeRender;
        [NMS(Index = 242)]
        /* 0xF39 */ public bool ApplyTaaTest;
        [NMS(Index = 51)]
        /* 0xF3A */ public bool CenterRenderSpaceOffset;
        [NMS(Index = 50)]
        /* 0xF3B */ public bool DebugLinesDepthTest;
        [NMS(Index = 139)]
        /* 0xF3C */ public bool DOFEnablePhysCamera;
        [NMS(Index = 298)]
        /* 0xF3D */ public bool EnableCrossPipeSharing;
        [NMS(Index = 299)]
        /* 0xF3E */ public bool EnableSSR;
        [NMS(Index = 272)]
        /* 0xF3F */ public bool EnableTerrainCachePs4Base;
        [NMS(Index = 273)]
        /* 0xF40 */ public bool EnableTerrainCachePs4Pro;
        [NMS(Index = 274)]
        /* 0xF41 */ public bool EnableTerrainCachePs5;
        [NMS(Index = 275)]
        /* 0xF42 */ public bool EnableTerrainCacheXb1Base;
        [NMS(Index = 276)]
        /* 0xF43 */ public bool EnableTerrainCacheXb1X;
        [NMS(Index = 278)]
        /* 0xF44 */ public bool EnableTerrainCacheXboxSeriesS;
        [NMS(Index = 277)]
        /* 0xF45 */ public bool EnableTerrainCacheXboxSeriesX;
        [NMS(Index = 287)]
        /* 0xF46 */ public bool EnableTextureStreaming;
        [NMS(Index = 320)]
        /* 0xF47 */ public bool EnableVariableUpdate;
        [NMS(Index = 279)]
        /* 0xF48 */ public bool ForceCachedTerrain;
        [NMS(Index = 289)]
        /* 0xF49 */ public bool ForceEvictAllTextures;
        [NMS(Index = 288)]
        /* 0xF4A */ public bool ForceStreamAllTextures;
        [NMS(Index = 280)]
        /* 0xF4B */ public bool ForceUncachedTerrain;
        [NMS(Index = 173)]
        /* 0xF4C */ public bool FullscreenScanEffect;
        [NMS(Index = 330)]
        /* 0xF4D */ public bool IBLReflections;
        [NMS(Index = 15)]
        /* 0xF4E */ public bool Redo_On;
        [NMS(Index = 85)]
        /* 0xF4F */ public bool ShadowQuantized;
        [NMS(Index = 300)]
        /* 0xF50 */ public bool ShowReflectionProbes;
        [NMS(Index = 243)]
        /* 0xF51 */ public bool ShowTaaBuf;
        [NMS(Index = 247)]
        /* 0xF52 */ public bool ShowTaaCVarianceBuf;
        [NMS(Index = 246)]
        /* 0xF53 */ public bool ShowTaaNVarianceBuf;
        [NMS(Index = 245)]
        /* 0xF54 */ public bool ShowTaaVarianceBuf;
        [NMS(Index = 244)]
        /* 0xF55 */ public bool TonemapInLuminance;
        [NMS(Index = 174)]
        /* 0xF56 */ public bool UseImposters;
        [NMS(Index = 241)]
        /* 0xF57 */ public bool UseTaaResolve;
    }
}
