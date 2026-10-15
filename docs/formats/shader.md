# Shaders (`scripts/*.shader`) y plantillas de tipo (`shadertypes/*.stype`)

Texto con bloques `nombre { directivas  { etapa } }`. Comentarios `//` y `/* */`.
Lector: `formats/Materials/ShaderFile.cs`; resolución: `formats/Materials/MaterialResolver.cs`.
La instalación trae 437 shaders en `scripts/*.shader` y 130 plantillas en `shadertypes/`
(72 `world`, 53 `model`, 5 `2d`).

## Resolución de un material

1. **Shader propio**: un bloque de `scripts/*.shader` con el mismo nombre (sin extensión).
2. **Plantilla de tipo**: el tipo es lo que va antes de `@` en el último componente del nombre
   (`textures/normandy/walls/wood_masked@fence1` → `wood_masked`; `skins/foliage_masked@tree1.tga`
   → `foliage_masked`). Se usa `shadertypes/world/<tipo>.stype` para el BSP y
   `shadertypes/model/<tipo>.stype` para los modelos, sustituyendo `$texturename` por el propio
   material.
3. **Por defecto**: una etapa con la textura del mismo nombre.

Texturas: `<ruta>.dds`, `.tga` o `.jpg` (supuesto sin verificar: preferencia en ese orden).

Evidencia del paso 2: el ejecutable contiene el mensaje "$texturename used in shader '%s', which
is not a shader type file", y en burnville **todos** los materiales con `@` tienen plantilla
(los 16 sin `@` tienen shader propio). Ejemplo decisivo: `shadertypes/model/foliage_masked.stype`
usa `alphaFunc LT128`, que explica el alfa invertido de las skins de follaje
(`docs/formats/xmodel.md`). Confianza: **probable** (falta ver el código de carga con REA).

## Directivas leídas

Nivel de shader: `surfaceparm`, `qer_editorimage`, `skyParms <caja> <altura nubes> -`,
`sunfile`, `cull` (none/disable/twosided, back), `polygonOffset`, `nomipmaps`, `nopicmip`,
`radialNormals`, `sort`, `deformVertexes`.

Etapa (`{ ... }`): `map`/`clampmap`, `animMap <fps> <fotogramas...>`, `nextbundle` (segunda
textura de la misma etapa), `blendFunc`, `alphaFunc` (GT0, LT128, GE128), `rgbGen`, `alphaGen`,
`tcMod` (scroll, scale, rotate, turb, stretch, transform), `tcGen`, `depthWrite`, `depthFunc`.

Modificadores de `map` antes de la ruta (cadenas del ejecutable): `clamp`, `clampx`/`clamps`,
`clampy`/`clampt`, `nomipmaps`, `mipmaps`, `picmip`, `nopicmip`, `noopt`, `only16bit`,
`only32bit`, `heightToNormal`. La ruta es el último argumento.

Un `blendFunc` escrito **después de `nextbundle`** no es la mezcla de la etapa: dice cómo se
combina ese bundle con el anterior dentro de la etapa (en los datos, siempre el lightmap con
`filter`). Ejemplo: `terrain.shader` `a_grass1b` lleva `blendFunc blend` en la etapa (la hierba se
funde sobre la tierra con el alfa de vértice) y `blendFunc filter` en el bundle del lightmap. Si se
aplicara a la etapa, la hierba saldría como una cinta oscura multiplicada.

`blendFunc` abreviados: `blend` = GL_SRC_ALPHA GL_ONE_MINUS_SRC_ALPHA, `add` = GL_ONE GL_ONE,
`filter` = GL_DST_COLOR GL_ZERO (como Quake 3). Aparece una vez `blendFunc GE128` (error en los
datos): se ignora.

No se interpretan: `requires`, `cubemap`, `waterMap`, los programas de vértice y fragmento
(`nvRegCombiners`, `atiFragmentShader`...), `q3map_*`, `tessSize`, `entityMergable`, `fogvars`.

## Traducción a Godot (`godot/bridge/StageMaterials.cs`)

- Las etapas y bundles de `$lightmap` se descartan: la luz es de Godot (CLAUDE.md, Iluminación).
- Primera etapa dibujable = material; las demás se encadenan como `next_pass`.
- Mezclas: `blend` → mezcla alfa; `add` y `GL_SRC_ALPHA GL_ONE` → aditiva (sin luz);
  `filter` → multiplicativa (sin luz); `GL_ONE GL_ONE_MINUS_SRC_ALPHA` → premultiplicada; el resto,
  a la más parecida.
- `alphaFunc` → `discard`. `depthWrite` en etapas mezcladas → escriben profundidad.
- `rgbGen vertex`/`exactVertex` → multiplica por el color de vértice del BSP (pintura del artista,
  no luz horneada: casi siempre 255). `alphaGen vertex` → alfa de vértice (mezcla del terreno y de
  calcos). `const` y `wave` se evalúan con el tiempo.
- `tcMod` en orden, como `RB_CalcTurbulentTexCoords` y compañía de Quake 3. `animMap` cambia de
  fotograma con el tiempo (hasta 8).
- `cull`: por defecto se descarta la cara trasera; `none` → doble cara.
- `polygonOffset` → la superficie se acerca a la cámara un 0.05 % para no parpadear.
- `radialNormals` (follaje de modelos): normales desde el origen del modelo.
- Sin brillo especular: las superficies de id Tech 3 solo reciben luz difusa (con el especular por
  defecto de Godot el follaje con normales radiales salía casi blanco bajo la luna).
