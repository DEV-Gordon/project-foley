using Foley.Formats.Bsp;
using Foley.Formats.Materials;
using Foley.Formats.Pk3;

namespace Foley.Formats.Tests.Materials;

/// <summary>Etapas de shader, plantillas .stype y resolución de materiales (docs/formats/shader.md).</summary>
public class ShaderStageTests
{
    private const string Sample = @"
textures/test/water
{
	cull none
	polygonOffset
	deformVertexes autosprite2
	{
		map clampy textures/test/a.tga
		blendFunc GL_SRC_ALPHA GL_ONE
		rgbGen wave sin 0.5 0.25 0 2
		alphaGen const .25
		tcMod scroll 0.1 -0.2
		tcMod turb 0 0.05 0 0.3
		tcMod rotate 30
	}
	{
		animMap 4 textures/test/f1 textures/test/f2 textures/test/f3
		blendFunc add
		alphaFunc LT128
		depthWrite
	nextbundle
		map $lightmap
	}
	/* comentario
	   de bloque */
	{
		map $lightmap
		blendFunc filter
	}
}
";

    private const string Type = @"{
	surfaceparm wood
	{
		map $texturename
		rgbGen exactVertex
		alphaFunc GE128
	nextbundle
		map $lightmap
	}
}";

    [Fact]
    public void LeeEtapasMezclasGeneradoresYModificadores()
    {
        var s = ShaderFile.Parse(Sample).Single();
        Assert.Equal(ShaderCull.None, s.Cull);
        Assert.True(s.PolygonOffset);
        Assert.Equal(new[] { "autosprite2" }, s.Deforms.Single());
        Assert.Equal(3, s.Stages.Count);

        var a = s.Stages[0];
        Assert.Equal("textures/test/a.tga", a.Main.Map);
        Assert.Equal(TextureClamp.Y, a.Main.Clamp);
        Assert.Equal(BlendFactor.SrcAlpha, a.BlendSrc);
        Assert.Equal(BlendFactor.One, a.BlendDst);
        Assert.Equal(RgbGen.Wave, a.RgbGen);
        Assert.Equal(new ShaderWave(WaveFunc.Sin, 0.5f, 0.25f, 0, 2), a.RgbWave);
        Assert.Equal(AlphaGen.Const, a.AlphaGen);
        Assert.Equal(0.25f, a.AlphaConst);
        Assert.Equal(new[] { TcModKind.Scroll, TcModKind.Turb, TcModKind.Rotate }, a.Main.TcMods.Select(m => m.Kind));
        Assert.Equal(new[] { 0.1f, -0.2f }, a.Main.TcMods[0].Args);
        Assert.Equal(0.05f, a.Main.TcMods[1].Wave!.Value.Amplitude);

        var b = s.Stages[1];
        Assert.Equal(4f, b.Main.AnimFrequency);
        Assert.Equal(3, b.Main.AnimFrames.Count);
        Assert.Equal(BlendFactor.One, b.BlendSrc);
        Assert.Equal(BlendFactor.One, b.BlendDst);
        Assert.Equal(ShaderAlphaFunc.Lt128, b.AlphaFunc);
        Assert.True(b.DepthWrite);
        Assert.Equal(2, b.Bundles.Count);
        Assert.True(b.Bundles[1].IsLightmap);

        Assert.True(s.Stages[2].IsLightmapOnly);
        Assert.Equal(BlendFactor.DstColor, s.Stages[2].BlendSrc);
        Assert.Equal("textures/test/a.tga", s.FirstMap);
    }

    [Fact]
    public void LaPlantillaSustituyeTexturename()
    {
        var t = ShaderFile.ParseType("world/wood_masked", Type);
        var inst = t.Instantiate("textures/x/wood_masked@fence", "textures/x/wood_masked@fence");
        Assert.Equal("textures/x/wood_masked@fence", inst.Stages[0].Main.Map);
        Assert.Equal("$texturename", t.Stages[0].Main.Map); // la plantilla no cambia
        Assert.Equal(ShaderAlphaFunc.Ge128, inst.Stages[0].AlphaFunc);
        Assert.Contains("wood", inst.SurfaceParms);
    }

    [Theory]
    [InlineData("textures/normandy/walls/wood_masked@fence1", "wood_masked")]
    [InlineData("skins/foliage_masked@tree1.tga", "foliage_masked")]
    [InlineData("textures/common/caulk", null)]
    public void TipoDeSuperficieDelNombre(string name, string? type) =>
        Assert.Equal(type, MaterialResolver.SurfaceType(name));

    [CorpusFact]
    public void TodosLosShadersYPlantillasDeLaInstalacionSeLeen()
    {
        using var pk3 = new Pk3Collection(Pk3Locator.FindPk3Directory(Cod1Corpus.Root!)!);
        var shaders = 0;
        foreach (var path in pk3.AllPaths.Where(p => p.StartsWith("scripts/") && p.EndsWith(".shader")))
            shaders += ShaderFile.Parse(pk3.ReadAllText(path)).Count();
        Assert.Equal(437, shaders); // bloques de primer nivel en scripts/*.shader de la instalación
        var types = pk3.AllPaths.Where(p => p.StartsWith("shadertypes/") && p.EndsWith(".stype")).ToList();
        Assert.Equal(130, types.Count);
        foreach (var t in types)
            Assert.NotEmpty(ShaderFile.ParseType(t, pk3.ReadAllText(t)).Stages);
    }

    [CorpusFact]
    public void LosMaterialesDeBurnvilleUsanSuPlantillaDeTipo()
    {
        using var pk3 = new Pk3Collection(Pk3Locator.FindPk3Directory(Cod1Corpus.Root!)!);
        var bsp = BspFile.Read(pk3.ReadAllBytes("maps/burnville.bsp"));
        var res = new MaterialResolver(pk3);
        var used = bsp.Surfaces.Select(s => s.MaterialIndex).Distinct().Select(i => res.Resolve(bsp.Materials[i].Name)).ToList();

        // Todo material con '@' tiene plantilla; los demás tienen shader propio.
        Assert.All(used, r => Assert.NotEqual(MaterialSource.Default, r.Source));
        Assert.All(used.Where(r => !r.IsSky && !r.NoDraw), r => Assert.NotNull(r.TexturePath));

        var fence = used.First(r => r.ShaderType == "wood_masked");
        Assert.Equal(ShaderAlphaFunc.Ge128, fence.Shader.Stages[0].AlphaFunc);
        var decal = used.First(r => r.ShaderType == "decal");
        Assert.True(decal.Shader.PolygonOffset);
        Assert.Equal(BlendFactor.SrcAlpha, decal.Shader.Stages[0].BlendSrc);
        // a_grass1b: "blendFunc blend" en la etapa y "blendFunc filter" en el bundle del lightmap.
        var grass = used.First(r => r.Name.EndsWith("a_grass1b"));
        Assert.Equal(BlendFactor.SrcAlpha, grass.Shader.Stages[0].BlendSrc);
        Assert.Equal(BlendFactor.OneMinusSrcAlpha, grass.Shader.Stages[0].BlendDst);
        Assert.Equal(AlphaGen.Vertex, grass.Shader.Stages[0].AlphaGen);
        Assert.Equal(BlendFactor.DstColor, grass.Shader.Stages[0].Bundles[1].CombineSrc);
        var clampy = used.First(r => r.ShaderType == "foliage_clampy");
        Assert.Equal(TextureClamp.Y, clampy.Shader.Stages[0].Main.Clamp);

        // Follaje de modelos: alphaFunc LT128 (la forma tiene alfa 0, ver docs/formats/xmodel.md).
        var tree = res.Resolve("skins/foliage_masked@tree1_detail1.tga", MaterialKind.Model);
        Assert.Equal(MaterialSource.ShaderType, tree.Source);
        Assert.Equal(ShaderAlphaFunc.Lt128, tree.Shader.Stages[0].AlphaFunc);
        Assert.Equal("skins/foliage_masked@tree1_detail1.tga", tree.TexturePath);
    }
}

