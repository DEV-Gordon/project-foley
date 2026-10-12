# Shaders (`scripts/*.shader`)

Texto con bloques `nombre { directivas  { etapa } }`. Los comentarios son `//`.
Por ahora solo se lee lo necesario para elegir la textura (`formats/Materials/ShaderFile.cs`).

| Directiva | Uso actual |
|---|---|
| `surfaceparm <x>` | `sky` y `nodraw` deciden qué no se dibuja |
| `map` / `clampmap [modificadores] <ruta>` | primera etapa que no sea `$...` = textura base. CoD admite modificadores antes de la ruta (`map clamp ...`, `map clampy ...`, `map heighttonormal ...`); se toma el primer argumento con `/` o `\` |
| `qer_editorimage <ruta>` | respaldo para la textura base |
| `skyParms <prefijo> ...` | prefijo del cubo de cielo (6 `.tga` en `env/`) |

Resolución de un material del BSP (`MaterialResolver`):
1. archivo `<nombre>.dds`, `.tga` o `.jpg`;
2. si no existe, el shader con ese nombre: primer `map`, luego `qer_editorimage`.

Confianza: observado en burnville (404 materiales: 388 por archivo, 16 por shader, 0 sin
resolver) y en los 43 BSP jugables de la instalación.

Materiales referenciados por los BSP que **no existen en ningún pk3** (ni textura ni shader);
faltan en los datos del juego, no es un fallo del lector. Cómo los dibuja el original está sin
determinar (solo afecta a la presentación); el visor los pinta en magenta:

| Material | Mapas |
|---|---|
| `textures/normandy/windows/glass@window2a` | `mp/mp_depot` |
| `textures/industrial/glass@factorywindow4a_broken` | `stalingrad` |
| `textures/industrial/glass@factorywindow4z_broken` | `stalingrad` |
| `textures/industrial/glass@factorywindow4o_broken` | `stalingrad` | Supuesto sin verificar: la preferencia dds > tga > jpg cuando hay varios.
No se interpretan `blendFunc`, `rgbGen`, `alphagen` ni `nextbundle` (terreno con mezcla).
