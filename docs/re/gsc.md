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

Más entradas identificadas por su uso desde el juego (índice de la tabla; confianza
**probable**). Las direcciones del ejecutable salen de las asignaciones de `0x45a430`, porque el
volcado se tomó antes de cargar un nivel y la tabla estaba vacía:

| Índice | Ejecutable | Uso |
|---|---|---|
| 1 | `0x469b20` | leer parámetro entero (`GetInt(i)`) |
| 2 | `0x469bb0` | leer parámetro animación ("cannot cast %s to anim") |
| 4 | `0x469e10` | leer parámetro float |
| 5, 6 | `0x469f50`, `0x469eb0` | leer parámetro cadena (de script / constante) |
| 9 | `0x46a0d0` | leer cadena localizada ("cannot cast %s to istring") |
| 10 | `0x46a1b0` | leer vector ("cannot cast %s to vector") |
| 14 | `0x46a310` | leer objeto o entidad ("cannot cast %s to object") |
| 15 | `0x46a550` | nº de parámetros |
| 17, 18, 21, 23, 25, 26 | | devolver int, float, entidad, cadena C, cadena de script, vector |
| 20 | `0x46a5e0` | devolver `undefined` |
| 42, 43 | `0x469380`, `0x4693f0` | lanzar hilo (global / sobre entidad, con nº de parámetros) |
| 45 | `0x46a960` | error de script (77 usos) |
| 47, 48 | `0x46aa40`, `0x46aa90` | error de parámetro / de objeto ("not an entity") |
| 50 | `0x45f8f0` | liberar el número de entidad del script al borrarla |
| 56 | `0x45fba0` | registrar campo de una clase |
| 79 | `0x45abc0` | cargar script (`.gsc`) |
| 80, 81 | `0x455bd0`, `0x455c80` | árbol de animación por nombre (`generic_human`) |
| 82 | `0x45a8c0` | buscar función de un script por nombre |
| 83 | `0x469560` | liberar hilo |
| 86, 87, 90 | `0x45d5b0`, `0x45d5f0`, `0x45ccc0` | cadenas de script: asignar, obtener por texto (124 usos), convertir a texto |
| 88 | `0x468640` | `notify` desde el motor |
| 95, 96 | `0x46aca0`, `0x46ad90` | avanzar reloj; ejecutar hilos pendientes |
| 100, 101 | `0x45b980`, `0x45b9a0` | memoria de la VM: reservar, liberar |

Las demás tienen 0–2 usos o se usan solo al guardar y cargar partida.

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

## `notify`, `waittill`, `endon` (confirmado salvo lo indicado)

- **`VM_Notify`** (`0x467f50`) busca la lista de espera del objeto para ese evento y la recorre:
  - hilos con **`endon`** de ese evento: se terminan **en el acto** (`0x467dc0`);
  - hilos con **`waittillmatch`**: solo si los parámetros coinciden (comparación `0x4636a0`);
  - hilos con **`waittill`**: reciben los parámetros del `notify` y **no se ejecutan en ese
    momento**. Se programan para el milisegundo actual del reloj de scripts.
- El opcode `notify` (`0x46673a`) llama a `VM_Notify` y el hilo que notifica **sigue
  ejecutándose**. Los hilos despertados corren cuando se procesa la lista del milisegundo actual:
  - si el `notify` ocurre durante el avance del reloj (fase 4), corren en esa misma pasada, porque
    el bucle de `0x468bb0` toma hijos de la lista mientras queden;
  - si ocurre fuera, corren en la siguiente llamada a "ejecutar hilos pendientes"
    (`0x4695b0`), que el juego hace tras sus propios `notify` (fases 1 y 3 del frame).
- `notify` desde el motor (`0x4683b0`, entrada de la tabla `0x008d8a6c`): mismo `VM_Notify`.
- **Orden** entre varios hilos que esperan el mismo evento o el mismo milisegundo:
  `0x468bb0` saca siempre el primer hijo de la lista. Falta confirmar si la inserción
  (`0x45dfa0`, lista circular) pone los nuevos al principio o al final. `TODO(fidelidad)`:
  resolverlo con una prueba en el juego (un `.gsc` de prueba en un pk3 aparte).

## Variables y tipos

- **Tipos** (tabla de nombres `0x5687e8`, índice = código): 0 `undefined`, 1 `string`,
  2 `localized string`, 3 `vector`, 4 `float`, 5 `int`, 6 `codepos`, 7 `object`, 8 `key/value`,
  9 `function`, 10 `stack`, 11 `animation`, 12 `thread`, 13 `entity`, 14 `struct`, 15 `array`,
  16 `dead thread`, 17 `dead entity`, 18 `dead object`.
- **Variables**: tabla hash de 65535 entradas; el índice es `(padre · 31 + nombre) % 0xffff + 1`
  (`0x45dfa0`). Máximo **65535** variables ("exceeded maximum number of script variables").
