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
