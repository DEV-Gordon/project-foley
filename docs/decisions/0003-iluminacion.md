# 0003 - Iluminación nueva: luces desde las entidades del BSP y SDFGI

Fecha: 2026-10-14. Contexto: CLAUDE.md decide iluminación nueva de Godot en todos los modos, sin
lightmaps originales, con luces guiadas por las entidades de luz del BSP y GI.

## Decisión

- **Luces al cargar el mapa, desde el BSP** (`godot/bridge/MapViewer.Lighting.cs`):
  - Sol o luna: `worldspawn` → `sundirection` (pitch yaw roll, AngleVectors; pitch negativo = sobre el
    horizonte), `suncolor`, `sunlight` → `DirectionalLight3D`.
  - Ambiente: `ambient · _color + sundiffusecolor · sunlight · diffusefraction` → luz ambiente de color.
  - Entidades `light` sin `target` → `OmniLight3D` con alcance = valor `light` (unidades; 300 por
    defecto, como q3map) y color `_color`.
  - Entidades `light` con `target` (un `info_null`) → `SpotLight3D` hacia el destino, con apertura
    `atan(radius / distancia)` (`radius` 64 por defecto, como q3map).
  - Burnville: 71 puntuales, 13 focos, luna a 30° sobre el horizonte, rumbo 160°.
- **Colores normalizados como q3map** (`ColorNormalize`: se divide por la componente mayor). Los
  mapas escriben los colores de luz en escala 0-1 o 0-255 (pavlov: `suncolor 100 100 105`,
  `_color 100 105 105`; también chateau, depot, powcamp, railyard, ship, pegasus); sin normalizar,
  esos mapas salían unas 100 veces más brillantes. La intensidad la ponen solo `sunlight` y `light`.
- **Recursos de ajuste**: `godot/render/maps/default.tres` para todos los mapas y, opcionalmente,
  `godot/render/maps/<mapa>.tres` para uno concreto (gana el del mapa). Solo multiplicadores
  artísticos (energía, tinte y suavidad de sombra del astro, alcance, sombras, SDFGI, exposición).
  No guardan posiciones ni colores del juego, así que no son derivados de sus archivos (CLAUDE.md,
  regla dura 1). Valores iniciales de `default.tres` tomados del aspecto de la escena de prueba `luztest.tscn` (ya borrada) que el
  usuario consideró más fiel (astro a la mitad de `sunlight`, sombras suaves de 1.5°, exposición 1.5).
- **GI en tiempo real con SDFGI** en vez de LightmapGI: las mallas del mapa se generan al cargar
  desde el BSP del jugador y no existen como recursos del proyecto, así que no hay nada que hornear
  en el editor. Las UV de lightmap se siguen cargando como UV2 por si más adelante se añade un paso
  de importación que guarde las mallas y permita hornear LightmapGI.
- Las claves de compilación de lightmaps (`overbrightShift`, `exponent`, `spawnflags` de luz) no se
  usan.

## Consecuencias

- La luz no es la del original: igualarla en lo jugable (zonas oscuras y visibles) es un ajuste
  artístico que se hace en el `.tres` del mapa comparando con el juego.
- Las 2 entidades `corona` (halos de farolas) y el destello, brillo y oscurecimiento de la luna
  (`scripts/<sunfile>.sun`) se dibujan como en el original, en una capa 2D aditiva
  (`godot/bridge/Flares.cs`); son presentación original, no parte de la luz nueva. Detalles y supuestos
  en `docs/formats/shader.md` ("Coronas y destellos").
