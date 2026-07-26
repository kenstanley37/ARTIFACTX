using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;
using System.Collections.Generic;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xD01BBC7DD015441F, NameHash = 0xFC70ABE3)]
    public class GcDestructableComponentData : NMSTemplate
    {
        [NMS(Index = 34, Size = 0x3, EnumType = typeof(GcRarity.RarityEnum))]
        /* 0x000 */ public NMSString0x10[] RarityLocators;
        [NMS(Index = 39)]
        /* 0x030 */ public TkModelResource DestroyedModel;
        [NMS(Index = 31)]
        /* 0x050 */ public NMSString0x20A OverrideRewardLoc;
        [NMS(Index = 50)]
        /* 0x070 */ public NMSString0x10 AreaDamage;
        [NMS(Index = 41)]
        /* 0x080 */ public NMSString0x10 DestroyedModelSpawnNode;
        [NMS(Index = 45)]
        /* 0x090 */ public NMSString0x10 DestroyEffect;
        [NMS(Index = 47)]
        /* 0x0A0 */ public NMSString0x10 DestroyEffectPoint;
        [NMS(Index = 0)]
        /* 0x0B0 */ public NMSString0x10 Explosion;
        [NMS(Index = 24)]
        /* 0x0C0 */ public NMSString0x10 GivesReward;
        [NMS(Index = 22)]
        /* 0x0D0 */ public List<GcSubstanceAmount> GivesSubstances;
        [NMS(Index = 56)]
        /* 0x0E0 */ public List<GcLootProbability> LootItems;
        [NMS(Index = 17)]
        /* 0x0F0 */ public NMSString0x10 LootReward;
        [NMS(Index = 27)]
        /* 0x100 */ public NMSString0x10 PirateSystemAltReward;
        [NMS(Index = 32)]
        /* 0x110 */ public List<GcRewardMissionOverride> RewardOverrideTable;
        [NMS(Index = 8)]
        /* 0x120 */ public NMSString0x10 TriggerAction;
        [NMS(Index = 4)]
        /* 0x130 */ public NMSString0x10 UnderwaterExplosion;
        [NMS(Index = 7)]
        /* 0x140 */ public NMSString0x10 VehicleDestroyEffect;
        [NMS(Index = 35, Size = 0xA, EnumType = typeof(GcMissionFaction.MissionFactionEnum))]
        /* 0x150 */ public int[] StandingChangeOnDeath;
        [NMS(Index = 21)]
        /* 0x178 */ public float CollisionImpulseForDestruction;
        [NMS(Index = 48)]
        /* 0x17C */ public float DestroyEffectTime;
        [NMS(Index = 43)]
        /* 0x180 */ public float DestroyForce;
        [NMS(Index = 44)]
        /* 0x184 */ public float DestroyForceRadius;
        [NMS(Index = 1)]
        /* 0x188 */ public float ExplosionScale;
        [NMS(Index = 10)]
        /* 0x18C */ public int IncreaseCorruptSentinelWanted;
        [NMS(Index = 13)]
        /* 0x190 */ public GcFiendCrime IncreaseFiendCrime;
        [NMS(Index = 12)]
        /* 0x194 */ public float IncreaseFiendWantedChance;
        [NMS(Index = 9)]
        /* 0x198 */ public int IncreaseWanted;
        [NMS(Index = 19)]
        /* 0x19C */ public int LootRewardAmountMax;
        [NMS(Index = 18)]
        /* 0x1A0 */ public int LootRewardAmountMin;
        [NMS(Index = 26)]
        /* 0x1A4 */ public int OverrideChipAmount;
        [NMS(Index = 53)]
        /* 0x1A8 */ public float ShowInteractRange;
        [NMS(Index = 23)]
        /* 0x1AC */ public GcStatsEnum StatToTrack;
        [NMS(Index = 5)]
        /* 0x1B0 */ public float UnderwaterExplosionScale;
        [NMS(Index = 33)]
        /* 0x1B4 */ public bool ActivateLocatorsFromRarity;
        [NMS(Index = 25)]
        /* 0x1B5 */ public bool BlockDestructionIfRewardFails;
        [NMS(Index = 20)]
        /* 0x1B6 */ public bool CanDestroyFromStoredInteraction;
        [NMS(Index = 15)]
        /* 0x1B7 */ public bool DamagesParentWhenDestroyed;
        [NMS(Index = 42)]
        /* 0x1B8 */ public bool DestroyedModelCollidesWithEverything;
        [NMS(Index = 40)]
        /* 0x1B9 */ public bool DestroyedModelUsesScale;
        [NMS(Index = 49)]
        /* 0x1BA */ public bool DestroyEffectMatrices;
        [NMS(Index = 46)]
        /* 0x1BB */ public bool DestroyEffectOnSurface;
        [NMS(Index = 2)]
        /* 0x1BC */ public bool ExplosionScaleToBounds;
        [NMS(Index = 3)]
        /* 0x1BD */ public bool ExplosionUsesImpactNormal;
        [NMS(Index = 55)]
        /* 0x1BE */ public bool GrenadeSingleHit;
        [NMS(Index = 52)]
        /* 0x1BF */ public bool HideInteractWhenAllArmourDestroyed;
        [NMS(Index = 54)]
        /* 0x1C0 */ public bool HideInteractWhenShielded;
        [NMS(Index = 38)]
        /* 0x1C1 */ public bool HideModel;
        [NMS(Index = 30)]
        /* 0x1C2 */ public bool HideReward;
        [NMS(Index = 11)]
        /* 0x1C3 */ public bool IncreaseFiendWanted;
        [NMS(Index = 29)]
        /* 0x1C4 */ public bool NoConsequencesDuringPirateBattle;
        [NMS(Index = 14)]
        /* 0x1C5 */ public bool NotifyEncounter;
        [NMS(Index = 6)]
        /* 0x1C6 */ public bool OnlyExplodeSelf;
        [NMS(Index = 37)]
        /* 0x1C7 */ public bool RemoveModel;
        [NMS(Index = 28)]
        /* 0x1C8 */ public bool RewardIfDestroyedByOther;
        [NMS(Index = 16)]
        /* 0x1C9 */ public bool SearchModelForShootable;
        [NMS(Index = 51)]
        /* 0x1CA */ public bool ShowInteract;
        [NMS(Index = 36)]
        /* 0x1CB */ public bool UseSystemColorsForTexture;
    }
}
