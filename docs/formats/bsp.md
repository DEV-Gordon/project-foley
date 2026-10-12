# BSP (IBSP v59)

Especificación tal como se entiende hoy. Se dedujo de los datos de `maps/burnville.bsp`
y luego se contrastó con los 49 BSP de la instalación (ver "Corpus"); no hay referencia
directa de ioquake3 para v59 (ioquake3 cubre v46). Confianza por sección entre paréntesis.

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

Lumps de colisión (hito 3):

| Lump | Contenido | Registro | Confianza |
|---:|---|---|---|
| 2 | Planos | 16 B | confirmado |
| 3 | Lados de brush | 8 B | confirmado |
| 4 | Brushes | 4 B | confirmado |
| 24 | Piezas de colisión (terreno/curva) | 16 B | confirmado (REA: CMod_LoadLeafpatchesAndTerrain) |
| 25 | Vértices de colisión | `float[3]` | confirmado |
| 26 | Índices de colisión | `uint16` | confirmado |
| 27 | Modelos (mundo + inline `*N`) | 48 B | confirmado (campos de brushes); resto probable |

El resto de lumps (1 lightbytes ≈ 5 lightmaps de 512×512×3, 9-11, 16-26, 28, 30-32) está
sin identificar o sin necesidad todavía. El árbol BSP (nodos, hojas) no se usa: el trazado
recorre los brushes con una estructura de aceleración propia (ver "Colisión").

### Lump 0, material (72 B)

| Offset | Tipo | Campo |
|---:|---|---|
| 0 | `char[64]` | nombre terminado en 0 |
| 64 | `uint32` | flags de superficie (significado sin confirmar) |
| 68 | `uint32` | flags de contenido (significado sin confirmar) |

El nombre es la ruta de textura sin extensión (`textures/...`) o el nombre de un shader.

Flags de contenido (offset 68). Los nombres de bit son **probables**: se dedujeron de qué
materiales los llevan en burnville y coinciden con la nomenclatura de juegos posteriores del
mismo motor. Falta confirmarlos con REA.

| Bit | Nombre probable | Evidencia en burnville |
|---|---|---|
| `0x00000001` | sólido | texturas normales, `common/caulk`, cielo |
| `0x00000002` | follaje | `common/clipfoliage` |
| `0x00000800` | cielo | `sky/burnville` (`0x801`) |
| `0x00001000` | no bloquea la vista de la IA | `nosight_noclip`, `clip_nosight*` |
| `0x00010000` | clip de jugador | `common/clipplayer` (`0x28010000`), `clip`, `clipfull` |
| `0x00020000` | clip de IA | `common/clipmonster` (`0x28020000`), `clip`, `clipfull` |
| `0x08000000` | detalle | la mayoría de texturas de detalle |
| `0x20000000` | translúcido | todos los `common/clip*` |

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

### Lump 2, plano (16 B)

`float[3]` normal (unitaria en los 17 914 planos de burnville) + `float` distancia.

### Lump 4, brush (4 B)

| Offset | Tipo | Campo |
|---:|---|---|
| 0 | `uint16` | cantidad de lados |
| 2 | `uint16` | índice de material (lump 0); sus flags de contenido son los del brush |

Los lados de cada brush van **consecutivos** en el lump 3, en el mismo orden que los brushes
(burnville: la suma de lados de los 5381 brushes es 52 457 = tamaño del lump 3 / 8).
Todo brush tiene al menos 6 lados.

### Lump 3, lado de brush (8 B)

| Offset | Tipo | Campo |
|---:|---|---|
| 0 | `float` **o** `uint32` | ver abajo |
| 4 | `uint32` | índice de material del lado |

- Los **6 primeros lados** de cada brush son la caja axial y el campo 0 es una **distancia
  `float`**, en el orden `minX, maxX, minY, maxY, minZ, maxZ` (en los 5381 brushes min ≤ max).
- Del 7.º lado en adelante el campo 0 es un **índice de plano** del lump 2 (los 20 171 de
  burnville están en rango).

### Lump 24, pieza de colisión (16 B)

| Offset | Tipo | Terreno (tipo 1) | Curva (tipo 0) |
|---:|---|---|---|
| 0 | `uint16` | material | material |
| 2 | `uint8` | tipo = 1 | tipo = 0 |
| 4 | `uint16` | nº de vértices | ancho de la rejilla |
| 6 | `uint16` | nº de índices | alto de la rejilla |
| 8 | `uint32` | primer vértice (lump 25) | umbral de subdivisión en unidades (2–12 en burnville; @0x4131f0) |
| 12 | `uint32` | primer índice (lump 26) | primer vértice (lump 25) |

Los índices del terreno son relativos a su primer vértice, de 3 en 3. Burnville: 1112 de
terreno y 512 curvas. Validado en los 49 BSP (rangos dentro de los lumps 25 y 26).
Los puntos de control de una curva van por filas: `punto[j·ancho + i]`, `ancho` y `alto` impares.

### Lump 27, modelo (48 B)

| Offset | Tipo | Campo |
|---:|---|---|
| 0 | `float[3]` | mínimos |
| 12 | `float[3]` | máximos |
| 24 | `uint32` | primera superficie (lump 6) — probable |
| 28 | `uint32` | cantidad de superficies — probable |
| 32 | `uint32` | primer elemento de un lump sin identificar |
| 36 | `uint32` | cantidad de ese elemento |
| 40 | `uint32` | primer brush (lump 4) |
| 44 | `uint32` | cantidad de brushes |

El modelo 0 es el mundo (burnville: brushes 0..4945). Los siguientes son los modelos inline
`*N` de las entidades (`script_brushmodel`, triggers): 427 en burnville, uno por cada valor
distinto de `"model" "*N"`. La suma de brushes de todos los modelos es exactamente el total.

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

## Corpus (confirmado con tests)

`formats.tests/Bsp/BspAllMapsCorpusTests.cs` lee los 49 BSP de la instalación, de uno en uno.

- Los 49 son `IBSP` v59 y se leen sin error con el layout de arriba.
- 43 son mapas jugables (campaña y multijugador). En todos, las superficies cubren
  exactamente vértices e índices, ningún índice se sale de su superficie y hay `worldspawn`.
- 6 son **marcadores** de 4248 bytes (`allied_start`, `ru_stalingrad`, `uk_6ab`, `uk_sas`,
  `us_intro`, `us_mid`): una sola caja cerrada de `textures/common/blackish`
  (416×256×192 unidades, 24 vértices, 36 índices) con `worldspawn` e `info_player_start`.
  Se excluyen de las comprobaciones de mapa jugable; el test verifica que sigan siendo esa caja.
  Su uso en el original (¿salas de transición entre misiones?) está sin determinar.
- Materiales: todos los usados resuelven a textura, cielo o `nodraw`, salvo 4 que no existen
  en ningún pk3 (ver `shader.md`).
- Comprobado visualmente además de burnville: `brecourt`, `stalingrad`, `mp/mp_carentan`.

## Pendiente

- Significado de flags de superficie/contenido (hace falta para decidir qué no se dibuja:
  sky, nodraw, clip). De momento el importador decide por nombre y por `.shader`.
- Lightmaps (lump 1): formato y empaquetado. Diferido al modo clásico (hito 8).
- Terreno/patches: no se ha visto ninguno distinto de los triangle soups.
- Lumps de colisión (planos, brushes, nodos): hito 3.
