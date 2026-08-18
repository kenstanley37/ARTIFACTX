namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x831ED513AA3B0717, NameHash = 0xF6A61103)]
    public class GcProjectileImpactType : NMSTemplate
    {
        // size: 0x12
        public enum ImpactEnum : uint {
            Default,
            Terrain,
            Substance,
            Rock,
            Asteroid,
            Shield,
            Creature,
            Robot,
            Freighter,
            Cargo,
            Ship,
            Plant,
            NeedsTech,
            Player,
            OtherPlayer,
            SentinelShield,
            SpaceshipShield,
            FreighterShield,
        }
        [NMS(Index = 0)]
        /* 0x0 */ public ImpactEnum Impact;
    }
}
