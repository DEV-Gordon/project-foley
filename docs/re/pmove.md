# pmove (movimiento del jugador)

## Qué hace

Un paso de pmove recibe el estado del jugador y un comando de usuario (ángulos, avance,
lateral, salto) y produce el nuevo estado: ángulos de vista, búsqueda de suelo, fricción,
aceleración, gravedad, salto, deslizamiento contra la colisión con escalones y aterrizaje.

## Fuente

Análisis con REA (Ghidra) de **gamex86.dll 1.5** (modo un jugador; hash en `version.md`),
que está en claro. La estructura general es la de id Tech 3 (ioquake3 `bg_pmove.c`,
`bg_slidemove.c`); las funciones y constantes son las de CoD1. Port en
`sim/Movement/Pmove.cs`. El usuario indica que el pmove de multijugador es el mismo; la
demo de multijugador (`docs/formats/demo.md`) lo corrobora en lo comprobado.

| Función (CoD1) | Dirección | Diferencias principales con Quake 3 |
|---|---|---|
| PmoveSingle | `0x20007fd0` | Sin `SnapVector`; al final, si el desplazamiento real² / dt² < ¼ · velocidad², la velocidad pasa a ser desplazamiento / dt |
| PM_UpdateViewAngles | `0x20006fd0` | Igual (pitch ±16000); conversión con `0.005493164f` (float) |
| PM_GroundTrace | `0x20004f30` | Traza de +0.25 a −0.25 respecto al origen; si empieza en sólido, reintenta desde −0.001; pone `fJumpPeak = 0` al tocar suelo (antes de PM_CrashLand) |
| PM_CorrectAllSolid | `0x20004dc0` | 26 desplazamientos fijos (tabla `0x2008b3a0`); **mueve** al jugador al primero libre y lo apoya 1 unidad abajo |
| PM_CrashLand | `0x200049b0` | Ver "Aterrizaje" |
| PM_WalkMove | `0x20003f60` | Aceleración según postura; conserva la rapidez tras recortar solo si no se invirtió la dirección; al saltar guarda `jumpTime` |
| PM_AirMove | `0x20003df0` | Escala de comando propia; aceleración 1 |
| PM_Friction | `0x200034c0` | Ver tabla de constantes |
| PM_Accelerate | `0x200035e0` | Control = max(wishspeed, 100); en el suelo se divide por `ps.friction` |
| PM_CmdScale (suelo) | `0x20003760` | Escalas atrás/lateral/andar/correr/inclinación/postura/agua/arma; un botón ×0.4 |
| PM_CmdScale (aire) | `0x200036a0` | Como Q3 × andar/correr |
| PM_CheckJump | `0x20003be0` | Velocidad por altura de salto; enfriamiento de 500 ms; solo de pie |
| PM_SlideMove | `0x20008730` | 8 planos (Q3: 5); "mismo plano" > 0.999 (Q3: 0.99) |
| PM_StepSlideMove | `0x20008ff0` | Reescrito; ver "Escalones" |
| PM_ClipVelocity | `0x20003410` | Igual; producto escalar en orden x, z, y |

## Constantes

| Constante | Valor | Evidencia | Confianza |
|---|---|---|---|
| `ps.speed` | 190 | Timedemo1.dm_2; gamex86.dll `0x95cd0` | confirmado |
| `ps.gravity` | 800 | Timedemo1.dm_2; `0x95cc0` | confirmado |
| `ps.friction` | 1 | Timedemo1.dm_2 | confirmado |
| Escalas (`walk`, `run`, `strafe`, `back`, `lean`, `crouch`, `prone`) | 0.4, 1, 0.8, 0.7, 0.4, 0.65, 0.15 | Timedemo1.dm_2 | confirmado |
| Aceleración de pie / agachado / tumbado | 9 / 12 / 19 | `0x2008b37c` / `0x2008b378` / `0x2008b374` | confirmado (binario) |
| Aceleración resbaladiza, empujado y aire | 1 | `0x2008b380` | confirmado (binario) |
| Aceleración con aturdimiento de aterrizaje | ×0.25 | `0x2009a310` | confirmado (binario) |
| Fricción | 5.5 | `0x2008b38c` | confirmado (binario) |
| Velocidad mínima de control | 100 | `0x2008b35c` | confirmado (binario) |
| Fricción con aturdimiento / tras saltar | ×0.3 / ×2 | `0x2009a2e8` / código | confirmado (binario) |
| Altura de salto | 39; v = sqrt(gravity · 78) = 249.8 | `0x2009a440`, `0x2009a444` | confirmado (binario) |
| Enfriamiento entre saltos | 500 ms | PM_CheckJump | confirmado (binario) |
| Escalón de pie / tumbado | 18 / 10 | `0x2009a30c` / `0x2009a308` | confirmado (binario) |
| Pendiente caminable | normal.z ≥ 0.7 | `0x2009a1c4` | confirmado (binario) |
| `OVERCLIP` | 1.001; en Walk/AirMove se multiplica por 0.99900097 en vez de dividir | `0x2009a3ac`, `0x2009a550` | confirmado (binario) |
| Caja de pie | (−15,−15,0)–(15,15,70), origen en los pies | Timedemo1.dm_2 | confirmado |
| Vista de pie / agachado / tumbado | 60 / 40 / 11 | Timedemo1.dm_2 | confirmado |
| `bg_fallDamageMinHeight` / `MaxHeight` | 256 / 480 | Timedemo1.dm_2 (systeminfo) y cadenas de gamex86.dll | confirmado (MP), probable (SP) |
| `moveSpeedScale` del arma | del archivo del arma | Timedemo1.dm_2: Luger 1.2 → 228 u/s | confirmado |
| Paso de tiempo | 8 ms | `pmove_msec 8` en Timedemo1.dm_2; CLAUDE.md | por confirmar en un jugador |

