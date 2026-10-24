# Entidades del juego y frame del mundo

Notas para los hitos 4–6: cómo nacen las entidades del BSP y en qué orden se actualiza el mundo
en cada frame. Fuente: `gamex86.dll` 1.5 (SP) y `CoDSP.exe` 1.5 (volcado), analizados con
Ghidra y `tools/re/`. Tablas completas en `gsc-api.md`.

## Ranuras de entidad

- Las entidades `0..3` son de clientes (el bucle de clientes del frame recorre 4). Las demás
  empiezan en 4. `0x7fe` = "ninguna" (`ENTITYNUM_NONE`) y `0x7ff` = mundo, como en Quake 3 con
  `MAX_GENTITIES` 2048. Tamaño de `gentity_t`: `0x2c8` bytes (array en `0x2012fa80`).
- Error "G_Spawn: no free entities" (`0x20045fa0`) si se agotan.

## Spawn desde el BSP (`G_CallSpawn`, `0x200412d0`)

Orden de decisión por `classname`:

1. `node_*` → nodo de caminos de la IA (no es entidad). Tipos (tabla `0x2008a470`, nombre →
   número): `node_pathnode` 1, `node_cover_stand` 2, `node_cover_crouch` 3, `node_cover_prone` 4,
   `node_cover_right` 5, `node_cover_left` 6, `node_cover_wide_right` 7, `node_cover_wide_left` 8,
   `node_concealment_stand` 9, `node_concealment_crouch` 10, `node_concealment_prone` 11,
   `node_stack` 12, `node_reacquire` 13, `node_balcony` 14, `node_scripted` 15,
   `node_negotiation_begin` 16, `node_negotiation_end` 17.
2. `info_vehicle_node` y `info_vehicle_node_rotate` → nodos de vehículo (no son entidades).
3. `actor_*` → entidad + *spawner* de actor (`0x2004c1e0`).
4. Clases de la lista de ítems (`bg_itemlist` en `0x200a7178`, registros de `0x30` bytes; las de
   armas se rellenan al cargar los archivos de arma) → ítem.
5. Tabla `{classname, spawn}` (`0x2008a500`, 31 clases, ver `gsc-api.md`).
6. Si no está en ninguna, la entidad se descarta.

Las claves del BSP se copian a la entidad con la tabla de campos de entidad (`0x2008a200`, 38
claves, ver `gsc-api.md`). Tipos del parser (`0x20040f70`): entero, short, byte, float, cadena de
script, vector "x y z" y modelo (`*N` = submodelo del BSP; si no, índice de modelo). `light` se
ignora. Tras copiarlas (`0x20041100`) se igualan `r.currentOrigin = s.origin` y
`r.currentAngles = s.angles` y se ponen a cero las trayectorias.

Pendiente: lista de claves `script_*` que no son campos de entidad (las lee el script con
`getent`/campos de struct) y la carga de `maps/<mapa>.gsc` (`main()` tras el spawn).

## Frame del juego

### Lo que hace el ejecutable (`CoDSP.exe`, `0x0044cf10`)

Acumula el tiempo real del cuadro en un residuo. Mientras el residuo sea ≥ 50 ms, resta 50 y
ejecuta un frame del mundo: primero el mensaje 10 de `vmMain` (inicio de frame) y luego el 11
(`G_RunFrame`). Si queda menos de 50 ms, no ejecuta ninguno. Confirma el frame fijo de **50 ms**
(ver `tick.md`).

Hay un segundo camino (`0x0044d200`, con `sv_framerate_smoothing`) que reparte un frame entre
varios cuadros de render con un presupuesto de ciclos de CPU: `G_RunFrame` es una máquina de
estados que puede devolver el control a mitad de frame y continuar después. Para la
reimplementación no importa: el resultado y el orden son los mismos ejecutando el frame entero
de una vez.

### Inicio de frame (`vmMain` 10, `0x2001f090`)

`level.framenum++`, `level.previousTime = level.time`, `level.time += 50`.

### `G_RunFrame` (`vmMain` 11, `0x2001f0c0`), por fases

| Fase | Qué hace | Confianza |
|---|---|---|
| 0 | Actualiza las dvars que cambiaron; avisa si el mapa no tiene caminos; limpia marcas por entidad | probable |
| 1 | Procesa los pares de contacto pendientes (entidad, otra): por cada uno, `notify` del motor a la entidad; luego ejecuta los hilos de script pendientes. Repite hasta que no quede ninguno | probable |
| 2 | Para cada entidad no cliente marcada (byte en `+0x112`): llamada al motor `0x7b` con 0.05 s. Por el valor, avance de la animación del modelo un frame | supuesto |
| 3 | Para cada actor (sin la bandera `0x10000`): llamada al motor `0x7a` (entidad, 0.05 s, 1) en bucle mientras devuelva distinto de 0, ejecutando los hilos de script entre llamadas. Por la forma, procesa las *notetracks* de animación de una en una | supuesto |
| 4 | **Avance del reloj de scripts** hasta `level.time`: despiertan los `wait` (ver `gsc.md`) | confirmado |
| 5 | Niebla y distancia de corte (`setcullfog`, `setexpfog`, `setculldist`) a los clientes; `0x20057ea0` | probable |
| 6 | Para cada entidad en uso, en orden de número: `G_RunEntity` (`0x2001ef10`); dos veces si `+0x294` no es 0 | probable |
| 7 | Para cada cliente: fin de frame (`0x2001a7f0`, `0x20012a60`). Después `0x2001e060`, copia de posiciones de actores, llamada al motor `0x92`, `0x20046a00`, `0x20070f40`, `0x20048200`, `0x2001eb50`, `g_listEntity`, `0x2001e940` | probable |

### Jugador

El movimiento del jugador no va en `G_RunFrame`: se procesa por comando de usuario, cuando el
ejecutable entrega los comandos al juego (pmove, ver `pmove.md` y `tick.md`).

## Pendiente

- Identificar las funciones de las fases 5–7 y la llamada `0x7a`/`0x7b` en el ejecutable.
- `G_RunEntity`: orden de movimiento de *movers* (`moveto`, `rotateto`), disparadores y
  pensamiento de actores.
- `trigger_*`: cuándo y cómo se comprueba el contacto (fase 1 lo consume).
