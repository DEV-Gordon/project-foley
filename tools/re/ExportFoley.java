// Exporta, para estudio local (nunca al repo): funciones decompiladas, cadenas con sus referencias
// y punteros a funciones en datos. Uso: analyzeHeadless ... -postScript ExportAll.java <dir_salida>
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.address.*;
import ghidra.program.model.symbol.*;
import ghidra.program.model.data.*;
import java.io.*;

public class ExportFoley extends GhidraScript {
    @Override
    public void run() throws Exception {
        String outDir = getScriptArgs()[0];
        new File(outDir).mkdirs();
        Listing listing = currentProgram.getListing();
        ReferenceManager refs = currentProgram.getReferenceManager();
        FunctionManager fm = currentProgram.getFunctionManager();

        // Cadenas con las funciones que las usan
        try (PrintWriter w = new PrintWriter(new FileWriter(outDir + "/strings.txt"))) {
            for (Data d : listing.getDefinedData(true)) {
                if (!d.hasStringValue()) continue;
                Object v = d.getValue();
                if (v == null) continue;
                StringBuilder sb = new StringBuilder();
                for (Reference r : refs.getReferencesTo(d.getAddress())) {
                    Function f = fm.getFunctionContaining(r.getFromAddress());
                    sb.append(' ').append(r.getFromAddress());
                    if (f != null) sb.append('(').append(f.getEntryPoint()).append(')');
                }
                w.println(d.getAddress() + "\t" + v.toString().replace("\n", "\\n") + "\t" + sb);
            }
        }

        // Lista de funciones
        try (PrintWriter w = new PrintWriter(new FileWriter(outDir + "/functions.txt"))) {
            for (Function f : fm.getFunctions(true))
                w.println(f.getEntryPoint() + "\t" + f.getName() + "\t" + f.getBody().getNumAddresses());
        }

        // Decompilación completa
        DecompInterface di = new DecompInterface();
        di.openProgram(currentProgram);
        try (PrintWriter w = new PrintWriter(new FileWriter(outDir + "/decomp.c"))) {
            int n = 0;
            for (Function f : fm.getFunctions(true)) {
                if (monitor.isCancelled()) break;
                DecompileResults res = di.decompileFunction(f, 60, monitor);
                w.println("// ==== " + f.getEntryPoint() + " " + f.getName());
                if (res != null && res.decompileCompleted()) w.println(res.getDecompiledFunction().getC());
                else w.println("// (fallo al decompilar)");
                if (++n % 200 == 0) println("decompiladas " + n);
            }
        }
        di.dispose();
        println("FIN export " + outDir);
    }
}
