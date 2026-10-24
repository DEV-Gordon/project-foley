# VM de GSC (scripts)

Notas para el hito 4. La lista completa de built-ins y campos está en `gsc-api.md` (generado);
aquí va el comportamiento.

## Fuente

- **Ejecutable** (`CoDSP.exe` 1.5, volcado de memoria `CoDSP_1.5_memoria.bin`, base `0x400000`;
  ver `version.md` y la memoria del proyecto sobre el empaquetado): compilador y VM.
- **`gamex86.dll` 1.5** (en claro): tablas de built-ins, campos y la llamada a la VM en cada
  frame.
- Análisis con Ghidra 12.1.4 en modo sin interfaz y con un desensamblador en Python
  (`tools/re/`). Las salidas del análisis se quedan en la carpeta de trabajo, fuera del repo.

## Reparto entre ejecutable y juego

- El **ejecutable** compila los `.gsc` y ejecuta la VM. Ofrece al juego una tabla de **102
  funciones** (`0x008d8908`, la rellena `0x0045a430`). El juego la copia en `0x200e9938` al
  recibir el mensaje 13 de `vmMain` (`0x2004c010`). Los punteros `DAT_200e99xx` de
  `gamex86.dll` son esa tabla: una entrada del juego en `G` corresponde a la entrada del
  ejecutable en `0x008d8908 + (G − 0x200e9938)`.
- El **juego** registra los built-ins (tablas en `gsc-api.md`) y las funciones de lectura y
  escritura de campos. Devuelve al ejecutable 5 funciones propias (`0x200e9920`): buscar
  función, buscar método, leer y escribir campo de entidad, y una quinta sin identificar.

Entradas de la tabla identificadas por su uso (confianza **probable**):

| Juego | Ejecutable | Uso |
|---|---|---|
| `0x200e997c` | `0x0046a580` | devolver un entero a la VM |
| `0x200e9980` | `0x0046a5a0` | devolver un float |
| `0x200e998c` | `0x0046a630` | devolver una entidad |
| `0x200e999c` | `0x0046a7a0` | devolver una cadena de script |
| `0x200e99a0` | `0x0046a7d0` | devolver un vector |
| `0x200e9a18` | — | registrar un campo de una clase (nombre, índice) |
| `0x200e9a98` | `0x00468640` | `notify` desde el motor (entidad, cadena, nº de parámetros) |
| `0x200e9ab4` | `0x0046aca0` | avanzar el reloj de scripts (ver "Tiempo") |
| `0x200e9ab8` | `0x0046ad90` | ejecutar los hilos pendientes |

Identificar el resto de las 102 es tarea del hito 4: cada una se reconoce por sus mensajes de
error y por dónde la llama el juego.

## Tiempo y `wait`

- El reloj de scripts va en **milisegundos**, con 24 bits (`& 0xffffff`), en `0xa68e6c`.
- `wait X`:
  - X < 0: error "negative wait of %g is not allowed".
  - X > 16777: error "wait of %.0f seconds is too long".
  - El hilo despierta en `(reloj + redondeo(X·1000 + 2⁻³⁰)) & 0xffffff`. El redondeo es el de
    la FPU (al más cercano); el sumando 2⁻³⁰ desempata hacia arriba. Evidencia: VM `0x46462d`;
    constantes `0x55b584` (0), `0x55b8c8` (16777) y `0x55b658` (1000).
- **Avance del reloj** (`0x0046aca0`, llamado por el juego con `level.time`): recorre uno a uno
  los milisegundos desde el valor guardado hasta el nuevo, sin incluir este. En cada uno
  despierta los hilos que esperaban ese milisegundo y luego incrementa el valor guardado.
  Consecuencias (deducidas, confianza **probable**):
  - Un hilo que despierta en el milisegundo `t` y hace `wait 0.05` vuelve a despertar en
    `t + 50`, que cae en el frame siguiente. Un bucle con `wait 0.05` se ejecuta una vez por
    frame.
  - Un hilo que llega a `wait` fuera del avance del reloj (por ejemplo, en el pensamiento de una
    entidad, después de la fase 4 del frame) parte de `level.time` y con `wait 0.05` despierta
    dos frames después.
  - `wait 0` despierta en el frame siguiente.
- El frame del mundo dura **50 ms** (ver `tick.md`), así que la resolución efectiva de `wait` es
  de 50 ms aunque el reloj cuente milisegundos.

## Errores y límites

- `waittill` y `notify` exigen que el primer parámetro sea una cadena ("first parameter of
  waittill/notify must evaluate to a string").
- No se permite `wait`, `waittill` ni `waittillmatch` dentro de `/# ... #/` (código de
  desarrollo) ni en scripts de desarrollo.
- "exceeded maximum number of script variables" y "exceeded maximum number of script strings":
  límites fijos. El valor del límite está por medir.
- **Bucle infinito**: en cada salto hacia atrás (`0x465a2f`), la VM compara el contador de ciclos
  de la CPU (`rdtsc`) con un plazo. Si se pasa, avisa ("potential infinite loop in script") o
  mata el hilo ("killing thread"), según un modo (`0xa6cfd0`). **No es determinista.**
  `TODO(fidelidad)`: la reimplementación usará un límite de instrucciones por hilo y por frame;
  hay que elegir el valor para que ningún script legítimo de la campaña lo alcance. Es una
  diferencia deliberada y debe documentarse como tal.
- Error de ejecución: "script runtime error\n(see console for details)" + volcado
  "******* script runtime error *******".
- Mensaje "#using_animtree was not specified": las funciones de animación exigen la directiva.

## Orden dentro del frame del mundo

Ver `entidades.md` ("Frame del juego"). En resumen: notificaciones de contacto pendientes →
actualización de las entidades con animación → pensamiento de los actores → **avance del reloj
de scripts** (despiertan los `wait`) → niebla y distancia de corte → pensamiento de cada
entidad → fin de frame de los clientes.

## Pendiente (hito 4)

- Identificar las 102 funciones de la tabla del ejecutable (sobre todo `notify`, `endon`,
  `waittill`, `thread` y las de pila y parámetros).
- Formato del bytecode: no hace falta copiarlo (compilamos nosotros), pero sí el **orden** en que
  se reanudan los hilos que esperan el mismo milisegundo y el orden de los que esperan el mismo
  `notify`.
- Semántica de `endon` al morir la entidad y al hacer `notify` sobre sí misma.
- Límites exactos de variables y cadenas.
- Lista de built-ins que usan los scripts de burnville frente a esta API (comando de diagnóstico
  de CLAUDE.md).