public class SunFileTests
{
    [Fact]
    public void LeeParesClaveValor()
    {
        var s = SunFile.Parse("r_sunsprite_shader \"gfx/sun/moon_full\"\r\nr_sunsprite_size \"32\"\n// comentario\nr_sunflare_fadeout \".2\"\n");
        Assert.Equal("gfx/sun/moon_full", s.Get("r_sunsprite_shader"));
        Assert.Equal(32f, s.GetFloat("r_sunsprite_size", 0));
        Assert.Equal(0.2f, s.GetFloat("r_sunflare_fadeout", 0), 4);
        Assert.Equal(7f, s.GetFloat("no_existe", 7));
    }

    [CorpusFact]
    public void ElCieloDeBurnvilleApuntaASuCajaYSuLuna()
    {
        using var pk3 = new Pk3Collection(Pk3Locator.FindPk3Directory(Cod1Corpus.Root!)!);
        var res = new MaterialResolver(pk3);
        var sky = res.Resolve("textures/sky/burnville");
        Assert.True(sky.IsSky);
        Assert.Equal("env/burnville4c", sky.Shader.SkyEnv);
        foreach (var f in new[] { "rt", "lf", "bk", "ft", "up", "dn" })
            Assert.NotNull(res.FindTexture("env/burnville4c_" + f));
        var sun = SunFile.Parse(pk3.ReadAllText($"scripts/{sky.Shader.SunFile}.sun"));
        Assert.Equal("gfx/sun/moon_full", sun.Get("r_sunsprite_shader"));
        Assert.NotNull(res.FindShader("gfx/sun/moon_full"));
    }
}
