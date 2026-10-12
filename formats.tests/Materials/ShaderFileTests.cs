using Foley.Formats.Materials;

namespace Foley.Formats.Tests.Materials;

public class ShaderFileTests
{
    // Texto sintético con la misma sintaxis que los .shader del juego.
    private const string Sample = @"
// comentario
textures/test/lit
{
	qer_editorimage textures/test/editor.tga
	surfaceparm rock
	{
		map $lightmap
		blendFunc filter
	}
	{
		map textures/test/base
		rgbGen exactVertex
	}
}

textures/sky/test
{
	surfaceparm sky
	skyParms env/test 512 -
}

textures/common/hidden
{
	surfaceparm nodraw
}
";

    [Fact]
    public void LeeNombreParmsYPrimerMapQueNoEsLightmap()
    {
        var defs = ShaderFile.Parse(Sample).ToDictionary(d => d.Name);
        Assert.Equal(3, defs.Count);

        var lit = defs["textures/test/lit"];
        Assert.Equal("textures/test/base", lit.FirstMap);
        Assert.Equal("textures/test/editor.tga", lit.EditorImage);
        Assert.Contains("rock", lit.SurfaceParms);
    }

    [Fact]
    public void LeeCieloYNodraw()
    {
        var defs = ShaderFile.Parse(Sample).ToDictionary(d => d.Name);
        Assert.Contains("sky", defs["textures/sky/test"].SurfaceParms);
        Assert.Equal("env/test", defs["textures/sky/test"].SkyEnv);
        Assert.Contains("NODRAW", defs["textures/common/hidden"].SurfaceParms); // sin distinguir mayúsculas
    }
}
