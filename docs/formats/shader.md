# Shaders (`scripts/*.shader`)

Texto con bloques `nombre { directivas  { etapa } }`. Los comentarios son `//`.
Por ahora solo se lee lo necesario para elegir la textura (`formats/Materials/ShaderFile.cs`).

| Directiva | Uso actual |
|---|---|
| `surfaceparm <x>` | `sky` y `nodraw` deciden qué no se dibuja |
| `map` / `clampmap <ruta>` | primera etapa que no sea `$lightmap` = textura base |
| `qer_editorimage <ruta>` | respaldo para la textura base |
| `skyParms <prefijo> ...` | prefijo del cubo de cielo (6 `.tga` en `env/`) |

Resolución de un material del BSP (`MaterialResolver`):
1. archivo `<nombre>.dds`, `.tga` o `.jpg`;
2. si no existe, el shader con ese nombre: primer `map`, luego `qer_editorimage`.

Confianza: observado en burnville (404 materiales: 388 por archivo, 16 por shader, 0 sin
resolver). Supuesto sin verificar: la preferencia dds > tga > jpg cuando hay varios.
No se interpretan `blendFunc`, `rgbGen`, `alphagen` ni `nextbundle` (terreno con mezcla).
