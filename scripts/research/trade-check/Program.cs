// Station trade stock research harness. Modes:
//   scan <scene>          every interaction in a scene graph with its scene-local position
//   check <x> <y> <z>     predict each captured station's terminal stock from that local terminal position
//   sweep <from> <to>     fit the trade-goods draw count against captured substances (PER=1: per station)
//   stats                 product table sizes and categories
using System.Numerics;
using System.Globalization;
Paks.Load();
switch (args[0])
{
    case "scan": Scan.Run(args[1]); break;
    case "sweep": Sweep.Run(new Vector3(-5.0109487f, 1.875f, -942.5f), int.Parse(args[1]), int.Parse(args[2])); break;
    case "stats": Stats.Run(); break;
    case "core": CoreCheck.Run(); break;
    case "timing": CoreCheck.Timing(); break;
    case "diff": Diff.Run(); break;
    case "outpost": Outpost.Run(); break;
    case "posts": Posts.Run(); break;
    case "price": Price.Run(""); break;
    case "post": Post.Run(args[1], double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture), double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture)); break;
    case "save": SaveCheck.Run(args[1], args.Length > 2 ? int.Parse(args[2]) : 0); break;
    case "check":
        float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        Check.Run(new Vector3(F(args[1]), F(args[2]), F(args[3])), args.Length > 4 ? args[4] : "mix"); break;
}
