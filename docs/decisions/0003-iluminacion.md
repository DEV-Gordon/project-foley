# 0003 - Iluminación nueva: luces desde las entidades del BSP, SDFGI y rig por mapa

Fecha: 2026-10-14; ampliada el 2026-11-09 (rig de iluminación por mapa). Contexto: CLAUDE.md decide
iluminación nueva de Godot en todos los modos, sin lightmaps originales, con luces guiadas por las
entidades de luz del BSP y GI.

## Decisión

- **Luces al cargar el mapa, desde el BSP** (`godot/bridge/MapBuilder.cs`, `ApplyLighting`):
  - Sol o luna: `worldspawn` → `sundirection` (pitch yaw roll, AngleVectors; pitch negativo = sobre el
    horizonte), `suncolor`, `sunlight` → `DirectionalLight3D`.
  - Ambiente: `ambient · _color + sundiffusecolor · sunlight · diffusefraction` → luz ambiente de color.
  - Entidades `light` sin `target` → `OmniLight3D` con alcance = valor `light` (unidades; 300 por
    defecto, como q3map) y color `_color`.
  - Entidades `light` con `target` (un `info_null`) → `SpotLight3D` hacia el destino, con apertura
    `atan(radius / distancia)` (`radius` 64 por defecto, como q3map).
  - Burnville: 66 puntuales, 18 focos, luna a 30° sobre el horizonte, rumbo 160°.
- **Colores normalizados como q3map** (`ColorNormalize`: se divide por la componente mayor). Los
  mapas escriben los colores de luz en escala 0-1 o 0-255 (pavlov: `suncolor 100 100 105`,
  `_color 100 105 105`; también chateau, depot, powcamp, railyard, ship, pegasus); sin normalizar,
  esos mapas salían unas 100 veces más brillantes. La intensidad la ponen solo `sunlight` y `light`.
- **Un solo constructor del mapa**: `MapBuilder` construye mallas, materiales, modelos con LODs,
  marcadores FX, cielo con la luna, entorno y luces del BSP bajo un nodo padre. Lo usan el juego
  (`MapViewer`, que añade el HUD, las teclas L/B, las coronas y el evento `MapLoaded`) y la vista
  previa del editor (`MapLightRig`), así que el editor ve exactamente lo mismo que el juego.
- **GI en tiempo real con SDFGI, no LightmapGI**: las mallas del mapa se generan al cargar desde el
  BSP del jugador y no existen como recursos del proyecto, así que no hay nada que hornear en el
  editor. Hornear exigiría guardar las mallas (derivados del juego, prohibidos en el repo por la regla
  dura 1) o un paso de importación local en cada instalación. Las UV de lightmap se siguen cargando
  como UV2 por si más adelante se añade ese paso.
- Las claves de compilación de lightmaps (`overbrightShift`, `exponent`, `spawnflags` de luz) no se
  usan.

### Ajustes: `default.tres` → rig del mapa

Orden de aplicación en el juego (`MapViewer.LoadLighting`):

1. Si existe `godot/render/maps/<mapa>.tscn` (un **rig**, raíz `MapLightRig`), se instancia en el
   origen y valen sus ajustes (`Iluminacion`) y sus ajustes por luz (`AjustesPorLuz`); sus luces
   manuales se suman a las del BSP.
2. Si no, `godot/render/maps/default.tres` (un `MapLighting`) para todos los mapas.
3. Si tampoco existe, los valores por defecto del código.

Los antiguos `<mapa>.tres` sueltos ya no se leen: su papel lo cumple el rig.

`MapLighting` solo guarda multiplicadores y ajustes artísticos: energía, luz indirecta (SDFGI) y
sombras de puntuales, focos y astro; alcance; desvanecido por distancia (inicio, sombra y tramo) y un
alcance mínimo para dar sombra; tinte y suavidad de sombra del astro; escala y relleno mínimo del
ambiente; SDFGI (energía, rebotes), SSIL; tonemap, exposición y glow. No guarda posiciones ni colores
del juego, así que no es derivado de sus archivos.

Valores de `default.tres`: los de la escena de prueba `luztest.tscn` (ya borrada) que el usuario
consideró más fiel (astro a la mitad de `sunlight`, sombras suaves de 1.5°) y, desde el 2026-11-09,
**tonemap AgX con exposición 2.0** (antes Linear con 1.5): AgX oscurecía la imagen un 15–25 % y con
2.0 el brillo medio de cuatro vistas de burnville queda entre −8 % y +3 % del de antes (la vista
inicial de `jugar.tscn`, un 10 % más oscura). Glow activo con intensidad 0.8, SSIL activo, rebotes
SDFGI 0.5, luz indirecta 1 y desvanecido de las luces desde 80 m (sombra hasta 40 m, tramo 10 m).

