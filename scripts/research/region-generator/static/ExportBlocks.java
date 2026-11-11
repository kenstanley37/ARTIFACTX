// Writes every initialized memory block of the open program to <outdir>/<name>_<start>.bin, plus blocks.txt, and the
// start of every function to functions.txt. usage (headless postScript): ExportBlocks.java <outdir>
import ghidra.app.script.GhidraScript;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.listing.Function;
import java.io.*;

public class ExportBlocks extends GhidraScript {
    @Override
    public void run() throws Exception {
        File out = new File(getScriptArgs()[0]);
        out.mkdirs();
        try (PrintWriter idx = new PrintWriter(new File(out, "blocks.txt"))) {
            for (MemoryBlock b : currentProgram.getMemory().getBlocks()) {
                if (!b.isInitialized()) continue;
                String name = b.getName().replaceAll("[^A-Za-z0-9_]", "_") + "_" + b.getStart().toString() + ".bin";
                byte[] buf = new byte[(int) b.getSize()];
                b.getBytes(b.getStart(), buf);
                try (FileOutputStream f = new FileOutputStream(new File(out, name))) { f.write(buf); }
                idx.println(b.getStart().toString() + " " + b.getSize() + " " + name);
            }
        }
        try (PrintWriter fn = new PrintWriter(new File(out, "functions.txt"))) {
            for (Function f : currentProgram.getFunctionManager().getFunctions(true))
                fn.println(f.getEntryPoint().toString() + " " + f.getBody().getNumAddresses());
        }
        println("exported " + currentProgram.getName() + " " + currentProgram.getExecutablePath());
    }
}
