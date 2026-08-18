using libMBIN.NMS.GameComponents;

namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0xB2708D9C7D7499C5, NameHash = 0x7A7B4ECF)]
    public class GcMissionConditionFactionRank : NMSTemplate
    {
        [NMS(Index = 0)]
        /* 0x0 */ public GcMissionFaction Faction;
        [NMS(Index = 2)]
        /* 0x4 */ public int Rank;
        [NMS(Index = 1)]
        /* 0x8 */ public bool UseSystemRace;
    }
}
