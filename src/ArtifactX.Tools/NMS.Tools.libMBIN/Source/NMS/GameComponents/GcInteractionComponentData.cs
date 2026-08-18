using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x7C6CCD9976A94F37, NameHash = 0xBE246A5)]
    public class GcInteractionComponentData : NMSTemplate
    {
        [NMS(Index = 3)]
        /* 0x000 */ public TkModelRendererData Renderer;
        [NMS(Index = 4)]
        /* 0x0C0 */ public TkModelRendererData RendererAlt;
        [NMS(Index = 12)]
        /* 0x180 */ public GcInteractionActivationCost ActivationCost;
        [NMS(Index = 22)]
        /* 0x1E8 */ public GcInteractionActivationCost SecondaryActivationCost;
        [NMS(Index = 30)]
        /* 0x250 */ public GcStoryUtilityOverride StoryUtilityOverrideData;
        [NMS(Index = 29)]
        /* 0x298 */ public List<GcAdditionalOptionMissionOverride> AdditionalOptionsOverrideTable;
        [NMS(Index = 23)]
        /* 0x2A8 */ public List<TkModelRendererData> EventRenderers;
        [NMS(Index = 24)]
        /* 0x2B8 */ public List<TkModelRendererData> EventRenderersAlt;
        [NMS(Index = 25)]
        /* 0x2C8 */ public List<GcInteractionDof> EventRenderersDoF;
        [NMS(Index = 44)]
        /* 0x2D8 */ public NMSTemplate InteractionSpecificData;
        [NMS(Index = 28)]
        /* 0x2E8 */ public List<GcAlienPuzzleMissionOverride> PuzzleMissionOverrideTable;
        [NMS(Index = 40)]
        /* 0x2F8 */ public NMSString0x10 StartMissionOnUse;
        [NMS(Index = 13)]
        /* 0x308 */ public NMSString0x10 TriggerAction;
        [NMS(Index = 14)]
        /* 0x318 */ public NMSString0x10 TriggerActionOnPrepare;
        [NMS(Index = 27)]
        /* 0x328 */ public GcInteractionDof DepthOfField;
        [NMS(Index = 6)]
        /* 0x33C */ public float AttractDistanceSq;
        [NMS(Index = 11)]
        /* 0x340 */ public float BlendFromCameraTime;
        [NMS(Index = 10)]
        /* 0x344 */ public float BlendToCameraTime;
        [NMS(Index = 18)]
        /* 0x348 */ public float InteractAngle;
        [NMS(Index = 19)]
        /* 0x34C */ public float InteractDistance;
        // size: 0x3
        public enum InteractionActionEnum : uint {
            PressButton,
            HoldButton,
            Shoot,
        }
        [NMS(Index = 0)]
        /* 0x350 */ public InteractionActionEnum InteractionAction;
        [NMS(Index = 1)]
        /* 0x354 */ public GcInteractionType InteractionType;
        [NMS(Index = 17)]
        /* 0x358 */ public float InteractMaxHeightDiff;
        [NMS(Index = 37)]
        /* 0x35C */ public float InWorldUIForcedOffset;
        [NMS(Index = 38)]
        /* 0x360 */ public float InWorldUIForcedOffsetV2;
        [NMS(Index = 35)]
        /* 0x364 */ public float InWorldUIMinDistOverride;
        [NMS(Index = 36)]
        /* 0x368 */ public float InWorldUIMinDistOverrideV2;
        [NMS(Index = 34)]
        /* 0x36C */ public float InWorldUIScaler;
        [NMS(Index = 5)]
        /* 0x370 */ public GcAlienRace Race;
        [NMS(Index = 16)]
        /* 0x374 */ public float RangeToAllowAtAnyAngle;
        [NMS(Index = 26)]
        /* 0x378 */ public float SecondaryCameraTransitionTime;
        [NMS(Index = 21)]
        /* 0x37C */ public GcInteractionType SecondaryInteractionType;
        [NMS(Index = 41)]
        /* 0x380 */ public bool AllowMissionUnderwater;
        [NMS(Index = 15)]
        /* 0x381 */ public bool BroadcastTriggerAction;
        [NMS(Index = 20)]
        /* 0x382 */ public bool InteractInvertFace;
        [NMS(Index = 39)]
        /* 0x383 */ public bool InWorldUIUseCameraUp;
        [NMS(Index = 42)]
        /* 0x384 */ public bool OnlyAvailableInAbandonedMode;
        // size: 0x3
        public enum OverrideInteriorExteriorMarkerEnum : byte {
            No,
            Interior,
            Exterior,
        }
        [NMS(Index = 31)]
        /* 0x385 */ public OverrideInteriorExteriorMarkerEnum OverrideInteriorExteriorMarker;
        [NMS(Index = 8)]
        /* 0x386 */ public bool RepeatInteraction;
        [NMS(Index = 32)]
        /* 0x387 */ public bool ReseedAfterRewardSuccess;
        [NMS(Index = 7)]
        /* 0x388 */ public bool SecondaryMeshAlwaysVisible;
        [NMS(Index = 9)]
        /* 0x389 */ public bool UseInteractCamera;
        [NMS(Index = 2)]
        /* 0x38A */ public bool UseIntermediateUI;
        [NMS(Index = 33)]
        /* 0x38B */ public bool UsePersonalPersistentBuffer;
        [NMS(Index = 43)]
        /* 0x38C */ public bool UseUnlockedInteractionIfMaintDone;
    }
}
