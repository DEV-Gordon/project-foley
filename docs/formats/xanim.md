# xanim (animaciones, versión 14)

Archivos `xanim/<nombre>` de los pk3. Formato deducido del cargador de `CoDSP.exe` 1.5
(`0x478280`) y **validado** con `tools/re/xanim_probe.py`: las 2957 animaciones de la instalación
se leen hasta el último byte. Little-endian.

## Cabecera

| Campo | Tipo | Notas |
|---|---|---|
| versión | u16 | 14 |
| nº de cuadros | u16 | hasta 3650 en la instalación |
| nº de huesos | u16 | |
| banderas | u8 | bit 0: en bucle; bit 1: tiene *delta* (movimiento de raíz) |
| cuadros por segundo | u16 | 30 en casi todas; también 15, 24, 10, 20, 60 y 1 |

- Con bucle, el cargador suma un cuadro más (el último repite el primero):
  `cuadros = nº + bucle`.
- Los índices de cuadro de las pistas ocupan **1 byte si `cuadros ≤ 256`** y 2 si no.
- El cargador guarda la frecuencia `fps / (cuadros − 1)`: ciclos por segundo.
- Banderas en la instalación: 0 (862 archivos), 1 (48), 2 (1490) y 3 (557).

## Pista de claves (formato común)

| Campo | Notas |
|---|---|
| u16 n | número de claves |
| n = 0 | pista vacía (valor de reposo) |
| n = 1 | un solo valor, sin índices |
| 1 < n < cuadros | n índices de cuadro (u8 o u16, ver arriba) y después n valores |
| n ≥ cuadros | n valores, uno por cuadro, sin índices |

## Delta (movimiento de raíz), solo si la bandera bit 1 está activa

1. Pista de **giro**: cada valor es un i16 `z` de un cuaternión de giro solo en el eje vertical;
   `w = sqrt(32767² − z²)` (constante `0x3fff0001`). El cargador invierte el signo de un
   cuaternión si el producto escalar con el anterior es negativo (camino corto).
2. Pista de **traslación**: cada valor son 3 × f32.

Es el desplazamiento del personaje que aporta la animación: lo usan `getmovedelta`,
`getangledelta` y `animmode` de la IA. Es simulación.

## Huesos

1. Dos mapas de bits de `(huesos + 7) / 8` bytes cada uno:
   - el primero tiene un uso en el cargador sin identificar todavía;
   - el segundo marca los huesos con **giro simple** (solo eje vertical).
2. Nombres de los huesos: cadenas C.
3. Por hueso:
   - pista de **giro**: con giro simple, i16 `z`; si no, 3 × i16 `x, y, z`. `w` se calcula y la
     escala es 32767. El cargador también invierte signos para el camino corto;
   - pista de **traslación**: 3 × f32.

## Notetracks

| Campo | Tipo |
|---|---|
| nº de notetracks | u8 |
| por cada una | cadena C (texto) + u16 cuadro |

Ninguna apunta más allá del último cuadro. Las más frecuentes:

- de la IA y los scripts: `dialogue`, `fire`, `anim_gunhand = "left"`, `footstep`, `bodyfall`,
  `anim_pose`, `anim_aiming`, `finish`, `attach`/`detach`, `shoot`, `reload`, `melee`;
- de sonido: `sound (1) <alias>`.

Los scripts las reciben como `notify` con `waittillmatch` (fase 3 del frame, `entidades.md`).

## Muestreo y mezcla (delta; `CoDSP.exe`, confianza probable)

- **Muestreo de una animación** (`0x479cd0`), con el tiempo normalizado `t ∈ [0, 1]`:
  - cuadro `f = (cuadros − 1) · t`;
  - con `t = 1`, o con un solo cuadro, se toma la **última** clave;
  - si la pista tiene índices, se busca el tramo (`0x4790c0` con índices u8, `0x479140` con
    u16) y la fracción es `(f − k_i) / (k_{i+1} − k_i)`; si no, `f` se separa en entero y
    fracción;
  - **interpolación lineal** componente a componente sobre los valores cuantizados: `z, w` del
    giro como enteros de 16 bits y traslación en float;
  - pista vacía: giro identidad `(0, 32767)` y traslación 0.
- **Acumulación en el árbol** (`0x47c750`, recursiva por los hijos con peso ≠ 0):
  - cada hoja suma `peso · giro / 32767` y `peso · traslación`, y acumula el peso
    (`0x47a270`, constante `1/32767`);
  - en cada nodo con varios hijos, el giro 2D se **normaliza** a longitud `peso` (es decir,
    *nlerp*) y la traslación se divide por la suma de pesos;
  - sin hijos activos, delta cero.
- El delta del movimiento de raíz es el de "tiempo anterior → tiempo actual"; lo piden las
  llamadas `0x6f`/`0x72` del juego (`0x47d090`/`0x47d1e0`).

## Pendiente

- Muestreo de los huesos (no solo el delta) para la pose de las cajas de impacto; por analogía
  con el delta, lineal y normalizado.
- Pesos y tiempos del árbol: `setanimknob`, `setflaggedanim`, transiciones (funciones
  `0x47cb30`–`0x47e5d0`).
- Uso del primer mapa de bits.
- Cuándo se dispara cada notetrack al avanzar el tiempo (¿al cruzar el cuadro, incluido el
  último?).
