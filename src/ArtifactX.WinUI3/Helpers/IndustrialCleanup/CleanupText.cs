using System.Text.RegularExpressions;

namespace ArtifactX.WinUI3.Helpers.IndustrialCleanup;

/// <summary>Plain presentation of game markup using action names rather than
/// invented keyboard/controller bindings. These controls are player-remappable.</summary>
internal static class CleanupText
{
    public static string Plain(string text)
    {
        text = text.Replace("<NEWLINE>", "\n");
        text = Regex.Replace(text, @"<IMG>([^<]+)<>", m => m.Groups[1].Value switch
        {
            "ALTATTACK" => "[Secondary action]", "ATTACK" => "[Primary action]",
            "CYCLEWEAPON" => "[Switch weapon mode]", "VEHICLESCAN" => "[Vehicle scan]",
            "QUICK_MENU" => "[Quick Menu]", "AIM" => "[Analysis Visor]",
            "OPTIONS" => "[Mission Log]", "NANITE" => " nanites", _ => ""
        });
        return Regex.Replace(text, "<[^>]*>", "").Trim().TrimEnd(':');
    }
}
