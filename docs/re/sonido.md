# Sonido: alias y avisos de fin de sonido

Notas para el hito 4 (ambiente, música y diálogos que lanzan los scripts). Fuente: `CoDSP.exe`
1.5 (volcado), `gamex86.dll` y `cgamex86.dll` 1.5, analizados con Ghidra y `tools/re/`.

## Alias (`soundaliases/*.csv`)

- Son CSV del juego; las filas cuyo primer campo empieza por `#` se ignoran. La cabecera da los
  nombres de columna, así que el lector se guía por los datos. Columnas que reconoce el
  ejecutable (`0x56d9e0`…):
  - `name`, `sequence`, `file`, `subtitle`;
  - `vol_min`, `vol_max`, `pitch_min`, `pitch_max`, `dist_min`, `dist_max`;
  - `channel`, `type` (`streamed` / `loaded`), `probability`, `loop` (`looping` /
    `nonlooping`), `masterslave` (`master` / esclavo), `loadspec`.
- Canales con nombre (tabla `0x56da..`): entre otros `local`, `voice`, `music`, `announcer`,
  `shellshock`.
- Validaciones con aviso:
  - nombre o archivo vacío; alias duplicado; columna repetida;
  - `dist_min ≤ 0`, `vol_min < 0`, `pitch_min ≤ 0`;
  - subtítulos con caracteres no válidos o demasiado largos;
  - un alias de ambiente o música que no es `streamed` o que usa un canal 3D.
- Un alias puede tener varias filas: variantes elegidas al azar según `probability`
  (`sequence`), por confirmar.
- Los subtítulos se localizan con `soundaliases/subtitle.st`.

## `playsound` con aviso de fin (simulación)

Afecta a los tiempos de la misión: los scripts esperan a que acabe un diálogo con
`playsound(alias, "evento")` + `waittill("evento")`.

1. El built-in `playsound` (`gamex86.dll`, `0x20032a10`) comprueba que el alias exista
   ("unknown sound alias") y emite el evento de entidad `0xac` (sin aviso) o `0xad` (con aviso).
   Con aviso guarda en la entidad: cadena del evento, alias, `level.time` de inicio y duración
   −1 (`0x200466a0`). Si ya había otro `playsound` con aviso pendiente en la misma entidad, da
   error ("issued a second playsound with notification string before the first finished").
2. El cliente (`cgamex86.dll`, `0x300169e0`, evento `0xad`) reproduce el alias. El sistema de
   sonido del ejecutable devuelve su **duración en ms** (llamada `0xc0` del cgame) y el cliente
   la envía al servidor con el comando **`sl <entidad> <ms>`**.
3. El juego (`0x20016e50`) guarda esa duración si es mayor que la que tenía.
4. En el pensamiento de la entidad (`G_RunEntity`, `0x2001ef10`, fase 6 del frame), si hay aviso
   pendiente y `level.time − inicio ≥ duración`, hace `notify(evento)` y lo borra. **Si el cliente
   no ha informado la duración, se usan 5000 ms.**

Para la reimplementación (un jugador, sin red): duración = longitud del archivo elegido en ms.
`TODO(fidelidad)`: confirmar si el ejecutable la ajusta por `pitch` y cómo redondea (función de
la llamada `0xc0` del cgame). El aviso se comprueba en frames de 50 ms, así que el `notify` llega
en el primer frame en que se cumple la duración.

## Pendiente

- Cómo elige variante y volumen y tono aleatorios (¿`rand()` del cliente?). Es presentación
  salvo la duración.
- `playloopsound`, `stoploopsound`, `soundfade`, `ambientplay`, `musicplay`: solo presentación,
  salvo avisos.
