using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.Globals
{
    [NMS(GUID = 0xE03AE967EB72F7D0, NameHash = 0xA10A7EF5)]
    public class GcGalaxyGlobals : NMSTemplate
    {
        [NMS(Index = 124, Size = 0x10, EnumType = typeof(GcGalaxyMarkerTypes.GalaxyMarkerTypeEnum))]
        /* 0x0000 */ public GcGalaxyMarkerSettings[] MarkerSettings;
        [NMS(Index = 175)]
        /* 0x0B00 */ public GcGalaxyRenderSetupData DefaultRenderSetup;
        [NMS(Index = 177)]
        /* 0x0E40 */ public GcGalaxyRenderSetupData FinalAnimationRenderSetup;
        [NMS(Index = 176)]
        /* 0x1180 */ public GcGalaxyGenerationSetupData DefaultGeneration;
        [NMS(Index = 178)]
        /* 0x1300 */ public GcGalaxyGenerationSetupData FinalAnimationGeneration;
        [NMS(Index = 112, Size = 0x9, EnumType = typeof(GcAlienRace.AlienRaceEnum))]
        /* 0x1480 */ public Colour[] RaceFilterDefaultColours;
        [NMS(Index = 114, Size = 0x9, EnumType = typeof(GcAlienRace.AlienRaceEnum))]
        /* 0x1510 */ public Colour[] RaceFilterDeuteranopiaColours;
        [NMS(Index = 113, Size = 0x9, EnumType = typeof(GcAlienRace.AlienRaceEnum))]
        /* 0x15A0 */ public Colour[] RaceFilterProtanopiaColours;
        [NMS(Index = 115, Size = 0x9, EnumType = typeof(GcAlienRace.AlienRaceEnum))]
        /* 0x1630 */ public Colour[] RaceFilterTritanopiaColours;
        [NMS(Index = 116, Size = 0x7, EnumType = typeof(GcTradingClass.TradingClassEnum))]
        /* 0x16C0 */ public Colour[] EconomyFilterDefaultColours;
        [NMS(Index = 118, Size = 0x7, EnumType = typeof(GcTradingClass.TradingClassEnum))]
        /* 0x1730 */ public Colour[] EconomyFilterDeuteranopiaColours;
        [NMS(Index = 117, Size = 0x7, EnumType = typeof(GcTradingClass.TradingClassEnum))]
        /* 0x17A0 */ public Colour[] EconomyFilterProtanopiaColours;
        [NMS(Index = 119, Size = 0x7, EnumType = typeof(GcTradingClass.TradingClassEnum))]
        /* 0x1810 */ public Colour[] EconomyFilterTritanopiaColours;
        [NMS(Index = 108, Size = 0x7)]
        /* 0x1880 */ public Colour[] GalacticWaypointDefaultColours;
        [NMS(Index = 110, Size = 0x7)]
        /* 0x18F0 */ public Colour[] GalacticWaypointDeuteranopiaColours;
        [NMS(Index = 109, Size = 0x7)]
        /* 0x1960 */ public Colour[] GalacticWaypointProtanopiaColours;
        [NMS(Index = 111, Size = 0x7)]
        /* 0x19D0 */ public Colour[] GalacticWaypointTritanopiaColours;
        [NMS(Index = 164)]
        /* 0x1A40 */ public GcGalaxyStarColours BaseStarDefaultColours;
        [NMS(Index = 166)]
        /* 0x1A90 */ public GcGalaxyStarColours BaseStarDeuteranopiaColours;
        [NMS(Index = 165)]
        /* 0x1AE0 */ public GcGalaxyStarColours BaseStarProtanopiaColours;
        [NMS(Index = 167)]
        /* 0x1B30 */ public GcGalaxyStarColours BaseStarTritanopiaColours;
        [NMS(Index = 120, Size = 0x4, EnumType = typeof(GcPlayerConflictData.ConflictLevelEnum))]
        /* 0x1B80 */ public Colour[] ConflictFilterDefaultColours;
        [NMS(Index = 122, Size = 0x4, EnumType = typeof(GcPlayerConflictData.ConflictLevelEnum))]
        /* 0x1BC0 */ public Colour[] ConflictFilterDeuteranopiaColours;
        [NMS(Index = 121, Size = 0x4, EnumType = typeof(GcPlayerConflictData.ConflictLevelEnum))]
        /* 0x1C00 */ public Colour[] ConflictFilterProtanopiaColours;
        [NMS(Index = 123, Size = 0x4, EnumType = typeof(GcPlayerConflictData.ConflictLevelEnum))]
        /* 0x1C40 */ public Colour[] ConflictFilterTritanopiaColours;
        [NMS(Index = 159)]
        /* 0x1C80 */ public GcGalaxyRenderAnostreakData AnostreakAway;
        [NMS(Index = 160)]
        /* 0x1CB0 */ public GcGalaxyRenderAnostreakData AnostreakFacing;
        [NMS(Index = 68)]
        /* 0x1CE0 */ public GcInWorldUIScreenData HandMenuOffset;
        [NMS(Index = 58)]
        /* 0x1D10 */ public Colour HandGizmoColourAt;
        [NMS(Index = 67)]
        /* 0x1D20 */ public Colour HandGizmoColourInner;
        [NMS(Index = 59)]
        /* 0x1D30 */ public Colour HandGizmoColourRight;
        [NMS(Index = 60)]
        /* 0x1D40 */ public Colour HandGizmoColourUp;
        [NMS(Index = 61)]
        /* 0x1D50 */ public Vector3f HandGizmoHeadOffset;
        [NMS(Index = 162, MxmlName = "Selection Tree Colour")]
        /* 0x1D60 */ public Colour SelectionTreeColour;
        [NMS(Index = 17)]
        /* 0x1D70 */ public GcFilename MarkerDefaultHex;
        [NMS(Index = 168)]
        /* 0x1D80 */ public GcGalaxyCameraData Camera;
        [NMS(Index = 179)]
        /* 0x1DF0 */ public GcGalaxySolarSystemParams SolarSystemParameters;
        [NMS(Index = 169)]
        /* 0x1E4C */ public GcGalaxyAudioSetupData Audio;
        [NMS(Index = 2)]
        /* 0x1E90 */ public Vector2f ClickToSelectIconOffset;
        [NMS(Index = 163, MxmlName = "Goal Distance Range")]
        /* 0x1E98 */ public Vector2f GoalDistanceRange;
        [NMS(Index = 72)]
        /* 0x1EA0 */ public Vector2f SolarInfoPanelAlignment;
        [NMS(Index = 70)]
        /* 0x1EA8 */ public Vector2f SolarInfoPanelLineOffset;
        [NMS(Index = 69)]
        /* 0x1EB0 */ public Vector2f SolarInfoPanelOffset;
        [NMS(Index = 71)]
        /* 0x1EB8 */ public Vector2f SolarInfoPanelOffsetVR;
        [NMS(Index = 73)]
        /* 0x1EC0 */ public Vector2f SolarMarkerAlignmentVR;
        [NMS(Index = 77)]
        /* 0x1EC8 */ public Vector2f SolarMarkerOriginOffsetVR;
        [NMS(Index = 76)]
        /* 0x1ED0 */ public Vector2f SolarMarkerOriginOffsetVRPS4;
        [NMS(Index = 75)]
        /* 0x1ED8 */ public Vector2f SolarMarkerSizeVR;
        [NMS(Index = 74)]
        /* 0x1EE0 */ public Vector2f SolarMarkerSizeVRPS4;
        [NMS(Index = 156, MxmlName = "Anostreak Alpha")]
        /* 0x1EE8 */ public float AnostreakAlpha;
        [NMS(Index = 3)]
        /* 0x1EEC */ public float ClickToSelectIconScale;
        [NMS(Index = 143, MxmlName = "Distance Computer Scale")]
        /* 0x1EF0 */ public float DistanceComputerScale;
        [NMS(Index = 141, MxmlName = "Early Stage Multiplier")]
        /* 0x1EF4 */ public float EarlyStageMultiplier;
        [NMS(Index = 173, MxmlName = "Fade Game In Time")]
        /* 0x1EF8 */ public float FadeGameInTime;
        [NMS(Index = 170, MxmlName = "Fade Game Out Time")]
        /* 0x1EFC */ public float FadeGameOutTime;
        [NMS(Index = 171, MxmlName = "Fade Map In Time")]
        /* 0x1F00 */ public float FadeMapInTime;
        [NMS(Index = 172, MxmlName = "Fade Map Out Time")]
        /* 0x1F04 */ public float FadeMapOutTime;
        [NMS(Index = 83)]
        /* 0x1F08 */ public float FadeGameOutTimeCentreJourney;
        [NMS(Index = 84)]
        /* 0x1F0C */ public float FadeMapInTimeCentreJourney;
        [NMS(Index = 134)]
        /* 0x1F10 */ public float FinalFadedTime;
        [NMS(Index = 136)]
        /* 0x1F14 */ public float FinalFadeInRate;
        [NMS(Index = 137)]
        /* 0x1F18 */ public float FinalFadeOutRate;
        [NMS(Index = 133)]
        /* 0x1F1C */ public float FinalHoldTime;
        [NMS(Index = 135)]
        /* 0x1F20 */ public float FinalHoldTowardsCenterTime;
        [NMS(Index = 139)]
        /* 0x1F24 */ public float FinalTransitionAcceleration;
        [NMS(Index = 140)]
        /* 0x1F28 */ public float FinalTransitionInterpolationValue;
        [NMS(Index = 138)]
        /* 0x1F2C */ public float FinalTransitionMaxSpeed;
        [NMS(Index = 102)]
        /* 0x1F30 */ public float GalacticPathMaximumJumpDistanceLightyears;
        [NMS(Index = 103)]
        /* 0x1F34 */ public float GalacticPathPreferGuideStarsTillJump;
        [NMS(Index = 46)]
        /* 0x1F38 */ public float HandControlDefaultOffset;
        [NMS(Index = 45)]
        /* 0x1F3C */ public float HandControlFreeMoveAngleOffset;
        [NMS(Index = 44)]
        /* 0x1F40 */ public float HandControlFreeMoveMaxOffset;
        [NMS(Index = 40)]
        /* 0x1F44 */ public float HandControlGizmoScale;
        [NMS(Index = 49)]
        /* 0x1F48 */ public float HandControlMaxLockDistance;
        [NMS(Index = 41)]
        /* 0x1F4C */ public float HandControlMaxOffset;
        [NMS(Index = 48)]
        /* 0x1F50 */ public float HandControlMinLockDistance;
        [NMS(Index = 42)]
        /* 0x1F54 */ public float HandControlMoveBlendRate;
        [NMS(Index = 36)]
        /* 0x1F58 */ public float HandControlMoveSpeed;
        [NMS(Index = 37)]
        /* 0x1F5C */ public float HandControlMoveSpeedTurbo;
        [NMS(Index = 35)]
        /* 0x1F60 */ public float HandControlPitchSpeed;
        [NMS(Index = 38)]
        /* 0x1F64 */ public float HandControlPointerLength;
        [NMS(Index = 39)]
        /* 0x1F68 */ public float HandControlPointerLengthMini;
        [NMS(Index = 43)]
        /* 0x1F6C */ public float HandControlRotateBlendRate;
        [NMS(Index = 34)]
        /* 0x1F70 */ public float HandControlRotateSpeed;
        [NMS(Index = 47)]
        /* 0x1F74 */ public float HandControlWarpSelectAngle;
        [NMS(Index = 57)]
        /* 0x1F78 */ public float HandControlZoomSpeed;
        [NMS(Index = 65)]
        /* 0x1F7C */ public float HandGizmoInnerRadius;
        [NMS(Index = 66)]
        /* 0x1F80 */ public float HandGizmoInnerThickness;
        [NMS(Index = 64)]
        /* 0x1F84 */ public float HandGizmoLineThickness;
        [NMS(Index = 62)]
        /* 0x1F88 */ public float HandGizmoMinAlpha;
        [NMS(Index = 63)]
        /* 0x1F8C */ public float HandGizmoRadius;
        [NMS(Index = 52)]
        /* 0x1F90 */ public float HandPitchFactorMin;
        [NMS(Index = 53)]
        /* 0x1F94 */ public float HandPitchFactorRange;
        [NMS(Index = 54)]
        /* 0x1F98 */ public float HandPitchMaxDistance;
        [NMS(Index = 50)]
        /* 0x1F9C */ public float HandTurnFactorMin;
        [NMS(Index = 51)]
        /* 0x1FA0 */ public float HandTurnFactorRange;
        [NMS(Index = 55)]
        /* 0x1FA4 */ public float HandZoomFactorMin;
        [NMS(Index = 56)]
        /* 0x1FA8 */ public float HandZoomFactorRange;
        [NMS(Index = 16)]
        /* 0x1FAC */ public float HexMarkerOuterWidth;
        [NMS(Index = 14)]
        /* 0x1FB0 */ public float HexMarkerRadius;
        [NMS(Index = 15)]
        /* 0x1FB4 */ public float HexMarkerRotation;
        [NMS(Index = 13)]
        /* 0x1FB8 */ public float HexMarkerWidth;
        [NMS(Index = 10)]
        /* 0x1FBC */ public float HexStackOffsetX;
        [NMS(Index = 11)]
        /* 0x1FC0 */ public float HexStackOffsetXOdd;
        [NMS(Index = 12)]
        /* 0x1FC4 */ public float HexStackOffsetY;
        [NMS(Index = 129)]
        /* 0x1FC8 */ public float IntroCameraLookSmoothRate;
        [NMS(Index = 125)]
        /* 0x1FCC */ public float IntroFadeInRate;
        [NMS(Index = 128)]
        /* 0x1FD0 */ public float IntroFadeOutRate;
        [NMS(Index = 126)]
        /* 0x1FD4 */ public float IntroTitleFadeTrigger;
        [NMS(Index = 127)]
        /* 0x1FD8 */ public float IntroTitleHoldTime;
        [NMS(Index = 130)]
        /* 0x1FDC */ public float IntroTitleTextureScale;
        [NMS(Index = 174, MxmlName = "Large Area Colour Scale")]
        /* 0x1FE0 */ public float LargeAreaColourScale;
        [NMS(Index = 144)]
        /* 0x1FE4 */ public float LastSelectedPathAlphaMul;
        [NMS(Index = 97)]
        /* 0x1FE8 */ public float MarkerDropShadowMult;
        [NMS(Index = 96)]
        /* 0x1FEC */ public float MarkerDropShadowSize;
        [NMS(Index = 9)]
        /* 0x1FF0 */ public float MenuCursorRadiusHmd;
        [NMS(Index = 6)]
        /* 0x1FF4 */ public float MenuOffsetHmd;
        [NMS(Index = 5)]
        /* 0x1FF8 */ public float MenuRotateHmd;
        [NMS(Index = 8)]
        /* 0x1FFC */ public float MenuScaleHmd;
        [NMS(Index = 7)]
        /* 0x2000 */ public float MenuSideOffsetHmd;
        [NMS(Index = 142, MxmlName = "OffWorld Distance")]
        /* 0x2004 */ public float OffWorldDistance;
        [NMS(Index = 146, MxmlName = "Path Rendering Selected End Alpha")]
        /* 0x2008 */ public float PathRenderingSelectedEndAlpha;
        [NMS(Index = 145, MxmlName = "Path Rendering Selected Start Alpha")]
        /* 0x200C */ public float PathRenderingSelectedStartAlpha;
        [NMS(Index = 147, MxmlName = "Path Rendering Selected Step Alpha")]
        /* 0x2010 */ public float PathRenderingSelectedStepAlpha;
        [NMS(Index = 149, MxmlName = "Path Rendering Unselected End Alpha")]
        /* 0x2014 */ public float PathRenderingUnselectedEndAlpha;
        [NMS(Index = 148, MxmlName = "Path Rendering Unselected Start Alpha")]
        /* 0x2018 */ public float PathRenderingUnselectedStartAlpha;
        [NMS(Index = 150, MxmlName = "Path Rendering Unselected Step Alpha")]
        /* 0x201C */ public float PathRenderingUnselectedStepAlpha;
        [NMS(Index = 19)]
        /* 0x2020 */ public float PathToTargetIndicatorTimeFactor;
        [NMS(Index = 18)]
        /* 0x2024 */ public float PathToTargetLineTimeFactor;
        [NMS(Index = 85)]
        /* 0x2028 */ public float PathUIAlpha;
        [NMS(Index = 22)]
        /* 0x202C */ public float PathUIConfirmSelectionMultiplier;
        [NMS(Index = 86)]
        /* 0x2030 */ public float PathUIDotLength;
        [NMS(Index = 87)]
        /* 0x2034 */ public float PathUIGapLength;
        [NMS(Index = 88)]
        /* 0x2038 */ public float PathUIHeight;
        [NMS(Index = 24)]
        /* 0x203C */ public float PathUISelectionGenerosity;
        [NMS(Index = 29)]
        /* 0x2040 */ public float PathUISelectionHandInvalidLength;
        [NMS(Index = 27)]
        /* 0x2044 */ public float PathUISelectionHandLineSelectAngle;
        [NMS(Index = 28)]
        /* 0x2048 */ public float PathUISelectionHandSystemSelectAngle;
        [NMS(Index = 26)]
        /* 0x204C */ public float PathUISelectionMouseDeadZone;
        [NMS(Index = 25)]
        /* 0x2050 */ public float PathUISelectionMouseSmoothRate;
        [NMS(Index = 21)]
        /* 0x2054 */ public float PathUISelectionMultiplierMouse;
        [NMS(Index = 20)]
        /* 0x2058 */ public float PathUISelectionMultiplierPad;
        [NMS(Index = 23)]
        /* 0x205C */ public float PathUISelectionMultiplierPushing;
        [NMS(Index = 92)]
        /* 0x2060 */ public float PathUISlotRadiusInner;
        [NMS(Index = 93)]
        /* 0x2064 */ public float PathUISlotRadiusOuter;
        [NMS(Index = 94)]
        /* 0x2068 */ public float PathUISlotRadiusRing;
        [NMS(Index = 91)]
        /* 0x206C */ public float PathUISlotSpacing;
        [NMS(Index = 95)]
        /* 0x2070 */ public float PathUISlotWidthRing;
        [NMS(Index = 81)]
        /* 0x2074 */ public float PathUIWidth;
        [NMS(Index = 89)]
        /* 0x2078 */ public float PathUIXOffset;
        [NMS(Index = 90)]
        /* 0x207C */ public float PathUIYOffset;
        [NMS(Index = 98)]
        /* 0x2080 */ public float PlanetUIIconLargeScale;
        [NMS(Index = 99)]
        /* 0x2084 */ public float PlanetUIIconMediumScale;
        [NMS(Index = 100)]
        /* 0x2088 */ public float PlanetUIIconSmallScale;
        [NMS(Index = 32)]
        /* 0x208C */ public float PurpleRevealFixedZoom;
        [NMS(Index = 33)]
        /* 0x2090 */ public float PurpleStarRevealAnimTime;
        [NMS(Index = 161, MxmlName = "Selection Tree Alpha")]
        /* 0x2094 */ public float SelectionTreeAlpha;
        [NMS(Index = 101)]
        /* 0x2098 */ public float ShowPopupAtCameraMinDistance;
        [NMS(Index = 104)]
        /* 0x209C */ public float ShowUIHelpDuration;
        [NMS(Index = 31)]
        /* 0x20A0 */ public int SolarInfoPanelHeight;
        [NMS(Index = 79)]
        /* 0x20A4 */ public float SolarInfoPanelScaleVR;
        [NMS(Index = 30)]
        /* 0x20A8 */ public int SolarInfoPanelWidth;
        [NMS(Index = 80)]
        /* 0x20AC */ public float SolarLabelScaleDistanceVR;
        [NMS(Index = 78)]
        /* 0x20B0 */ public float SolarMarkerPanelScaleVR;
        [NMS(Index = 155, MxmlName = "Star Blur Intro Multiplier")]
        /* 0x20B4 */ public float StarBlurIntroMultiplier;
        [NMS(Index = 154, MxmlName = "Star Blur Line Width")]
        /* 0x20B8 */ public float StarBlurLineWidth;
        [NMS(Index = 152, MxmlName = "Star Blur Max Blur Length")]
        /* 0x20BC */ public float StarBlurMaxBlurLength;
        [NMS(Index = 151, MxmlName = "Star Blur Max Distance From Camera")]
        /* 0x20C0 */ public float StarBlurMaxDistanceFromCamera;
        [NMS(Index = 153, MxmlName = "Star Blur Size Multiplier")]
        /* 0x20C4 */ public float StarBlurSizeMultiplier;
        [NMS(Index = 82)]
        /* 0x20C8 */ public float StarPathUIWidth;
        [NMS(Index = 105)]
        /* 0x20CC */ public float SystemInfoPanelGeneralAlpha;
        [NMS(Index = 4)]
        /* 0x20D0 */ public float TimeForGalmapAutoNavModeSelectionInSeconds;
        [NMS(Index = 131, MxmlName = "Transition Time")]
        /* 0x20D4 */ public float TransitionTime;
        [NMS(Index = 157, MxmlName = "Anostreak Alpha Curve")]
        /* 0x20D8 */ public TkCurveType AnostreakAlphaCurve;
        [NMS(Index = 158, MxmlName = "Anostreak Value Curve")]
        /* 0x20D9 */ public TkCurveType AnostreakValueCurve;
        [NMS(Index = 1)]
        /* 0x20DA */ public bool GizmoOnHand;
        [NMS(Index = 107)]
        /* 0x20DB */ public TkCurveType MarkerPulseEndCurve;
        [NMS(Index = 106)]
        /* 0x20DC */ public TkCurveType MarkerPulseStartCurve;
        [NMS(Index = 0)]
        /* 0x20DD */ public bool NewStyleLookAtCamera;
        [NMS(Index = 132)]
        /* 0x20DE */ public TkCurveType TransitionOutCurve;
    }
}
