// Prints the instructions from each start address for a count of instructions (e.g. around a reference the
// decompiler can't reach because the function is too big).
// usage (headless postScript): Disasm.java <output.txt> 0xADDR:COUNT [0xADDR:COUNT ...]
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Instruction;
import java.io.*;

public class Disasm extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        try (PrintWriter out = new PrintWriter(new FileWriter(args[0]))) {
            for (int i = 1; i < args.length; i++) {
                String[] parts = args[i].split(":");
                Address a = toAddr(Long.parseUnsignedLong(parts[0].replace("0x", ""), 16));
                int count = parts.length > 1 ? Integer.parseInt(parts[1]) : 40;
                out.println("== " + parts[0]);
                Instruction ins = getInstructionAt(a);
                if (ins == null) ins = getInstructionAfter(a);
                for (int n = 0; n < count && ins != null; n++, ins = ins.getNext())
                    out.println("  " + ins.getAddress() + "  " + ins);
            }
        }
    }
}
