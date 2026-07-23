using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.Globals
{
    [NMS(GUID = 0x6F4439BD7FB55340, NameHash = 0x42902548)]
    public class GcSpaceshipGlobals : NMSTemplate
    {
        [NMS(Index = 214)]
        /* 0x0000 */ public GcScanEffectData ShieldEffectScanData;
        [NMS(Index = 364)]
        /* 0x0050 */ public Colour AlarmLightColour;
        [NMS(Index = 365)]
        /* 0x0060 */ public Colour AlarmLightColourHostile;
        [NMS(Index = 129)]
        /* 0x0070 */ public Vector3f AtmosphereLightOffset;
        [NMS(Index = 633)]
        /* 0x0080 */ public Vector3f CockpitScale;
        [NMS(Index = 428)]
        /* 0x0090 */ public Colour DamageLightColour;
        [NMS(Index = 427)]
        /* 0x00A0 */ public Colour DamageLightColourShield;
        [NMS(Index = 423)]
        /* 0x00B0 */ public Vector3f DamageLightOffsetLeft;
        [NMS(Index = 424)]
        /* 0x00C0 */ public Vector3f DamageLightOffsetRight;
        [NMS(Index = 422)]
        /* 0x00D0 */ public Vector3f DamageLightOffsetTop;
        [NMS(Index = 261)]
        /* 0x00E0 */ public Vector3f DefaultCentreOffset;
        [NMS(Index = 262)]
        /* 0x00F0 */ public Vector3f DefaultCentreOffsetDropship;
        [NMS(Index = 264)]
        /* 0x0100 */ public Vector3f DefaultCentreOffsetRoyal;
        [NMS(Index = 265)]
        /* 0x0110 */ public Vector3f DefaultCentreOffsetSail;
        [NMS(Index = 263)]
        /* 0x0120 */ public Vector3f DefaultCentreOffsetScientific;
        [NMS(Index = 167)]
        /* 0x0130 */ public Colour DirectionDockingInRangeColour;
        [NMS(Index = 168)]
        /* 0x0140 */ public Colour DirectionDockingOutRangeColour;
        [NMS(Index = 118)]
        /* 0x0150 */ public Colour GroundEffectBuildingColour;
        [NMS(Index = 119)]
        /* 0x0160 */ public Colour GroundEffectWaterColour;
        [NMS(Index = 614)]
        /* 0x0170 */ public Vector3f GunOffset3rdPersonLeft;
        [NMS(Index = 615)]
        /* 0x0180 */ public Vector3f GunOffset3rdPersonRight;
        [NMS(Index = 610)]
        /* 0x0190 */ public Vector3f GunOffsetLeft;
        [NMS(Index = 612)]
        /* 0x01A0 */ public Vector3f GunOffsetLeft2;
        [NMS(Index = 611)]
        /* 0x01B0 */ public Vector3f GunOffsetRight;
        [NMS(Index = 613)]
        /* 0x01C0 */ public Vector3f GunOffsetRight2;
        [NMS(Index = 65)]
        /* 0x01D0 */ public Vector3f HandControllerDeadZone;
        [NMS(Index = 66)]
        /* 0x01E0 */ public Vector3f HandControllerExtents;
        [NMS(Index = 63)]
        /* 0x01F0 */ public Vector3f HandControllerValueMultiplier;
        [NMS(Index = 64)]
        /* 0x0200 */ public Vector3f HandControllerValueMultiplierSpace;
        [NMS(Index = 117)]
        /* 0x0210 */ public Colour LandingEffectSpaceColourOverride;
        [NMS(Index = 421)]
        /* 0x0220 */ public Colour MuzzleLightColour;
        [NMS(Index = 297)]
        /* 0x0230 */ public Vector3f PostCollisionAngularFactor;
        [NMS(Index = 685)]
        /* 0x0240 */ public Vector3f StickAnimationDamping;
        [NMS(Index = 390)]
        /* 0x0250 */ public Colour TargetLockDangerColour;
        [NMS(Index = 389)]
        /* 0x0260 */ public Colour TargetLockPassiveColour;
        [NMS(Index = 366)]
        /* 0x0270 */ public List<Vector3f> AlarmLightOffsets;
        [NMS(Index = 476)]
        /* 0x0280 */ public List<GcPlayerSpaceshipAim> GunAimArray;
        [NMS(Index = 474)]
        /* 0x0290 */ public List<GcPlayerSpaceshipAim> LaserAimArray;
        [NMS(Index = 22)]
        /* 0x02A0 */ public NMSString0x10 SailShipCoreTechID;
        [NMS(Index = 672)]
        /* 0x02B0 */ public List<GcFilename> ShipModels;
        [NMS(Index = 120)]
        /* 0x02C0 */ public NMSString0x10 WaterEffectID;
        [NMS(Index = 124)]
        /* 0x02D0 */ public NMSString0x10 WaterJetHoverEffectID;
        [NMS(Index = 122)]
        /* 0x02E0 */ public NMSString0x10 WaterJetLandingEffectID;
        [NMS(Index = 123)]
        /* 0x02F0 */ public NMSString0x10 WaterJetTakeoffEffectID;
        [NMS(Index = 343)]
        /* 0x0300 */ public GcPlayerSpaceshipControlData Control;
        [NMS(Index = 347)]
        /* 0x0518 */ public GcPlayerSpaceshipControlData ControlCorvette;
        [NMS(Index = 345)]
        /* 0x0730 */ public GcPlayerSpaceshipControlData ControlHeavy;
        [NMS(Index = 346)]
        /* 0x0948 */ public GcPlayerSpaceshipControlData ControlHeavyHover;
        [NMS(Index = 348)]
        /* 0x0B60 */ public GcPlayerSpaceshipControlData ControlHover;
        [NMS(Index = 344)]
        /* 0x0D78 */ public GcPlayerSpaceshipControlData ControlLight;
        [NMS(Index = 351)]
        /* 0x0F90 */ public GcPlayerSpaceshipClassBonuses ControlBonusA;
        [NMS(Index = 350)]
        /* 0x0FC0 */ public GcPlayerSpaceshipClassBonuses ControlBonusB;
        [NMS(Index = 349)]
        /* 0x0FF0 */ public GcPlayerSpaceshipClassBonuses ControlBonusC;
        [NMS(Index = 352)]
        /* 0x1020 */ public GcPlayerSpaceshipClassBonuses ControlBonusS;
        [NMS(Index = 549, Size = 0xC, EnumType = typeof(GcSpaceshipClasses.ShipClassEnum))]
        /* 0x1050 */ public float[] SummonShipAnywhereRangeMax;
        [NMS(Index = 191)]
        /* 0x1080 */ public GcSpaceshipAvoidanceData Avoidance;
        [NMS(Index = 192)]
        /* 0x10A4 */ public GcSpaceshipAvoidanceData AvoidanceLowAltitude;
        [NMS(Index = 148, Size = 0x9, EnumType = typeof(GcAISpaceshipRoles.AIShipRoleEnum))]
        /* 0x10C8 */ public float[] WarpFadeInTime;
        [NMS(Index = 466)]
        /* 0x10EC */ public GcPlayerStickData StickData;
        [NMS(Index = 480)]
        /* 0x1108 */ public GcPlayerSpaceshipAim MissileAim;
        [NMS(Index = 9)]
        /* 0x1120 */ public TkEasedFalloff CorvetteLandingRotateNoseLiftFalloff;
        [NMS(Index = 7)]
        /* 0x1134 */ public TkEasedFalloff CorvetteLandingRotateTiltFalloff;
        [NMS(Index = 616)]
        /* 0x1148 */ public GcPlayerSpaceshipWarpData Warp;
        [NMS(Index = 426)]
        /* 0x1158 */ public TkHitCurveData DamageLightCurve;
        [NMS(Index = 420)]
        /* 0x1164 */ public TkHitCurveData MuzzleLightCurve;
        [NMS(Index = 457)]
        /* 0x1170 */ public Vector2f DeathSpinPitch;
        [NMS(Index = 456)]
        /* 0x1178 */ public Vector2f DeathSpinRoll;
        [NMS(Index = 225, MxmlName = "3rdPersonAngleMinSpeed")]
        /* 0x1180 */ public float _3rdPersonAngleMinSpeed;
        [NMS(Index = 226, MxmlName = "3rdPersonAngleSpeedRangePitch")]
        /* 0x1184 */ public float _3rdPersonAngleSpeedRangePitch;
        [NMS(Index = 227, MxmlName = "3rdPersonAngleSpeedRangeYaw")]
        /* 0x1188 */ public float _3rdPersonAngleSpeedRangeYaw;
        [NMS(Index = 224, MxmlName = "3rdPersonAngleSpringTime")]
        /* 0x118C */ public float _3rdPersonAngleSpringTime;
        [NMS(Index = 233, MxmlName = "3rdPersonAvoidanceAdjustPitchFactor")]
        /* 0x1190 */ public float _3rdPersonAvoidanceAdjustPitchFactor;
        [NMS(Index = 235, MxmlName = "3rdPersonAvoidanceAdjustRollFactor")]
        /* 0x1194 */ public float _3rdPersonAvoidanceAdjustRollFactor;
        [NMS(Index = 236, MxmlName = "3rdPersonAvoidanceAdjustYawFactor")]
        /* 0x1198 */ public float _3rdPersonAvoidanceAdjustYawFactor;
        [NMS(Index = 238, MxmlName = "3rdPersonFlashDuration ")]
        /* 0x119C */ public float _3rdPersonFlashDuration;
        [NMS(Index = 237, MxmlName = "3rdPersonFlashIntensity ")]
        /* 0x11A0 */ public float _3rdPersonFlashIntensity;
        [NMS(Index = 234, MxmlName = "3rdPersonHeightForceAdjustPitchFactor")]
        /* 0x11A4 */ public float _3rdPersonHeightForceAdjustPitchFactor;
        [NMS(Index = 229, MxmlName = "3rdPersonLowHeightMax")]
        /* 0x11A8 */ public float _3rdPersonLowHeightMax;
        [NMS(Index = 228, MxmlName = "3rdPersonLowHeightMin")]
        /* 0x11AC */ public float _3rdPersonLowHeightMin;
        [NMS(Index = 230, MxmlName = "3rdPersonLowHeightOffsetVertRotationY")]
        /* 0x11B0 */ public float _3rdPersonLowHeightOffsetVertRotationY;
        [NMS(Index = 231, MxmlName = "3rdPersonLowHeightOffsetY")]
        /* 0x11B4 */ public float _3rdPersonLowHeightOffsetY;
        [NMS(Index = 232, MxmlName = "3rdPersonLowHeightSpringTime")]
        /* 0x11B8 */ public float _3rdPersonLowHeightSpringTime;
        [NMS(Index = 221, MxmlName = "3rdPersonPitchAngle")]
        /* 0x11BC */ public float _3rdPersonPitchAngle;
        [NMS(Index = 217, MxmlName = "3rdPersonRollAngle")]
        /* 0x11C0 */ public float _3rdPersonRollAngle;
        [NMS(Index = 220, MxmlName = "3rdPersonRollAngleAlien")]
        /* 0x11C4 */ public float _3rdPersonRollAngleAlien;
        [NMS(Index = 218, MxmlName = "3rdPersonRollAngleDropship")]
        /* 0x11C8 */ public float _3rdPersonRollAngleDropship;
        [NMS(Index = 219, MxmlName = "3rdPersonRollAngleScience")]
        /* 0x11CC */ public float _3rdPersonRollAngleScience;
        [NMS(Index = 239, MxmlName = "3rdPersonTransitionTime ")]
        /* 0x11D0 */ public float _3rdPersonTransitionTime;
        [NMS(Index = 216, MxmlName = "3rdPersonUpOffsetRollChangeSpeed")]
        /* 0x11D4 */ public float _3rdPersonUpOffsetRollChangeSpeed;
        [NMS(Index = 244, MxmlName = "3rdPersonWarpWanderSpring")]
        /* 0x11D8 */ public float _3rdPersonWarpWanderSpring;
        [NMS(Index = 243, MxmlName = "3rdPersonWarpWanderStartTime")]
        /* 0x11DC */ public float _3rdPersonWarpWanderStartTime;
        [NMS(Index = 245, MxmlName = "3rdPersonWarpWanderTimeX")]
        /* 0x11E0 */ public float _3rdPersonWarpWanderTimeX;
        [NMS(Index = 246, MxmlName = "3rdPersonWarpWanderTimeY")]
        /* 0x11E4 */ public float _3rdPersonWarpWanderTimeY;
        [NMS(Index = 247, MxmlName = "3rdPersonWarpWanderTimeZ")]
        /* 0x11E8 */ public float _3rdPersonWarpWanderTimeZ;
        [NMS(Index = 240, MxmlName = "3rdPersonWarpXWander")]
        /* 0x11EC */ public float _3rdPersonWarpXWander;
        [NMS(Index = 241, MxmlName = "3rdPersonWarpYWander")]
        /* 0x11F0 */ public float _3rdPersonWarpYWander;
        [NMS(Index = 242, MxmlName = "3rdPersonWarpZWander")]
        /* 0x11F4 */ public float _3rdPersonWarpZWander;
        [NMS(Index = 222, MxmlName = "3rdPersonYawAngle")]
        /* 0x11F8 */ public float _3rdPersonYawAngle;
        [NMS(Index = 223, MxmlName = "3rdPersonYawAngleLateralExtra")]
        /* 0x11FC */ public float _3rdPersonYawAngleLateralExtra;
        [NMS(Index = 215)]
        /* 0x1200 */ public float AcrobaticLowFlightLevel;
        [NMS(Index = 432)]
        /* 0x1204 */ public float AimCritAngle;
        [NMS(Index = 434)]
        /* 0x1208 */ public float AimCritBehindAngle;
        [NMS(Index = 433)]
        /* 0x120C */ public float AimCritMinFwdAngle;
        [NMS(Index = 444)]
        /* 0x1210 */ public float AimFoVBoost;
        [NMS(Index = 445)]
        /* 0x1214 */ public float AimFoVBoostTime;
        [NMS(Index = 446)]
        /* 0x1218 */ public float AimFoVBoostTimeAuto;
        [NMS(Index = 448)]
        /* 0x121C */ public float AimMaxAutoAngle;
        [NMS(Index = 468)]
        /* 0x1220 */ public float AimSpeedTrackDistance;
        [NMS(Index = 469)]
        /* 0x1224 */ public float AimSpeedTrackForce;
        [NMS(Index = 447)]
        /* 0x1228 */ public float AimTurnSlower;
        [NMS(Index = 372)]
        /* 0x122C */ public float AlarmLastHitTime;
        [NMS(Index = 367)]
        /* 0x1230 */ public float AlarmLightIntensity;
        [NMS(Index = 369)]
        /* 0x1234 */ public float AlarmLightIntensityHostile;
        [NMS(Index = 368)]
        /* 0x1238 */ public float AlarmRate;
        [NMS(Index = 371)]
        /* 0x123C */ public float AlarmRateHostileMax;
        [NMS(Index = 370)]
        /* 0x1240 */ public float AlarmRateHostileMin;
        [NMS(Index = 600)]
        /* 0x1244 */ public float AngularDamping;
        [NMS(Index = 556)]
        /* 0x1248 */ public float AnomalyStationMaxApproachSpeed;
        [NMS(Index = 430)]
        /* 0x124C */ public float AsteroidHitAngle;
        [NMS(Index = 431)]
        /* 0x1250 */ public float AsteroidHitAngleBoosting;
        [NMS(Index = 624)]
        /* 0x1254 */ public float AtmosphereAngle;
        [NMS(Index = 21)]
        /* 0x1258 */ public float AtmosphereCombatHeight;
        [NMS(Index = 128)]
        /* 0x125C */ public float AtmosphereLightIntensity;
        [NMS(Index = 623)]
        /* 0x1260 */ public float AtmosphereSpeed;
        [NMS(Index = 380)]
        /* 0x1264 */ public float AutoLevelMaxAngle;
        [NMS(Index = 378)]
        /* 0x1268 */ public float AutoLevelMaxPitchAngle;
        [NMS(Index = 379)]
        /* 0x126C */ public float AutoLevelMinAngle;
        [NMS(Index = 377)]
        /* 0x1270 */ public float AutoLevelMinPitchAngle;
        [NMS(Index = 296)]
        /* 0x1274 */ public float AutoLevelPitchCorrectMargin;
        [NMS(Index = 295)]
        /* 0x1278 */ public float AutoLevelWaterAngle;
        [NMS(Index = 293)]
        /* 0x127C */ public float AutoLevelWaterMargin;
        [NMS(Index = 294)]
        /* 0x1280 */ public float AutoLevelWaterTorque;
        [NMS(Index = 485)]
        /* 0x1284 */ public float AutoPilotAlignStrength;
        [NMS(Index = 486)]
        /* 0x1288 */ public float AutoPilotAlignStrengthCorvetteAutopilot;
        [NMS(Index = 490)]
        /* 0x128C */ public float AutoPilotCallAngle;
        [NMS(Index = 493)]
        /* 0x1290 */ public float AutoPilotCallAngleGhost;
        [NMS(Index = 491)]
        /* 0x1294 */ public float AutoPilotCallAngleVertical;
        [NMS(Index = 494)]
        /* 0x1298 */ public float AutoPilotCallAngleVerticalGhost;
        [NMS(Index = 488)]
        /* 0x129C */ public float AutoPilotCallDistance;
        [NMS(Index = 492)]
        /* 0x12A0 */ public float AutoPilotCallDistanceGhost;
        [NMS(Index = 489)]
        /* 0x12A4 */ public float AutoPilotCallDistanceSpacePOI;
        [NMS(Index = 495)]
        /* 0x12A8 */ public float AutoPilotPositionAlignStrength;
        [NMS(Index = 487)]
        /* 0x12AC */ public float AutoPilotSmallShipAlignStrength;
        [NMS(Index = 519)]
        /* 0x12B0 */ public float AutoPilotStoppingMargin;
        [NMS(Index = 257)]
        /* 0x12B4 */ public float AvoidanceDistancePower;
        [NMS(Index = 256)]
        /* 0x12B8 */ public float AvoidancePower;
        [NMS(Index = 569)]
        /* 0x12BC */ public float BoostChargeRate;
        [NMS(Index = 537, MxmlName = "BoostNoAsteroidRadius ")]
        /* 0x12C0 */ public float BoostNoAsteroidRadius;
        [NMS(Index = 411)]
        /* 0x12C4 */ public float CameraPostWarpFov;
        [NMS(Index = 410)]
        /* 0x12C8 */ public float CameraPostWarpFovTime;
        [NMS(Index = 81)]
        /* 0x12CC */ public float CockpitDriftAngle;
        [NMS(Index = 78)]
        /* 0x12D0 */ public float CockpitDriftAngleHmd;
        [NMS(Index = 106)]
        /* 0x12D4 */ public float CockpitExitAnimMul;
        [NMS(Index = 108)]
        /* 0x12D8 */ public float CockpitExitAnimOffset;
        [NMS(Index = 107)]
        /* 0x12DC */ public float CockpitExitAnimTime;
        [NMS(Index = 80)]
        /* 0x12E0 */ public float CockpitPitchCorrectAngle;
        [NMS(Index = 77)]
        /* 0x12E4 */ public float CockpitPitchCorrectAngleHmd;
        [NMS(Index = 79)]
        /* 0x12E8 */ public float CockpitRollAngle;
        [NMS(Index = 82)]
        /* 0x12EC */ public float CockpitRollAngleExtra;
        [NMS(Index = 76)]
        /* 0x12F0 */ public float CockpitRollAngleHmd;
        [NMS(Index = 84)]
        /* 0x12F4 */ public float CockpitRollMultiplierCentre;
        [NMS(Index = 85)]
        /* 0x12F8 */ public float CockpitRollMultiplierOpposite;
        [NMS(Index = 83)]
        /* 0x12FC */ public float CockpitRollTime;
        [NMS(Index = 259)]
        /* 0x1300 */ public float CollisionAlignStrength;
        [NMS(Index = 579)]
        /* 0x1304 */ public float CollisionAsteroidDamp;
        [NMS(Index = 577)]
        /* 0x1308 */ public float CollisionDeflectDamping;
        [NMS(Index = 258)]
        /* 0x130C */ public float CollisionDeflectForce;
        [NMS(Index = 578)]
        /* 0x1310 */ public float CollisionDeflectNormalFactor;
        [NMS(Index = 576)]
        /* 0x1314 */ public float CollisionDeflectTime;
        [NMS(Index = 581)]
        /* 0x1318 */ public float CollisionDistance;
        [NMS(Index = 583)]
        /* 0x131C */ public float CollisionDistanceAsteroid;
        [NMS(Index = 584)]
        /* 0x1320 */ public float CollisionDistanceAsteroidSide;
        [NMS(Index = 582)]
        /* 0x1324 */ public float CollisionDistanceGround;
        [NMS(Index = 585)]
        /* 0x1328 */ public float CollisionDistanceSpaceships;
        [NMS(Index = 580)]
        /* 0x132C */ public float CollisionGroundDamp;
        [NMS(Index = 590)]
        /* 0x1330 */ public float CollisionRadiusMultiplier;
        [NMS(Index = 575)]
        /* 0x1334 */ public float CollisionSpeedDamageAmount;
        [NMS(Index = 470)]
        /* 0x1338 */ public float CombatBoostMultiplier;
        [NMS(Index = 471)]
        /* 0x133C */ public float CombatBoostTurnDamp;
        [NMS(Index = 208)]
        /* 0x1340 */ public float ContrailDefaultAlpha;
        [NMS(Index = 197)]
        /* 0x1344 */ public float ContrailSpeedDamping;
        [NMS(Index = 483)]
        /* 0x1348 */ public float CorvetteAutopilotSpeed;
        [NMS(Index = 484)]
        /* 0x134C */ public float CorvetteAutopilotSpeedSpace;
        [NMS(Index = 4)]
        /* 0x1350 */ public float CorvetteBignessLandingMultiplier;
        [NMS(Index = 3)]
        /* 0x1354 */ public float CorvetteBignessLandingTurnMultiplier;
        [NMS(Index = 10)]
        /* 0x1358 */ public float CorvetteHoverBobPosAmount;
        [NMS(Index = 11)]
        /* 0x135C */ public float CorvetteHoverBobPosSpeed;
        [NMS(Index = 12)]
        /* 0x1360 */ public float CorvetteHoverBobRotationAmount;
        [NMS(Index = 13)]
        /* 0x1364 */ public float CorvetteHoverBobRotationSpeed;
        [NMS(Index = 8)]
        /* 0x1368 */ public float CorvetteLandingRotateNoseLiftAmount;
        [NMS(Index = 6)]
        /* 0x136C */ public float CorvetteLandingRotateTilt;
        [NMS(Index = 5)]
        /* 0x1370 */ public float CorvetteLandingRotateTime;
        [NMS(Index = 14)]
        /* 0x1374 */ public float CorvettePulseBoost;
        [NMS(Index = 15)]
        /* 0x1378 */ public float CorvetteSizeMaxTurnDamping;
        [NMS(Index = 440)]
        /* 0x137C */ public float CruiseForce;
        [NMS(Index = 436)]
        /* 0x1380 */ public float CruiseHeight;
        [NMS(Index = 437)]
        /* 0x1384 */ public float CruiseHeightRange;
        [NMS(Index = 438)]
        /* 0x1388 */ public float CruiseOffAngle;
        [NMS(Index = 439)]
        /* 0x138C */ public float CruiseOffAngleRange;
        [NMS(Index = 300)]
        /* 0x1390 */ public float DamageFlashMin;
        [NMS(Index = 299)]
        /* 0x1394 */ public float DamageFlashScale;
        [NMS(Index = 425)]
        /* 0x1398 */ public float DamageLightIntensity;
        [NMS(Index = 571)]
        /* 0x139C */ public float DamageMaxHitTime;
        [NMS(Index = 570)]
        /* 0x13A0 */ public float DamageMinHitTime;
        [NMS(Index = 572)]
        /* 0x13A4 */ public float DamageMinWoundTime;
        [NMS(Index = 193)]
        /* 0x13A8 */ public float DefaultTrailInitialSpeed;
        [NMS(Index = 195)]
        /* 0x13AC */ public float DefaultTrailMinForwardSpeed;
        [NMS(Index = 194)]
        /* 0x13B0 */ public float DefaultTrailSpeedDamping;
        [NMS(Index = 588)]
        /* 0x13B4 */ public float DeflectAlignTimeMax;
        [NMS(Index = 587)]
        /* 0x13B8 */ public float DeflectAlignTimeMin;
        [NMS(Index = 586)]
        /* 0x13BC */ public float DeflectDistance;
        [NMS(Index = 267)]
        /* 0x13C0 */ public float DirectionBrakeVerticalMultiplier;
        [NMS(Index = 416)]
        /* 0x13C4 */ public float DirectionBrakeVRBoost;
        [NMS(Index = 602)]
        /* 0x13C8 */ public float DirectionDockingAlignmentAngle;
        [NMS(Index = 601)]
        /* 0x13CC */ public float DirectionDockingAngle;
        [NMS(Index = 169)]
        /* 0x13D0 */ public float DirectionDockingCircleOffset;
        [NMS(Index = 170)]
        /* 0x13D4 */ public float DirectionDockingCircleOffsetExtra;
        [NMS(Index = 172)]
        /* 0x13D8 */ public float DirectionDockingCircleRadius;
        [NMS(Index = 173)]
        /* 0x13DC */ public float DirectionDockingCircleRadiusExtra;
        [NMS(Index = 171)]
        /* 0x13E0 */ public float DirectionDockingCircleWidth;
        [NMS(Index = 164)]
        /* 0x13E4 */ public float DirectionDockingIndicatorAngleRange;
        [NMS(Index = 163)]
        /* 0x13E8 */ public float DirectionDockingIndicatorClearAngleRange;
        [NMS(Index = 166)]
        /* 0x13EC */ public float DirectionDockingIndicatorMaxHeight;
        [NMS(Index = 165)]
        /* 0x13F0 */ public float DirectionDockingIndicatorMinHeight;
        [NMS(Index = 162)]
        /* 0x13F4 */ public float DirectionDockingIndicatorRange;
        [NMS(Index = 174)]
        /* 0x13F8 */ public float DirectionDockingIndicatorSpeed;
        [NMS(Index = 604)]
        /* 0x13FC */ public float DirectionDockingInfoRange;
        [NMS(Index = 603)]
        /* 0x1400 */ public float DirectionDockTime;
        [NMS(Index = 88)]
        /* 0x1404 */ public float DistanceFromShipToAllowSpawningOnFreighter;
        [NMS(Index = 116)]
        /* 0x1408 */ public float DockingApproachActiveRange;
        [NMS(Index = 28)]
        /* 0x140C */ public float DockingApproachBrakeHmdMod;
        [NMS(Index = 26)]
        /* 0x1410 */ public float DockingApproachRollHmdMod;
        [NMS(Index = 27)]
        /* 0x1414 */ public float DockingApproachSpeedHmdMod;
        [NMS(Index = 617)]
        /* 0x1418 */ public float DockingRotateSpeed;
        [NMS(Index = 618)]
        /* 0x141C */ public float DockingRotateSpeedVR;
        [NMS(Index = 298)]
        /* 0x1420 */ public float DrawLineLockTargetLineWidth;
        [NMS(Index = 406)]
        /* 0x1424 */ public float DriftEffectIntensity;
        [NMS(Index = 413)]
        /* 0x1428 */ public float DriftSpring;
        [NMS(Index = 415)]
        /* 0x142C */ public float DriftTurnBrakeMultiplier;
        [NMS(Index = 414)]
        /* 0x1430 */ public float DriftTurnStrengthMultiplier;
        [NMS(Index = 662)]
        /* 0x1434 */ public float DroneAlertAngle;
        [NMS(Index = 663)]
        /* 0x1438 */ public float DroneAlertRange;
        [NMS(Index = 625)]
        /* 0x143C */ public float DroneAlignUpTime;
        [NMS(Index = 626)]
        /* 0x1440 */ public float DroneDustHeight;
        [NMS(Index = 628)]
        /* 0x1444 */ public float DroneHeightAdjust;
        [NMS(Index = 627)]
        /* 0x1448 */ public float DroneMinHeight;
        [NMS(Index = 665)]
        /* 0x144C */ public float DroneMoveArrivedRange;
        [NMS(Index = 664)]
        /* 0x1450 */ public float DronePatrolRadius;
        [NMS(Index = 666)]
        /* 0x1454 */ public float DronePatrolTime;
        [NMS(Index = 660)]
        /* 0x1458 */ public float DronePlanetAttackMinRange;
        [NMS(Index = 659)]
        /* 0x145C */ public float DronePlanetAttackRange;
        [NMS(Index = 661)]
        /* 0x1460 */ public float DroneShootTime;
        [NMS(Index = 632)]
        /* 0x1464 */ public float DroneWarpMaxForce;
        [NMS(Index = 631)]
        /* 0x1468 */ public float DroneWarpMinForce;
        [NMS(Index = 630)]
        /* 0x146C */ public float DroneWarpTime;
        [NMS(Index = 620)]
        /* 0x1470 */ public float EjectAnimSpeedFactor;
        [NMS(Index = 619)]
        /* 0x1474 */ public float EjectAnimSwitchPoint;
        [NMS(Index = 210)]
        /* 0x1478 */ public float EngineEffectsThrustContribution;
        [NMS(Index = 209)]
        /* 0x147C */ public float EngineJetLightIntensityMultiplier;
        [NMS(Index = 176)]
        /* 0x1480 */ public float ExhaustSpeed;
        [NMS(Index = 362)]
        /* 0x1484 */ public float ExplorerTakeOffMod;
        [NMS(Index = 360)]
        /* 0x1488 */ public float FighterTakeOffMod;
        [NMS(Index = 497)]
        /* 0x148C */ public float FreighterApproachCombatDistanceMax;
        [NMS(Index = 496)]
        /* 0x1490 */ public float FreighterApproachCombatDistanceMin;
        [NMS(Index = 506)]
        /* 0x1494 */ public float FreighterApproachCombatMinSpeedFactor;
        [NMS(Index = 499)]
        /* 0x1498 */ public float FreighterApproachDistanceMax;
        [NMS(Index = 498)]
        /* 0x149C */ public float FreighterApproachDistanceMin;
        [NMS(Index = 505)]
        /* 0x14A0 */ public float FreighterApproachExtraMargin;
        [NMS(Index = 503)]
        /* 0x14A4 */ public float FreighterApproachExtraMarginCombat;
        [NMS(Index = 504)]
        /* 0x14A8 */ public float FreighterApproachExtraMarginPirate;
        [NMS(Index = 500)]
        /* 0x14AC */ public float FreighterApproachSpeedDamper;
        [NMS(Index = 89)]
        /* 0x14B0 */ public float FreighterBattleIgnoreFriendlyFireDistance;
        [NMS(Index = 16)]
        /* 0x14B4 */ public float FreighterBattleRangeBoost;
        [NMS(Index = 502)]
        /* 0x14B8 */ public float FreighterCombatBoostMul;
        [NMS(Index = 501)]
        /* 0x14BC */ public float FreighterCombatSpeedMul;
        [NMS(Index = 629)]
        /* 0x14C0 */ public float FreighterSpeed;
        [NMS(Index = 383)]
        /* 0x14C4 */ public float FrigateTargetLockRange;
        [NMS(Index = 465)]
        /* 0x14C8 */ public float GravityDropForce;
        [NMS(Index = 463)]
        /* 0x14CC */ public float GravityDropMaxForceHeight;
        [NMS(Index = 464)]
        /* 0x14D0 */ public float GravityDropMaxHeight;
        [NMS(Index = 462)]
        /* 0x14D4 */ public float GravityDropMinHeight;
        [NMS(Index = 319)]
        /* 0x14D8 */ public float GroundHeightBrakeMultiplier;
        [NMS(Index = 318)]
        /* 0x14DC */ public float GroundHeightDownSpeedMultiplier;
        [NMS(Index = 311)]
        /* 0x14E0 */ public float GroundHeightHard;
        [NMS(Index = 313)]
        /* 0x14E4 */ public float GroundHeightHardCorvetteAutopilot;
        [NMS(Index = 312)]
        /* 0x14E8 */ public float GroundHeightHardHorizontal;
        [NMS(Index = 314)]
        /* 0x14EC */ public float GroundHeightHardHorizontalCorvetteAutopilot;
        [NMS(Index = 310)]
        /* 0x14F0 */ public float GroundHeightHardOverWater;
        [NMS(Index = 592)]
        /* 0x14F4 */ public float GroundHeightHardTimeMax;
        [NMS(Index = 591)]
        /* 0x14F8 */ public float GroundHeightHardTimeMin;
        [NMS(Index = 597)]
        /* 0x14FC */ public int GroundHeightNumRays;
        [NMS(Index = 593)]
        /* 0x1500 */ public float GroundHeightPostCollisionDamper;
        [NMS(Index = 316)]
        /* 0x1504 */ public float GroundHeightPostCollisionMultiplier;
        [NMS(Index = 317)]
        /* 0x1508 */ public float GroundHeightPostCollisionMultiplierTime;
        [NMS(Index = 598)]
        /* 0x150C */ public float GroundHeightSmoothTime;
        [NMS(Index = 304)]
        /* 0x1510 */ public float GroundHeightSoft;
        [NMS(Index = 307)]
        /* 0x1514 */ public float GroundHeightSoftCorvetteAutopilot;
        [NMS(Index = 303)]
        /* 0x1518 */ public float GroundHeightSoftForce;
        [NMS(Index = 306)]
        /* 0x151C */ public float GroundHeightSoftForceCorvetteAutopilot;
        [NMS(Index = 305)]
        /* 0x1520 */ public float GroundHeightSoftHorizontal;
        [NMS(Index = 308)]
        /* 0x1524 */ public float GroundHeightSoftHorizontalCorvetteAutopilot;
        [NMS(Index = 595)]
        /* 0x1528 */ public float GroundHeightSpeedAngle;
        [NMS(Index = 596)]
        /* 0x152C */ public float GroundHeightSpeedAngleRange;
        [NMS(Index = 594)]
        /* 0x1530 */ public float GroundHeightSpeedLength;
        [NMS(Index = 184)]
        /* 0x1534 */ public float GroundNearEffectBuildingFade;
        [NMS(Index = 181)]
        /* 0x1538 */ public float GroundNearEffectHeight;
        [NMS(Index = 125)]
        /* 0x153C */ public float GroundNearEffectLightFactor;
        [NMS(Index = 183)]
        /* 0x1540 */ public float GroundNearEffectNormalOffset;
        [NMS(Index = 182)]
        /* 0x1544 */ public float GroundNearEffectRange;
        [NMS(Index = 126)]
        /* 0x1548 */ public float GroundNearEffectWaterLightFactor;
        [NMS(Index = 127)]
        /* 0x154C */ public float GroundWaterSpeedFactor;
        [NMS(Index = 479)]
        /* 0x1550 */ public int GunAimLevel;
        [NMS(Index = 473)]
        /* 0x1554 */ public int GunAmmoMultiplier;
        [NMS(Index = 609)]
        /* 0x1558 */ public float GunOffset3rdPersonMultiplier;
        [NMS(Index = 61)]
        /* 0x155C */ public float HandControllerActiveBlendMinTime;
        [NMS(Index = 62)]
        /* 0x1560 */ public float HandControllerActiveBlendTime;
        [NMS(Index = 74)]
        /* 0x1564 */ public float HandControllerDirOffsetAngle;
        [NMS(Index = 75)]
        /* 0x1568 */ public float HandControllerDirOffsetAngleMove;
        [NMS(Index = 73)]
        /* 0x156C */ public float HandControllerThrottleDeadZone;
        [NMS(Index = 71)]
        /* 0x1570 */ public float HandControllerThrottleDistance;
        [NMS(Index = 72)]
        /* 0x1574 */ public float HandControllerThrottleRange;
        [NMS(Index = 69)]
        /* 0x1578 */ public float HandControllerXReorientation;
        [NMS(Index = 67)]
        /* 0x157C */ public float HandControllerXReorientationMove;
        [NMS(Index = 70)]
        /* 0x1580 */ public float HandControllerZReorientation;
        [NMS(Index = 68)]
        /* 0x1584 */ public float HandControllerZReorientationMove;
        [NMS(Index = 359)]
        /* 0x1588 */ public float HaulerTakeOffMod;
        [NMS(Index = 409)]
        /* 0x158C */ public int HitAsteroidDamage;
        [NMS(Index = 275)]
        /* 0x1590 */ public float HoverAlignTime;
        [NMS(Index = 277)]
        /* 0x1594 */ public float HoverAlignTimeAlt;
        [NMS(Index = 449)]
        /* 0x1598 */ public float HoverBrakeStrength;
        [NMS(Index = 279)]
        /* 0x159C */ public float HoverHeightFactor;
        [NMS(Index = 287)]
        /* 0x15A0 */ public float HoverLandManeuvreBrake;
        [NMS(Index = 289)]
        /* 0x15A4 */ public float HoverLandManeuvreTimeCorvetteMultiplier;
        [NMS(Index = 286)]
        /* 0x15A8 */ public float HoverLandManeuvreTimeHmdMax;
        [NMS(Index = 285)]
        /* 0x15AC */ public float HoverLandManeuvreTimeHmdMin;
        [NMS(Index = 284)]
        /* 0x15B0 */ public float HoverLandManeuvreTimeMax;
        [NMS(Index = 283)]
        /* 0x15B4 */ public float HoverLandManeuvreTimeMin;
        [NMS(Index = 288)]
        /* 0x15B8 */ public float HoverLandManeuvreTimeWaterMultiplier;
        [NMS(Index = 282)]
        /* 0x15BC */ public float HoverLandReachedDistance;
        [NMS(Index = 281)]
        /* 0x15C0 */ public float HoverLandReachedMinTime;
        [NMS(Index = 441)]
        /* 0x15C4 */ public float HoverMaxSpeed;
        [NMS(Index = 280)]
        /* 0x15C8 */ public float HoverMinSpeed;
        [NMS(Index = 278)]
        /* 0x15CC */ public float HoverSpeedFactor;
        [NMS(Index = 442)]
        /* 0x15D0 */ public float HoverStopTime;
        [NMS(Index = 273)]
        /* 0x15D4 */ public float HoverTakeoffHeight;
        [NMS(Index = 274)]
        /* 0x15D8 */ public float HoverTime;
        [NMS(Index = 276)]
        /* 0x15DC */ public float HoverTimeAlt;
        [NMS(Index = 673)]
        /* 0x15E0 */ public float HUDBoostUpgradeMultiplier;
        [NMS(Index = 272)]
        /* 0x15E4 */ public float KBThrustSmoothTime;
        [NMS(Index = 559)]
        /* 0x15E8 */ public float LandGroundTakeOffTime;
        [NMS(Index = 561)]
        /* 0x15EC */ public float LandHeightThreshold;
        [NMS(Index = 565)]
        /* 0x15F0 */ public float LandingAreaFloorOffset;
        [NMS(Index = 564)]
        /* 0x15F4 */ public float LandingAreaRadius;
        [NMS(Index = 301)]
        /* 0x15F8 */ public float LandingButtonMinTime;
        [NMS(Index = 292)]
        /* 0x15FC */ public float LandingCheckBuildingRadiusFactor;
        [NMS(Index = 478)]
        /* 0x1600 */ public int LandingCost;
        [NMS(Index = 29)]
        /* 0x1604 */ public float LandingDirectionalSideOffsetFactor;
        [NMS(Index = 553)]
        /* 0x1608 */ public float LandingHelperMinAngle;
        [NMS(Index = 555)]
        /* 0x160C */ public float LandingHelperRollTime;
        [NMS(Index = 554)]
        /* 0x1610 */ public float LandingHelperTurnTime;
        [NMS(Index = 30)]
        /* 0x1614 */ public float LandingHoverOffset;
        [NMS(Index = 36)]
        /* 0x1618 */ public float LandingMargin;
        [NMS(Index = 35)]
        /* 0x161C */ public float LandingMaxAngle;
        [NMS(Index = 34)]
        /* 0x1620 */ public float LandingMaxSpeed;
        [NMS(Index = 37)]
        /* 0x1624 */ public float LandingObstacleMinHeight;
        [NMS(Index = 290)]
        /* 0x1628 */ public float LandingOnGroundTip;
        [NMS(Index = 302)]
        /* 0x162C */ public float LandingPushNoseUpFactor;
        [NMS(Index = 38)]
        /* 0x1630 */ public float LandingTooManyLowPointsFraction;
        [NMS(Index = 32)]
        /* 0x1634 */ public float LandingWaterHoverHeight;
        [NMS(Index = 33)]
        /* 0x1638 */ public float LandingWaterHoverHeightCorvette;
        [NMS(Index = 31)]
        /* 0x163C */ public float LandingWaterHoverOffset;
        [NMS(Index = 560)]
        /* 0x1640 */ public float LandLookingForward;
        [NMS(Index = 443)]
        /* 0x1644 */ public float LandOffset;
        [NMS(Index = 563)]
        /* 0x1648 */ public float LandSlopeMax;
        [NMS(Index = 562)]
        /* 0x164C */ public float LandWidthThreshold;
        [NMS(Index = 475)]
        /* 0x1650 */ public int LaserAimLevel;
        [NMS(Index = 541)]
        /* 0x1654 */ public float LaserCoolFactor;
        [NMS(Index = 540)]
        /* 0x1658 */ public float LaserOverheatDownTime;
        [NMS(Index = 539)]
        /* 0x165C */ public float LaserOverheatTime;
        [NMS(Index = 269)]
        /* 0x1660 */ public float LateralDriftRange;
        [NMS(Index = 268)]
        /* 0x1664 */ public float LateralDriftRollAmount;
        [NMS(Index = 675)]
        /* 0x1668 */ public float LaunchThrustersMinimumSummonPercentage;
        [NMS(Index = 674)]
        /* 0x166C */ public float LaunchThrustersRegenTimePeriod;
        [NMS(Index = 676)]
        /* 0x1670 */ public float LaunchThrustersSummonCostMultiplier;
        [NMS(Index = 599)]
        /* 0x1674 */ public float LinearDamping;
        [NMS(Index = 376)]
        /* 0x1678 */ public float LockTargetMaxScale;
        [NMS(Index = 373)]
        /* 0x167C */ public float LockTargetMinDistance;
        [NMS(Index = 375)]
        /* 0x1680 */ public float LockTargetMinScale;
        [NMS(Index = 374)]
        /* 0x1684 */ public float LockTargetRange;
        [NMS(Index = 605)]
        /* 0x1688 */ public float LootAttractDistance;
        [NMS(Index = 606)]
        /* 0x168C */ public float LootAttractTime;
        [NMS(Index = 608)]
        /* 0x1690 */ public float LootCollectDistance;
        [NMS(Index = 607)]
        /* 0x1694 */ public float LootDampForce;
        [NMS(Index = 177)]
        /* 0x1698 */ public float LowAltitudeAnimationHeight;
        [NMS(Index = 178)]
        /* 0x169C */ public float LowAltitudeAnimationHysteresisTime;
        [NMS(Index = 179)]
        /* 0x16A0 */ public float LowAltitudeAnimationTime;
        [NMS(Index = 180)]
        /* 0x16A4 */ public float LowAltitudeContrailFadeAtAnimProgress;
        [NMS(Index = 472)]
        /* 0x16A8 */ public float MarkerEventTime;
        [NMS(Index = 86)]
        /* 0x16AC */ public float MaximumDistanceFromShipWhenExiting;
        [NMS(Index = 87)]
        /* 0x16B0 */ public float MaximumHeightWhenExitingShip;
        [NMS(Index = 198)]
        /* 0x16B4 */ public float MaxOverspeedBrake;
        [NMS(Index = 681)]
        /* 0x16B8 */ public float MaxSpeedUpDistance;
        [NMS(Index = 682)]
        /* 0x16BC */ public float MaxSpeedUpVelocity;
        [NMS(Index = 139)]
        /* 0x16C0 */ public float MiniWarpAlignSlerp;
        [NMS(Index = 511)]
        /* 0x16C4 */ public float MiniWarpAlignStrength;
        [NMS(Index = 512)]
        /* 0x16C8 */ public float MiniWarpChargeTime;
        [NMS(Index = 516)]
        /* 0x16CC */ public float MiniWarpCooldownTime;
        [NMS(Index = 523, MxmlName = "MiniWarpExitSpeed ")]
        /* 0x16D0 */ public float MiniWarpExitSpeed;
        [NMS(Index = 524, MxmlName = "MiniWarpExitSpeedStation ")]
        /* 0x16D4 */ public float MiniWarpExitSpeedStation;
        [NMS(Index = 525, MxmlName = "MiniWarpExitTime ")]
        /* 0x16D8 */ public float MiniWarpExitTime;
        [NMS(Index = 535, MxmlName = "MiniWarpFlashDelay ")]
        /* 0x16DC */ public float MiniWarpFlashDelay;
        [NMS(Index = 534, MxmlName = "MiniWarpFlashDuration ")]
        /* 0x16E0 */ public float MiniWarpFlashDuration;
        [NMS(Index = 533, MxmlName = "MiniWarpFlashIntensity ")]
        /* 0x16E4 */ public float MiniWarpFlashIntensity;
        [NMS(Index = 510)]
        /* 0x16E8 */ public float MiniWarpFuelTime;
        [NMS(Index = 526)]
        /* 0x16EC */ public float MiniWarpHUDArrowAttractAngle;
        [NMS(Index = 530)]
        /* 0x16F0 */ public float MiniWarpHUDArrowAttractAngleDense;
        [NMS(Index = 528)]
        /* 0x16F4 */ public float MiniWarpHUDArrowAttractAngleOtherPlayerStuff;
        [NMS(Index = 529)]
        /* 0x16F8 */ public float MiniWarpHUDArrowAttractAngleSaveBeacon;
        [NMS(Index = 527)]
        /* 0x16FC */ public float MiniWarpHUDArrowAttractAngleStation;
        [NMS(Index = 531)]
        /* 0x1700 */ public int MiniWarpHUDArrowNumMarkersToBeDense;
        [NMS(Index = 461)]
        /* 0x1704 */ public float MiniWarpLinesHeight;
        [NMS(Index = 458)]
        /* 0x1708 */ public int MiniWarpLinesNum;
        [NMS(Index = 460)]
        /* 0x170C */ public float MiniWarpLinesOffset;
        [NMS(Index = 459)]
        /* 0x1710 */ public float MiniWarpLinesSpacing;
        [NMS(Index = 137)]
        /* 0x1714 */ public float MiniWarpMarkerAlignSlowdown;
        [NMS(Index = 138)]
        /* 0x1718 */ public float MiniWarpMarkerAlignSlowdownRange;
        [NMS(Index = 136)]
        /* 0x171C */ public float MiniWarpMarkerApproachSlowdown;
        [NMS(Index = 518)]
        /* 0x1720 */ public float MiniWarpMinPlanetDistance;
        [NMS(Index = 536, MxmlName = "MiniWarpNoAsteroidRadius ")]
        /* 0x1724 */ public float MiniWarpNoAsteroidRadius;
        [NMS(Index = 508)]
        /* 0x1728 */ public float MiniWarpPlanetRadius;
        [NMS(Index = 507)]
        /* 0x172C */ public float MiniWarpShakeStrength;
        [NMS(Index = 513)]
        /* 0x1730 */ public float MiniWarpSpeed;
        [NMS(Index = 509)]
        /* 0x1734 */ public float MiniWarpStationRadius;
        [NMS(Index = 520)]
        /* 0x1738 */ public float MiniWarpStoppingMarginDefault;
        [NMS(Index = 521)]
        /* 0x173C */ public float MiniWarpStoppingMarginLong;
        [NMS(Index = 517)]
        /* 0x1740 */ public float MiniWarpStoppingMarginPlanet;
        [NMS(Index = 515)]
        /* 0x1744 */ public float MiniWarpTime;
        [NMS(Index = 514)]
        /* 0x1748 */ public float MiniWarpTopSpeedTime;
        [NMS(Index = 522)]
        /* 0x174C */ public float MiniWarpTrackingMargin;
        [NMS(Index = 481)]
        /* 0x1750 */ public float MissileLockSpeedUp;
        [NMS(Index = 482)]
        /* 0x1754 */ public float MissileLockTime;
        [NMS(Index = 435)]
        /* 0x1758 */ public float MissileShootTime;
        [NMS(Index = 419)]
        /* 0x175C */ public float MuzzleAnimSpeed;
        [NMS(Index = 418)]
        /* 0x1760 */ public float MuzzleLightIntensity;
        [NMS(Index = 132)]
        /* 0x1764 */ public float NearGroundPitchCorrectMinHeight;
        [NMS(Index = 134)]
        /* 0x1768 */ public float NearGroundPitchCorrectMinHeightRemote;
        [NMS(Index = 133)]
        /* 0x176C */ public float NearGroundPitchCorrectRange;
        [NMS(Index = 135)]
        /* 0x1770 */ public float NearGroundPitchCorrectRangeRemote;
        [NMS(Index = 161)]
        /* 0x1774 */ public float NetworkDockSearchRadius;
        [NMS(Index = 394)]
        /* 0x1778 */ public float NoBoostAnomalyDistance;
        [NMS(Index = 401)]
        /* 0x177C */ public float NoBoostCombatEventMinBattleTime;
        [NMS(Index = 402)]
        /* 0x1780 */ public float NoBoostCombatEventMinFreighterBattleTime;
        [NMS(Index = 403)]
        /* 0x1784 */ public float NoBoostCombatEventTime;
        [NMS(Index = 397)]
        /* 0x1788 */ public float NoBoostFreighterAngle;
        [NMS(Index = 396)]
        /* 0x178C */ public float NoBoostFreighterDistance;
        [NMS(Index = 398)]
        /* 0x1790 */ public float NoBoostShipDistance;
        [NMS(Index = 400)]
        /* 0x1794 */ public float NoBoostShipLastHitTime;
        [NMS(Index = 399)]
        /* 0x1798 */ public float NoBoostShipNearMinTime;
        [NMS(Index = 395)]
        /* 0x179C */ public float NoBoostSpaceAnomalyDistance;
        [NMS(Index = 393)]
        /* 0x17A0 */ public float NoBoostStationDistance;
        [NMS(Index = 130)]
        /* 0x17A4 */ public float OutpostDockSpeedAlignMinDistance;
        [NMS(Index = 131)]
        /* 0x17A8 */ public float OutpostDockSpeedAlignRange;
        [NMS(Index = 271)]
        /* 0x17AC */ public float PadThrustSmoothTime;
        [NMS(Index = 270)]
        /* 0x17B0 */ public float PadTurnSpeed;
        [NMS(Index = 326)]
        /* 0x17B4 */ public float PitchCorrectCockpitSpring;
        [NMS(Index = 325)]
        /* 0x17B8 */ public float PitchCorrectDownSpeedHeightMax;
        [NMS(Index = 324)]
        /* 0x17BC */ public float PitchCorrectDownSpeedHeightMin;
        [NMS(Index = 323)]
        /* 0x17C0 */ public float PitchCorrectDownSpeedMaxDownAngle;
        [NMS(Index = 320)]
        /* 0x17C4 */ public float PitchCorrectDownSpeedMinSpeed;
        [NMS(Index = 321)]
        /* 0x17C8 */ public float PitchCorrectDownSpeedRange;
        [NMS(Index = 322)]
        /* 0x17CC */ public float PitchCorrectDownSpeedSoftAngle;
        [NMS(Index = 338)]
        /* 0x17D0 */ public float PitchCorrectHeightMax;
        [NMS(Index = 337)]
        /* 0x17D4 */ public float PitchCorrectHeightMin;
        [NMS(Index = 336)]
        /* 0x17D8 */ public float PitchCorrectHeightSpring;
        [NMS(Index = 328)]
        /* 0x17DC */ public float PitchCorrectMaxDownAngle;
        [NMS(Index = 329)]
        /* 0x17E0 */ public float PitchCorrectMaxDownAnglePostCollision;
        [NMS(Index = 331)]
        /* 0x17E4 */ public float PitchCorrectMaxDownAngleWater;
        [NMS(Index = 327)]
        /* 0x17E8 */ public float PitchCorrectSoftDownAngle;
        [NMS(Index = 330)]
        /* 0x17EC */ public float PitchCorrectSoftDownAnglePostCollision;
        [NMS(Index = 332)]
        /* 0x17F0 */ public float PitchCorrectSoftDownAngleWater;
        [NMS(Index = 335)]
        /* 0x17F4 */ public float PitchCorrectTimeHeight;
        [NMS(Index = 334)]
        /* 0x17F8 */ public float PitchCorrectTimeMax;
        [NMS(Index = 333)]
        /* 0x17FC */ public float PitchCorrectTimeMin;
        [NMS(Index = 412)]
        /* 0x1800 */ public float PlayerFreighterClearSpaceRadius;
        [NMS(Index = 200)]
        /* 0x1804 */ public float PostFreighterWarpTransitionTime;
        [NMS(Index = 199)]
        /* 0x1808 */ public float PostWarpSlowDownTime;
        [NMS(Index = 354)]
        /* 0x180C */ public float PowerSettingEngineDamper;
        [NMS(Index = 353)]
        /* 0x1810 */ public float PowerSettingEngineMul;
        [NMS(Index = 358)]
        /* 0x1814 */ public float PowerSettingShieldDamper;
        [NMS(Index = 357)]
        /* 0x1818 */ public float PowerSettingShieldMul;
        [NMS(Index = 356)]
        /* 0x181C */ public float PowerSettingWeaponDamper;
        [NMS(Index = 355)]
        /* 0x1820 */ public float PowerSettingWeaponMul;
        [NMS(Index = 545)]
        /* 0x1824 */ public int ProjectileClipSize;
        [NMS(Index = 543)]
        /* 0x1828 */ public float ProjectileFireRate;
        [NMS(Index = 542)]
        /* 0x182C */ public float ProjectileOverheatTime;
        [NMS(Index = 544)]
        /* 0x1830 */ public float ProjectileReloadTime;
        [NMS(Index = 41)]
        /* 0x1834 */ public float PulseDriveBoostDoubleTapTime;
        [NMS(Index = 203)]
        /* 0x1838 */ public float PulseDrivePlanetApproachHeight;
        [NMS(Index = 202)]
        /* 0x183C */ public float PulseDrivePlanetApproachMaxAngle;
        [NMS(Index = 201)]
        /* 0x1840 */ public float PulseDrivePlanetApproachMinAngle;
        [NMS(Index = 53)]
        /* 0x1844 */ public float PulseDriveStationApproachAngleMin;
        [NMS(Index = 54)]
        /* 0x1848 */ public float PulseDriveStationApproachAngleRange;
        [NMS(Index = 57)]
        /* 0x184C */ public float PulseDriveStationApproachOffset;
        [NMS(Index = 55)]
        /* 0x1850 */ public float PulseDriveStationApproachPerpAngleMin;
        [NMS(Index = 56)]
        /* 0x1854 */ public float PulseDriveStationApproachPerpAngleRange;
        [NMS(Index = 58)]
        /* 0x1858 */ public float PulseDriveStationApproachSlowdown;
        [NMS(Index = 60)]
        /* 0x185C */ public float PulseDriveStationApproachSlowdownRange;
        [NMS(Index = 59)]
        /* 0x1860 */ public float PulseDriveStationApproachSlowdownRangeMin;
        [NMS(Index = 388)]
        /* 0x1864 */ public float RemotePlayerLockTimeAfterShot;
        [NMS(Index = 384)]
        /* 0x1868 */ public float ResetTargetLockAngle;
        [NMS(Index = 467)]
        /* 0x186C */ public float ResourceCollectOffset;
        [NMS(Index = 363)]
        /* 0x1870 */ public float RoyalTakeOffMod;
        [NMS(Index = 52)]
        /* 0x1874 */ public float RudderToRollAngleDownMax;
        [NMS(Index = 51)]
        /* 0x1878 */ public float RudderToRollAngleDownMin;
        [NMS(Index = 50)]
        /* 0x187C */ public float RudderToRollAngleUpMax;
        [NMS(Index = 47)]
        /* 0x1880 */ public float RudderToRollCutoffRotation;
        [NMS(Index = 46)]
        /* 0x1884 */ public float RudderToRollMultiplierLow;
        [NMS(Index = 43)]
        /* 0x1888 */ public float RudderToRollMultiplierMax;
        [NMS(Index = 42)]
        /* 0x188C */ public float RudderToRollMultiplierMin;
        [NMS(Index = 44)]
        /* 0x1890 */ public float RudderToRollMultiplierOpposite;
        [NMS(Index = 45)]
        /* 0x1894 */ public float RudderToRollMultiplierSpace;
        [NMS(Index = 48)]
        /* 0x1898 */ public float RudderToRollUpsideDownRotation;
        [NMS(Index = 638)]
        /* 0x189C */ public float ShakeAlignBrake;
        [NMS(Index = 636)]
        /* 0x18A0 */ public float ShakeMaxPower;
        [NMS(Index = 637)]
        /* 0x18A4 */ public float ShakeMaxSpeed;
        [NMS(Index = 634)]
        /* 0x18A8 */ public float ShakePowerScaler;
        [NMS(Index = 635)]
        /* 0x18AC */ public float ShakeSpeed;
        [NMS(Index = 212)]
        /* 0x18B0 */ public float ShieldEffectHitTime;
        [NMS(Index = 20)]
        /* 0x18B4 */ public float ShieldLeechMul;
        [NMS(Index = 391)]
        /* 0x18B8 */ public float ShieldRechargeMinHitTime;
        [NMS(Index = 392)]
        /* 0x18BC */ public float ShieldRechargeRate;
        [NMS(Index = 622)]
        /* 0x18C0 */ public float ShipDifferentRepelAmount;
        [NMS(Index = 621)]
        /* 0x18C4 */ public float ShipDifferentRepelRange;
        [NMS(Index = 668)]
        /* 0x18C8 */ public float ShipEnterAngle;
        [NMS(Index = 671)]
        /* 0x18CC */ public float ShipEnterMinTime;
        [NMS(Index = 667)]
        /* 0x18D0 */ public float ShipEnterRange;
        [NMS(Index = 670)]
        /* 0x18D4 */ public float ShipEnterSpeed;
        [NMS(Index = 669)]
        /* 0x18D8 */ public float ShipEnterTransitionTime;
        [NMS(Index = 538)]
        /* 0x18DC */ public float ShipHeatAlertTime;
        [NMS(Index = 455)]
        /* 0x18E0 */ public float ShipMotionDeadZone;
        [NMS(Index = 18)]
        /* 0x18E4 */ public float ShipThrottleBrakeVibrationStrength;
        [NMS(Index = 17)]
        /* 0x18E8 */ public float ShipThrottleNotchVibrationStrength;
        [NMS(Index = 19)]
        /* 0x18EC */ public float ShipThrustReverseThreshhold;
        [NMS(Index = 361)]
        /* 0x18F0 */ public float ShuttleTakeOffMod;
        [NMS(Index = 574)]
        /* 0x18F4 */ public float SpaceBrakeAngularRange;
        [NMS(Index = 573)]
        /* 0x18F8 */ public float SpaceBrakeMinAngularSpeed;
        [NMS(Index = 260)]
        /* 0x18FC */ public float SpaceCombatCentreOffsetSwoopMul;
        [NMS(Index = 687)]
        /* 0x1900 */ public float SpaceCombatFollowModeAimTime;
        [NMS(Index = 691)]
        /* 0x1904 */ public float SpaceCombatFollowModeBrakeBehindAngle;
        [NMS(Index = 689)]
        /* 0x1908 */ public float SpaceCombatFollowModeEvadeRoll;
        [NMS(Index = 694)]
        /* 0x190C */ public float SpaceCombatFollowModeEvadeThrust;
        [NMS(Index = 690)]
        /* 0x1910 */ public float SpaceCombatFollowModeEvadeTime;
        [NMS(Index = 692)]
        /* 0x1914 */ public float SpaceCombatFollowModeMaxBrakeBehind;
        [NMS(Index = 693)]
        /* 0x1918 */ public float SpaceCombatFollowModeMaxBrakeHeadOn;
        [NMS(Index = 686)]
        /* 0x191C */ public float SpaceCombatFollowModeMaxTorque;
        [NMS(Index = 688)]
        /* 0x1920 */ public float SpaceCombatFollowModeTargetDistance;
        [NMS(Index = 408)]
        /* 0x1924 */ public float SpeedCoolNormalSpeedAmount;
        [NMS(Index = 407)]
        /* 0x1928 */ public float SpeedCoolOffset;
        [NMS(Index = 678)]
        /* 0x192C */ public float SpeedUpDistanceFadeThreshold;
        [NMS(Index = 677)]
        /* 0x1930 */ public float SpeedUpDistanceThreshold;
        [NMS(Index = 680)]
        /* 0x1934 */ public float SpeedUpVelocityCoeff;
        [NMS(Index = 679)]
        /* 0x1938 */ public float SpeedUpVelocityThreshold;
        [NMS(Index = 254)]
        /* 0x193C */ public float SpringSpeedBoosting;
        [NMS(Index = 255)]
        /* 0x1940 */ public float SpringSpeedBraking;
        [NMS(Index = 252)]
        /* 0x1944 */ public float SpringSpeedDefault;
        [NMS(Index = 253)]
        /* 0x1948 */ public float SpringSpeedRolling;
        [NMS(Index = 251)]
        /* 0x194C */ public float SpringSpeedSpringSpeedIn;
        [NMS(Index = 250)]
        /* 0x1950 */ public float SpringSpeedSpringSpeedOut;
        [NMS(Index = 683)]
        /* 0x1954 */ public float StickLandThreshold;
        [NMS(Index = 684)]
        /* 0x1958 */ public float StickPulseThreshold;
        [NMS(Index = 454)]
        /* 0x195C */ public float StickyStickAngle;
        [NMS(Index = 451)]
        /* 0x1960 */ public float StickyTurnAngleRange;
        [NMS(Index = 453)]
        /* 0x1964 */ public float StickyTurnHigh;
        [NMS(Index = 452)]
        /* 0x1968 */ public float StickyTurnLow;
        [NMS(Index = 450)]
        /* 0x196C */ public float StickyTurnMinAngle;
        [NMS(Index = 547)]
        /* 0x1970 */ public float SummonShipAnywhereFwdOffset;
        [NMS(Index = 546)]
        /* 0x1974 */ public float SummonShipAnywhereHeightOffset;
        [NMS(Index = 550)]
        /* 0x1978 */ public float SummonShipAnywhereRangeMin;
        [NMS(Index = 552)]
        /* 0x197C */ public float SummonShipApproachOffset;
        [NMS(Index = 551)]
        /* 0x1980 */ public float SummonShipHeightOffset;
        [NMS(Index = 548)]
        /* 0x1984 */ public float SummonShipInSpaceRange;
        [NMS(Index = 477)]
        /* 0x1988 */ public int TakeOffCost;
        [NMS(Index = 557)]
        /* 0x198C */ public float TakeOffSphereCastLength;
        [NMS(Index = 558)]
        /* 0x1990 */ public float TakeOffSphereCastRadiusMul;
        [NMS(Index = 385)]
        /* 0x1994 */ public float TargetLockAngleTorpedo;
        [NMS(Index = 567)]
        /* 0x1998 */ public float TargetLockChangeTime;
        [NMS(Index = 568)]
        /* 0x199C */ public float TargetLockLoseTime;
        [NMS(Index = 387)]
        /* 0x19A0 */ public float TargetLockNearestAngle;
        [NMS(Index = 386)]
        /* 0x19A4 */ public float TargetLockRange;
        [NMS(Index = 566)]
        /* 0x19A8 */ public float TargetLockTime;
        [NMS(Index = 206)]
        /* 0x19AC */ public float TestJetsBoost;
        [NMS(Index = 204)]
        /* 0x19B0 */ public float TestJetsStage1;
        [NMS(Index = 205)]
        /* 0x19B4 */ public float TestJetsStage2;
        [NMS(Index = 211)]
        /* 0x19B8 */ public float TestShieldEffect;
        [NMS(Index = 190)]
        /* 0x19BC */ public float TestShipAnimLowAltitude;
        [NMS(Index = 188)]
        /* 0x19C0 */ public float TestShipAnimPulse;
        [NMS(Index = 186)]
        /* 0x19C4 */ public float TestShipAnimRoll;
        [NMS(Index = 189)]
        /* 0x19C8 */ public float TestShipAnimSpace;
        [NMS(Index = 187)]
        /* 0x19CC */ public float TestShipAnimThrust;
        [NMS(Index = 103)]
        /* 0x19D0 */ public float TestTrailRadius;
        [NMS(Index = 102)]
        /* 0x19D4 */ public float TestTrailSpeed;
        [NMS(Index = 207)]
        /* 0x19D8 */ public float TestTrailThreshold;
        [NMS(Index = 404)]
        /* 0x19DC */ public float ThrustDecaySpring;
        [NMS(Index = 405)]
        /* 0x19E0 */ public float ThrustDecaySpringCombat;
        [NMS(Index = 101)]
        /* 0x19E4 */ public int TrailMaxNumPointsPerFrameOverride;
        [NMS(Index = 196)]
        /* 0x19E8 */ public float TrailVelocityFactor;
        [NMS(Index = 417)]
        /* 0x19EC */ public float TurnRudderStrength;
        [NMS(Index = 39)]
        /* 0x19F0 */ public float VignetteAmountAcceleration;
        [NMS(Index = 40)]
        /* 0x19F4 */ public float VignetteAmountTurning;
        [NMS(Index = 105)]
        /* 0x19F8 */ public float WarpAnimMaxSpeed;
        [NMS(Index = 104)]
        /* 0x19FC */ public float WarpAnimMinSpeed;
        [NMS(Index = 141)]
        /* 0x1A00 */ public float WarpInFlashTime;
        [NMS(Index = 145)]
        /* 0x1A04 */ public float WarpInFlashTimeFreighter;
        [NMS(Index = 154)]
        /* 0x1A08 */ public float WarpInFlashTimeNexus;
        [NMS(Index = 149)]
        /* 0x1A0C */ public float WarpInLineWidth;
        [NMS(Index = 142)]
        /* 0x1A10 */ public float WarpInRange;
        [NMS(Index = 146)]
        /* 0x1A14 */ public float WarpInRangeFreighter;
        [NMS(Index = 155)]
        /* 0x1A18 */ public float WarpInRangeNexus;
        [NMS(Index = 140)]
        /* 0x1A1C */ public float WarpInTime;
        [NMS(Index = 144)]
        /* 0x1A20 */ public float WarpInTimeFreighter;
        [NMS(Index = 153)]
        /* 0x1A24 */ public float WarpInTimeNexus;
        [NMS(Index = 159)]
        /* 0x1A28 */ public float WarpNexusDistance;
        [NMS(Index = 158)]
        /* 0x1A2C */ public float WarpNexusPitch;
        [NMS(Index = 157)]
        /* 0x1A30 */ public float WarpNexusRotation;
        [NMS(Index = 160)]
        /* 0x1A34 */ public float WarpOnFootInCorvetteMaxWaitTime;
        [NMS(Index = 151)]
        /* 0x1A38 */ public float WarpOutRange;
        [NMS(Index = 152)]
        /* 0x1A3C */ public float WarpOutTime;
        [NMS(Index = 143)]
        /* 0x1A40 */ public float WarpScale;
        [NMS(Index = 147)]
        /* 0x1A44 */ public float WarpScaleFreighter;
        [NMS(Index = 156)]
        /* 0x1A48 */ public float WarpScaleNexus;
        [NMS(Index = 121)]
        /* 0x1A4C */ public float WaterEffectScaler;
        [NMS(Index = 266)]
        /* 0x1A50 */ public float WeaponDamagePotentialReferenceRange;
        [NMS(Index = 653)]
        /* 0x1A54 */ public float WingmanAlign;
        [NMS(Index = 654)]
        /* 0x1A58 */ public float WingmanAngle;
        [NMS(Index = 655)]
        /* 0x1A5C */ public float WingmanAngle2;
        [NMS(Index = 648)]
        /* 0x1A60 */ public float WingmanAttackAimAngle;
        [NMS(Index = 646)]
        /* 0x1A64 */ public float WingmanAttackAngle;
        [NMS(Index = 651)]
        /* 0x1A68 */ public float WingmanAttackCoolTime;
        [NMS(Index = 645)]
        /* 0x1A6C */ public float WingmanAttackMinRange;
        [NMS(Index = 643)]
        /* 0x1A70 */ public float WingmanAttackOffset;
        [NMS(Index = 644)]
        /* 0x1A74 */ public float WingmanAttackRange;
        [NMS(Index = 649)]
        /* 0x1A78 */ public float WingmanAttackTime;
        [NMS(Index = 650)]
        /* 0x1A7C */ public float WingmanAttackTimeout;
        [NMS(Index = 640)]
        /* 0x1A80 */ public float WingmanAtTime;
        [NMS(Index = 641)]
        /* 0x1A84 */ public float WingmanAtTimeBack;
        [NMS(Index = 639)]
        /* 0x1A88 */ public float WingmanAtTimeStart;
        [NMS(Index = 657)]
        /* 0x1A8C */ public float WingmanFwd1;
        [NMS(Index = 658)]
        /* 0x1A90 */ public float WingmanFwd2;
        [NMS(Index = 642)]
        /* 0x1A94 */ public float WingmanPerpTime;
        [NMS(Index = 656)]
        /* 0x1A98 */ public float WingmanRadius;
        [NMS(Index = 652)]
        /* 0x1A9C */ public float WingmanSpawnDist;
        [NMS(Index = 114)]
        /* 0x1AA0 */ public float WingmanSpeedApproachSpeed;
        [NMS(Index = 115)]
        /* 0x1AA4 */ public float WingmanSpeedApproachSpeedSpace;
        [NMS(Index = 112)]
        /* 0x1AA8 */ public float WingmanSpeedTrackDistance;
        [NMS(Index = 110)]
        /* 0x1AAC */ public float WingmanSpeedTrackForceMax;
        [NMS(Index = 111)]
        /* 0x1AB0 */ public float WingmanSpeedTrackForceMin;
        [NMS(Index = 113)]
        /* 0x1AB4 */ public float WingmanSpeedTrackOffset;
        [NMS(Index = 647)]
        /* 0x1AB8 */ public float WingmanViewerAngle;
        [NMS(Index = 0)]
        /* 0x1ABC */ public GcShipDataNames HoverShipDataNames;
        [NMS(Index = 1)]
        /* 0x1BDC */ public GcShipDataNames HoverShipDataNamesSpecial;
        [NMS(Index = 2)]
        /* 0x1CFC */ public GcShipDataNames SpookShipDataNames;
        [NMS(Index = 92, MxmlName = "3rdPersonShipEnabled")]
        /* 0x1E1C */ public bool _3rdPersonShipEnabled;
        [NMS(Index = 248, MxmlName = "3rdPersonWarpWanderCurve")]
        /* 0x1E1D */ public TkCurveType _3rdPersonWarpWanderCurve;
        [NMS(Index = 24)]
        /* 0x1E1E */ public bool AddShipToCollectionOnEnter;
        [NMS(Index = 93)]
        /* 0x1E1F */ public bool AimZoomAuto;
        [NMS(Index = 23)]
        /* 0x1E20 */ public bool AllowSideScreenPointing;
        [NMS(Index = 429)]
        /* 0x1E21 */ public bool AltAtmosphere;
        [NMS(Index = 90)]
        /* 0x1E22 */ public bool AltControls;
        [NMS(Index = 98)]
        /* 0x1E23 */ public bool ApplyHeightAlign;
        [NMS(Index = 97)]
        /* 0x1E24 */ public bool ApplyHeightForce;
        [NMS(Index = 381)]
        /* 0x1E25 */ public bool AutoEjectOnLanding;
        [NMS(Index = 109)]
        /* 0x1E26 */ public TkCurveType CockpitExitAnimCurve;
        [NMS(Index = 95)]
        /* 0x1E27 */ public bool CritsFromBehind;
        [NMS(Index = 589)]
        /* 0x1E28 */ public TkCurveType DeflectCurve;
        [NMS(Index = 175)]
        /* 0x1E29 */ public TkCurveType DirectionDockingIndicatorCurve;
        [NMS(Index = 99)]
        /* 0x1E2A */ public bool DoPreCollision;
        [NMS(Index = 96)]
        /* 0x1E2B */ public bool DrawLineLockTarget;
        [NMS(Index = 94)]
        /* 0x1E2C */ public bool EnableDepthTestedCrosshairSections;
        [NMS(Index = 25)]
        /* 0x1E2D */ public bool EnablePulseDriveSpaceStationOrient;
        [NMS(Index = 315)]
        /* 0x1E2E */ public TkCurveType GroundHeightHardCurve;
        [NMS(Index = 309)]
        /* 0x1E2F */ public TkCurveType GroundHeightSoftCurve;
        [NMS(Index = 382)]
        /* 0x1E30 */ public bool LandedCockpitFreeLook;
        [NMS(Index = 291)]
        /* 0x1E31 */ public bool LandingCheckBuildings;
        [NMS(Index = 340)]
        /* 0x1E32 */ public TkCurveType LandingCurve;
        [NMS(Index = 341)]
        /* 0x1E33 */ public TkCurveType LandingCurveHeavy;
        [NMS(Index = 342)]
        /* 0x1E34 */ public TkCurveType LandingCurveWater;
        [NMS(Index = 532)]
        /* 0x1E35 */ public TkCurveType MiniWarpCurve;
        [NMS(Index = 339)]
        /* 0x1E36 */ public TkCurveType PitchCorrectHeightCurve;
        [NMS(Index = 49)]
        /* 0x1E37 */ public TkCurveType RudderToRollCurve;
        [NMS(Index = 213)]
        /* 0x1E38 */ public TkCurveType ShieldEffectHitCurve;
        [NMS(Index = 696)]
        /* 0x1E39 */ public bool SpaceCombatFollowModeUseBoost;
        [NMS(Index = 695)]
        /* 0x1E3A */ public bool SpaceCombatFollowModeUseEvadeTarget;
        [NMS(Index = 91)]
        /* 0x1E3B */ public bool SpaceMapInWorld;
        [NMS(Index = 100)]
        /* 0x1E3C */ public bool SpeedTrackModeEnabled;
        [NMS(Index = 249)]
        /* 0x1E3D */ public bool SpringSpeedSpringEnabled;
        [NMS(Index = 185)]
        /* 0x1E3E */ public bool TestShipAnims;
        [NMS(Index = 150)]
        /* 0x1E3F */ public TkCurveType WarpInCurve;
    }
}
