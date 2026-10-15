# Modelos del mapa (entidades con xmodel)

## Qué hay en burnville

836 entidades con un `model` que no es un modelo inline (`*N`), con 81 xmodels distintos:

| Clase | Instancias | Qué es |
|---|---|---|
| `misc_model` | 633 | decorado estático: árboles, setos, muebles, vehículos destruidos, lápidas, hierba |
| `script_model` | 49 | modelos que los scripts pueden mover o borrar |
| `item_health*` | 23 | botiquines |
| `misc_mg42` | 9 | ametralladoras fijas |
| `actor_*` | 122 | soldados: son *spawners* de la IA (hito 6), no se dibujan en el visor |

El formato está en `docs/formats/xmodel.md`. La orientación de cada entidad sale de `angles`
(pitch, yaw, roll) o de `angle` (solo yaw), con `AnglesToAxis` de id Tech 3; `modelscale` escala
el modelo (por defecto 1).

## Colisión de los misc_model

Los xmodels traen sus propios triángulos de colisión, pero en los `misc_model` **la colisión ya
viene horneada en los brushes del BSP** por el compilador de mapas.

Evidencia (burnville): para cada `misc_model` se trazaron dos rayos horizontales de 200 unidades,
cruzados, a 20 unidades sobre su origen, solo contra el BSP:

- Chocan en todas las instancias de modelos que en el juego son sólidos: `tallskinnytreea`
  (46/46), lápidas, `minen1/2`, `crypt1/2`, vehículos, sacos terreros y armarios.
- No chocan en las variantes sin colisión (`fullspikeyshrub_nocollision`: 0/3) ni en modelos sin
  colisión propia (`lamp_plain_off_d`, `light_ext_lamp1off`: `LOD de colisión = −1`).
- Los resultados parciales (hierba, sillas, cajas pequeñas) corresponden a modelos sin colisión o
  más bajos que la altura del rayo.

Confianza: **probable**. Se mantiene la regla de CLAUDE.md: toda la colisión del mundo sale del BSP.

Pendiente: los `script_model` y los ítems son entidades con colisión propia en tiempo de
ejecución. Se verán con las entidades (hitos 4–6).

## Marcadores de efectos (`xmodel/fx`)

Los `script_model` con `model xmodel/fx` (letras "FX" azules) son marcadores del editor: indican dónde
lanza el script un efecto (`script_fxid`, `script_exploder`, `script_fxcommand`). `maps/_load.gsc`
(línea 208) los oculta al cargar el nivel, salvo los que se llaman `exploderchunk`. El visor aplica
la misma regla mientras no se ejecuten los scripts (hito 4).
