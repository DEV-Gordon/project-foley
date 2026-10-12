# CLAUDE.md — Project Foley

Reimplementación en Godot de la campaña de Call of Duty (2003). Juega **exactamente igual** que el original y se ve moderna. El motor lee los archivos de la copia legal del jugador. Este repositorio nunca contiene material del juego original.

## Regla de oro

**La simulación es CoD1 exacto. Solo cambia la presentación.**

- **Simulación** (fiel al original): movimiento, colisión, armas, daño, hitboxes, IA, scripts GSC y tiempos de las misiones.
- **Presentación** (moderna): mallas, materiales PBR, luces, GI, efectos, postproceso, audio espacial e interpolación visual.

Si un cambio de presentación altera la simulación, es un bug. Ante la duda sobre si algo es simulación o presentación, se trata como simulación y se pregunta.

## Reglas duras (nunca romper)

1. **Nunca commitear archivos del juego original** ni derivados directos: `.pk3`, `.iwi`, `.bsp`, xmodels, sonidos, scripts GSC, texturas convertidas o extraídas. Tampoco en tests, fixtures ni capturas.
2. **Nunca commitear salidas de decompilación** (pseudocódigo, dumps de ensamblador). Los hallazgos se documentan en prosa y con valores en `docs/re/`, y el código se escribe a partir de esa documentación.
3. **No usar "Call of Duty", "Activision" ni "Infinity Ward"** en el nombre del proyecto, en la UI, en los iconos ni en los nombres de ejecutables. En la documentación interna se puede nombrar el juego original como referencia.
4. **La ruta del juego original nunca va hardcodeada.** El juego la lee de la configuración del usuario, y los tests de la variable de entorno `COD1_PATH`.
5. **Ningún dato de gameplay se inventa.** Cada valor de simulación (velocidades, daño, spread, tiempos) debe tener una fuente citada: un archivo del pk3, el código de ioquake3 o una nota en `docs/re/`. Si no hay fuente, se deja un `TODO(fidelidad)` y se avisa.

## Licencia

GPL-2.0-or-later, para poder portar código de ioquake3 (GPL) como base del comportamiento heredado de id Tech 3. Todo archivo portado o adaptado de ioquake3 lleva un comentario con el archivo y la función de origen.

## Stack

- **Godot, edición .NET, última versión estable al iniciar el proyecto.** Su número exacto queda anotado en `docs/decisions/0001-version-godot.md`. No se sube de versión sin pedirlo.
- El `TargetFramework` de todos los proyectos C# (`sim`, `formats`, tests y `godot`) es el mismo que exige esa versión de Godot.
- **C# (.NET)** para el núcleo de simulación y los lectores de formatos:
  - Floats de 32 bits (`float`) para replicar la aritmética del original. Nunca usar `double` en simulación salvo que el original lo haga.
  - El núcleo de simulación es una biblioteca C# sin dependencias de Godot, para poder testearla con `dotnet test` y mantenerla determinista.
- **GDScript** para la presentación, la UI y las herramientas de editor que no comparten código con la simulación.

## Estructura

```
/sim/              Biblioteca C# pura (sin Godot): pmove, armas, daño, IA, VM de GSC
/sim.tests/        Tests xUnit del núcleo de simulación
/formats/          Lectores C#: pk3, BSP, IWI, xmodel, configs de armas, GSC (parser)
/formats.tests/
/godot/            Proyecto Godot
  /bridge/         Adaptadores sim <-> nodos de Godot (C#)
  /render/         Presentación: materiales, luces, efectos (GDScript)
  /import/         Plugins de importación de editor
  /ui/
/overrides/        Estructura vacía para assets remasterizados (contenido en .gitignore)
/docs/
  /re/             Hallazgos de ingeniería inversa: un .md por sistema
  /formats/        Especificación de cada formato tal como se entiende
  /decisions/      Decisiones de arquitectura (ADR cortos)
```

## Arquitectura

### Tiempo de simulación (modelo id Tech 3)
- La simulación usa un **reloj entero en milisegundos**, como el original. Nunca usar el `delta` de Godot en float para la lógica de gameplay.
- **Dos ritmos dentro de `/sim`:**
  - **Movimiento del jugador (pmove)**: un paso por comando de usuario, fijo en **8 ms (125 Hz)**. En id Tech 3 el movimiento depende de la tasa de cuadros del cliente (de ahí los famosos saltos a 125 fps), así que se fija una tasa constante para que sea determinista.
  - **Frame del mundo** (scripts GSC, IA, entidades, `wait`): cada **50 ms (20 Hz)**, valor de referencia de id Tech 3.
- Ambos valores están **por confirmar** contra CoD1 con REA y los configs, sobre todo si el modo de un jugador se comporta igual que el multijugador. Los hallazgos van en `docs/re/tick.md`.
- Godot: `physics_ticks_per_second = 125`. Cada tick avanza la simulación 8 ms; la simulación dispara el frame del mundo cuando su acumulador llega a 50 ms. Con la interpolación de física de Godot activada, el render queda fluido a cualquier fps.
- Toda la lógica de gameplay vive en `/sim` y avanza solo con este reloj, nunca desde `_process`.

