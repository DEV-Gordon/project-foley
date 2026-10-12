# BSP (observaciones iniciales)

Solo lo observado en `maps/burnville.bsp`; falta contrastar con otros mapas y con ioquake3/REA.

- Cabecera: `IBSP`, versión 59 (int32 LE).
- Directorio: 33 lumps de 8 bytes, a partir del byte 8, como **(longitud, offset)** (orden inverso al de Quake 3).
- Lump 0: materiales, registros de 72 bytes (nombre de 64 bytes terminado en 0 + 2 enteros sin interpretar).
  El nombre es la ruta de la textura sin extensión (`textures/...`) o el nombre de un shader.
- Lump 29: entidades, texto estilo Quake 3 (`{ "clave" "valor" }`).
- Confianza: observado en un mapa; el resto de lumps sin identificar.
