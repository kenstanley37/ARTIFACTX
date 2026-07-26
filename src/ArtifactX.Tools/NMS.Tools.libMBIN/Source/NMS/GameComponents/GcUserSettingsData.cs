using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;
using System.Collections.Generic;
using System;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x8127279909D4EBEF, NameHash = 0x6C799781)]
    public class GcUserSettingsData : NMSTemplate
    {
        [NMS(Index = 105)]
        /* 0x0000 */ public List<GcInputActionMapping2> CustomBindingsMac;
        [NMS(Index = 104)]
        /* 0x0010 */ public List<GcInputActionMapping2> CustomBindingsPC;
        [NMS(Index = 107)]
        /* 0x0020 */ public List<GcInputActionMapping2> CustomBindingsPlaystation;
        [NMS(Index = 108)]
        /* 0x0030 */ public List<GcInputActionMapping2> CustomBindingsSwitch;
        [NMS(Index = 106)]
        /* 0x0040 */ public List<GcInputActionMapping2> CustomBindingsXbox;
        [NMS(Index = 33)]
        /* 0x0050 */ public List<NMSString0x10> SeenProducts;
        [NMS(Index = 31)]
        /* 0x0060 */ public List<NMSString0x10> SeenSubstances;
        [NMS(Index = 32)]
        /* 0x0070 */ public List<NMSString0x10> SeenTechnologies;
        [NMS(Index = 34)]
        /* 0x0080 */ public List<NMSString0x20A> SeenWikiTopics;
        [NMS(Index = 40)]
        /* 0x0090 */ public List<NMSString0x10> UnlockedPlatformRewards;
        [NMS(Index = 38)]
        /* 0x00A0 */ public List<NMSString0x10> UnlockedSeasonRewards;
        [NMS(Index = 37)]
        /* 0x00B0 */ public List<NMSString0x10> UnlockedSpecials;
        [NMS(Index = 36)]
        /* 0x00C0 */ public List<NMSString0x10> UnlockedTitles;
        [NMS(Index = 39)]
        /* 0x00D0 */ public List<NMSString0x10> UnlockedTwitchRewards;
        [NMS(Index = 35)]
        /* 0x00E0 */ public List<NMSString0x20A> UnlockedWikiTopics;
        [NMS(Index = 81)]
        /* 0x00F0 */ public List<NMSString0x80> UpgradedUsers;
        [NMS(Index = 19)]
        /* 0x0100 */ public GcBlockListPersistence BlockList;
        [NMS(Index = 90)]
        /* 0x3950 */ public GcGyroSettingsData GyroSettings;
        // size: 0x3
        public enum BaseSharingModeEnum : uint {
            Undecided,
            On,
            Off,
        }
        [NMS(Index = 30)]
        /* 0x39C4 */ public BaseSharingModeEnum BaseSharingMode;
        [NMS(Index = 78)]
        /* 0x39C8 */ public int CamerShakeStrength;
        // size: 0x2
        public enum ConsoleHFREnum : uint {
            False,
            True,
        }
        [NMS(Index = 29)]
        /* 0x39CC */ public ConsoleHFREnum ConsoleHFR;
        [NMS(Index = 65)]
        /* 0x39D0 */ public float CrossSavesUploadTimeout;
        [NMS(Index = 16)]
        /* 0x39D4 */ public int CursorSensitivityMode1;
        [NMS(Index = 17)]
        /* 0x39D8 */ public int CursorSensitivityMode2;
        [NMS(Index = 93)]
        /* 0x39DC */ public GcHand DominantHand;
        // size: 0x4
        [Flags]
        public enum EyeTrackingFlagsEnum : uint {
            None = 0x0,
            BaseBuilding = 0x1,
            WristMenus = 0x2,
            Menus = 0x4,
        }
        [NMS(Index = 88)]
        /* 0x39E0 */ public EyeTrackingFlagsEnum EyeTrackingFlags;
        [NMS(Index = 21)]
        /* 0x39E4 */ public int Filter;
        [NMS(Index = 75)]
        /* 0x39E8 */ public int FireteamSessionCount;
        [NMS(Index = 14)]
        /* 0x39EC */ public int FlightSensitivityMode1;
        [NMS(Index = 15)]
        /* 0x39F0 */ public int FlightSensitivityMode2;
        [NMS(Index = 62)]
        /* 0x39F4 */ public float FrontendZoom;
        [NMS(Index = 94)]
        /* 0x39F8 */ public float HazardEffectsStrength;
        [NMS(Index = 76)]
        /* 0x39FC */ public int HeadsetVibrationStrength;
        // size: 0x2
        public enum HighResVRUIEnum : uint {
            High,
            Low,
        }
        [NMS(Index = 98)]
        /* 0x3A00 */ public HighResVRUIEnum HighResVRUI;
        [NMS(Index = 61)]
        /* 0x3A04 */ public float HUDZoom;
        [NMS(Index = 91)]
        /* 0x3A08 */ public TkLanguages Language;
        [NMS(Index = 51)]
        /* 0x3A0C */ public int LastSeenCommunityMission;
        [NMS(Index = 52)]
        /* 0x3A10 */ public int LastSeenCommunityMissionTier;
        [NMS(Index = 12)]
        /* 0x3A14 */ public int LookSensitivityMode1;
        [NMS(Index = 13)]
        /* 0x3A18 */ public int LookSensitivityMode2;
        [NMS(Index = 20)]
        /* 0x3A1C */ public int MotionBlurAmount;
        [NMS(Index = 18)]
        /* 0x3A20 */ public int MouseSpringSmoothing;
        [NMS(Index = 55)]
        /* 0x3A24 */ public GcMovementDirection MovementDirectionHands;
        [NMS(Index = 54)]
        /* 0x3A28 */ public GcMovementDirection MovementDirectionPad;
        // size: 0x2
        public enum MovementModeEnum : uint {
            Teleporter,
            Smooth,
        }
        [NMS(Index = 53)]
        /* 0x3A2C */ public MovementModeEnum MovementMode;
        [NMS(Index = 7)]
        /* 0x3A30 */ public int MusicVolume;
        [NMS(Index = 99)]
        /* 0x3A34 */ public float PlayerHUDVROffset;
        // size: 0x4
        public enum PlayerVoiceEnum : uint {
            Off,
            High,
            Low,
            Alien,
        }
        [NMS(Index = 11)]
        /* 0x3A38 */ public PlayerVoiceEnum PlayerVoice;
        // size: 0x4
        public enum PS4FixedFPSEnum : uint {
            Invalid,
            True,
            False,
            MaxPerformance,
        }
        [NMS(Index = 23)]
        /* 0x3A3C */ public PS4FixedFPSEnum PS4FixedFPS;
        [NMS(Index = 24)]
        /* 0x3A40 */ public float PS4FOVFoot;
        [NMS(Index = 25)]
        /* 0x3A44 */ public float PS4FOVShip;
        [NMS(Index = 6)]
        /* 0x3A48 */ public int ScreenBrightness;
        [NMS(Index = 8)]
        /* 0x3A4C */ public int SfxVolume;
        [NMS(Index = 100)]
        /* 0x3A50 */ public float ShipHUDVROffset;
        // size: 0x3
        public enum SpaceCombatFollowModeEnum : uint {
            Disabled,
            Hold,
            Toggle,
        }
        [NMS(Index = 80)]
        /* 0x3A54 */ public SpaceCombatFollowModeEnum SpaceCombatFollowMode;
        // size: 0x3
        public enum SuitVoiceEnum : uint {
            Off,
            High,
            Low,
        }
        [NMS(Index = 10)]
        /* 0x3A58 */ public SuitVoiceEnum SuitVoice;
        // size: 0x4
        public enum TemperatureUnitEnum : uint {
            Invalid,
            C,
            F,
            K,
        }
        [NMS(Index = 49)]
        /* 0x3A5C */ public TemperatureUnitEnum TemperatureUnit;
        [NMS(Index = 79)]
        /* 0x3A60 */ public int TriggerFeedbackStrength;
        // size: 0x2
        public enum TurnModeEnum : uint {
            Smooth,
            Snap,
        }
        [NMS(Index = 58)]
        /* 0x3A64 */ public TurnModeEnum TurnMode;
        // size: 0x4
        public enum UIColourSchemeEnum : uint {
            Default,
            Protanopia,
            Deuteranopia,
            Tritanopia,
        }
        [NMS(Index = 67)]
        /* 0x3A68 */ public UIColourSchemeEnum UIColourScheme;
        [NMS(Index = 95)]
        /* 0x3A6C */ public float UnderwaterDepthOfFieldStrength;
        [NMS(Index = 77)]
        /* 0x3A70 */ public int VibrationStrength;
        [NMS(Index = 9)]
        /* 0x3A74 */ public int VoiceVolume;
        [NMS(Index = 59)]
        /* 0x3A78 */ public float VRVignetteStrength;
        [NMS(Index = 83)]
        /* 0x3A7C */ public bool AccessibleText;
        [NMS(Index = 82)]
        /* 0x3A7D */ public bool AllowWhiteScreenTransitions;
        [NMS(Index = 92)]
        /* 0x3A7E */ public bool AutoRotateThirdPersonPlayerCamera;
        [NMS(Index = 84)]
        /* 0x3A7F */ public bool AutoScanDiscoveries;
        [NMS(Index = 57)]
        /* 0x3A80 */ public bool BaseBuildingShowOptionsFromVision;
        [NMS(Index = 73)]
        /* 0x3A81 */ public bool BaseComplexityLimitsEnabled;
        [NMS(Index = 47)]
        /* 0x3A82 */ public bool CrossPlatform;
        [NMS(Index = 48)]
        /* 0x3A83 */ public bool CrossSaves;
        [NMS(Index = 64)]
        /* 0x3A84 */ public bool CrossSavesAutoUploads;
        [NMS(Index = 66)]
        /* 0x3A85 */ public bool CrossSavesSuppressAutoUploadTimeoutPopup;
        [NMS(Index = 22)]
        /* 0x3A86 */ public bool DamageNumbers;
        [NMS(Index = 56)]
        /* 0x3A87 */ public bool EnableControllerCursorInVR;
        [NMS(Index = 74)]
        /* 0x3A88 */ public bool EnableLargeLobbies;
        [NMS(Index = 68)]
        /* 0x3A89 */ public bool EnableModdingConsole;
        [NMS(Index = 69)]
        /* 0x3A8A */ public bool HeadBob;
        [NMS(Index = 101)]
        /* 0x3A8B */ public bool HighlightInteractableObjects;
        [NMS(Index = 4)]
        /* 0x3A8C */ public bool HUDHidden;
        [NMS(Index = 87)]
        /* 0x3A8D */ public bool IncreaseMissionTextContrast;
        [NMS(Index = 44)]
        /* 0x3A8E */ public bool InstantUIDelete;
        [NMS(Index = 43)]
        /* 0x3A8F */ public bool InstantUIInputs;
        [NMS(Index = 1)]
        /* 0x3A90 */ public bool InvertFlightControls;
        [NMS(Index = 0)]
        /* 0x3A91 */ public bool InvertLookControls;
        [NMS(Index = 2)]
        /* 0x3A92 */ public bool InvertVRInWorldFlightControls;
        [NMS(Index = 89)]
        /* 0x3A93 */ public bool MoveableWristMenus;
        [NMS(Index = 42)]
        /* 0x3A94 */ public bool Multiplayer;
        [NMS(Index = 86)]
        /* 0x3A95 */ public bool PlaceJumpSwap;
        [NMS(Index = 28)]
        /* 0x3A96 */ public bool PS4VignetteAndScanlines;
        [NMS(Index = 27)]
        /* 0x3A97 */ public bool PS5ProVRPSSR;
        [NMS(Index = 97)]
        /* 0x3A98 */ public bool QuickMenuBuildMenuSwap;
        [NMS(Index = 45)]
        /* 0x3A99 */ public bool SpeechToText;
        [NMS(Index = 96)]
        /* 0x3A9A */ public bool SpookHazardSkySpin;
        [NMS(Index = 85)]
        /* 0x3A9B */ public bool SprintScanSwap;
        [NMS(Index = 46)]
        /* 0x3A9C */ public bool Translate;
        [NMS(Index = 63)]
        /* 0x3A9D */ public bool UseAutoTorch;
        [NMS(Index = 72)]
        /* 0x3A9E */ public bool UseCharacterHeightForCamera;
        [NMS(Index = 50)]
        /* 0x3A9F */ public bool UseOldMouseFlight;
        [NMS(Index = 60)]
        /* 0x3AA0 */ public bool UseShipAutoControlVignette;
        [NMS(Index = 5)]
        /* 0x3AA1 */ public bool Vibration;
        [NMS(Index = 41)]
        /* 0x3AA2 */ public bool VoiceChat;
        [NMS(Index = 103)]
        /* 0x3AA3 */ public bool VRHandControllerEnableTwist;
        [NMS(Index = 102)]
        /* 0x3AA4 */ public bool VRHandControllerSwapYawAndRoll;
        [NMS(Index = 70)]
        /* 0x3AA5 */ public bool VRHeadBob;
        [NMS(Index = 71)]
        /* 0x3AA6 */ public bool VRShowBody;
        [NMS(Index = 3)]
        /* 0x3AA7 */ public bool VRVehiclesUseWorldControls;
        [NMS(Index = 26)]
        /* 0x3AA8 */ public bool XboxOneXHighResolutionMode;
    }
}
