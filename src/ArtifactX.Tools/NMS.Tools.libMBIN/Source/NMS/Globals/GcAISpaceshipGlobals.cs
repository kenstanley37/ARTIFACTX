using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.Globals
{
    [NMS(GUID = 0x94859C1E46C886E4, NameHash = 0xF1C0C3FD)]
    public class GcAISpaceshipGlobals : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x000 */ public GcPlayerSquadronConfig PlayerSquadronConfig;
        [NMS(Index = 148)]
        /* 0x230 */ public Colour AlertLightColour;
        [NMS(Index = 146)]
        /* 0x240 */ public Colour FreighterDoorColourActive;
        [NMS(Index = 147)]
        /* 0x250 */ public Colour FreighterDoorColourInactive;
        [NMS(Index = 1)]
        /* 0x260 */ public Colour FreighterEngineGlowDefaultColour;
        [NMS(Index = 155)]
        /* 0x270 */ public Vector3f TurretAlertLightOffset;
        [NMS(Index = 73, Size = 0x8, EnumType = typeof(GcAISpaceshipTypes.ShipTypeEnum))]
        /* 0x280 */ public NMSString0x10[] ProjectileWeaponMuzzleFlashes;
        [NMS(Index = 341, Size = 0x8, EnumType = typeof(GcAISpaceshipTypes.ShipTypeEnum))]
        /* 0x300 */ public NMSString0x10[] WarpArriveEffectIDs;
        [NMS(Index = 340, Size = 0x8, EnumType = typeof(GcAISpaceshipTypes.ShipTypeEnum))]
        /* 0x380 */ public NMSString0x10[] WarpStartEffectIDs;
        [NMS(Index = 81)]
        /* 0x400 */ public GcSpaceshipTravelData AsteroidMiningPositioningTravelData;
        [NMS(Index = 82)]
        /* 0x448 */ public GcSpaceshipTravelData AsteroidMiningTravelData;
        [NMS(Index = 79)]
        /* 0x490 */ public GcSpaceshipTravelData FallbackTravelData;
        [NMS(Index = 99)]
        /* 0x4D8 */ public GcSpaceshipTravelData OutpostLanding;
        [NMS(Index = 100)]
        /* 0x520 */ public GcSpaceshipTravelData PlanetLanding;
        [NMS(Index = 80)]
        /* 0x568 */ public GcSpaceshipTravelData SlowCombatEffectAttackTravel;
        [NMS(Index = 421)]
        /* 0x5B0 */ public GcShipAIPlanetPatrolData WingmanPathData;
        [NMS(Index = 420)]
        /* 0x5E8 */ public NMSString0x20A AmbientCivFreighterSquadName;
        [NMS(Index = 418)]
        /* 0x608 */ public NMSString0x20A PirateSquadName;
        [NMS(Index = 419)]
        /* 0x628 */ public NMSString0x20A PoliceFreighterSquadName;
        [NMS(Index = 422)]
        /* 0x648 */ public List<GcAIShipDebugSpawnData> DebugShipSpawns;
        [NMS(Index = 34)]
        /* 0x658 */ public GcFilename EnergyShield;
        [NMS(Index = 35)]
        /* 0x668 */ public NMSString0x10 EnergyShieldDepletedEffect;
        [NMS(Index = 36)]
        /* 0x678 */ public NMSString0x10 EnergyShieldStartRechargeEffect;
        [NMS(Index = 37)]
        /* 0x688 */ public NMSString0x10 EnergyShieldStartRechargeFromDepletedEffect;
        [NMS(Index = 143)]
        /* 0x698 */ public GcFilename HangarFilename;
        [NMS(Index = 142)]
        /* 0x6A8 */ public GcFilename LegacyHangarFilename;
        [NMS(Index = 428)]
        /* 0x6B8 */ public List<GcSwarmDroneFlockingParams> SwarmDroneFlockingParams;
        [NMS(Index = 423)]
        /* 0x6C8 */ public NMSString0x10 SwarmHiveSpaceAnomalyAttackDefinition;
        [NMS(Index = 427)]
        /* 0x6D8 */ public NMSString0x10 SwarmHiveSpaceAnomalyMinorLaserPlayerDamage;
        [NMS(Index = 425)]
        /* 0x6E8 */ public NMSString0x10 SwarmHiveSpaceAnomalyPrimaryLaserPlayerDamage;
        [NMS(Index = 269)]
        /* 0x6F8 */ public List<Colour> TradeRouteColours;
        [NMS(Index = 78)]
        /* 0x708 */ public GcCombatEffectsComponentData CombatEffectsComponentData;
        [NMS(Index = 359)]
        /* 0x750 */ public GcProjectileLineData ShipBullet;
        [NMS(Index = 417)]
        /* 0x778 */ public GcShipAIDeathData Death;
        [NMS(Index = 149)]
        /* 0x794 */ public TkHitCurveData FreighterLightHitCurve;
        [NMS(Index = 137)]
        /* 0x7A0 */ public Vector2f ConeSpawnOffsetFactor;
        [NMS(Index = 125)]
        /* 0x7A8 */ public Vector2f ExpeditionFleetSpawnAngle;
        [NMS(Index = 128)]
        /* 0x7B0 */ public Vector2f ExpeditionFleetSpawnOffset;
        [NMS(Index = 126)]
        /* 0x7B8 */ public Vector2f ExpeditionFleetSpawnPitch;
        [NMS(Index = 127)]
        /* 0x7C0 */ public Vector2f ExpeditionFleetSpawnRange;
        [NMS(Index = 130)]
        /* 0x7C8 */ public Vector2f ExpeditionFleetSpawnSunAroundAngle;
        [NMS(Index = 129)]
        /* 0x7D0 */ public Vector2f ExpeditionFleetSpawnSunHeightAngle;
        [NMS(Index = 343)]
        /* 0x7D8 */ public Vector2f FlagshipBattleFighterWarpOutDelayTimeRange;
        [NMS(Index = 342)]
        /* 0x7E0 */ public Vector2f FlagshipBattleFrigateWarpOutDelayTimeRange;
        [NMS(Index = 177)]
        /* 0x7E8 */ public Vector2f FreighterMiniSpeeds;
        [NMS(Index = 174)]
        /* 0x7F0 */ public Vector2f PirateFreighterAttackRange;
        [NMS(Index = 274)]
        /* 0x7F8 */ public Vector2f PoliceSideOffset;
        [NMS(Index = 275)]
        /* 0x800 */ public Vector2f PoliceUpOffset;
        [NMS(Index = 200)]
        /* 0x808 */ public float AbandonedSystemShipSpawnProbablity;
        [NMS(Index = 247)]
        /* 0x80C */ public float ArrivalStaggerOffset;
        [NMS(Index = 84)]
        /* 0x810 */ public float AsteroidMiningMaxAsteroidRadius;
        [NMS(Index = 90)]
        /* 0x814 */ public float AsteroidMiningMaxMiningTime;
        [NMS(Index = 87)]
        /* 0x818 */ public float AsteroidMiningMaxViewAnglePitch;
        [NMS(Index = 85)]
        /* 0x81C */ public float AsteroidMiningMaxViewAngleYaw;
        [NMS(Index = 88)]
        /* 0x820 */ public float AsteroidMiningMinDistFromPlayer;
        [NMS(Index = 89)]
        /* 0x824 */ public float AsteroidMiningMinMiningAngle;
        [NMS(Index = 86)]
        /* 0x828 */ public float AsteroidMiningMinViewAnglePitch;
        [NMS(Index = 83)]
        /* 0x82C */ public float AsteroidMiningSearchRadius;
        [NMS(Index = 192)]
        /* 0x830 */ public float AsteroidShootAngle;
        [NMS(Index = 409)]
        /* 0x834 */ public float AtmosphereEffectMax;
        [NMS(Index = 408)]
        /* 0x838 */ public float AtmosphereEffectMin;
        [NMS(Index = 402)]
        /* 0x83C */ public float AtmosphereTerminalSpeed;
        [NMS(Index = 176)]
        /* 0x840 */ public float AttackAfterSpawnTime;
        [NMS(Index = 306)]
        /* 0x844 */ public float AttackAimTime;
        [NMS(Index = 179)]
        /* 0x848 */ public float AttackBuildingApproachDistance;
        [NMS(Index = 181)]
        /* 0x84C */ public float AttackBuildingAttackRunDistTolerance;
        [NMS(Index = 183)]
        /* 0x850 */ public float AttackBuildingBugOutDistance;
        [NMS(Index = 185)]
        /* 0x854 */ public float AttackBuildingBugOutSpeedUp;
        [NMS(Index = 184)]
        /* 0x858 */ public float AttackBuildingBugOutTurnUp;
        [NMS(Index = 182)]
        /* 0x85C */ public float AttackBuildingFiringAngleTolerance;
        [NMS(Index = 178)]
        /* 0x860 */ public float AttackBuildingGetThereBoost;
        [NMS(Index = 189)]
        /* 0x864 */ public float AttackBuildingNextRunAngleDeltaMax;
        [NMS(Index = 188)]
        /* 0x868 */ public float AttackBuildingNextRunAngleDeltaMin;
        [NMS(Index = 187)]
        /* 0x86C */ public float AttackBuildingRunAngleMax;
        [NMS(Index = 186)]
        /* 0x870 */ public float AttackBuildingRunAngleMin;
        [NMS(Index = 180)]
        /* 0x874 */ public float AttackBuildingRunStartDistance;
        [NMS(Index = 191)]
        /* 0x878 */ public float AttackBuildingTargetGroundOffsetScaleEnd;
        [NMS(Index = 190)]
        /* 0x87C */ public float AttackBuildingTargetGroundOffsetScaleStart;
        [NMS(Index = 171)]
        /* 0x880 */ public float AttackFreighterAngle;
        [NMS(Index = 158)]
        /* 0x884 */ public float AttackFreighterApproach;
        [NMS(Index = 161)]
        /* 0x888 */ public float AttackFreighterApproachDistance;
        [NMS(Index = 162)]
        /* 0x88C */ public float AttackFreighterAttackRunStartDistance;
        [NMS(Index = 163)]
        /* 0x890 */ public float AttackFreighterBugOutDistance;
        [NMS(Index = 165)]
        /* 0x894 */ public float AttackFreighterButOutSpeedUp;
        [NMS(Index = 164)]
        /* 0x898 */ public float AttackFreighterButOutTurnUp;
        [NMS(Index = 157)]
        /* 0x89C */ public float AttackFreighterGetThereBoost;
        [NMS(Index = 172)]
        /* 0x8A0 */ public float AttackFreighterRunOffset;
        [NMS(Index = 159)]
        /* 0x8A4 */ public float AttackFreighterWingmanAlignMinDist;
        [NMS(Index = 160)]
        /* 0x8A8 */ public float AttackFreighterWingmanAlignRange;
        [NMS(Index = 169)]
        /* 0x8AC */ public float AttackFreighterWingmanLock;
        [NMS(Index = 168)]
        /* 0x8B0 */ public float AttackFreighterWingmanLockAlign;
        [NMS(Index = 170)]
        /* 0x8B4 */ public float AttackFreighterWingmanMaxForce;
        [NMS(Index = 167)]
        /* 0x8B8 */ public float AttackFreighterWingmanOffset;
        [NMS(Index = 166)]
        /* 0x8BC */ public float AttackFreighterWingmanRadius;
        [NMS(Index = 173)]
        /* 0x8C0 */ public float AttackFreighterWingmanStart;
        [NMS(Index = 92)]
        /* 0x8C4 */ public float AttackMinimumTimeBeforeTargetSwitch;
        [NMS(Index = 307)]
        /* 0x8C8 */ public float AttackRunSlowdown;
        [NMS(Index = 91)]
        /* 0x8CC */ public float AttackShipAvoidStartTime;
        [NMS(Index = 72)]
        /* 0x8D0 */ public float AttackTooCloseMinRelSpeed;
        [NMS(Index = 233)]
        /* 0x8D4 */ public float BattleSpawnStationMinDistance;
        [NMS(Index = 202)]
        /* 0x8D8 */ public float BountySpawnAngle;
        [NMS(Index = 403)]
        /* 0x8DC */ public float CircleApproachDistance;
        [NMS(Index = 98)]
        /* 0x8E0 */ public float CollisionRayLengthMax;
        [NMS(Index = 97)]
        /* 0x8E4 */ public float CollisionRayLengthMin;
        [NMS(Index = 227)]
        /* 0x8E8 */ public float CollisionReactionTime;
        [NMS(Index = 139)]
        /* 0x8EC */ public float ConeSpawnFlattenDown;
        [NMS(Index = 138)]
        /* 0x8F0 */ public float ConeSpawnFlattenUp;
        [NMS(Index = 56)]
        /* 0x8F4 */ public float CrashedShipBrokenSlotChance;
        [NMS(Index = 57)]
        /* 0x8F8 */ public float CrashedShipBrokenTechChance;
        [NMS(Index = 59)]
        /* 0x8FC */ public float CrashedShipGeneralCostDiscount;
        [NMS(Index = 54)]
        /* 0x900 */ public int CrashedShipMinNonBrokenSlots;
        [NMS(Index = 58)]
        /* 0x904 */ public float CrashedShipRepairSlotCostIncreaseFactor;
        [NMS(Index = 60)]
        /* 0x908 */ public float CrashedShipTechSlotsCostDiscount;
        [NMS(Index = 272)]
        /* 0x90C */ public float DirectionBrakeThresholdSq;
        [NMS(Index = 69)]
        /* 0x910 */ public float DistanceFlareFlickerAmp;
        [NMS(Index = 68)]
        /* 0x914 */ public float DistanceFlareFlickerFreq;
        [NMS(Index = 65)]
        /* 0x918 */ public float DistanceFlareMaxScale;
        [NMS(Index = 62)]
        /* 0x91C */ public float DistanceFlareMinDistance;
        [NMS(Index = 64)]
        /* 0x920 */ public float DistanceFlareMinScale;
        [NMS(Index = 66)]
        /* 0x924 */ public float DistanceFlareMinSpeed;
        [NMS(Index = 63)]
        /* 0x928 */ public float DistanceFlareRange;
        [NMS(Index = 67)]
        /* 0x92C */ public float DistanceFlareSpeedRange;
        [NMS(Index = 390)]
        /* 0x930 */ public float DockingLandingBounceHeight;
        [NMS(Index = 389)]
        /* 0x934 */ public float DockingLandingBounceTime;
        [NMS(Index = 386)]
        /* 0x938 */ public float DockingLandingTime;
        [NMS(Index = 387)]
        /* 0x93C */ public float DockingLandingTimeDirectional;
        [NMS(Index = 392)]
        /* 0x940 */ public float DockingRotateSpeed;
        [NMS(Index = 391)]
        /* 0x944 */ public float DockingRotateStartTime;
        [NMS(Index = 388)]
        /* 0x948 */ public float DockingSpringTime;
        [NMS(Index = 26)]
        /* 0x94C */ public float DockingWaitDistance;
        [NMS(Index = 394)]
        /* 0x950 */ public float DockWaitMaxTime;
        [NMS(Index = 393)]
        /* 0x954 */ public float DockWaitMinTime;
        [NMS(Index = 42)]
        /* 0x958 */ public float EnergyShieldFadeInRate;
        [NMS(Index = 39)]
        /* 0x95C */ public float EnergyShieldFadeMinOpacityInCombat;
        [NMS(Index = 41)]
        /* 0x960 */ public float EnergyShieldFadeNonPlayerHitOpacity;
        [NMS(Index = 43)]
        /* 0x964 */ public float EnergyShieldFadeOutRate;
        [NMS(Index = 40)]
        /* 0x968 */ public float EnergyShieldFreighterFadeMinOpacityInCombat;
        [NMS(Index = 304)]
        /* 0x96C */ public float EngineFireSize;
        [NMS(Index = 298)]
        /* 0x970 */ public float EngineFlareAccelMax;
        [NMS(Index = 297)]
        /* 0x974 */ public float EngineFlareAccelMin;
        [NMS(Index = 303)]
        /* 0x978 */ public float EngineFlareOffset;
        [NMS(Index = 300)]
        /* 0x97C */ public float EngineFlareSizeMax;
        [NMS(Index = 299)]
        /* 0x980 */ public float EngineFlareSizeMin;
        [NMS(Index = 301)]
        /* 0x984 */ public float EngineFlareVibrateAmp;
        [NMS(Index = 302)]
        /* 0x988 */ public float EngineFlareVibrateFreq;
        [NMS(Index = 53)]
        /* 0x98C */ public float EscapeRoll;
        [NMS(Index = 52)]
        /* 0x990 */ public float EscapeRollPlanet;
        [NMS(Index = 51)]
        /* 0x994 */ public float EscapeRollTime;
        [NMS(Index = 50)]
        /* 0x998 */ public float EscapeRollTimePlanet;
        [NMS(Index = 414)]
        /* 0x99C */ public float FinalDeathExplosionScale;
        [NMS(Index = 415)]
        /* 0x9A0 */ public float FinalDeathExplosionTime;
        [NMS(Index = 416)]
        /* 0x9A4 */ public float FinalDeathFadeTime;
        [NMS(Index = 201)]
        /* 0x9A8 */ public int FlybyCloseOdds;
        [NMS(Index = 357)]
        /* 0x9AC */ public float FlybyHeight;
        [NMS(Index = 356)]
        /* 0x9B0 */ public float FlybyLength;
        [NMS(Index = 358)]
        /* 0x9B4 */ public float FlybyOffset;
        [NMS(Index = 77)]
        /* 0x9B8 */ public float FlybyPlanetLandingProbability;
        [NMS(Index = 141)]
        /* 0x9BC */ public float FreighterAlertLightCapitalSize;
        [NMS(Index = 151)]
        /* 0x9C0 */ public float FreighterAlertLightIntensity;
        [NMS(Index = 150)]
        /* 0x9C4 */ public float FreighterAlertLightTime;
        [NMS(Index = 280)]
        /* 0x9C8 */ public float FreighterAlertThreshold;
        [NMS(Index = 284)]
        /* 0x9CC */ public float FreighterAlertTimeOutMinTime;
        [NMS(Index = 283)]
        /* 0x9D0 */ public float FreighterAlertTimeOutRate;
        [NMS(Index = 282)]
        /* 0x9D4 */ public float FreighterAttackAlertThreshold;
        [NMS(Index = 281)]
        /* 0x9D8 */ public float FreighterAttackDisengageDistance;
        [NMS(Index = 279)]
        /* 0x9DC */ public float FreighterImpactScale;
        [NMS(Index = 287)]
        /* 0x9E0 */ public float FreighterLaunchStartTime;
        [NMS(Index = 286)]
        /* 0x9E4 */ public float FreighterLaunchTime;
        [NMS(Index = 290)]
        /* 0x9E8 */ public int FreighterMaxNumLaunchedShips;
        [NMS(Index = 289)]
        /* 0x9EC */ public float FreighterRegisterHitCooldown;
        [NMS(Index = 228)]
        /* 0x9F0 */ public float FreighterScale;
        [NMS(Index = 285)]
        /* 0x9F4 */ public float FreighterShipLaunchSpeed;
        [NMS(Index = 291)]
        /* 0x9F8 */ public float FreighterSpawnMargin;
        [NMS(Index = 292)]
        /* 0x9FC */ public float FreighterSpawnRadius;
        [NMS(Index = 293)]
        /* 0xA00 */ public float FreighterSpawnRate;
        [NMS(Index = 294)]
        /* 0xA04 */ public float FreighterSpawnViewAngle;
        [NMS(Index = 295)]
        /* 0xA08 */ public float FreighterSpawnVisibleFreightersDistance;
        [NMS(Index = 296)]
        /* 0xA0C */ public float FrigateSpawnMargin;
        [NMS(Index = 405)]
        /* 0xA10 */ public float GroundCircleHeight;
        [NMS(Index = 406)]
        /* 0xA14 */ public float GroundCircleHeightMax;
        [NMS(Index = 222)]
        /* 0xA18 */ public float HeightTestSampleDistance;
        [NMS(Index = 223)]
        /* 0xA1C */ public float HeightTestSampleTime;
        [NMS(Index = 401)]
        /* 0xA20 */ public float LandingDirectionalHoverPointReachedDistance;
        [NMS(Index = 400)]
        /* 0xA24 */ public float LandingHoverPointReachedDistance;
        [NMS(Index = 124)]
        /* 0xA28 */ public float LandingLongTipAngle;
        [NMS(Index = 399)]
        /* 0xA2C */ public float LandingManeuvreAlignTime;
        [NMS(Index = 398)]
        /* 0xA30 */ public float LandingManuevreTime;
        [NMS(Index = 123)]
        /* 0xA34 */ public float LandingTipAngle;
        [NMS(Index = 271)]
        /* 0xA38 */ public float LaserHitOffset;
        [NMS(Index = 385)]
        /* 0xA3C */ public float LowerLandingGearDistanceMultiplier;
        [NMS(Index = 14)]
        /* 0xA40 */ public float MaxDifficultySpaceCombatSpeedExtra;
        [NMS(Index = 13)]
        /* 0xA44 */ public float MaxDifficultySpaceCombatTurnExtra;
        [NMS(Index = 29)]
        /* 0xA48 */ public int MaxNumActivePolice;
        [NMS(Index = 30)]
        /* 0xA4C */ public float MaxNumActivePoliceRadius;
        [NMS(Index = 28)]
        /* 0xA50 */ public float MaxNumActiveTraderRadius;
        [NMS(Index = 27)]
        /* 0xA54 */ public int MaxNumActiveTraders;
        [NMS(Index = 230)]
        /* 0xA58 */ public int MaxNumFreighters;
        [NMS(Index = 152)]
        /* 0xA5C */ public int MaxNumTurretMissiles;
        [NMS(Index = 410)]
        /* 0xA60 */ public float MaxTorque;
        [NMS(Index = 31)]
        /* 0xA64 */ public int MinAggroDamage;
        [NMS(Index = 15)]
        /* 0xA68 */ public float MinimumCircleTimeBeforeLanding;
        [NMS(Index = 16)]
        /* 0xA6C */ public float MinimumTimeBetweenOutpostLandings;
        [NMS(Index = 44)]
        /* 0xA70 */ public float MinLaserFireTime;
        [NMS(Index = 175)]
        /* 0xA74 */ public float MissileLaunchSpeed;
        [NMS(Index = 145)]
        /* 0xA78 */ public float MissileRange;
        [NMS(Index = 310)]
        /* 0xA7C */ public float MoveAvoidRange;
        [NMS(Index = 309)]
        /* 0xA80 */ public float MoveHeightAdjust;
        [NMS(Index = 313)]
        /* 0xA84 */ public float MoveHeightCheckTime;
        [NMS(Index = 311)]
        /* 0xA88 */ public int MoveHeightNumSamples;
        [NMS(Index = 312)]
        /* 0xA8C */ public float MoveHeightSampleSectionSize;
        [NMS(Index = 407)]
        /* 0xA90 */ public float OrbitHeight;
        [NMS(Index = 116)]
        /* 0xA94 */ public float OutpostDockAIApproachSpeedForce;
        [NMS(Index = 115)]
        /* 0xA98 */ public float OutpostDockAIGetToApproachBrakeForce;
        [NMS(Index = 114)]
        /* 0xA9C */ public float OutpostDockAIGetToApproachForce;
        [NMS(Index = 112)]
        /* 0xAA0 */ public float OutpostDockApproachDistance;
        [NMS(Index = 122)]
        /* 0xAA4 */ public float OutpostDockApproachRenderFlickerOffset;
        [NMS(Index = 121)]
        /* 0xAA8 */ public float OutpostDockApproachRenderRadius;
        [NMS(Index = 111)]
        /* 0xAAC */ public float OutpostDockApproachSpeedForce;
        [NMS(Index = 113)]
        /* 0xAB0 */ public float OutpostDockApproachSpeedUpDamper;
        [NMS(Index = 120)]
        /* 0xAB4 */ public float OutpostDockApproachUpAmount;
        [NMS(Index = 109)]
        /* 0xAB8 */ public float OutpostDockGetToApproachBrakeForce;
        [NMS(Index = 110)]
        /* 0xABC */ public float OutpostDockGetToApproachExtraBrakeForce;
        [NMS(Index = 108)]
        /* 0xAC0 */ public float OutpostDockGetToApproachForce;
        [NMS(Index = 106)]
        /* 0xAC4 */ public float OutpostDockMaxApproachSpeed;
        [NMS(Index = 117)]
        /* 0xAC8 */ public float OutpostDockMaxForce;
        [NMS(Index = 119)]
        /* 0xACC */ public float OutpostDockMaxTipLength;
        [NMS(Index = 118)]
        /* 0xAD0 */ public float OutpostDockMinTipLength;
        [NMS(Index = 107)]
        /* 0xAD4 */ public float OutpostDockOverspeedBrake;
        [NMS(Index = 105)]
        /* 0xAD8 */ public float OutpostDockUpAlignMaxAngle;
        [NMS(Index = 104)]
        /* 0xADC */ public float OutpostDockUpAlignMaxAngleFirstPerson;
        [NMS(Index = 102)]
        /* 0xAE0 */ public float OutpostLandingNoiseAmp;
        [NMS(Index = 101)]
        /* 0xAE4 */ public float OutpostLandingNoiseFreq;
        [NMS(Index = 103)]
        /* 0xAE8 */ public float OutpostLandingNoiseOffset;
        [NMS(Index = 224)]
        /* 0xAEC */ public float OutpostToLandingDistance;
        [NMS(Index = 215)]
        /* 0xAF0 */ public float PirateArriveTime;
        [NMS(Index = 219)]
        /* 0xAF4 */ public float PirateBattleInterestTime;
        [NMS(Index = 214)]
        /* 0xAF8 */ public float PirateBattleMaxTime;
        [NMS(Index = 217)]
        /* 0xAFC */ public float PirateBattleStartSpeed;
        [NMS(Index = 93)]
        /* 0xB00 */ public float PirateExtraDamage;
        [NMS(Index = 355)]
        /* 0xB04 */ public float PirateFlybyLength;
        [NMS(Index = 218)]
        /* 0xB08 */ public float PirateInterestTime;
        [NMS(Index = 95)]
        /* 0xB0C */ public float PirateMaintainBuildingTargetTime;
        [NMS(Index = 308)]
        /* 0xB10 */ public float PiratePlayerAttackRange;
        [NMS(Index = 213)]
        /* 0xB14 */ public float PirateSpawnAngle;
        [NMS(Index = 61)]
        /* 0xB18 */ public float PirateSpawnSpacing;
        [NMS(Index = 216)]
        /* 0xB1C */ public float PirateStartSpeed;
        [NMS(Index = 305)]
        /* 0xB20 */ public float PitchFlip;
        [NMS(Index = 46)]
        /* 0xB24 */ public float PlanetaryPirateHostileShipPerceptionRange;
        [NMS(Index = 47)]
        /* 0xB28 */ public float PlanetaryPirateRaidFocusBuildingsTime;
        [NMS(Index = 49)]
        /* 0xB2C */ public int PlanetaryPirateRaidMaxTradersJoinCombat;
        [NMS(Index = 48)]
        /* 0xB30 */ public float PlanetaryPirateRaidTradersEngageTime;
        [NMS(Index = 226)]
        /* 0xB34 */ public float PlanetUpAlignTime;
        [NMS(Index = 245)]
        /* 0xB38 */ public float PoliceAbortRange;
        [NMS(Index = 244)]
        /* 0xB3C */ public float PoliceArriveTime;
        [NMS(Index = 242)]
        /* 0xB40 */ public float PoliceEntranceCargoAttackWaitTime;
        [NMS(Index = 241)]
        /* 0xB44 */ public float PoliceEntranceCargoOpenCommsWaitTime;
        [NMS(Index = 240)]
        /* 0xB48 */ public float PoliceEntranceCargoProbingTime;
        [NMS(Index = 243)]
        /* 0xB4C */ public float PoliceEntranceCargoScanHailNotificationWaitTime;
        [NMS(Index = 239)]
        /* 0xB50 */ public float PoliceEntranceCargoScanStartTime;
        [NMS(Index = 238)]
        /* 0xB54 */ public float PoliceEntranceEscalateIncomingTime;
        [NMS(Index = 237)]
        /* 0xB58 */ public float PoliceEntranceEscalateProbingTime;
        [NMS(Index = 236)]
        /* 0xB5C */ public float PoliceEntranceProbe;
        [NMS(Index = 235)]
        /* 0xB60 */ public float PoliceEntranceStartTime;
        [NMS(Index = 208)]
        /* 0xB64 */ public float PoliceEscapeMinTime;
        [NMS(Index = 207)]
        /* 0xB68 */ public float PoliceEscapeTime;
        [NMS(Index = 5)]
        /* 0xB6C */ public float PoliceFreighterLaserActiveTime;
        [NMS(Index = 4)]
        /* 0xB70 */ public float PoliceFreighterLaserRandomExtraPauseMax;
        [NMS(Index = 2)]
        /* 0xB74 */ public float PoliceFreighterLaserRange;
        [NMS(Index = 3)]
        /* 0xB78 */ public float PoliceFreighterLaserShootTime;
        [NMS(Index = 7)]
        /* 0xB7C */ public int PoliceFreighterProjectileBurstCount;
        [NMS(Index = 8)]
        /* 0xB80 */ public float PoliceFreighterProjectileBurstTime;
        [NMS(Index = 11)]
        /* 0xB84 */ public int PoliceFreighterProjectileModulo;
        [NMS(Index = 9)]
        /* 0xB88 */ public float PoliceFreighterProjectilePauseTime;
        [NMS(Index = 10)]
        /* 0xB8C */ public float PoliceFreighterProjectileRandomExtraPauseMax;
        [NMS(Index = 6)]
        /* 0xB90 */ public float PoliceFreighterProjectileRange;
        [NMS(Index = 144)]
        /* 0xB94 */ public float PoliceFreighterWarpOutRange;
        [NMS(Index = 316)]
        /* 0xB98 */ public float PoliceLaunchDistance;
        [NMS(Index = 203)]
        /* 0xB9C */ public float PoliceLaunchSpeed;
        [NMS(Index = 204)]
        /* 0xBA0 */ public float PoliceLaunchTime;
        [NMS(Index = 273)]
        /* 0xBA4 */ public int PoliceNumPerTarget;
        [NMS(Index = 205)]
        /* 0xBA8 */ public float PolicePauseTime;
        [NMS(Index = 206)]
        /* 0xBAC */ public float PolicePauseTimeSpaceBattle;
        [NMS(Index = 234)]
        /* 0xBB0 */ public float PoliceSpawnViewAngle;
        [NMS(Index = 210)]
        /* 0xBB4 */ public float PoliceStationEngageRange;
        [NMS(Index = 211)]
        /* 0xBB8 */ public int PoliceStationNumToLaunch;
        [NMS(Index = 209)]
        /* 0xBBC */ public float PoliceStationWaveTimer;
        [NMS(Index = 246)]
        /* 0xBC0 */ public float PoliceWarnBeaconPulseTime;
        [NMS(Index = 76)]
        /* 0xBC4 */ public float RewardLootAngularSpeed;
        [NMS(Index = 74)]
        /* 0xBC8 */ public float RewardLootOffset;
        [NMS(Index = 75)]
        /* 0xBCC */ public float RewardLootOffsetSpeed;
        [NMS(Index = 412)]
        /* 0xBD0 */ public float RollAmount;
        [NMS(Index = 413)]
        /* 0xBD4 */ public float RollMinTurnAngle;
        [NMS(Index = 24)]
        /* 0xBD8 */ public float SalvageRemovalTime;
        [NMS(Index = 23)]
        /* 0xBDC */ public float SalvageTime;
        [NMS(Index = 25)]
        /* 0xBE0 */ public float SalvageValueMultiplier;
        [NMS(Index = 326)]
        /* 0xBE4 */ public float ScaleHeightMax;
        [NMS(Index = 325)]
        /* 0xBE8 */ public float ScaleHeightMin;
        [NMS(Index = 317)]
        /* 0xBEC */ public float Scaler;
        [NMS(Index = 321)]
        /* 0xBF0 */ public float ScalerCombat;
        [NMS(Index = 320)]
        /* 0xBF4 */ public float ScalerMaxDist;
        [NMS(Index = 324)]
        /* 0xBF8 */ public float ScalerMaxDistCombat;
        [NMS(Index = 319)]
        /* 0xBFC */ public float ScalerMinDist;
        [NMS(Index = 323)]
        /* 0xC00 */ public float ScalerMinDistCombat;
        [NMS(Index = 318)]
        /* 0xC04 */ public float ScaleTime;
        [NMS(Index = 322)]
        /* 0xC08 */ public float ScaleTimeCombat;
        [NMS(Index = 55)]
        /* 0xC0C */ public float SentinelGunBrokenSlotChance;
        [NMS(Index = 96)]
        /* 0xC10 */ public float ShieldCollisionRadiusMultiplier;
        [NMS(Index = 45)]
        /* 0xC14 */ public float ShipAlertPirateRange;
        [NMS(Index = 411)]
        /* 0xC18 */ public float ShipAngularFactor;
        [NMS(Index = 344)]
        /* 0xC1C */ public float ShipEscapeTimeBeforeWarpOut;
        [NMS(Index = 349)]
        /* 0xC20 */ public float ShipEscortBackForceTime;
        [NMS(Index = 354)]
        /* 0xC24 */ public float ShipEscortForwardOffsetScaleMax;
        [NMS(Index = 353)]
        /* 0xC28 */ public float ShipEscortForwardOffsetScaleMin;
        [NMS(Index = 348)]
        /* 0xC2C */ public float ShipEscortFwdForceTime;
        [NMS(Index = 345)]
        /* 0xC30 */ public float ShipEscortLockOnDistance;
        [NMS(Index = 350)]
        /* 0xC34 */ public float ShipEscortPerpForceTime;
        [NMS(Index = 352)]
        /* 0xC38 */ public float ShipEscortRadialOffsetScaleMax;
        [NMS(Index = 351)]
        /* 0xC3C */ public float ShipEscortRadialOffsetScaleMin;
        [NMS(Index = 346)]
        /* 0xC40 */ public float ShipEscortVelocityBand;
        [NMS(Index = 347)]
        /* 0xC44 */ public float ShipEscortVelocityBandForce;
        [NMS(Index = 232)]
        /* 0xC48 */ public float ShipSpawnAnomalyRadius;
        [NMS(Index = 231)]
        /* 0xC4C */ public float ShipSpawnStationRadius;
        [NMS(Index = 135)]
        /* 0xC50 */ public float SpaceBattleExistingFreighterInitialPirateOffset;
        [NMS(Index = 136)]
        /* 0xC54 */ public float SpaceBattleExistingFreighterInitialPirateSpread;
        [NMS(Index = 134)]
        /* 0xC58 */ public float SpaceBattleFlybyTime;
        [NMS(Index = 131)]
        /* 0xC5C */ public float SpaceBattleGuardOffset;
        [NMS(Index = 132)]
        /* 0xC60 */ public float SpaceBattleGuardUpOffset;
        [NMS(Index = 133)]
        /* 0xC64 */ public float SpaceBattleObstructionRadius;
        [NMS(Index = 199)]
        /* 0xC68 */ public float SpaceStationTraderRequestTime;
        [NMS(Index = 426)]
        /* 0xC6C */ public int SwarmHiveSpaceAnomalyMinorLaserDamageLevel;
        [NMS(Index = 424)]
        /* 0xC70 */ public int SwarmHiveSpaceAnomalyPrimaryLaserDamageLevel;
        [NMS(Index = 396)]
        /* 0xC74 */ public float TakeOffExitHeightOffset;
        [NMS(Index = 397)]
        /* 0xC78 */ public float TakeOffExtraAIHeight;
        [NMS(Index = 395)]
        /* 0xC7C */ public float TakeOffHoverPointReachedDistance;
        [NMS(Index = 220)]
        /* 0xC80 */ public float TraderArriveSpeed;
        [NMS(Index = 221)]
        /* 0xC84 */ public float TraderArriveTime;
        [NMS(Index = 375)]
        /* 0xC88 */ public float TraderAtTime;
        [NMS(Index = 376)]
        /* 0xC8C */ public float TraderAtTimeBack;
        [NMS(Index = 212)]
        /* 0xC90 */ public int TraderIgnoreHits;
        [NMS(Index = 265)]
        /* 0xC94 */ public int TradeRouteDivisions;
        [NMS(Index = 267)]
        /* 0xC98 */ public float TradeRouteFlickerAmp;
        [NMS(Index = 266)]
        /* 0xC9C */ public float TradeRouteFlickerFreq;
        [NMS(Index = 255)]
        /* 0xCA0 */ public float TradeRouteFollowOffset;
        [NMS(Index = 268)]
        /* 0xCA4 */ public int TradeRouteMaxNum;
        [NMS(Index = 254)]
        /* 0xCA8 */ public float TradeRouteSeekOutpostRange;
        [NMS(Index = 263)]
        /* 0xCAC */ public float TradeRouteSlowRange;
        [NMS(Index = 264)]
        /* 0xCB0 */ public float TradeRouteSlowSpeed;
        [NMS(Index = 257)]
        /* 0xCB4 */ public float TradeRouteSpawnDistance;
        [NMS(Index = 262)]
        /* 0xCB8 */ public float TradeRouteSpeed;
        [NMS(Index = 261)]
        /* 0xCBC */ public float TradeRouteStationRadius;
        [NMS(Index = 258)]
        /* 0xCC0 */ public float TradeRouteTrailDrawDistance;
        [NMS(Index = 259)]
        /* 0xCC4 */ public float TradeRouteTrailFadeTime;
        [NMS(Index = 260)]
        /* 0xCC8 */ public float TradeRouteTrailTimeOffset;
        [NMS(Index = 377)]
        /* 0xCCC */ public float TraderPerpTime;
        [NMS(Index = 195)]
        /* 0xCD0 */ public float TraderPostCombatRequestTime;
        [NMS(Index = 194)]
        /* 0xCD4 */ public float TraderRequestTime;
        [NMS(Index = 378)]
        /* 0xCD8 */ public float TraderVelocityBand;
        [NMS(Index = 379)]
        /* 0xCDC */ public float TraderVelocityBandForce;
        [NMS(Index = 193)]
        /* 0xCE0 */ public float TraderWantedTime;
        [NMS(Index = 197)]
        /* 0xCE4 */ public float TradingPostTraderRange;
        [NMS(Index = 198)]
        /* 0xCE8 */ public float TradingPostTraderRangeSpace;
        [NMS(Index = 196)]
        /* 0xCEC */ public float TradingPostTraderRequestTime;
        [NMS(Index = 32)]
        /* 0xCF0 */ public float TrailLandingFadeTime;
        [NMS(Index = 249)]
        /* 0xCF4 */ public float TrailScale;
        [NMS(Index = 252)]
        /* 0xCF8 */ public float TrailScaleFreighterMaxScale;
        [NMS(Index = 251)]
        /* 0xCFC */ public float TrailScaleMaxScale;
        [NMS(Index = 248)]
        /* 0xD00 */ public float TrailScaleMinDistance;
        [NMS(Index = 250)]
        /* 0xD04 */ public float TrailScaleRange;
        [NMS(Index = 384)]
        /* 0xD08 */ public float TrailSpeedFadeFalloff;
        [NMS(Index = 383)]
        /* 0xD0C */ public float TrailSpeedFadeMinSpeed;
        [NMS(Index = 404)]
        /* 0xD10 */ public float TravelMinBoostTime;
        [NMS(Index = 154)]
        /* 0xD14 */ public float TurretAlertLightIntensity;
        [NMS(Index = 153)]
        /* 0xD18 */ public float TurretOriginOffset;
        [NMS(Index = 315)]
        /* 0xD1C */ public float TurretRandomAIShipOffset;
        [NMS(Index = 314)]
        /* 0xD20 */ public float TurretRandomOffset;
        [NMS(Index = 17)]
        /* 0xD24 */ public float VisibleDistance;
        [NMS(Index = 331)]
        /* 0xD28 */ public float WarpFadeInTime;
        [NMS(Index = 382)]
        /* 0xD2C */ public float WarpForce;
        [NMS(Index = 338)]
        /* 0xD30 */ public float WarpInAudioFXDelay;
        [NMS(Index = 335)]
        /* 0xD34 */ public float WarpInDistance;
        [NMS(Index = 329)]
        /* 0xD38 */ public float WarpInPlayerLocatorMinOffset;
        [NMS(Index = 328)]
        /* 0xD3C */ public float WarpInPlayerLocatorTime;
        [NMS(Index = 336)]
        /* 0xD40 */ public float WarpInPostSpeed;
        [NMS(Index = 337)]
        /* 0xD44 */ public float WarpInPostSpeedFreighter;
        [NMS(Index = 330)]
        /* 0xD48 */ public float WarpInTime;
        [NMS(Index = 332)]
        /* 0xD4C */ public float WarpInTimeFreighter;
        [NMS(Index = 334)]
        /* 0xD50 */ public float WarpInVariance;
        [NMS(Index = 339)]
        /* 0xD54 */ public float WarpOutDistance;
        [NMS(Index = 381)]
        /* 0xD58 */ public float WarpSpeed;
        [NMS(Index = 380)]
        /* 0xD5C */ public float WingmanAlign;
        [NMS(Index = 370)]
        /* 0xD60 */ public float WingmanAtTime;
        [NMS(Index = 371)]
        /* 0xD64 */ public float WingmanAtTimeBack;
        [NMS(Index = 364)]
        /* 0xD68 */ public float WingmanHeightAdjust;
        [NMS(Index = 362)]
        /* 0xD6C */ public float WingmanLockArriveTime;
        [NMS(Index = 361)]
        /* 0xD70 */ public float WingmanLockBetweenTime;
        [NMS(Index = 360)]
        /* 0xD74 */ public float WingmanLockDistance;
        [NMS(Index = 363)]
        /* 0xD78 */ public float WingmanMinHeight;
        [NMS(Index = 367)]
        /* 0xD7C */ public float WingmanOffset;
        [NMS(Index = 366)]
        /* 0xD80 */ public float WingmanOffsetStart;
        [NMS(Index = 372)]
        /* 0xD84 */ public float WingmanPerpTime;
        [NMS(Index = 369)]
        /* 0xD88 */ public float WingmanRotate;
        [NMS(Index = 368)]
        /* 0xD8C */ public float WingmanSideOffset;
        [NMS(Index = 365)]
        /* 0xD90 */ public float WingmanStartTime;
        [NMS(Index = 373)]
        /* 0xD94 */ public float WingmanVelocityBand;
        [NMS(Index = 374)]
        /* 0xD98 */ public float WingmanVelocityBandForce;
        [NMS(Index = 276)]
        /* 0xD9C */ public float WitnessHearingRange;
        [NMS(Index = 278)]
        /* 0xDA0 */ public float WitnessSightAngle;
        [NMS(Index = 277)]
        /* 0xDA4 */ public float WitnessSightRange;
        [NMS(Index = 256)]
        /* 0xDA8 */ public NMSString0x100 TradeRouteIcon;
        [NMS(Index = 94, Size = 0x3F, EnumType = typeof(GcBuildingClassification.BuildingClassEnum))]
        /* 0xEA8 */ public bool[] PirateAttackableBuildingClasses;
        [NMS(Index = 21)]
        /* 0xEE7 */ public bool AtmosphereEffectEnabled;
        [NMS(Index = 70)]
        /* 0xEE8 */ public bool AttackRepositionBoost;
        [NMS(Index = 71)]
        /* 0xEE9 */ public bool AttackShipsFollowLeader;
        [NMS(Index = 156)]
        /* 0xEEA */ public bool DisableTradeRoutes;
        [NMS(Index = 12)]
        /* 0xEEB */ public bool DisplayShipAttackTypes;
        [NMS(Index = 270)]
        /* 0xEEC */ public bool EnableLoot;
        [NMS(Index = 38)]
        /* 0xEED */ public bool EnergyShieldAlwaysVisible;
        [NMS(Index = 33)]
        /* 0xEEE */ public bool EnergyShieldsEnabled;
        [NMS(Index = 22)]
        /* 0xEEF */ public bool FillUpOutposts;
        [NMS(Index = 140)]
        /* 0xEF0 */ public bool FreighterAlertLights;
        [NMS(Index = 288)]
        /* 0xEF1 */ public bool FreighterIgnorePlayer;
        [NMS(Index = 19)]
        /* 0xEF2 */ public bool FreightersAlwaysAttackPlayer;
        [NMS(Index = 225)]
        /* 0xEF3 */ public bool FreightersSamePalette;
        [NMS(Index = 20)]
        /* 0xEF4 */ public bool GroundEffectEnabled;
        [NMS(Index = 229)]
        /* 0xEF5 */ public bool PoliceSpawnEffect;
        [NMS(Index = 327)]
        /* 0xEF6 */ public bool ScaleDisabledWhenOnFreighter;
        [NMS(Index = 18)]
        /* 0xEF7 */ public bool TradersAttackPirates;
        [NMS(Index = 253)]
        /* 0xEF8 */ public TkCurveType TrailScaleCurve;
        [NMS(Index = 333)]
        /* 0xEF9 */ public TkCurveType WarpInCurve;
    }
}
