# Colisión (trazado de cajas contra brushes)

## Qué hace

La colisión del jugador sale **solo de los brushes del BSP** (CLAUDE.md). Un trazado mueve una
caja alineada a los ejes (`mins`/`maxs`) de `start` a `end` y devuelve la fracción recorrida
antes de tocar un brush sólido, el plano tocado, y si empezó o terminó dentro de un sólido.

## Fuente

Portado de ioquake3, `code/qcommon/cm_trace.c`:

| Función de origen | Port en `/sim` |
|---|---|
| `CM_Trace` (preparación: offset simétrico, `offsets[8]`, bounds, endpos) | `ClipMap.Trace` |
| `CM_TraceThroughBrush` (caja, sin cápsula) | `ClipMap.TraceThroughBrush` |
| `CM_TestBoxInBrush` (prueba de posición, `start == end`) | `ClipMap.TestBoxInBrush` |
| `SURFACE_CLIP_EPSILON = 0.125` (`cm_local.h`) | `ClipMap.SurfaceClipEpsilon` |

Contra brushes el jugador es una caja. El terreno y las curvas (lump 24) se tratan aparte; ver
"Terreno y curvas" más abajo.

## Datos del BSP (ver `docs/formats/bsp.md`)

- Brushes del **modelo 0** (mundo). Los modelos inline (`*N`: puertas, `script_brushmodel`,
  triggers) se moverán con su entidad y quedan para cuando haya entidades.
- Los 6 lados axiales se convierten en planos como hace Quake 3 en `CM_BoundBrush`/carga:
  `-X: (-1,0,0), -minX`, `+X: (1,0,0), maxX`, y lo mismo en Y y Z. Los bounds del brush son
  esos mínimos y máximos.
- Contenidos del brush = flags de contenido de su material (offset 68 del lump 0).

## Constantes

| Constante | Valor | Fuente | Confianza |
|---|---|---|---|
| `SURFACE_CLIP_EPSILON` | 0.125 | ioquake3 `cm_local.h` | Q3 confirmado; CoD1 **supuesto** |
| Máscara sólida del jugador | `0x1 \| 0x10000` (sólido \| clip de jugador) | nombres de bits deducidos (`bsp.md`) + `MASK_PLAYERSOLID` de Q3 sin `CONTENTS_BODY` | **supuesto**, `TODO(fidelidad)` |

## Diferencias deliberadas con el original

- **Sin árbol BSP.** Quake 3 recorre nodos y hojas (`CM_TraceThroughTree`); aquí se buscan
  los brushes candidatos con una rejilla propia y se prueban **en orden de índice**. El
  resultado de cada brush es idéntico; solo puede cambiar qué plano se reporta cuando dos
  brushes dan **exactamente** la misma fracción (Q3 se queda con el primero que encuentra en
  su recorrido). Confianza: supuesto aceptable; se revisará si la demo muestra diferencias.
- **Aritmética.** En C, `(d1 - SURFACE_CLIP_EPSILON) / (d1 - d2)` se evalúa en `double`
  porque la constante es `double`; el port reproduce esa promoción. El resto va en `float`.
  El binario de CoD1 (MSVC, x87) pudo usar precisión extendida: **por confirmar** con REA.

## Pendiente

- Confirmar con REA: máscara del jugador, epsilon y si CoD1 conserva `CM_TraceThroughBrush`.
- Modelos inline y entidades sólidas (`CONTENTS_BODY`).

## Terreno y curvas (CoD1, confirmado con REA sobre CoDSP.exe 1.5)

Además de los brushes, el BSP trae **piezas de colisión** (lumps 24–26, `docs/formats/bsp.md`)
de dos tipos. En burnville son 1112 de terreno y 512 curvas; sin ellas el 69 % del suelo
visible no tenía colisión (el jugador lo atravesaba).

**Carga** (`CMod_LoadLeafpatchesAndTerrain`, `0x411fd0`): tipo 1 → terreno
(`CM_GenerateTerrainCollide`, `0x418860`); tipo 0 → curva (`CM_GeneratePatchFacets`, como
el `cm_patch.c` de Quake 3). Contenido y banderas de superficie salen del material de la pieza.

**Terreno** (`TerrainCollide.cs`):
- Por triángulo: plano `(v2 − v0) × (v1 − v0)` normalizado; base dual para coordenadas
  baricéntricas (`k = 1/(1 − d²)`, dividido por la longitud de cada arista); punteros a sus
  vértices y aristas "colisionables".
- Aristas: todas las de la malla; una arista compartida solo se descarta si
  `n·(Q − P1) > 0` y `|n|² < max(|Q−a|², |Q−b|²) · 6.4e-9` (en la práctica, solo degeneradas).
  Vértices: los que pertenecen a alguna arista conservada.
- Si todas las normales miran abajo (z < −0.001 y ninguna > 0.001) la pieza "mira abajo".

