# pk3

Archivo zip renombrado. Se lee con `System.IO.Compression` (`formats/Pk3/Pk3Archive.cs`).

- Rutas internas con `/`; se normalizan a minúsculas para buscar.
- Hay entradas de directorio (terminan en `/`); se ignoran.
- Precedencia entre pk3 de un mismo directorio: orden alfabético y el último que contiene la ruta gana
  (convención de ioquake3, `FS_AddGameDirectory`). **Supuesto**: no verificado contra el binario de CoD1.
  Implementado en `Pk3Collection`.
- Inventario de la instalación revisada (Steam, 16 pk3 en `Main/`): ~14.9k entradas, 2.27 GB descomprimidos.
  Las texturas son `.dds` y `.tga`, **no `.iwi`** (ese formato es de CoD2; el glosario de CLAUDE.md debe corregirse).
  Los modelos y animaciones no llevan extensión (`xmodel/`, `xmodelparts/`, `xmodelsurfs/`, `xanim/`).
