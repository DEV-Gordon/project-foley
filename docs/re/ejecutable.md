# Ejecutable (`CoDSP.exe` 1.5): servicios que da al juego

Notas para los hitos 4–7. El ejecutable de Steam está cifrado en disco; se analiza el volcado de
memoria `CoDSP_1.5_memoria.bin` (base `0x400000`) con Ghidra y `tools/re/`. Las salidas (decompilado,
lista completa de casos `game_syscalls.txt`) quedan en la carpeta de trabajo.

## Carga de las DLL

- `0x452190` carga `<nombre>x86.dll` y busca `dllEntry` y `vmMain`. A `dllEntry` le pasa la
  función de llamadas al sistema de esa DLL.
- `0x44f0e0` crea la "VM" (registro de DLL) con su despachador:
  - juego: **`0x44a2f0`** (`push 0x44a2f0` en `0x44c504`);
  - cgame: `0x401a50`;
  - ui: `0x410b00`.
- `0x44f270` llama a `vmMain` de una DLL.

## Llamadas del juego al ejecutable (`0x44a2f0`)

Hay 212 números (0–0xd3), con tabla de saltos en `0x44b310`; 0xa2–0xc7 no existen (error). Nombre
deducido del uso en `gamex86.dll` y de lo que hace el ejecutable. Confianza **probable** salvo lo
marcado.

| Nº | Función del exe | Qué es |
|---|---|---|
| 0x00, 0x01, 0x02 | `0x42e300`, `0x42e660` | imprimir, error, error de fin de partida |
| 0x05–0x0b | `0x4320a0`… | dvars: registrar, actualizar, `set`, `setValue`, valor entero, valor cadena… |
| 0x0c, 0x0d | `0x421170`… | `argc`, `argv` de comandos |
| 0x0e–0x11, 0x53–0x55 | | memoria (`Hunk_Alloc` y variantes, liberar) |
| 0x15–0x29 | `0x425fd0`, `0x423010`… | archivos: abrir (0x15), leer (0x16), escribir (0x17), cerrar (0x25), renombrar (0x24), listar (0x28) |
| 0x2a | `0x449990` | **`locateGameData`** (entidades, nº, tamaños 0x2c8 y 0x74e8) — confirmado por los argumentos |
| 0x2c | `0x449430` | enviar comando al cliente (`cs`, `scr_fade`, `snd_fade`…) |
| 0x2d–0x2f | `0x44b900`… | *configstrings*: poner, leer, índice |
| 0x31 | `0x449470` | `SetBrushModel` |
| 0x32, 0x33, **0x3b** | `0x44e6c0` | **trazado**; con 0x3b se pasa la tabla de prioridad de zonas (ver `armas.md`) — confirmado |
| 0x34 | `0x44eab0` | trazado de visión (sin entidades) |
| 0x36 | `0x44edb0` | contacto de una entidad con otra |
| 0x37/0x38, 0x39/0x3a | `0x41d380`, `0x41ef50` | trazado del mapa de colisión con/sin entidades concretas |
| 0x3c | `0x44ef60` | contenido en un punto |
| 0x42, 0x43 | `0x44db30`, `0x44daf0` | **enlazar / desenlazar** entidad en el mundo ("touching 3 areas") — confirmado por el texto |
| 0x44 | `0x41fab0` | entidades en una caja (con máscara) |
| 0x46, 0x47 | | comando de usuario del cliente |
| 0x48 | `0x43fbb0` | siguiente token de las entidades del BSP (`G_ParseSpawnVars`) |
| 0x4f, 0x50 | `0x431790`… | tipo de superficie por nombre (`brick`, `default`…) |
| 0x51, 0x52 | | dibujo de depuración (texto, línea) |
| 0x58–0x5e | `0x4313a0`… `0x449b50` | cadenas de script y modelos (`xmodel`) |
| **0x61–0x76** | `0x47cb30`–`0x47e5d0` | **árbol de animación (XAnim)**: crear, borrar, poner animación, pesos, tiempo, *notetracks*, longitud, delta de movimiento… |
| **0x77–0x90** | `0x449ba0`–`0x44a0a0` | **modelo animado de una entidad (DObj)**: crear, borrar, adjuntar, tags (`0x83` = `gettagorigin`), límites |
| 0x7a | `0x449d20` | procesar *notetracks* de la entidad (fase 3 del frame) |
| 0x7b | `0x449d50` | avanzar la animación de la entidad (fase 2 del frame) |
| 0x92 | `0x431150` | fin de frame del servidor (lo llaman el frame y la carga) |
| 0x96, 0x97 | `0x4254b0` | cargar script y buscar función (`aitype`, `animscripts/traverse`) |
| 0xa0, 0xa1 | `0x497d00`, `0x49d8a0` | comprimir/descomprimir partida guardada (zlib 1.1.4) |
| 0xc8–0xd3 | | matemáticas y utilidades sin identificar |

## Interfaz de la VM de scripts

Ver `gsc.md` ("Reparto entre ejecutable y juego"): tabla de 102 funciones en `0x008d8908`.

## Pendiente

- Nombrar las llamadas 0x61–0x90 una a una al reimplementar la animación (hito 6).
- 0xc8–0xd3.
