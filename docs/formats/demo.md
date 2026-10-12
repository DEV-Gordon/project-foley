# Demo (`.dm_2`)

Especificación tal como se entiende hoy, deducida de `Main/demos/Timedemo1.dm_2` (incluida en
la instalación) y contrastada con ioquake3. Código: `formats/Net/`.

**Grabación:** el comando `record` solo funciona en multijugador (`CoDMP.exe`); en un jugador
no existe o no funciona (comprobado por el usuario).

## Contenedor (confirmado)

Igual que Quake 3 (ioquake3 `cl_main.c`, `CL_ReadDemoMessage`):

```
repetir: int32 secuencia | int32 longitud | longitud bytes de mensaje
fin:     int32 secuencia | int32 -1
```

Little endian. En `Timedemo1.dm_2`: 1534 mensajes, secuencias consecutivas 26080..27613, sin
bytes sobrantes tras el fin.

## Mensaje de servidor (confirmado)

1. 4 bytes sin comprimir: `reliableAcknowledge` (int32). **Difiere de ioquake3**, que lo comprime.
2. El resto se **descomprime entero** con el Huffman de id Tech 3 (tabla `msg_hData` de ioquake3)
   y se lee sobre los bytes resultantes (`formats/Net/CodMessage.cs`).
3. Lectura (CoDMP.exe 1.5, ver `docs/re/red.md`): los bytes y enteros se leen en orden; los bits
   salen de un byte "reserva" que se toma del flujo cuando el cursor de bits está alineado, LSB
   primero. Bytes y reservas quedan intercalados. En ioquake3, en cambio, todo es un único flujo
   de bits comprimido símbolo a símbolo.

Comandos (`svc_*`, mismos valores que ioquake3): `2` gamestate, `3` configstring, `4` baseline,
`5` serverCommand (`long` secuencia + string), `7` snapshot, `8` EOF.

### `svc_gamestate` (mensaje 0)

`long serverCommandSequence` (520) y luego `byte 3, short índice, string` por cada cadena de
configuración: **259** en la demo. Le sigue `svc_baseline` (aún sin leer).

| Cadena | Contenido |
|---|---|
| 0 (serverinfo) | `mapname mp_dawnville`, `g_gametype tdm`, `protocol 2`, `shortversion 1.2` |
| 1 (systeminfo) | `pmove_fixed 0`, `pmove_msec 8`, `g_synchronousClients 0`, `bg_fallDamageMaxHeight 480`, `bg_fallDamageMinHeight 256`, `timescale 1` |

`pmove_fixed` existe en CoD1; esta demo se grabó **sin** paso fijo.

### `svc_snapshot`

| Campo | Tipo |
|---|---|
| `serverTime` | long |
| `deltaNum` | byte (0 = sin delta; si no, delta respecto al mensaje `secuencia - deltaNum`) |
| `snapFlags` | byte |
| estado del jugador | delta (ver abajo) |
| entidades, clientes | sin leer todavía |

**No hay máscara de áreas** (Quake 3 la envía aquí). Fuente: CL_ParseSnapshot de CoDMP.exe 1.5
(`0x4173e0`).

### Estado del jugador en delta

`byte lc`; para cada uno de los `lc` primeros campos de `playerStateFields`
(`docs/re/red.md`): un bit "cambiado" y, si cambió:

- `bits == 0` (float): un bit; si es 1, float completo (4 bytes); si es 0, entero de 13 bits:
  5 bits de la reserva + 1 byte, valor = `bajos + (byte − 128) · 32`.
- `bits != 0` (entero, se usa `|bits|`): `|bits| & 7` bits de la reserva y luego bytes enteros,
  LSB primero. Sin extensión de signo.

Los campos desde `lc` se copian del estado base. Siguen bloques de estadísticas, munición,
cargadores, objetivos y HUD (cada uno con su bit de presencia), que aún no se leen.

### Resultado en `Timedemo1.dm_2`

- 1534 mensajes: 1533 instantáneas con el estado del jugador decodificado, sin desbordes.
  Todas las posiciones con el jugador vivo caen dentro del BSP de `mp_dawnville`.
- Las tablas son de la 1.5 y la demo de la 1.2: el estado del jugador decodifica bien con ellas.
- Valores que el servidor envía (constantes en toda la demo con el jugador vivo):

| Campo | Valor |
|---|---|
| `speed` | 190 |
| `gravity` | 800 |
| `mins` / `maxs` | (−15,−15,0) / (15,15,70): **origen en los pies** |
| `standViewHeight` / `crouchViewHeight` / `proneViewHeight` | 60 / 40 / 11 |
| `walkSpeedScale`, `runSpeedScale`, `proneSpeedScale`, `crouchSpeedScale`, `strafeSpeedScale`, `backSpeedScale`, `leanSpeedScale` | 0.4, 1, 0.15, 0.65, 0.8, 0.7, 0.4 |
| `friction` | 1 (multiplicador; su uso, por confirmar) |

`maxs.z` sigue en 70 agachado (la vista baja a 40): la caja agachada se ajusta en otro sitio.

## Nota sobre la validación del pmove

Una demo graba lo que el servidor envía (instantáneas a 20 Hz), **no las teclas** del jugador.
La comparación con el pmove se hará por tramos: en el aire (las teclas apenas influyen) y en
el suelo deduciendo las teclas (combinaciones discretas que reproduzcan la posición
siguiente). Además, esta demo se grabó con `pmove_fixed 0`, así que la duración de cada
comando del cliente no es fija.
