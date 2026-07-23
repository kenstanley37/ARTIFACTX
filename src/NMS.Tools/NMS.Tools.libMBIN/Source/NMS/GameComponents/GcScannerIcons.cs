using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xE222DE89F2565EAB, NameHash = 0xF873D7AD)]
    public class GcScannerIcons : NMSTemplate
    {
        [NMS(Index = 41, Size = 0x4E, EnumType = typeof(GcScannerIconTypes.ScanIconTypeEnum))]
        /* 0x0000 */ public Colour[] ScannableColours;
        [NMS(Index = 63, Size = 0x4)]
        /* 0x04E0 */ public Colour[] NetworkFSPlayerColours;
        [NMS(Index = 42)]
        /* 0x0520 */ public Colour BuildingColour;
        [NMS(Index = 43)]
        /* 0x0530 */ public Colour GenericColour;
        [NMS(Index = 44)]
        /* 0x0540 */ public Colour RelicColour;
        [NMS(Index = 45)]
        /* 0x0550 */ public Colour SignalColour;
        [NMS(Index = 46)]
        /* 0x0560 */ public Colour UnknownColour;
        [NMS(Index = 39, Size = 0x4E, EnumType = typeof(GcScannerIconTypes.ScanIconTypeEnum))]
        /* 0x0570 */ public GcScannerIcon[] ScannableIcons;
        [NMS(Index = 40, Size = 0x4E, EnumType = typeof(GcScannerIconTypes.ScanIconTypeEnum))]
        /* 0x1680 */ public GcScannerIcon[] ScannableIconsBinocs;
        [NMS(Index = 36, Size = 0x25, EnumType = typeof(GcScannerBuildingIconTypes.ScanBuildingIconTypeEnum))]
        /* 0x2790 */ public GcScannerIcon[] BuildingIcons;
        [NMS(Index = 37, Size = 0x25, EnumType = typeof(GcScannerBuildingIconTypes.ScanBuildingIconTypeEnum))]
        /* 0x2FA8 */ public GcScannerIcon[] BuildingIconsBinocs;
        [NMS(Index = 38, Size = 0x25, EnumType = typeof(GcScannerBuildingIconTypes.ScanBuildingIconTypeEnum))]
        /* 0x37C0 */ public GcScannerIcon[] BuildingIconsHuge;
        [NMS(Index = 5, Size = 0x7, EnumType = typeof(GcVehicleType.VehicleTypeEnum))]
        /* 0x3FD8 */ public GcScannerIcon[] Vehicles;
        [NMS(Index = 35, Size = 0x6, EnumType = typeof(GcGenericIconTypes.GenericIconTypeEnum))]
        /* 0x4160 */ public GcScannerIcon[] GenericIcons;
        [NMS(Index = 64, Size = 0x4)]
        /* 0x42B0 */ public GcScannerIcon[] NetworkFSPlayerCorvetteTeleporter;
        [NMS(Index = 61, Size = 0x4)]
        /* 0x4390 */ public GcScannerIcon[] NetworkFSPlayerMarkers;
        [NMS(Index = 62, Size = 0x4)]
        /* 0x4470 */ public GcScannerIcon[] NetworkFSPlayerMarkersShip;
        [NMS(Index = 65, Size = 0x4)]
        /* 0x4550 */ public GcScannerIcon[] NetworkPlayerFreighter;
        [NMS(Index = 74, Size = 0x5, EnumType = typeof(GcScannerIconHighlightTypes.ScannerIconHighlightTypeEnum))]
        /* 0x4630 */ public TkTextureResource[] HighlightIcons;
        [NMS(Index = 34)]
        /* 0x46A8 */ public GcScannerIcon ArrowLarge;
        [NMS(Index = 33)]
        /* 0x46E0 */ public GcScannerIcon ArrowSmall;
        [NMS(Index = 52)]
        /* 0x4718 */ public GcScannerIcon BaseBuildingMarker;
        [NMS(Index = 18)]
        /* 0x4750 */ public GcScannerIcon Battle;
        [NMS(Index = 23)]
        /* 0x4788 */ public GcScannerIcon BattleSmall;
        [NMS(Index = 67)]
        /* 0x47C0 */ public GcScannerIcon BlackHole;
        [NMS(Index = 15)]
        /* 0x47F8 */ public GcScannerIcon Bounty1;
        [NMS(Index = 16)]
        /* 0x4830 */ public GcScannerIcon Bounty2;
        [NMS(Index = 17)]
        /* 0x4868 */ public GcScannerIcon Bounty3;
        [NMS(Index = 22)]
        /* 0x48A0 */ public GcScannerIcon BountySmall;
        [NMS(Index = 25)]
        /* 0x48D8 */ public GcScannerIcon Checkpoint;
        [NMS(Index = 30)]
        /* 0x4910 */ public GcScannerIcon CircleAnimation;
        [NMS(Index = 3)]
        /* 0x4948 */ public GcScannerIcon Corvette;
        [NMS(Index = 29)]
        /* 0x4980 */ public GcScannerIcon CorvetteDeployedTeleporter;
        [NMS(Index = 69)]
        /* 0x49B8 */ public GcScannerIcon CreatureAction;
        [NMS(Index = 68)]
        /* 0x49F0 */ public GcScannerIcon CreatureCurious;
        [NMS(Index = 71)]
        /* 0x4A28 */ public GcScannerIcon CreatureDanger;
        [NMS(Index = 47)]
        /* 0x4A60 */ public GcScannerIcon CreatureDiscovered;
        [NMS(Index = 72)]
        /* 0x4A98 */ public GcScannerIcon CreatureFiend;
        [NMS(Index = 80)]
        /* 0x4AD0 */ public GcScannerIcon CreatureInteraction;
        [NMS(Index = 73)]
        /* 0x4B08 */ public GcScannerIcon CreatureMilk;
        [NMS(Index = 70)]
        /* 0x4B40 */ public GcScannerIcon CreatureTame;
        [NMS(Index = 48)]
        /* 0x4B78 */ public GcScannerIcon CreatureUndiscovered;
        [NMS(Index = 49)]
        /* 0x4BB0 */ public GcScannerIcon CreatureUnknown;
        [NMS(Index = 9)]
        /* 0x4BE8 */ public GcScannerIcon DamagedFrigate;
        [NMS(Index = 14)]
        /* 0x4C20 */ public GcScannerIcon Death;
        [NMS(Index = 21)]
        /* 0x4C58 */ public GcScannerIcon DeathSmall;
        [NMS(Index = 32)]
        /* 0x4C90 */ public GcScannerIcon DiamondAnimation;
        [NMS(Index = 13)]
        /* 0x4CC8 */ public GcScannerIcon EditingBase;
        [NMS(Index = 11)]
        /* 0x4D00 */ public GcScannerIcon Expedition;
        [NMS(Index = 6)]
        /* 0x4D38 */ public GcScannerIcon Freighter;
        [NMS(Index = 7)]
        /* 0x4D70 */ public GcScannerIcon FreighterBase;
        [NMS(Index = 87)]
        /* 0x4DA8 */ public GcScannerIcon FriendlyDrone;
        [NMS(Index = 26)]
        /* 0x4DE0 */ public GcScannerIcon Garage;
        [NMS(Index = 31)]
        /* 0x4E18 */ public GcScannerIcon HexAnimation;
        [NMS(Index = 50)]
        /* 0x4E50 */ public GcScannerIcon MessageBeacon;
        [NMS(Index = 51)]
        /* 0x4E88 */ public GcScannerIcon MessageBeaconSmall;
        [NMS(Index = 79)]
        /* 0x4EC0 */ public GcScannerIcon MissionAbandonedFreighter;
        [NMS(Index = 76)]
        /* 0x4EF8 */ public GcScannerIcon MissionEnterBuilding;
        [NMS(Index = 78)]
        /* 0x4F30 */ public GcScannerIcon MissionEnterFreighter;
        [NMS(Index = 75)]
        /* 0x4F68 */ public GcScannerIcon MissionEnterOrbit;
        [NMS(Index = 77)]
        /* 0x4FA0 */ public GcScannerIcon MissionEnterStation;
        [NMS(Index = 57)]
        /* 0x4FD8 */ public GcScannerIcon MonumentMarker;
        [NMS(Index = 58)]
        /* 0x5010 */ public GcScannerIcon NetworkPlayerMarker;
        [NMS(Index = 59)]
        /* 0x5048 */ public GcScannerIcon NetworkPlayerMarkerShip;
        [NMS(Index = 60)]
        /* 0x5080 */ public GcScannerIcon NetworkPlayerMarkerVehicle;
        [NMS(Index = 27)]
        /* 0x50B8 */ public GcScannerIcon NPC;
        [NMS(Index = 86)]
        /* 0x50F0 */ public GcScannerIcon OtherPlayerSettlement;
        [NMS(Index = 82)]
        /* 0x5128 */ public GcScannerIcon Pet;
        [NMS(Index = 84)]
        /* 0x5160 */ public GcScannerIcon PetActivity;
        [NMS(Index = 81)]
        /* 0x5198 */ public GcScannerIcon PetInteraction;
        [NMS(Index = 83)]
        /* 0x51D0 */ public GcScannerIcon PetSad;
        [NMS(Index = 88)]
        /* 0x5208 */ public GcScannerIcon PirateRaid;
        [NMS(Index = 55)]
        /* 0x5240 */ public GcScannerIcon PlanetPoleEast;
        [NMS(Index = 53)]
        /* 0x5278 */ public GcScannerIcon PlanetPoleNorth;
        [NMS(Index = 54)]
        /* 0x52B0 */ public GcScannerIcon PlanetPoleSouth;
        [NMS(Index = 56)]
        /* 0x52E8 */ public GcScannerIcon PlanetPoleWest;
        [NMS(Index = 12)]
        /* 0x5320 */ public GcScannerIcon PlayerBase;
        [NMS(Index = 8)]
        /* 0x5358 */ public GcScannerIcon PlayerFreighter;
        [NMS(Index = 85)]
        /* 0x5390 */ public GcScannerIcon PlayerSettlement;
        [NMS(Index = 66)]
        /* 0x53C8 */ public GcScannerIcon PortalMarker;
        [NMS(Index = 10)]
        /* 0x5400 */ public GcScannerIcon PurchasableFrigate;
        [NMS(Index = 28)]
        /* 0x5438 */ public GcScannerIcon SettlementNPC;
        [NMS(Index = 2)]
        /* 0x5470 */ public GcScannerIcon Ship;
        [NMS(Index = 20)]
        /* 0x54A8 */ public GcScannerIcon ShipSmall;
        [NMS(Index = 19)]
        /* 0x54E0 */ public GcScannerIcon SwarmHiveBattle;
        [NMS(Index = 0)]
        /* 0x5518 */ public GcScannerIcon TaggedBuilding;
        [NMS(Index = 1)]
        /* 0x5550 */ public GcScannerIcon TaggedPlanet;
        [NMS(Index = 24)]
        /* 0x5588 */ public GcScannerIcon TimedEvent;
        [NMS(Index = 4)]
        /* 0x55C0 */ public GcScannerIcon VehicleGeneric;
    }
}
