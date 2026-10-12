namespace Foley.Formats.Pk3;

/// <summary>Cantidad y tamaño descomprimido/comprimido de un grupo de archivos.</summary>
public sealed class InventoryTotals
{
    public int Count { get; private set; }
    public long Size { get; private set; }
    public long CompressedSize { get; private set; }

    internal void Add(Pk3Entry e)
    {
        Count++;
        Size += e.Size;
        CompressedSize += e.CompressedSize;
    }
}

/// <summary>Inventario de contenido de un conjunto de pk3, por pk3 y por tipo (extensión).</summary>
public sealed class Pk3Inventory
{
    public const string NoExtension = "(sin extensión)";

    public SortedDictionary<string, InventoryTotals> ByPk3 { get; } = new(StringComparer.OrdinalIgnoreCase);
    public SortedDictionary<string, InventoryTotals> ByType { get; } = new(StringComparer.Ordinal);
    /// <summary>pk3 → tipo → totales.</summary>
    public SortedDictionary<string, SortedDictionary<string, InventoryTotals>> ByPk3AndType { get; }
        = new(StringComparer.OrdinalIgnoreCase);
    public InventoryTotals Total { get; } = new();

    public static string TypeOf(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        var dot = name.LastIndexOf('.');
        return dot < 0 ? NoExtension : name[dot..];
    }

    public static Pk3Inventory Build(IEnumerable<Pk3Archive> archives)
    {
        var inv = new Pk3Inventory();
        foreach (var a in archives)
        {
            var perType = new SortedDictionary<string, InventoryTotals>(StringComparer.Ordinal);
            inv.ByPk3AndType[a.Name] = perType;
            var pk3Totals = new InventoryTotals();
            inv.ByPk3[a.Name] = pk3Totals;
            foreach (var e in a.Entries)
            {
                var t = TypeOf(e.Path);
                Get(inv.ByType, t).Add(e);
                Get(perType, t).Add(e);
                pk3Totals.Add(e);
                inv.Total.Add(e);
            }
        }
        return inv;
    }

    private static InventoryTotals Get(IDictionary<string, InventoryTotals> d, string key)
    {
        if (!d.TryGetValue(key, out var t)) d[key] = t = new InventoryTotals();
        return t;
    }
}
