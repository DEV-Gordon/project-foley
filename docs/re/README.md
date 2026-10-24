# Ingeniería inversa: índice y estado

Un `.md` por sistema (CLAUDE.md, "Fuentes de verdad"). Los apéndices marcados como *generado*
salen de `tools/re/gen_docs.py` y no se editan a mano.

| Archivo | Sistema | Hito | Estado |
|---|---|---|---|
| `version.md` | Versión y hashes de los binarios analizados | — | hecho |
| `tick.md` | Frame del mundo (50 ms, confirmado) y ritmo del pmove | 3–4 | confirmado el frame; pmove por comando |
| `colision.md` | Trazado contra brushes, terreno y curvas | 3 | portado; faltan trazados de punto |
| `pmove.md` | Movimiento del jugador | 3 | de pie portado; adelanto de posturas, escaleras, agua |
| `red.md` | Tablas de red (`playerState`, `entityState`) | 3 | hecho (para leer demos) |
| `modelos.md` | Modelos del mapa y marcadores FX | 2 | hecho |
| `entidades.md` | Spawn desde el BSP y orden del frame | 4–6 | orden de fases y reglas de spawn |
| `gsc.md` | VM de GSC: reparto exe/juego, tiempo, `wait`, límites | 4 | tiempo y `wait` confirmados |
| `gsc-api.md` *(generado)* | 360 built-ins, clases de spawn y campos de script | 4 | completo para un jugador |
| `dvars.md` *(generado)* | 99 dvars del juego con valores por defecto | 3–7 | completo |
| `armas.md` | Archivo de arma, daño, zonas, disparo, retroceso | 5 | `G_Damage` y bala; falta dispersión del jugador |
| `armas-campos.md` *(generado)* | 248 claves del archivo de arma | 5 | completo |
| `ia.md` | Actores, sentients, precisión, dificultad | 6 | precisión contra el jugador |
| `ejecutable.md` | Carga de las DLL y las 212 llamadas del juego al ejecutable | 4–7 | mapa por bloques |
| `sonido.md` | Alias de sonido y aviso de fin de `playsound` | 4 | flujo completo; falta el ajuste por `pitch` |
| `../formats/xanim.md` | Formato de animación y muestreo | 6 | formato validado con las 2957 animaciones |
| `../formats/bsp.md` (lump 31) | Caminos de la IA | 6 | validado en los 31 mapas de campaña |

## Cobertura de built-ins (hito 4)

`tools/re/gsc_cobertura.py` compara las llamadas de los 809 `.gsc` de la instalación con
`gsc-api.md`:

- todo lo que llaman los scripts de un jugador existe en la API (lo que falta es de
  `maps/mp/*` o son falsos positivos de la búsqueda por texto);
- 310 de los 360 built-ins se usan en algún script y 50 no se usan nunca, así que no hace falta
  implementarlos para la campaña.

## Herramientas (`tools/re/`)

| Archivo | Qué hace |
|---|---|
| `petabla.py` | Lector mínimo de PE de 32 bits: secciones, VA↔offset, cadenas, punteros |
| `tablas_gsc.py` | Busca tablas `{nombre, función[, dev]}` de built-ins |
| `campos.py` | Vuelca una tabla de campos `{nombre, ...}` deduciendo el paso |
| `desens.py` | Desensambla desde una VA anotando cadenas y floats (requiere `capstone`) |
| `consts.py` | Constantes, cadenas y llamadas que usa cada función |
| `gen_docs.py` | Genera `gsc-api.md`, `armas-campos.md` y `dvars.md` |
| `gsc_cobertura.py` | Cobertura de built-ins en los `.gsc` de la instalación |
| `ExportFoley.java` | Script de Ghidra: exporta cadenas con referencias, funciones y decompilado |
| `syscalls.py` | Recorre la tabla de saltos de un despachador y lista funciones y cadenas por caso |
| `vm_api.py` | Tabla de funciones de la VM que el ejecutable pasa al juego, con usos |
| `xanim_probe.py` | Valida el formato `xanim` contra todas las animaciones |
| `paths_probe.py` | Valida el lump 31 (caminos) contra todos los BSP |

Todas leen los archivos del juego de la instalación del usuario y escriben **fuera del repo**
salvo `gen_docs.py`, que solo escribe nombres, direcciones y tipos. Las salidas de Ghidra
(decompilado, desensamblado) van a la carpeta de trabajo (`project-foley-trabajo/re/`), nunca
al repo (CLAUDE.md, regla dura 2).

### Reproducir el análisis

Ghidra corre en WSL (ADR 0002). Sin REA, en modo sin interfaz:

```sh
export JAVA_HOME=/usr/lib/jvm/java-21-openjdk-amd64 GHIDRA_HEADLESS_MAXMEM=4G
~/tools/ghidra_12.1.4_PUBLIC/support/analyzeHeadless ~/foley/ghidra foley \
  -import ~/foley/binarios/gamex86.dll -overwrite \
  -scriptPath <repo>/tools/re -postScript ExportFoley.java ~/foley/salidas/gamex86
```

El proyecto de Ghidra (`~/foley/ghidra/foley`) ya contiene `gamex86.dll`, `cgamex86.dll` y
`CoDSP_1.5_memoria.bin` analizados; para volver a exportar sin reanalizar se usa
`-process <archivo> -noanalysis`. El volcado de `CoDSP.exe` se carga como PE con base
`0x400000`.

## Siguiente trabajo de RE por hito

- **Validación con el original** (se verá más adelante): alternativa a `record`, grabando el
  estado del jugador desde la memoria del proceso; y un `.gsc` de prueba en un pk3 aparte para
  el orden de los hilos (`gsc.md`).
- **3**: portar posturas, escaleras, agua, tumbado e inclinación (`pmove.md`, "Adelanto");
  eventos de pasos y tipos de superficie; portar los trazados de punto (`colision.md`, ya
  leídos).
- **4**: orden de reanudación de hilos; `endon` al borrar entidades; `G_RunEntity`;
  disparadores; ajuste por `pitch` de la duración del sonido.
- **5**: dispersión y retroceso del arma del jugador; granadas y proyectiles; `G_RadiusDamage`;
  cuerpo a cuerpo.
- **6**: pensamiento del actor, percepción y A* sobre el lump 31; árbol de animación (pesos,
  transiciones); muestreo de huesos.
- **7**: HUD (`newhudelem`, campos y métodos en `gsc-api.md`), objetivos, guardado
  (`G_SaveGame`/`G_LoadGame` en `gamex86.dll`, `0x2002c1b0`/`0x2002d3a0`); `cgamex86.dll` ya está
  analizada (vista, balanceo, HUD) y `uix86.dll` falta.