- **Conversiones** (`0x462f50` bool, `0x463040` int, `0x463120` float, `0x463200` cadena):
  - a int: float → truncado (`_ftol`); cadena → `atol`, y error si da 0 y la cadena no empieza
    (tras espacios y signo) por `0` o `.0`;
  - a float: int → float; cadena → `atof`, con el mismo error;
  - a bool: float → `≠ 0`; cadena → `atol ≠ 0`, con el mismo error;
  - a cadena: int → `%i`; float → **`%g`** (printf de MSVC, exponente de 3 cifras: `1e+006`);
    vector → `(%.2f, %.2f, %.2f)`.
- **Operadores binarios** (promoción `0x463550`):
  - dos operandos del mismo tipo: sin cambio;
  - cadena con int, float o vector: el otro pasa a cadena;
  - float con int: el int pasa a float;
  - cualquier otra combinación: error "pair has unmatching types".

  Consecuencias:
  - `+`: concatena cadenas y suma vectores, floats e ints.
  - `-`: vectores, floats e ints.
  - `*`: **solo int e int, o float y float**. No existe `vector * escalar`; los scripts usan
    `vectorScale` de `maps/_utility.gsc`.
  - `/`: int entre int es **división entera truncada** (`idiv`). Entre 0 da el error
    "divide by 0" y el resultado es 0.
  - `++`/`--`: solo int. `~`: solo int.
- **Índices**: arrays, cadenas (solo lectura: "string characters cannot be individually
  changed") y vectores (solo lectura: "vector components cannot be individually changed").
  `size` da error con los tipos que no lo admiten ("size cannot be applied to %s"); cuáles
  lo admiten (arrays y cadenas, al menos) está por confirmar en `0x45f5f0`.
- El intérprete tiene **90 opcodes** (0–0x59, tabla `0x467640`). No hace falta copiarlos porque
  compilamos nuestro propio bytecode; sí su semántica.

## Aleatoriedad (confirmado, binario)

Hay **dos generadores** en `gamex86.dll` y los dos se siembran con el reloj real al cargar el
nivel, así que el original **no es reproducible**. Nosotros fijaremos las semillas (decisión de
determinismo, como el pmove de 8 ms).

1. **`rand()` de la CRT de MSVC** (`0x20074d05`): `s = s·214013 + 2531011`; devuelve
   `(s >> 16) & 0x7fff`. `G_InitGame` (`0x2001e110`) hace `srand(semilla)` con la semilla que le
   pasa el ejecutable, que es `Com_Milliseconds()` (`0x42f450`). Lo usan 47 sitios del juego: la
   dispersión de las balas (`armas.md`), la IA, los efectos...
2. **Generador de los scripts** (estado en `0x200a6330`; el valor estático `0x89abcdef` se pisa
   en `G_InitGame` con la llamada 4 = `Sys_Milliseconds()`). Por cada número:
   `s = s·214013 + 2531011` y `r = s >> 17` (15 bits, **no** el mismo bit que `rand()`).
   - `randomint(n)`: `(r · n) >> 15`. Error si `n ≤ 0` ("RandomInt parm must be positive
     integer").
   - `randomfloat(f)`: `r · f / 32768`.
   - `randomintrange(a, b)`: `a + ((r · (b − a)) >> 15)`.
   - `randomfloatrange(a, b)`: por analogía, falta confirmarlo.

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

## Compilador

- Analizador léxico generado con **flex** (`0x46beb0`; mensajes "fatal flex scanner internal
  error", "bad token '%s'") y sintáctico con **bison** (`0x46ae90`; "parse error", "parser stack
  overflow"). Las palabras clave están en las tablas del autómata, no como texto, así que la
  gramática se reconstruye a partir de los scripts: el parser de `/formats` debe aceptar los
  809 `.gsc` de la instalación.
- Errores del compilador útiles para la gramática:
  - `case` solo admite expresiones int o cadena;
  - no se permite `/# ... #/` justo alrededor de `case`, `break` o `continue`;
  - `/#` no puede anidarse ("cannot recurse /#");
  - "function exceeds operand stack size".
- "#using_animtree was not specified" (en tiempo de ejecución): los built-ins de animación
  necesitan la directiva en el archivo.

## Pendiente (hito 4)

- **Orden** de reanudación de hilos que esperan el mismo milisegundo o el mismo evento (FIFO o
  LIFO): verificar con un `.gsc` de prueba en un pk3 aparte.
- Qué pasa con los hilos `endon("death")` cuando se borra la entidad (el borrado notifica
  `death`: `0x20032c00` hace `notify` antes de liberar la entidad).
- Límite de cadenas de script (el de variables es 65535).
- Tipos que admiten `size` (`0x45f5f0`) y comparaciones `==`/`!=` entre tipos distintos
  (`0x4636a0`).
- Nombre del resto de entradas de la tabla de la VM con pocos usos (`vm_api.txt`, en la carpeta
  de trabajo).
- Lista de built-ins que usan los scripts de burnville frente a esta API (comando de diagnóstico
  de CLAUDE.md); la cobertura global ya está hecha (`README.md`).