### Separación sim / presentación
- `/sim` no conoce nodos, escenas ni recursos de Godot. Recibe inputs y produce un estado.
- `/godot/bridge` traduce el estado de la simulación a nodos y los inputs de Godot a comandos de la simulación.
- La presentación **solo lee** el estado de la simulación; nunca lo escribe.

### Colisión
- Sale **siempre** de los brushes del BSP original, con trazado propio en `/sim` (no con la física de Godot).
- Las mallas remasterizadas son solo visuales y nunca generan colisión.

### Capa de reemplazo de assets
- Para cada asset original se busca primero un reemplazo en `overrides/` con la misma ruta lógica. Si no existe, se usa el original convertido en tiempo de carga.
- Hay un interruptor en vivo **clásico / moderno**: como la simulación es la misma, solo cambia la capa de presentación.

### Iluminación (modo moderno)
- Se descartan los lightmaps originales y las texturas se tratan como albedo.
- Las UV de lightmap del BSP se reutilizan como UV2 para LightmapGI.
- Las luces de cada mapa se definen en un recurso por mapa (`render/maps/<mapa>.tres`), no en código.

## Fuentes de verdad para la fidelidad (en orden)

1. **Datos del juego**: configs de armas, scripts GSC, menús y entidades del BSP.
2. **ioquake3**: comportamiento base de id Tech 3 (pmove, trazado, estructura del BSP).
3. **Binario de CoD1 vía REA**: lo propio de CoD (posturas, inclinación, reglas de daño, IA, built-ins de GSC, tasa de tick).

Cada hallazgo de REA se documenta en `docs/re/<sistema>.md` con:
- qué hace el sistema, en prosa;
- las constantes y fórmulas, con sus valores;
- la evidencia (dirección de la función, referencias, strings);
- la confianza (confirmado con tests / probable / supuesto).

## Intérprete GSC

- Parser en `/formats`, VM en `/sim`.
- Los threads son corrutinas cooperativas: `wait` se mide en ticks de simulación y el orden de ejecución dentro de un tick debe coincidir con el original.
- Built-ins: cada uno vive en su propio archivo bajo `/sim/gsc/builtins/`. Un built-in sin implementar lanza un error con su nombre; nunca falla en silencio.
- Comando de diagnóstico: correr todos los GSC de una misión y listar los built-ins faltantes. Esa lista es la barra de progreso.

## Tests

- **Unitarios** (`dotnet test`): toda fórmula de simulación tiene tests con valores del original.
- **Corpus**: lectores que procesan todos los archivos de la instalación de `COD1_PATH`. Si la variable no existe, el test se salta (no falla). Nunca se copian archivos del juego como fixtures.
- **Fidelidad de movimiento**: reproducir demos grabadas en el original y comparar posiciones por tick. La tolerancia queda documentada por test.
- Antes de dar una tarea por terminada: compilar, correr los tests y reportar qué se validó y qué no.

## Flujo de trabajo

- Una tarea acotada por sesión ("lector de lumps del BSP", "built-in `spawn`"), nunca "reconstruir el juego".
- Antes de implementar un sistema, leer su nota en `docs/re/` y su especificación en `docs/formats/`. Si no existen, crearlas primero.
- No mezclar cambios de simulación y de presentación en el mismo commit.
- Commits en español, en imperativo y cortos: `sim: implementar fricción del pmove`.
- Si algo del original no se puede determinar, preguntar en vez de suponer.

## Hitos

1. Lector de pk3 e inventario de contenido.
2. Lector de BSP e importador: el mapa visible en Godot.
3. Trazado de colisión y pmove: caminar por un mapa con movimiento validado contra una demo.
4. VM de GSC: la misión elegida corre sus scripts hasta el primer built-in faltante.
5. Armas y daño con valores verificados.
6. IA básica funcional en la misión elegida.
7. Misión completa jugable de principio a fin (vertical slice).
8. Modo moderno: materiales PBR, iluminación y GI en esa misión, con el interruptor clásico/moderno.

## Decisiones abiertas

- Misión del vertical slice: **misión 3 de la campaña, Sainte-Mère-Église de noche** (la incluida en la demo original). Nombre del archivo de mapa: por confirmar con el inventario del hito 1.
- Tiempos de simulación (8 ms de pmove, 50 ms de frame del mundo): **por confirmar** contra el original.
- Versión exacta de Godot: se anota al instalar.

## Glosario

- **pk3**: zip renombrado con los datos del juego.
- **IWI**: formato de textura de CoD.
- **xmodel**: formato de modelos de CoD.
- **BSP**: mapa compilado (variante del IBSP de Quake 3).
- **GSC**: lenguaje de scripts de CoD (texto plano).
- **pmove**: código de movimiento del jugador heredado de Quake 3.
- **built-in**: función del motor que se llama desde GSC.
