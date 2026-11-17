// Prints every reference to the functions at the given addresses, with the referencing function.
// usage (headless postScript): Callers.java <output.txt> 0xADDR [0xADDR ...]
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.Reference;
import java.io.*;

public class Callers extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        try (PrintWriter out = new PrintWriter(new FileWriter(args[0]))) {
            for (int i = 1; i < args.length; i++) {
                Address target = toAddr(Long.parseUnsignedLong(args[i].replace("0x", ""), 16));
                Function tf = getFunctionContaining(target);
                if (tf != null) target = tf.getEntryPoint();
                out.println("== " + target + (tf != null ? " " + tf.getName() : ""));
                for (Reference r : getReferencesTo(target)) {
                    Function f = getFunctionContaining(r.getFromAddress());
                    out.println("  " + r.getFromAddress() + " " + r.getReferenceType() + " in "
                        + (f == null ? "?" : f.getEntryPoint() + " " + f.getName()));
                }
            }
        }
    }
}