### Rig de iluminación por mapa (`godot/bridge/MapLightRig.cs`)

- Nodo `[Tool]` y `[GlobalClass]`, raíz de `res://render/maps/<mapa>.tscn`. El mapa sale de su
  propiedad `Mapa` o, si está vacía, del nombre de la escena.
- **Un rig nuevo copia `default.tres`** (`Duplicate`) en su propio recurso al abrirse en el editor:
  lo que se ve en el inspector es exactamente lo que se guarda y nada cambia por debajo. Se descartó
  "heredar de `default.tres` y guardar solo lo cambiado" porque el inspector no distingue "sin tocar"
  de "igual al valor por defecto". Consecuencia: si después cambia `default.tres`, los rigs ya creados
  no se enteran. (Godot omite al guardar los valores iguales a los del código, así que esos sí siguen
  al código.)
- **Vista previa sin Owner**: la casilla "Vista previa del mapa" (solo editor; `_ValidateProperty`
  le quita el uso `Storage`, así que nunca se guarda) construye el mapa con `MapBuilder` como hijo
  **interno y sin Owner**. Godot solo guarda los nodos cuyo Owner es la raíz de la escena, así que la
  geometría, las texturas, el cielo y las luces del BSP nunca llegan al `.tscn` (regla dura 1).
  Desmarcarla borra todo lo generado. El entorno de la vista previa parte del de `jugar.tscn` (SSAO,
  SSR, niebla) para parecerse al juego.
- La vista previa se refresca sola al cambiar un ajuste (los recursos emiten `changed`; varios
  cambios seguidos se juntan en una sola aplicación, que solo rehace entorno y luces) y entera con el
  botón "Refrescar". Todo va protegido con `Engine.IsEditorHint()`. Si no se encuentra el juego
  (`COD1_PATH` o `user://foley.cfg`) o el mapa, se avisa en Salida y la casilla se desmarca.
- **Ajustes por luz** (`MapLightOverride`): apagar una luz del BSP o multiplicar su energía. La luz se
  identifica por su `targetname` o, si no tiene, por su `origin` tal como lo escribe el BSP
  (`"x y z"`); el astro es `sol`. Se aplican en la vista previa y en el juego. Como las luces de la
  vista previa no se pueden seleccionar (no tienen Owner), el rig trae los botones "Añadir ajuste de
  la luz del BSP más cercana a la cámara" y "Listar las luces del BSP en Salida". Un ajuste guarda la
  posición de una luz del original solo como identificador, y solo de las luces que se ajustan.
- **Luces manuales**: hijos normales del rig, con Owner; se guardan en la escena. Son trabajo
  artístico propio del proyecto (no derivan de los archivos del juego) y en el juego se suman a las
  del BSP sin que los ajustes del mapa las toquen.
- `formats.tests/Repo/MapRigContentTests.cs` lee todos los `.tscn` y `.tres` de
  `godot/render/maps/` y falla si encuentra mallas, imágenes, texturas, formas de colisión, datos de
  GI horneados, datos en bruto (`PackedByteArray`, `PackedVector3Array`…) o recursos externos fuera
  de `res://`.

### Propiedades del `Environment` que controla el código

`MapBuilder` usa el `WorldEnvironment` de la escena si existe (si no, crea uno) y pisa siempre:

- `background_mode` y `sky` (si el mapa tiene cielo: el cielo original);
- `ambient_light_source` (Color), `ambient_light_color`, `ambient_light_energy`;
- `tonemap_mode`, `tonemap_exposure`;
- `glow_enabled`, `glow_intensity`;
- `sdfgi_enabled`, `sdfgi_energy`, `sdfgi_use_occlusion`, `sdfgi_bounce_feedback`;
- `ssil_enabled`.

Por eso el `Environment` de `jugar.tscn` solo guarda lo que el código no toca: SSAO, SSR y niebla
volumétrica. La tecla B del juego cambia además `ambient_light_energy` en vivo (ayuda de inspección).

## Consecuencias

- La luz no es la del original: igualarla en lo jugable (zonas oscuras y visibles) es un ajuste
  artístico que se hace en el rig del mapa comparando con el juego. Paso a paso en
  `docs/flujo-iluminacion.md`.
- Las 2 entidades `corona` (halos de farolas) y el destello, brillo y oscurecimiento de la luna
  (`scripts/<sunfile>.sun`) se dibujan como en el original, en una capa 2D aditiva
  (`godot/bridge/Flares.cs`); son presentación original, no parte de la luz nueva. Detalles y supuestos
  en `docs/formats/shader.md` ("Coronas y destellos"). La vista previa del editor no las dibuja.
