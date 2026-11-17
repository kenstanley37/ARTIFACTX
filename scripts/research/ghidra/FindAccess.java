// Lists functions whose instructions use every given displacement ([reg + disp], read or write), e.g. to find the
// code that reads a known struct by its field offsets.
// usage (headless postScript): FindAccess.java <output.txt> 0xDISP [0xDISP ...]
import ghidra.app.script.GhidraScript;
import ghidra.program.model.lang.OperandType;
import ghidra.program.model.listing.*;
import ghidra.program.model.scalar.Scalar;
import java.io.*;
import java.util.*;

public class FindAccess extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        Set<Long> wanted = new HashSet<>();
        for (int i = 1; i < args.length; i++) wanted.add(Long.parseLong(args[i].replace("0x", ""), 16));
        Listing listing = currentProgram.getListing();
        try (PrintWriter out = new PrintWriter(new FileWriter(args[0]))) {
            for (Function f : listing.getFunctions(true)) {
                if (monitor.isCancelled()) break;
                Set<Long> found = new HashSet<>();
                for (Instruction ins : listing.getInstructions(f.getBody(), true))
                    for (int op = 0; op < ins.getNumOperands(); op++) {
                        if ((ins.getOperandType(op) & OperandType.DYNAMIC) == 0) continue;
                        for (Object o : ins.getOpObjects(op))
                            if (o instanceof Scalar s && wanted.contains(s.getUnsignedValue())) found.add(s.getUnsignedValue());
                    }
                if (found.size() == wanted.size())
                    out.println(f.getEntryPoint() + " " + f.getName() + " size " + f.getBody().getNumAddresses());
            }
        }
    }
}
