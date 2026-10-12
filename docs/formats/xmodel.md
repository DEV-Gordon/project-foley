# xmodel (modelos de CoD1, versión 14)

Un modelo son tres archivos dentro de los pk3, todos little-endian:

- `xmodel/<nombre>`: cabecera, LODs, colisión y materiales.
- `xmodelparts/<lod 0>`: huesos. El cargador (`0x4802f0`) solo lee el del primer LOD y todos los
  LODs lo comparten (muchos LODs no tienen `xmodelparts` propio).
- `xmodelsurfs/<lod>`: mallas (uno por LOD).

Las texturas de los materiales están en `skins/<material>` (el nombre ya trae la extensión
`.dds`, `.tga` o `.jpg`). El prefijo antes de `@` es el tipo de superficie (`wood@`, `metal@`);
`_masked` indica alfa recortado (`foliage_masked@`).

**Alfa invertido en el follaje.** En las skins cuyo tipo empieza por `foliage`
(`foliage_masked@`, `foliage_detail@`) la forma (hojas, ramas, hierba) tiene **alfa 0** y el
fondo **alfa 255**, relleno con un color plano. El resto de skins `_masked` (`metal_masked@`,
`wood_masked@`, `objective_incomplete_masked@`...) y las texturas del BSP usan la convención
normal (forma con alfa 255). Evidencia: inspección visual del color y del alfa de las 14 skins
de follaje y las 14 `_masked` de otros tipos que usa burnville, más un recuento automático de
las 175 skins de follaje de la instalación (fondo plano con alfa alto en casi todas). El visor
invierte el alfa de esas skins al cargarlas y recorta al 50 %. Confianza: **probable**; falta
localizar en el renderer del ejecutable dónde se aplica (REA).

Fuentes: lectura de los 1019 xmodels de la instalación (todos cuadran byte a byte con esta
descripción) y, para las mallas, el cargador del ejecutable (CoDSP 1.5 en memoria:
`0x47f8a0` carga el archivo, `0x47f190` lee una superficie, `0x47f000` lee un hueso + posición,
`0x47f090` convierte tiras en triángulos). Nombres de mensaje: "xmodelsurfs '%s' out of date
(version %d, expecting %d)", con 14 como versión esperada.

## xmodel/

| Campo | Tipo | Notas |
|---|---|---|
| versión | u16 | 14 |
| mins, maxs | 6 × f32 | límites (en los modelos con huesos es un cubo simétrico) |
| LOD × 3 | f32 distancia + cadena C | nombre vacío = LOD sin usar; la distancia del último es 0 |
| LOD de colisión | i32 | −1 = sin colisión |
| nº de superficies de colisión | u32 | |
| superficie de colisión | u32 nº de triángulos, triángulos × 48 B, mins/maxs (6 × f32), i32 hueso, u32 contents, u32 surfaceFlags | |
| materiales, por cada LOD con nombre | u16 cantidad + cadenas C | uno por superficie del LOD, en orden |

Triángulo de colisión (48 B): plano (normal + distancia) y dos planos de borde (`svec`+`sdist`,
`tvec`+`tdist`), 12 × f32. Sin uso todavía: la colisión de los `misc_model` ya viene horneada en los
brushes del BSP (ver `docs/re/modelos.md`).

## xmodelparts/

| Campo | Tipo |
|---|---|
| versión | u16 (14) |
| nº de huesos relativos | u16 |
| nº de huesos absolutos | u16 (siempre 1 en la instalación: `tag_origin` o sin nombre) |
| hueso relativo × n | i8 padre (índice absoluto), 3 × f32 desplazamiento, 3 × i16 cuaternión (x, y, z) |
| por cada hueso (absolutos + relativos) | cadena C nombre + 6 × f32 (límites del hueso) |
| por cada hueso | u8 (clasificación de parte, para daño; sin interpretar) |

El cuaternión se guarda como `x, y, z / 32767` y `w = sqrt(1 − x² − y² − z²)`. La pose de
reposo se obtiene componiendo desde el padre: `pos = pos_padre + rot_padre · desplazamiento`,
`rot = rot_padre · rot_local`. Confianza: **probable** (con esa composición los personajes quedan de
pie con 0–63 unidades de alto y simétricos; la alternativa sin rotar el desplazamiento los deforma).

## xmodelsurfs/

| Campo | Tipo |
|---|---|
| versión | u16 (14) |
| nº de superficies | u16 |

Superficie:

| Campo | Tipo | Notas |
|---|---|---|
| modo | u8 | sin interpretar (0, 1, 2) |
| nº de vértices | u16 | |
| nº de triángulos | u16 | sin contar los degenerados |
| nº de tiras | u16 | |
| hueso | i16 | −1 = vértices con pesos |
| si hueso = −1: nº de influencias extra | u16 | total de la superficie |
| si hueso = −1: (sin interpretar) | u16 | el cargador lo resta al reservar memoria |
| tiras × nº de tiras | u8 n + n × u16 índices | |
| vértices | ver abajo | |
| si hueso = −1: influencias extra | (u16 hueso, 3 × f32 posición, f32 peso) × total | en orden de vértice |

Vértice rígido (32 B): normal (3 × f32), uv (2 × f32), posición (3 × f32) en el espacio del hueso
de la superficie.

Vértice con pesos: normal (3 × f32), uv (2 × f32), i16 nº de influencias extra, u16 hueso,
posición (3 × f32) en el espacio de ese hueso y, solo si hay influencias extra, f32 peso del
primer hueso.

Tiras → triángulos (`0x47f090`): el primero es `(i0, i1, i2)`; luego, para k = 3, 5, 7…,
`(i[k−1], i[k−2], i[k])` y, si existe `i[k+1]`, `(i[k−1], i[k], i[k+1])`. Se descartan los
triángulos con índices repetidos (las tiras empiezan repitiendo el primer índice para fijar el giro).
El orden resultante es el mismo que el de los triángulos del BSP.
