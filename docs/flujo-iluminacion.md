# Iluminar un mapa a mano en el editor

Guía paso a paso para crear el rig de iluminación de un mapa (`res://render/maps/<mapa>.tscn`).
Decisión y detalles técnicos en `docs/decisions/0003-iluminacion.md`.

## Antes de empezar

- El editor tiene que encontrar el juego: variable de entorno `COD1_PATH` (apuntando a la carpeta
  `main` de la instalación) o `game_path` en la sección `[game]` de `user://foley.cfg`. Si falta, la
  vista previa avisa en el panel Salida y no se genera.
- Si acabas de cambiar código C#, compila (botón **Compilar** arriba a la derecha) antes de abrir la
  escena del rig.

## 1. Crear la escena

1. **Escena → Nueva escena → Otro nodo**, busca **MapLightRig** y créalo como raíz.
2. Cambia el nombre del nodo raíz si quieres (no importa).
3. Guarda enseguida con **Ctrl+S** como `res://render/maps/<mapa>.tscn`, con el nombre del BSP sin
   extensión: `burnville.tscn` para `maps/burnville.bsp`. Si prefieres otro nombre de archivo, rellena
   la propiedad **Mapa** (`burnville` o `maps/burnville.bsp`), pero el juego solo busca
   `<mapa>.tscn`.
4. En el inspector, **Iluminacion** ya trae una copia de `default.tres`: un rig nuevo se ve igual
   que un mapa sin rig.

## 2. Activar la vista previa

1. En el inspector, grupo **Editor**, marca **Vista previa del mapa**. Tarda unos segundos: se
   construyen la geometría, los modelos, el cielo con la luna, el entorno y las luces del BSP.
2. La vista previa **no aparece en el árbol de la escena y nunca se guarda**: no tiene Owner. Por
   eso no se pueden seleccionar sus nodos.
3. Desmarcar la casilla borra todo lo generado. **Refrescar** la reconstruye entera (por ejemplo,
   tras cambiar **Mapa** o recompilar).

## 3. Apagar la previsualización del editor

En la barra superior del visor 3D, desactiva los dos botones de **previsualizar sol** (icono de sol)
y **previsualizar entorno** (icono de globo). Si no, el sol y el cielo de prueba del editor se suman a
la luz del mapa y lo que ves no es lo del juego.

## 4. Ajustar la iluminación

- **Ajustes del mapa**: abre **Iluminacion** en el inspector (energías, sombras, desvanecido por
  distancia, ambiente, SDFGI, SSIL, tonemap, exposición, glow). La vista previa se actualiza sola.
- **Ajustes de una luz del BSP** (apagarla o multiplicar su energía):
  1. acerca la cámara del editor a la luz;
  2. pulsa **Añadir ajuste de la luz del BSP más cercana a la cámara** (en Salida se dice qué luz
     ha elegido y a qué distancia);
  3. en **Ajustes por luz**, abre el nuevo elemento y cambia **Enabled** o **Energy Scale**.

  **Listar las luces del BSP en Salida** muestra todas con su identificador (`targetname` o
  `origin`) y su posición. El astro es `sol`: un ajuste con Id `sol` apaga o atenúa la luna.
- **Luces manuales**: añade nodos como hijos de la raíz del rig (**Ctrl+A** → `OmniLight3D`,
  `SpotLight3D`…). Para colocarlas, vuela con la cámara del editor (clic derecho + WASD) hasta donde
  quieras la luz, selecciónala en el árbol y pulsa **Ctrl+Alt+M** (*Alinear transformación con la
  vista*): la luz pasa a la posición y orientación de la cámara. Con un foco, apunta hacia donde
  mirabas. Ajusta el color, la energía, el alcance y las sombras en su inspector. Estas
  luces se guardan y en el juego se suman a las del BSP; los ajustes del mapa no las tocan.
- No añadas mallas, texturas ni imágenes al rig: el test `MapRigContentTests` lo prohíbe (regla dura 1).

## 5. Guardar y revisar

1. **Ctrl+S**.
2. Revisa lo que se va a commitear:

   ```sh
   git status
   git diff -- godot/render/maps/
   ```

   El `.tscn` debe tener solo el nodo raíz con sus ajustes (`Iluminacion`, `AjustesPorLuz`) y tus
   luces manuales. Si ves `ArrayMesh`, `ImageTexture`, `PackedByteArray` o cientos de líneas, algo
   ha ido mal: no lo commitees.
3. Pasa el test:

   ```sh
   dotnet test formats.tests --filter MapRigContent
   ```

## 6. Probar en el juego

Ejecuta `jugar.tscn` (botón **Ejecutar escena actual** con esa escena abierta, o **F6**). En Salida
debe aparecer:

```
[MapViewer] iluminación: res://render/maps/<mapa>.tscn (N ajustes por luz, M luces manuales)
```

Para otro mapa sin cambiar la escena, desde la línea de comandos:

```sh
<godot> --path godot res://jugar.tscn -- --map=maps/<mapa>.bsp
```

En el juego, **B** cambia el brillo ambiental y **L** el modo de vista (normal, sin sombreado,
alambre) para comparar zonas oscuras con el original.
