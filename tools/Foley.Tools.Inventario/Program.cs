using System.Globalization;
using System.Text;
using Foley.Formats.Pk3;

// Uso: Foley.Tools.Inventario [ruta-juego] [salida.md]
// La ruta del juego sale del primer argumento o de la variable COD1_PATH.
// Acepta la raíz de la instalación (con subcarpeta Main) o la propia carpeta de pk3.

var gameRoot = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("COD1_PATH");
if (string.IsNullOrWhiteSpace(gameRoot))
{
    Console.Error.WriteLine("Falta la ruta del juego: pasa un argumento o define COD1_PATH.");
    return 2;
}

var pk3Dir = Pk3Locator.FindPk3Directory(gameRoot);
if (pk3Dir is null)
{
    Console.Error.WriteLine($"No se encontraron archivos .pk3 en '{gameRoot}' ni en '{Path.Combine(gameRoot, "Main")}'.");
    return 2;
}

using var pk3s = new Pk3Collection(pk3Dir);
var inv = Pk3Inventory.Build(pk3s.Archives);

var md = Render(inv, pk3s);
if (args.Length > 1)
{
    var outPath = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    File.WriteAllText(outPath, md, new UTF8Encoding(false));
    Console.WriteLine($"Inventario escrito en {outPath}");
}
else
{
    Console.Write(md);
}
return 0;

static string Mb(long bytes) => (bytes / 1048576.0).ToString("N2", CultureInfo.InvariantCulture);

static string Render(Pk3Inventory inv, Pk3Collection pk3s)
{
    var sb = new StringBuilder();
    sb.AppendLine("# Inventario de pk3");
    sb.AppendLine();
    sb.AppendLine($"- Generado: {DateTime.Now:yyyy-MM-dd HH:mm}");
    sb.AppendLine($"- Archivos pk3: {inv.ByPk3.Count}");
    sb.AppendLine($"- Entradas: {inv.Total.Count}");
    sb.AppendLine($"- Rutas lógicas únicas (tras precedencia): {pk3s.AllPaths.Count()}");
    sb.AppendLine($"- Tamaño descomprimido: {Mb(inv.Total.Size)} MB (comprimido: {Mb(inv.Total.CompressedSize)} MB)");
    sb.AppendLine();
    sb.AppendLine("## Por pk3");
    sb.AppendLine();
    sb.AppendLine("| pk3 | archivos | tamaño MB | comprimido MB |");
    sb.AppendLine("|---|---:|---:|---:|");
    foreach (var (name, t) in inv.ByPk3)
        sb.AppendLine($"| {name} | {t.Count} | {Mb(t.Size)} | {Mb(t.CompressedSize)} |");
    sb.AppendLine();
    sb.AppendLine("## Por tipo de archivo");
    sb.AppendLine();
    sb.AppendLine("| tipo | archivos | tamaño MB |");
    sb.AppendLine("|---|---:|---:|");
    foreach (var (type, t) in inv.ByType.OrderByDescending(kv => kv.Value.Size))
        sb.AppendLine($"| {type} | {t.Count} | {Mb(t.Size)} |");
    sb.AppendLine();
    sb.AppendLine("## Por pk3 y tipo");
    foreach (var (name, types) in inv.ByPk3AndType)
    {
        sb.AppendLine();
        sb.AppendLine($"### {name}");
        sb.AppendLine();
        sb.AppendLine("| tipo | archivos | tamaño MB |");
        sb.AppendLine("|---|---:|---:|");
        foreach (var (type, t) in types.OrderByDescending(kv => kv.Value.Size))
            sb.AppendLine($"| {type} | {t.Count} | {Mb(t.Size)} |");
    }
    return sb.ToString();
}
