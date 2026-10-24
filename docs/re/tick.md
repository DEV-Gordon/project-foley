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

## Pendiente

- Confirmar con la demo grabada.
- Si el cliente de un jugador limita `com_maxfps` (valor por defecto y tope) y si `cl_maxpackets`
  agrupa comandos.