- `deformVertexes autosprite2` (cables de burnville): como Autosprite2Deform de Quake 3, cada
  grupo de 4 vértices gira sobre su eje largo para mirar a la cámara; la malla lleva en CUSTOM0 el
  punto medio y la media anchura de cada lado corto y en CUSTOM1 el eje largo.
- `deformVertexes autosprite` (brillo de los farolillos, `skins/light_sprite.tga`): como
  AutospriteDeform y RB_AddQuadStamp de Quake 3, cada grupo de 4 vértices se sustituye por un
  cuadrado que mira a la cámara, de radio |v0 − centro| · 0.707, con UV (0,0) (1,0) (1,1) (0,1).
- `sort` → prioridad de dibujado de las superficies mezcladas, con los valores de Quake 3
  (`shaderSort_t`) y, para las palabras propias de CoD1, un orden por su significado: `innerblend`
  antes que `outerblend` (ventanillas de los coches), ambas antes que `additive`. TODO(presentación):
  valores exactos de CoD1 sin verificar.
- Pendiente: `tcGen` (environment, vector), el resto de `deformVertexes` (wave, flap, bulge, move) y
  `depthFunc`. Ninguno aparece en burnville.

## Mips hechos a mano (`#0`, `#1`, `#2`)

56 texturas de la instalación se llaman `nombre#0` y traen sus niveles de mip siguientes como
`nombre#1`, `nombre#2`... a la mitad de tamaño cada uno (p. ej. los cables `metal_masked@wire_single`:
256×32, 128×16, 64×8). Los niveles a mano mantienen el cable más grueso que los automáticos, para que
el recorte por alfa no lo borre de lejos. El visor usa esos niveles y genera solo los más pequeños
que falten. Confianza: probable (evidencia en los datos; falta ver el cargador de imágenes con REA).

## Cielo

`skyParms env/burnville4c 512 -`: 6 imágenes `env/burnville4c_{rt,lf,bk,ft,up,dn}.tga`. Asignación
de caras de id Tech 3 (tr_sky.c, `suf[]`, `sky_texorder[]` y `st_to_vec[]`): +X `rt`, −X `lf`,
+Y `bk`, −Y `ft`, +Z `up`, −Z `dn`. El visor dibuja la caja en el cielo de Godot
(`godot/bridge/MapViewer.Sky.cs`).

`sunfile burnville` → `scripts/burnville.sun` (pares `clave "valor"`): `r_sunsprite_shader
gfx/sun/moon_full` (mezcla aditiva) con `r_sunsprite_size 32`, más destello y deslumbramiento. El
visor dibuja el sprite en la dirección `sundirection` del worldspawn. TODO(presentación): el
tamaño se interpreta como píxeles de una pantalla de 640 con 80° de campo (0.125° por píxel), sin
verificar. Destello y deslumbramiento, pendientes.

## Materiales sin datos

Materiales referenciados por los BSP que **no existen en ningún pk3** (ni textura ni shader);
faltan en los datos del juego, no es un fallo del lector. El visor los pinta en magenta:

| Material | Mapas |
|---|---|
| `textures/normandy/windows/glass@window2a` | `mp/mp_depot` |
| `textures/industrial/glass@factorywindow4a_broken` | `stalingrad` |
| `textures/industrial/glass@factorywindow4z_broken` | `stalingrad` |
| `textures/industrial/glass@factorywindow4o_broken` | `stalingrad` |

## Coronas y destellos (`godot/bridge/Flares.cs`)

- **Entidad `corona`**: el renderer de CoDSP.exe lee, junto al mensaje "corona scale must be > 0" y
  "MAX_MAP_CORONAS(%i) exceeded", las claves `scale`, `zcutoff`, `zfadeout` y el color (`_color`,
  `dl_color`), y dibuja con el shader `flareShader` (`gfx/misc/corona_temp.tga`, mezcla aditiva).
  Tamaño e intensidad como `RB_RenderFlare` de Quake 3 (`r_flareSize 40`, `r_flareCoeff 150`) y
  desvanecido como `r_flareFade 7`; se oculta si la colisión del mapa tapa la línea de visión (Quake 3
  lee el buffer de profundidad). TODO(presentación): `zcutoff`/`zfadeout` se interpretan como la
  componente vertical de la dirección del halo al jugador (con −0.15/−0.25, la farola brilla vista
  desde abajo y se apaga al verla desde arriba); valores de CoD1 sin verificar.
- **Sol o luna** (`scripts/<sunfile>.sun`): destello `r_sunflare_shader` cuyo tamaño pasa de
  `r_sunflare_min_size` a `max_size` (píxeles de una pantalla de 640) entre `min_angle` y `max_angle`
  grados respecto a la mirada; brillo de pantalla `r_sunglare_*` (`max_lighten`) y oscurecimiento
  `r_sunblind_*` (`max_darken`). Solo si el rayo hacia el astro llega al cielo (`SURF_SKY` 0x4).
  TODO(presentación): `fadein`/`fadeout` interpretados como segundos.
- El ambiente del worldspawn mayor que 1 se divide por 255: el renderer avisa "ambient too big,
  assuming it uses the old 0-255 scale instead of the proper 0-1 scale".