**Trazado** (`0x41a350` → `0x41a2d0` → `0x4196f0`): con una caja, el motor avisa
"Box collision on terrain currently being faked with capsule collision" y usa una **cápsula**:
radio `size[1].x` (15), media altura `size[1].z` (35), como el `sphere` de Quake 3. Solo se
barre **una esfera** de la cápsula (la inferior; la superior si la pieza mira abajo), con radio
`+0.125`, contra caras (dentro del triángulo), aristas (cilindro) y vértices (esfera). Al tocar,
la fracción queda `1e-5` antes; si empieza dentro, fracción 0 y `startsolid` (no `allsolid`).
Cada vértice/arista se prueba una vez por trazado. Antes se filtran las piezas cuyos límites
no solapan los del barrido (`0x41b690`, sin margen).

Consecuencias observadas en el port: el jugador se apoya a 0.125 del suelo, igual que en
brushes; un trazado de posición (delta nulo) **no** detecta el terreno, como en el original.

Corrección (hito 3): el port enlazaba cada triángulo con sus aristas antes de crearlas, así que
ninguna arista se probaba. Cerca de cualquier cresta o valle entre triángulos el contacto cae
fuera de ambos triángulos y solo lo detecta la arista: la cápsula atravesaba el suelo o lo
perdía. Caminando por burnville el jugador estaba "en el aire" en el 26 % de los pasos (resbalaba
y frenaba en pendientes suaves); corregido, ~3 %, y lo que queda son caídas reales y empujones
contra paredes de más de 45°. Test: `TerrainTests.LaCapsulaChocaConLaAristaEntreDosTriangulos` y
`CaminandoPorElTerrenoDeBurnvilleCasiNuncaSePierdeElSuelo`.

**Curvas** (`PatchCollide.cs`): port de `cm_patch.c` y `cm_polylib.c` de ioquake3 comprobado
función a función contra CoDSP.exe 1.5:

| Paso | Dirección | Igual que ioquake3 salvo |
|---|---|---|
| CM_GeneratePatchCollide | `0x415170` | límites iniciales ±262144; se amplían ±1 al final (igual) |
| CM_SetGridWrapWidth / RemoveDegenerateColumns / TransposeGrid | `0x413070` / `0x4134d0` / `0x412f10` | — (ε = 0.1) |
| CM_SubdivideGridColumns | `0x4131f0` | umbral **de la pieza** (campo 8 del lump 24) en vez de 16, y si la columna no necesita subdivisión **se conserva** (Quake 3 la colapsa) |
| CM_PatchCollideFromGrid | `0x414950` | `CM_GridPlane` irresoluble devuelve −1 sin aviso |
| CM_FindPlane / FindPlane2 / PlaneEqual | `0x413830` / `0x413750` / `0x413600` | — (ε plano 0.1, normal 0.0001, distancia 0.02; 4096 planos, 1024 facetas) |
| CM_ValidateFacet / BaseWindingForPlane | `0x414050` / `0x416b00` | `MAX_MAP_BOUNDS` = 131072 (Quake 3: 65535) |
| CM_AddFacetBevels | `0x414240` | un bisel de arista solo se añade si algún punto de la faceta queda detrás a más de 0.1 |
| CM_TraceThroughPatchCollide | `0x415a40` | `leaveFrac` empieza en la fracción actual (no en 1); solo copia la normal del plano |
| CM_CheckFacetPlane | `0x415910` | — (ε 0.125) |

Contra curvas el jugador es una **caja** (rama sin cápsula, `offsets[signbits]`), no la cápsula
del terreno. El despachador de piezas (`0x41b860`) es el mismo para terreno y curvas: filtra por
límites (`0x41b690`) y, si baja la fracción, toma contenido y banderas de la pieza. Como el
terreno, un trazado de posición no detecta la curva.

En burnville: 512 curvas, 7026 facetas, como mucho 23 bordes por faceta; con terreno y curvas el
suelo visible sin colisión baja al 0.7 % (tablones decorativos sobre un suelo sólido 6 unidades
más bajo).

Diferencia deliberada: las piezas se recorren después de los brushes y en el orden del lump, no
hoja a hoja como el árbol BSP del original (mismo criterio que con los brushes).

### Trazados de punto (sin extensión)

Leídos en `CoDSP.exe` 1.5; confianza **probable** hasta portarlos y probarlos.

- **Terreno** (`0x419580`): recorre los triángulos de la pieza (registros de 72 bytes: plano del
  triángulo y dos planos baricéntricos).
  - Solo cuenta si el final queda detrás del plano y el inicio delante o encima.
  - Fracción `(d_ini − 0.125) / (d_ini − d_fin)`, con el mismo ε que Quake 3,
    `SURFACE_CLIP_EPSILON`.
  - El punto de corte (sin ε) debe tener las coordenadas baricéntricas `u, v ≥ −0.001`
    y `u + v ≤ 1.001` (constantes `0x55b5dc` y `0x55b718`).
  - Si la fracción baja, guarda la normal del triángulo.
- **Curvas** (`0x415400`): es `CM_TracePointThroughPatchCollide` de ioquake3:
  - solo se ejecuta si `cm_playerCurveClip` (por defecto 1, `0x201`) está activa y el trazado es
    un punto;
  - fracción con ε 0.125 y comprobación de los biseles de cada faceta como en Quake 3.
