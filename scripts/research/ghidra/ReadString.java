// Prints the NUL-terminated ASCII string at each given address (string constants the decompiler shows as DAT_).
// usage (headless postScript): ReadString.java <output.txt> 0xADDR [0xADDR ...]
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import java.io.*;

public class ReadString extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        try (PrintWriter out = new PrintWriter(new FileWriter(args[0]))) {
            for (int i = 1; i < args.length; i++) {
                Address a = toAddr(Long.parseUnsignedLong(args[i].replace("0x", ""), 16));
                StringBuilder sb = new StringBuilder();
                for (int k = 0; k < 256; k++) {
                    byte b = getByte(a.add(k));
                    if (b == 0) break;
                    sb.append((char) (b & 0xFF));
                }
                out.println(a + " \"" + sb + "\"");
            }
        }
    }
}
