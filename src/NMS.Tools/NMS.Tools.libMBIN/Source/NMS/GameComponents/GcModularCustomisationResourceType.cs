namespace libMBIN.NMS.GameComponents
{
    [NMS(GUID = 0x669BC661D6D6E7DB, NameHash = 0x89CBAB6)]
    public class GcModularCustomisationResourceType : NMSTemplate
    {
        // size: 0xB
        public enum ModularCustomisationResourceTypeEnum : uint {
            MultiToolStaff,
            Fighter,
            Dropship,
            Scientific,
            Shuttle,
            Sail,
            ExhibitTRex,
            ExhibitWorm,
            ExhibitGrunt,
            ExhibitQuadruped,
            ExhibitBird,
        }
        [NMS(Index = 0)]
        /* 0x0 */ public ModularCustomisationResourceTypeEnum ModularCustomisationResourceType;
    }
}
