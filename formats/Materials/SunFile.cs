using System.Globalization;

namespace Foley.Formats.Materials;

/// <summary>
/// Archivo <c>scripts/&lt;nombre&gt;.sun</c> que cita el shader del cielo con <c>sunfile</c>:
/// pares <c>clave "valor"</c> con el sprite del sol o la luna, el destello y el deslumbramiento.
/// </summary>
public sealed class SunFile
{
    public IReadOnlyDictionary<string, string> Values { get; }

    private SunFile(Dictionary<string, string> values) => Values = values;

    public string? Get(string key) => Values.GetValueOrDefault(key.ToLowerInvariant());

    public float GetFloat(string key, float fallback) =>
        Get(key) is { } v && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;

    public static SunFile Parse(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0) line = line[..comment].Trim();
            if (line.Length == 0) continue;
            var space = line.IndexOfAny(new[] { ' ', '\t' });
            if (space < 0) continue;
            var key = line[..space].ToLowerInvariant();
            var value = line[(space + 1)..].Trim().Trim('"');
            values[key] = value;
        }
        return new SunFile(values);
    }
}
