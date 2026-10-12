# BSP (IBSP v59)

Especificación tal como se entiende hoy. Todo lo de aquí se dedujo de los datos de
`maps/burnville.bsp` y se validó con comprobaciones cruzadas; no hay referencia directa
de ioquake3 para v59 (ioquake3 cubre v46). Confianza por sección entre paréntesis.

## Cabecera y directorio (confirmado)

- `char[4] "IBSP"`, `int32 version = 59`, todo little endian.
- A partir del byte 8, **33 entradas** de 8 bytes: `uint32 longitud, uint32 offset`
  (orden inverso al de Quake 3). Los lumps van contiguos y en orden de índice.
- Lumps de longitud 0 son válidos (p. ej. 5, 12-15 en burnville).

## Lumps usados por el importador

| Lump | Contenido | Registro | Confianza |
|---:|---|---|---|
| 0 | Materiales | 72 B | confirmado |
| 6 | Superficies (triangle soups) | 16 B | confirmado |
| 7 | Vértices | 44 B | confirmado |
| 8 | Índices | `uint16` | confirmado |
| 29 | Entidades | texto | confirmado |

El resto de lumps (1 lightbytes ≈ 5 lightmaps de 512×512×3, 2-3 light grid, 4, 9-11, 16-28,
30-32) está sin identificar o sin necesidad todavía. Colisión (planos, brushes) es del hito 3.

### Lump 0, material (72 B)

| Offset | Tipo | Campo |
|---:|---|---|
| 0 | `char[64]` | nombre terminado en 0 |
| 64 | `uint32` | flags de superficie (significado sin confirmar) |
| 68 | `uint32` | flags de contenido (significado sin confirmar) |

El nombre es la ruta de textura sin extensión (`textures/...`) o el nombre de un shader.

### Lump 6, superficie (16 B)

| Offset | Tipo | Campo |
|---:|---|---|
| 0 | `uint16` | índice de material |
| 2 | `uint16` | índice de lightmap (`65535` = sin lightmap) |
| 4 | `uint32` | primer vértice (en el lump 7) |
| 8 | `uint16` | cantidad de vértices |
| 10 | `uint16` | cantidad de índices |
| 12 | `uint32` | primer índice (en el lump 8) |

Validación en burnville: 2424 superficies; la suma de vértices es 82 665 (= tamaño del
lump 7 / 44) y la de índices 176 601 (= tamaño del lump 8 / 2), ambas exactas, y ningún
rango se sale.

### Lump 7, vértice (44 B)

| Offset | Tipo | Campo |
|---:|---|---|
| 0 | `float[3]` | posición |
| 12 | `float[2]` | UV de la textura (puede salirse de 0..1) |
| 20 | `float[2]` | UV de lightmap |
| 28 | `float[3]` | normal |
| 40 | `uint8[4]` | color RGBA |

Validación: las normales tienen longitud 1 en las muestras revisadas y la posición cae en
un bbox coherente (x −3832..7560, y −23964..−9600, z −144..672 en burnville).

### Lump 8, índices

`uint16`, **relativos al primer vértice de su superficie** (no globales). Cada 3 forman un
triángulo. En burnville ningún índice alcanza la cantidad de vértices de su superficie.

### Sentido de giro (confirmado)

Los 58 867 triángulos de burnville cumplen `cross(v1-v0, v2-v0) · normal < 0`: el giro de
la cara frontal es **horario** visto desde delante en un sistema diestro Z arriba. Hay que
invertirlo al pasarlo a motores con cara frontal antihoraria (Godot).

### Lump 29, entidades

Texto Quake 3: bloques `{ "clave" "valor" ... }`, terminado en 0.

## Sistema de coordenadas (observado)

Z arriba, unidades de Quake (pulgadas), diestro. La conversión a Godot (Y arriba, metros)
es solo de presentación y vive en `godot/bridge`; la simulación conserva las unidades
originales.

## Importación a Godot (presentación)

- Ejes: `(x, y, z) -> (x, z, -y)`, rotación propia; el giro horario del original queda como
  cara frontal de Godot sin invertir índices. Comprobado visualmente en burnville.
- `v` de la UV se usa tal cual (sin invertir): las texturas salen en su orientación correcta.
- Escala: 0.03 m por unidad, valor provisional solo visual (`TODO(fidelidad)`).
- No se dibujan superficies de shaders con `sky` ni `nodraw` (burnville: 4 y 1 superficies).
- Sin vertex color ni lightmap: el modo moderno descarta los lightmaps (hito 8).

## Pendiente

- Significado de flags de superficie/contenido (hace falta para decidir qué no se dibuja:
  sky, nodraw, clip). De momento el importador decide por nombre y por `.shader`.
- Contrastar con los otros 48 BSP.
- Lightmaps (lump 1): formato y empaquetado.
- Terreno/patches: no se ha visto ninguno distinto de los triangle soups.
