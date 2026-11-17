// Decompiles the functions starting at the addresses listed in a text file (one hex VA per line) into one C file.
// usage (headless postScript): DecompileList.java <addresses.txt> <output.c>
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import java.io.*;
import java.nio.file.*;
import java.util.*;

public class DecompileList extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        List<String> lines = Files.readAllLines(Paths.get(args[0]));
        DecompInterface decomp = new DecompInterface();
        decomp.openProgram(currentProgram);
        Set<Long> seen = new HashSet<>();
        try (PrintWriter out = new PrintWriter(new FileWriter(args[1]))) {
            for (String line : lines) {
                line = line.trim();
                if (line.isEmpty()) continue;
                Address a = toAddr(Long.parseUnsignedLong(line.replace("0x", ""), 16));
                Function f = getFunctionContaining(a);
                if (f == null) { disassemble(a); f = createFunction(a, null); }
                if (f == null) { out.println("// no function at " + line); continue; }
                if (!seen.add(f.getEntryPoint().getOffset())) continue;
                DecompileResults r = decomp.decompileFunction(f, 300, monitor);
                out.println("// ===== 0x" + Long.toHexString(f.getEntryPoint().getOffset()) + " " + f.getName() + " size " + f.getBody().getNumAddresses());
                out.println(r.decompileCompleted() ? r.getDecompiledFunction().getC() : "// failed: " + r.getErrorMessage());
                out.flush();
            }
        }
        decomp.dispose();
    }
}
