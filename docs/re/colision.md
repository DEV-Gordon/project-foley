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

Cápsulas y patches de Quake 3 no se portan: CoD1 no usa patches en los BSP revisados y el
modelo de jugador es una caja (confirmar con REA).

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
el `cm_patch.c` de Quake 3; **sin portar**). Contenido y banderas de superficie salen del
material de la pieza.

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

Pendiente: curvas (tipo 0) y trazados de punto contra terreno (`0x419580`).
