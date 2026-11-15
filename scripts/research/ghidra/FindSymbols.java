// Prints every symbol whose name matches a Java regex (e.g. RTTI vftable labels such as "cGcShipWeapons.*vftable").
// usage (headless postScript): FindSymbols.java <output.txt> <regex> [<regex> ...]
import ghidra.app.script.GhidraScript;
import ghidra.program.model.symbol.Symbol;
import java.io.*;
import java.util.regex.Pattern;

public class FindSymbols extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        try (PrintWriter out = new PrintWriter(new FileWriter(args[0]))) {
            for (int i = 1; i < args.length; i++) {
                Pattern p = Pattern.compile(args[i]);
                out.println("== " + args[i]);
                for (Symbol s : currentProgram.getSymbolTable().getAllSymbols(false)) {
                    String name = s.getName(true);
                    if (p.matcher(name).find()) out.println("  " + s.getAddress() + "  " + name);
                }
            }
        }
    }
}
