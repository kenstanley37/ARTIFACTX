// Prints every code reference to each given ASCII string (exact, NUL-terminated), with the referencing function.
// usage (headless postScript): StringRefs.java <output.txt> TEXT [TEXT ...]
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.Reference;
import java.io.*;
import java.nio.charset.StandardCharsets;

public class StringRefs extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        try (PrintWriter out = new PrintWriter(new FileWriter(args[0]))) {
            for (int i = 1; i < args.length; i++) {
                byte[] text = args[i].getBytes(StandardCharsets.US_ASCII);
                byte[] pattern = new byte[text.length + 1];
                System.arraycopy(text, 0, pattern, 0, text.length);
                Address start = currentProgram.getMinAddress();
                while (start != null) {
                    Address hit = currentProgram.getMemory().findBytes(start, pattern, null, true, monitor);
                    if (hit == null) break;
                    // Only whole strings: the byte before must be NUL or the reference would be to a longer string.
                    byte before = 1;
                    try { before = getByte(hit.subtract(1)); } catch (Exception e) { before = 0; }
                    if (before == 0) {
                        out.println("== \"" + args[i] + "\" at " + hit);
                        for (Reference r : getReferencesTo(hit)) {
                            Function f = getFunctionContaining(r.getFromAddress());
                            out.println("  " + r.getFromAddress() + " in " + (f == null ? "?" : f.getEntryPoint() + " " + f.getName()));
                        }
                    }
                    start = hit.add(1);
                }
            }
        }
    }
}
