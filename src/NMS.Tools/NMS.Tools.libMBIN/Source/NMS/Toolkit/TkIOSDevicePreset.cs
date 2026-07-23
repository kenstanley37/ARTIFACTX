using libMBIN.NMS.Toolkit;
using System.Collections.Generic;

namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0x91DC6C797CC7D4F7, NameHash = 0x85F192FA)]
    public class TkIOSDevicePreset : NMSTemplate
    {
        [NMS(Index = 2)]
        /* 0x000 */ public TkGraphicsSettings DefaultGraphicsSettings;
        [NMS(Index = 1)]
        /* 0x1D0 */ public List<NMSString0x100> ModelIdentifiers;
        [NMS(Index = 0)]
        /* 0x1E0 */ public NMSString0x100 DeviceName;
    }
}
