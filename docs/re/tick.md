# Tasa de simulación (tick)

## Qué se usa hoy

| Ritmo | Valor | Dónde | Fuente | Confianza |
|---|---|---|---|---|
| Comando de usuario / pmove | 8 ms (125 Hz) | `Pmove.Move` (troceo fijo) | CLAUDE.md; equivale a `pmove_fixed 1` + `pmove_msec 8` | **elección del proyecto** (el original no es fijo, ver abajo) |
| Frame del mundo (GSC, IA, entidades) | 50 ms (20 Hz) | aún no implementado | `CoDSP.exe` y `gamex86.dll` 1.5 | **confirmado** (binario) |

## Frame del mundo: 50 ms (confirmado)

- `CoDSP.exe` (`0x0044cf10`) acumula el tiempo real de cada cuadro. Mientras el acumulado sea
  ≥ 50 (`0x32`), resta 50 y ejecuta un frame del juego: mensaje 10 de `vmMain` (inicio) y luego
  mensaje 11 (`G_RunFrame`). Con menos de 50 no ejecuta ninguno.
- `gamex86.dll`, mensaje 10 (`0x2001f090`): `level.time += 50` (constante `0x32`).
- En un jugador **no existe `sv_fps`**: la cadena no está en el ejecutable y el 50 está fijo en
  el código. `sv_framerate_smoothing` solo reparte el trabajo de un frame entre varios cuadros de
  render; no cambia la duración del frame.
- Orden de lo que pasa dentro del frame: `entidades.md`.

## Movimiento del jugador: un pmove por comando de usuario

- El servidor (`0x00449050`) recibe del cliente un paquete con hasta 32 comandos de usuario
  ("cmdCount > MAX_PACKET_USERCMDS"). Por **cada** comando nuevo llama a `ClientThink` del juego
  (mensaje 6 de `vmMain`, `0x200129c0`), que ejecuta el pmove (`0x20012430` → `0x20008580` →
  `PmoveSingle` `0x20007fd0`).
- Así que el pmove va al ritmo de los comandos del cliente, es decir, de sus cuadros, como en
  Quake 3. No va al ritmo del frame del mundo.
- `pmove_fixed` (por defecto **0**) y `pmove_msec` (por defecto 8) existen en `gamex86.dll` de un
  jugador (tabla de dvars, ver `dvars.md`). Con `pmove_fixed 0`, cada comando dura lo que dure el
  cuadro del cliente (`PmoveSingle` lo limita a 1..200 ms). El movimiento original depende, por
  tanto, de los fps (`com_maxfps`).
- El proyecto fija 8 ms (equivalente a `pmove_fixed 1`, `pmove_msec 8`), lo mismo que jugar el
  original a 125 fps. Es una decisión de determinismo, no un valor del original. Las demos de
  validación deben grabarse a 125 fps.

## Cuadro del cliente (`Com_Frame`, `0x430690`)

- `com_maxfps` se registra con valor por defecto **0** (`0x42fde0`), es decir, sin límite. El
  `configure.csv` del juego (ajustes por hardware) no lo toca.
- Duración mínima de un cuadro: `1000 / com_maxfps` ms en **división entera**, o 1 ms si es 0.
  El bucle espera activamente hasta cumplirla. Con `com_maxfps 125` el cuadro es exactamente de
  8 ms, que es el paso fijo del proyecto.
- Después, el tiempo pasa por `timescale` y `fixedtime` (`0x430620`) y llega al servidor
  (`0x44cf10`, frames de 50 ms) y al cliente.

## Pendiente

- Confirmar con la demo grabada.
- Si `cl_maxpackets` agrupa comandos en un jugador.
