// Prints the bytes at each given address as float, double, uint32 and uint64 (constants the decompiler shows as DAT_).
// usage (headless postScript): ReadData.java <output.txt> 0xADDR [0xADDR ...]
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import java.io.*;

public class ReadData extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        try (PrintWriter out = new PrintWriter(new FileWriter(args[0]))) {
            for (int i = 1; i < args.length; i++) {
                Address a = toAddr(Long.parseUnsignedLong(args[i].replace("0x", ""), 16));
                int u32 = getInt(a);
                long u64 = getLong(a);
                out.println(a + " float " + Float.intBitsToFloat(u32) + " double " + Double.longBitsToDouble(u64)
                    + " u32 0x" + Integer.toHexString(u32) + " u64 0x" + Long.toHexString(u64));
            }
        }
    }
}
