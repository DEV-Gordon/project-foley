# Tasa de simulación (tick)

## Qué se usa hoy

| Ritmo | Valor | Dónde | Fuente | Confianza |
|---|---|---|---|---|
| Comando de usuario / pmove | 8 ms (125 Hz) | `Pmove.Move` (troceo fijo) | CLAUDE.md; equivale a `pmove_fixed 1` + `pmove_msec 8` de ioquake3 | **por confirmar** |
| Frame del mundo (GSC, IA, entidades) | 50 ms (20 Hz) | aún no implementado | CLAUDE.md; `sv_fps 20` de id Tech 3 | **por confirmar** |

## Por qué se fija el pmove

En id Tech 3, sin `pmove_fixed`, cada comando dura lo que dure el cuadro del cliente
(troceado a 66 ms como máximo en `Pmove`), así que el movimiento depende de los fps. Fijar
8 ms hace la simulación determinista. Si CoD1 de un jugador no usa `pmove_fixed`, la
fidelidad exacta contra una demo dependerá de los fps con que se grabó: la demo debe
grabarse a 125 fps (`com_maxfps 125`).

## Evidencia nueva

- `pmove_fixed` y `pmove_msec` existen en CoD1: Timedemo1.dm_2 (multijugador) los trae en la
  systeminfo con `pmove_fixed 0`, `pmove_msec 8`. El valor 8 coincide con el elegido.
- PmoveSingle (gamex86.dll `0x20007fd0`) limita cada paso a 1..200 ms, como Quake 3.

## Pendiente

- REA: buscar en `CoDSP.exe` / `gamex86.dll` si existe `pmove_fixed`/`pmove_msec` y el valor
  de `sv_fps` en un jugador.
- Confirmar con la demo grabada.
