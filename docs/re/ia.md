# IA (actores)

Notas para el hito 6. Fuente: `gamex86.dll` 1.5 (SP), analizado con Ghidra y `tools/re/`.
Campos y métodos completos en `gsc-api.md`; dvars en `dvars.md`.

## Piezas

- **Actor** (`actor_t`): la IA de un soldado. Se crea desde una entidad `actor_*` del BSP, que es
  un *spawner* (`0x2004c1e0`); `dospawn`/`stalingradspawn` lo instancian. Error "Actor allocation
  failed" si no hay ranuras.
- **Sentient** (`sentient_t`): lo que comparten actores y jugador para la percepción y el
  combate: equipo, `threatbias`, `node`, `enemy`, `goalradius`... Error "Sentient allocation
  failed".
- **Nodos de caminos**: entidades `node_*` del BSP (17 tipos, ver `entidades.md`). Los caminos
  entre nodos se precalculan (`g_connectpaths`). Si el mapa no los tiene, se avisa: "Map does not
  have paths generated".
- **Animación**: la decide el GSC de `animscripts/` (el comportamiento de alto nivel está en
  script). El motor ofrece los métodos de animación (`setanim*`, `setflaggedanim*`, `animmode`,
  `orientmode`, `traversemode`) y los de percepción y movimiento (`cansee`, `canshoot`,
  `maymovetopoint`, `setgoal*`, `approxpathdist`, `predictanim`...).

Consecuencia para el proyecto: gran parte de la IA es GSC del juego (`animscripts/*.gsc`,
`maps/_spawner.gsc`...) y funcionará con la VM. Lo que hay que reimplementar en `/sim` son los
59 métodos de actor, los 5 de sentient, los 43 + 9 campos y el pensamiento del actor.

## Campos de actor

43 campos en `0x20089760` y 9 de sentient en `0x20089200`. Destacan los de percepción
(`fovcosine`, `maxsightdistsqrd`, `visibilitythreshold`, `defaultsightlatency`), combate
(`accuracy`, `accuracystationarymod`, `bravery`, `suppressionwait`, `grenadeawareness`),
movimiento (`goalradius`, `walkdist`, `followmin`/`followmax`, `interval`, `personalspace`) y
estado del script (`scriptstate`, `lastscriptstate`, `statechangereason`). Algunos tienen función
de lectura o escritura propia (validación).

## Precisión contra el jugador

### Actor que dispara (`0x20055d80`)

Confianza: **probable**; las constantes están confirmadas en el binario.

1. Distancia `d` al objetivo, con los rangos del arma `aiEffectiveRange` (E) y `aiMissRange` (M):
   - si `d < E`: `t = 0`, `g = 1`;
   - si no: `t = (d − E) / (M − E)` cuando `d < M`, o `t = d / M` cuando `d ≥ M`; y
     `g = max(0, 1 − d / (3M))`.
2. Base: `quieto = g · accuracystationarymod` y `moviéndose = max(0, (1 − t) · accuracy)`.
3. Si el objetivo es el jugador, `q = 1 − movimiento` (`0x20055c20`, entre 0 y 1); si no,
   `q = 0`. Mezcla: `q · quieto + (1 − q) · moviéndose`.
4. Postura del jugador (`pm_flags`): tumbado × `g_accuracyStanceProneMod` (0.5), agachado ×
   `g_accuracyStanceCrouchMod` (0.75).
5. Visibilidad: 3 trazados a partes del objetivo (`0x2006a1e0`).
   - Con 3 visibles: × postura.
   - Con 2: × 0.8 · postura.
   - Con 1: × 0.5 · postura.
   - Con 0: × 0.1.

   Constantes `0x20089d18`, `0x20089d1c` y `0x2009a354`.
6. Si el objetivo es el jugador y `g_gameskill` es 0 o 1 (fácil o normal), × 0.5.

`TODO(fidelidad)`: confirmar qué campos son `+0xd4`/`+0xd8` del actor (por el desplazamiento son
`accuracy` y `accuracystationarymod`) y la fórmula de `movimiento`.

### Torreta o tirador sin actor (`0x20056f80`)

Interpola entre `ai_playerNearAccuracy` (0.5) a `ai_playerNearRange` (800) y
`ai_playerFarAccuracy` (0.1) a `ai_playerFarRange` (2000), y multiplica por 5. Si hay un actor
manejándola, usa su precisión × un factor de la torreta.

## Dificultad

- `g_gameskill` (0–3, por defecto 1) se lee en el juego y en el ejecutable. En el juego se usa al
  menos en la precisión (punto 6) y `getdifficulty` la expone al script.
- `g_player_maxhealth` vale 600 por defecto. Los scripts (`maps/_gameskill.gsc` y similares)
  ajustan la salud y otros valores por dificultad. Revisarlos en el hito 4.

## Pendiente (hito 6)

- Pensamiento del actor: dónde se llama (fase 6 de `G_RunFrame`, ver `entidades.md`) y su orden
  interno (percepción → decisión del script → movimiento → animación).
- Percepción: cálculo de visibilidad con `fovcosine`, `maxsightdistsqrd`, latencia y umbral.
- Búsqueda de caminos (A* sobre nodos), `badplace_*` y esquivar granadas.
- Movimiento por animación (*root motion*): `animmode`, `getmovedelta`.
- `threatbias` y elección de enemigo.
