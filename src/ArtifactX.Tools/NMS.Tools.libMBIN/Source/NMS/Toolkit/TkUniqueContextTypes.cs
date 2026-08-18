namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0x82B300B57997E9C4, NameHash = 0x24EBA11D)]
    public class TkUniqueContextTypes : NMSTemplate
    {
        // size: 0x7
        public enum UniqueContextTypeEnum : uint {
            Debug,
            Generic,
            Environment,
            Building,
            Event,
            BaseObject,
            Dungeon,
        }
        [NMS(Index = 0)]
        /* 0x0 */ public UniqueContextTypeEnum UniqueContextType;
    }
}
