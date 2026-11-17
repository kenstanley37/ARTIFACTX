// Lists functions whose instructions contain every scalar given (hex), e.g. to find a routine by the constants a
// port of it uses. Writes "entry size matched" lines.
// usage (headless postScript): FindByScalars.java <output.txt> <hex scalar> [<hex scalar> ...]
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.*;
import ghidra.program.model.scalar.Scalar;
import java.io.*;
import java.util.*;

public class FindByScalars extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        Set<Long> wanted = new HashSet<>();
        for (int i = 1; i < args.length; i++) wanted.add(Long.parseUnsignedLong(args[i].replace("0x", ""), 16));
        try (PrintWriter out = new PrintWriter(new FileWriter(args[0]))) {
            for (Function f : currentProgram.getFunctionManager().getFunctions(true)) {
                if (monitor.isCancelled()) break;
                Set<Long> found = new HashSet<>();
                for (Instruction ins : currentProgram.getListing().getInstructions(f.getBody(), true)) {
                    for (int op = 0; op < ins.getNumOperands(); op++) {
                        for (Object o : ins.getOpObjects(op)) {
                            if (o instanceof Scalar s) {
                                long v = s.getUnsignedValue() & 0xFFFFFFFFL;
                                if (wanted.contains(v)) found.add(v);
                            }
                        }
                    }
                }
                if (found.size() == wanted.size())
                    out.println("0x" + Long.toHexString(f.getEntryPoint().getOffset()) + " " + f.getBody().getNumAddresses());
            }
        }
    }
}