## Aterrizaje (PM_CrashLand)

La altura de caída se calcula de forma analítica con la posición y velocidad vertical del
inicio del paso (resolviendo la parábola hasta la altura actual): `h = v_aterrizaje² / 2g`.

- `h ≤ 4`: nada. `4 < h < 12`: solo eventos de sonido. `h ≥ 12` sin daño: velocidad ×0.67.
- `h > bg_fallDamageMinHeight` (y la superficie sin la bandera `0x1`): daño
  `(int)((h − min) / (max − min) · 100)` (100 si `h ≥ max`; ×0.5 con agua a nivel 2). Con
  daño < 100 y suelo no resbaladizo: aturdimiento `daño·35 + 500` ms (máx. 2000) en
  `pm_time`, bandera `0x100` y velocidad × (0.5 si ≤ 500 ms, decreciendo linealmente hasta
  0.2). El daño lo aplica el juego; el pmove solo lo deja en `ps.LastFallDamage`.
- La rama "aterriza de un salto" (`pm_time 200`, bandera `0x2000`) mira `fJumpPeak`, pero
  PM_GroundTrace lo pone a 0 antes de llamar a PM_CrashLand, así que en la práctica no se
  ejecuta por esta vía. El port reproduce ese orden.

## Escalones (PM_StepSlideMove)

1. Desliza normalmente. Si no chocó o no hay salto en curso y está en el aire, termina.
2. Durante un salto, la subida máxima es la que falta hasta la cima del salto (`fJumpPeak`).
3. Si chocó: traza hacia arriba `escalón + 1`; la subida útil es `(escalón+1)·fracción − 1`
   (si es < 1 no sube) y repite el deslizamiento desde arriba con la velocidad inicial.
4. Baja lo subido y, si estaba en el suelo, medio escalón más para pegarse al suelo.
5. Si con el escalón no avanzó más (en la dirección de la velocidad) que sin él, lo descarta
   y solo hace la bajada de medio escalón.
6. Si subió durante un salto, limita la velocidad vertical a la necesaria para llegar a la cima.
7. Si la altura final difiere más de 0.5 de la tras el deslizamiento, multiplica la velocidad
   por `(1 − |Δz desde el inicio| / escalón) · 0.8 + 0.2`.

## Banderas `pm_flags` (modo un jugador)

Valores del binario; los nombres son deducidos del uso: `0x1` tumbado, `0x2` agachado,
`0x8` salto mantenido, `0x10` escalera, `0x80` velocidad de andar, `0x100` aturdimiento de
aterrizaje, `0x200` empujado, `0x800` sin saltar, `0x2000` tras salto. Al vencer `pm_time`
se limpian `0x100 | 0x200 | 0x1000 | 0x10000`.

## Validación con la demo

En Timedemo1.dm_2 la velocidad horizontal estable con la Luger es 228 (190 × 1.2) y con el
MP44 agachado andando 47 (190 · 0.96 · 0.65 · 0.4) y de lado 38 (× 0.8): coincide con
PM_CmdScale y con que la velocidad estable sea la deseada (fricción 5.5 frente a
aceleración 9 · max(v, 100)).

## Pendientes (comprobado al investigar "resbala y frena en pendientes suaves")

- En pendientes limpias (brush o terreno, 3°–30°) el jugador no despega ni se mueve estando
  quieto, y al caminar mantiene 190 u/s a lo largo de la pendiente (190·cos θ en horizontal),
  como Quake 3. Test: `TerrainTests.SobreUnaPendienteLimpiaElJugadorNoDespegaNiResbala`.
- Al aterrizar desde una caída, si la caja queda dentro de la ventana de 0.25 del trazado de
  suelo sin haber chocado en el SlideMove, la velocidad vertical no se recorta. En el paso
  siguiente PM_WalkMove (`0x20003f60`) recorta contra el suelo y, si la dirección no se invierte,
  conserva la rapidez: en pendientes de más de ~1.8° esa caída se convierte en un deslizamiento
  breve (21 unidades al caer 40 sobre 3°). PM_GroundTrace (`0x20004f30`) y PM_CrashLand
  (`0x200049b0`) no tocan la velocidad antes de eso, así que el original se comporta igual.
  Confianza: probable (leído en el binario, sin comparar contra una demo).
- La causa real del problema eran las aristas del terreno sin enlazar (ver `colision.md`).

## Aritmética

El binario usa la FPU x87 con precisión extendida (80 bits) y redondea a float solo al
guardar en memoria. El port evalúa cada expresión en `double` y redondea a `float` en los
mismos puntos de guardado; es la aproximación más cercana disponible en .NET y puede diferir
en el último bit en casos raros. También se respeta el orden de las sumas de los productos
escalares del original (p. ej. x·nx + z·nz + y·ny en PM_ClipVelocity).

## No portado todavía

Agua (PM_SetWaterLevel `0x200051c0`), postura y caja agachado/tumbado (`0x20005a80`),
transiciones de postura en PM_CmdScale (`0x200053c0`), inclinación, escaleras
(`0x200079e0`, `0x20007bf0`), tumbado (`0x200076b0`), botón ×0.4 de PM_CmdScale, eventos,
y entidades sólidas en el trazado.
