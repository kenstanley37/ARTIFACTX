namespace libMBIN.NMS.Toolkit
{
    [NMS(GUID = 0xA53AA77A63E78727, NameHash = 0x307BF86B)]
    public class TkNGuiUserSettings : NMSTemplate
    {
        [NMS(Index = 7, Size = 0xA)]
        /* 0x0000 */ public NMSString0x10[] AnimationViewerRecents;
        [NMS(Index = 8, Size = 0xA)]
        /* 0x00A0 */ public NMSString0x10[] AnimationViewerRecentWindows;
        [NMS(Index = 6)]
        /* 0x0140 */ public float FileBrowserThumbnailSize;
        [NMS(Index = 11)]
        /* 0x0144 */ public float NguiScale;
        [NMS(Index = 3, Size = 0x14)]
        /* 0x0148 */ public NMSString0x80[] FavouriteWindows;
        [NMS(Index = 5, Size = 0xA)]
        /* 0x0B48 */ public NMSString0x100[] FileBrowserFavourites;
        [NMS(Index = 4, Size = 0xA)]
        /* 0x1548 */ public NMSString0x100[] FileBrowserRecents;
        [NMS(Index = 1)]
        /* 0x1F48 */ public NMSString0x100 LastActiveLayout;
        [NMS(Index = 0)]
        /* 0x2048 */ public NMSString0x100 LastLoadedModel;
        [NMS(Index = 9)]
        /* 0x2148 */ public bool CanSelectRegionDecoratorNodesInDebugEditor;
        [NMS(Index = 10)]
        /* 0x2149 */ public bool DebugEditorDebugDrawInPlayMode;
        [NMS(Index = 2)]
        /* 0x214A */ public bool FileBrowserAutoBuildTree;
    }
}
