// Lists functions that store to every given displacement ([reg + disp] as a write destination), e.g. to find the
// code that fills a known struct by its field offsets. "0xDISP=VALUE" also requires that immediate value to be
// stored there (e.g. 0x38=3 for "type field set to 3").
// usage (headless postScript): FindStores.java <output.txt> 0xDISP[=VALUE] [0xDISP[=VALUE] ...]
import ghidra.app.script.GhidraScript;
import ghidra.program.model.lang.OperandType;
import ghidra.program.model.listing.*;
import ghidra.program.model.scalar.Scalar;
import java.io.*;
import java.util.*;

public class FindStores extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        Map<String, Long> wanted = new LinkedHashMap<>(); // key "disp" or "disp=value"
        for (int i = 1; i < args.length; i++) {
            String[] p = args[i].split("=");
            long disp = Long.parseLong(p[0].replace("0x", ""), 16);
            wanted.put(p.length > 1 ? disp + "=" + Long.decode(p[1]) : Long.toString(disp), disp);
        }
        Listing listing = currentProgram.getListing();
        try (PrintWriter out = new PrintWriter(new FileWriter(args[0]))) {
            for (Function f : listing.getFunctions(true)) {
                if (monitor.isCancelled()) break;
                Set<String> found = new HashSet<>();
                for (Instruction ins : listing.getInstructions(f.getBody(), true)) {
                    if (ins.getNumOperands() < 2) continue;
                    if (!ins.getMnemonicString().startsWith("MOV")) continue;
                    if ((ins.getOperandType(0) & OperandType.DYNAMIC) == 0) continue;
                    Long value = null;
                    if ((ins.getOperandType(1) & OperandType.SCALAR) != 0) value = ins.getScalar(1).getValue();
                    for (Object o : ins.getOpObjects(0)) {
                        if (!(o instanceof Scalar s)) continue;
                        String d = Long.toString(s.getUnsignedValue());
                        if (wanted.containsKey(d)) found.add(d);
                        if (value != null && wanted.containsKey(d + "=" + value)) found.add(d + "=" + value);
                    }
                }
                if (found.size() == wanted.size())
                    out.println(f.getEntryPoint() + " " + f.getName() + " size " + f.getBody().getNumAddresses());
            }
        }
    }
}
