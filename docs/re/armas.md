# Armas y daño

Notas para el hito 5. Fuente: `gamex86.dll` 1.5 (SP), analizado con Ghidra y `tools/re/`.
La lista de las 248 claves del archivo de arma está en `armas-campos.md` (generado).

## Archivo de arma (`weapons/sp/<nombre>`)

- Texto plano que empieza por `WEAPONFILE` y sigue como *info string*: `\clave\valor\...`
  (`0x20009bb0`). Máximo 8191 bytes; error si contiene `"` o `;`.
- Parser genérico (`0x2004b540`, también para la tabla de zonas de impacto). Tipos:
  - 0: cadena (copiada a memoria del juego);
  - 1, 2 y 3: cadena en búfer fijo de 1024, 64 y 256;
  - 4: entero (`atol`);
  - 5: booleano (`atol ≠ 0`);
  - 6: float (`atof`, redondeado a float);
  - 7: **tiempo**: segundos × 1000 truncado a milisegundos enteros. Evidencia: `fmul 1000` en
    `0x2004b62d` y `__ftol` después. Son los `*Time`, `fireDelay` y `meleeDelay`.
  - 8–13: enumeraciones.

| Tipo | Clave | Valores (índice = valor) |
|---|---|---|
| 8 | `weaponType` | `bullet`, `grenade`, `projectile` |
| 9 | `weaponClass` | `rifle`, `mg`, `smg`, `pistol`, `grenade`, `rocketlauncher`, `turret`, `non-player` |
| 10 | `adsOverlayReticle` | `none`, `crosshair`, `FG42`, `Springfield`, `Gewehr43` |
| 11 | `weaponSlot` | `none`, `primary`, `primaryb`, `pistol`, `grenade`, `smokegrenade` |
| 12 | `stance` | `stand`, `duck`, `prone` |
| 13 | `projExplosionType` | `grenade`, `rocket`, `molotov`, `none` |

- Las claves que no aparecen en el archivo quedan a 0 o con cadena vacía. El arma 0 es la vacía
  (`none`).
- Valores derivados al cargar (`0x20009eb0`): `1 / adsTransInTime` y `1 / adsTransOutTime`
  (velocidades de transición de apuntado). Si el tiempo es ≤ 0, se usan 1/300 y 1/500.
- Hay 49 archivos en `weapons/sp/` en la instalación.

## Zonas de impacto (`hitLoc`)

19 zonas (`0x200a5aa0`), en este orden y con este índice: `none`, `helmet`, `head`, `neck`,
`torso_upper`, `torso_lower`, `right_arm_upper`, `left_arm_upper`, `right_arm_lower`,
`left_arm_lower`, `right_hand`, `left_hand`, `right_leg_upper`, `left_leg_upper`,
`right_leg_lower`, `left_leg_lower`, `right_foot`, `left_foot`, `gun`.

- **Multiplicadores de daño de la IA**: archivo `info/ai_lochit_dmgtable` del pk3. Empieza por
  `LOCDMGTABLE` y sigue con `\zona\multiplicador` (`0x200182f0`). Si una zona falta, su
  multiplicador es 1. Se leen en tiempo de carga; no se copian al repo. Hay otro archivo,
  `info/mp_lochit_dmgtable`, que es de multijugador.
- **Prioridad de zona** al trazar la bala contra los modelos: dos tablas de un byte por zona.
  - Bala normal (`0x200a5aec`): `none` 1, todas las demás 3.
  - Bala de rifle (`0x200a5b00`): `none` 1, `helmet`/`head`/`neck` 9, `torso_upper` 8,
    `torso_lower` 7, brazos 6, manos 5, piernas 4, pies 3.

  Cómo se usan en el trazado (lo hace el motor, llamada `0x3b`) está por confirmar.

## Modos de muerte (`MOD_*`)

Índice: 0 `MOD_UNKNOWN`, 1 `PISTOL_BULLET`, 2 `RIFLE_BULLET`, 3 `GRENADE`, 4 `GRENADE_SPLASH`,
5 `PROJECTILE`, 6 `PROJECTILE_SPLASH`, 7 `MELEE`, 8 `HEAD_SHOT`, 9 `MORTAR`, 10 `MORTAR_SPLASH`,
11 `KICKED`, 12 `GRABBER`, 13 `DYNAMITE`, 14 `DYNAMITE_SPLASH`, 15 `AIRSTRIKE`, 16 `WATER`,
17 `SLIME`, 18 `LAVA`, 19 `CRUSH`, 20 `TELEFRAG`, 21 `FALLING`, 22 `SUICIDE`,
23 `TRIGGER_HURT`, 24 `EXPLOSIVE` (tabla `0x200a5b18`).

## `G_Damage` (`0x20018800`)

Parámetros: objetivo, inflictor, atacante, dirección, punto, daño, banderas, MOD y zona.
Orden (confianza **probable** salvo lo marcado):

1. No hace nada si el objetivo no recibe daño (`takedamage`) o si se está cargando partida.
2. **Jugador** como objetivo: con MOD 3–6 (granada, proyectil y sus explosiones), daño =
   `trunc(daño · 0.75)`. Constante `0x2009a1e4`: **confirmado** (binario). Además, si el jugador
   usa cierta torreta, no recibe daño (`0x2003ef40`).
3. **Actor** como objetivo: si tiene la bandera `0x2000` y la zona es `helmet`, pasa a `head`
   (casco ya perdido). Daño = `trunc(daño · multiplicador[zona])`, con la tabla de la IA.
   **Confirmado** (binario, `0x200188a6`).
4. Sin dirección: se marca "sin retroceso" (bandera 8). Con dirección, se normaliza.
5. **Retroceso** (confirmado, binario):
   - `k = trunc(daño · f)`, con `f` = 0.05 de pie, 0.025 agachado (`pm_flags & 2`) y 0.01
     tumbado (`pm_flags & 1`). Máximo 60. Es 0 si la entidad tiene la bandera `0x20`.
   - Si hay retroceso, cliente y no bandera 8: `velocidad += dir · k · g_knockback · 0.004`.
   - Si es su propia explosión (MOD 3–6), la `z` de la velocidad se multiplica por 0.25.
   - Si no hay `pm_time`, `pm_time = clamp(2k, 50, 200)` y `pm_flags |= 0x200` (empujado).
6. Si el objetivo no es invulnerable (bandera `0x1` en `+0x134`): daño mínimo 1. Si es actor,
   resta la armadura (`0x200187e0`).
7. `health -= daño`. Se notifica el daño al script (`notify` con el daño como parámetro).
8. Si `health ≤ 0`: muere. Si baja de −999, se fija en −999; se llama a la función de muerte de
   la entidad. Si no muere, se llama a la de dolor.

`g_knockback` vale 1000 por defecto (ver `dvars.md`).

## Disparo de bala (`Bullet_Fire`, `0x20048b70` / `0x20048ac0` / `0x20048bd0`)

- **Alcance**: 8192 unidades (`0x2009a3fc`).
- **Dispersión** (`0x20048ac0`) para un ángulo de dispersión `s` en grados:
  - `ángulo = rand()/32768 · 360°` y `r = rand()/32768` (`0x2000db80`). Uniforme en radio, no en
    área: concentra los disparos en el centro.
  - `x = r·cos(ángulo)` e `y = r·sin(ángulo)`; el orden seno/coseno está por confirmar.
  - `fin = origen + adelante·8192 + derecha·x·tan(s)·8192 + arriba·y·tan(s)·8192`.
  - `rand()` es el de la CRT de MSVC: `x = x·214013 + 2531011`, devuelve `(x >> 16) & 0x7fff`.
    Su semilla en el original es el reloj real al cargar el nivel (ver `gsc.md`,
    "Aleatoriedad"), así que la secuencia exacta no se puede reproducir. Se reproduce la
    fórmula y la distribución con una semilla fija.
- **Trazado** (`0x20048bd0`): máscara `0x280e031`. Con `rifleBullet 1`: MOD 2 (rifle), bandera
  `0x40` y la tabla de prioridad de rifle; si no, MOD 1 (pistola).
- **Atraviesa** (recursivo, como máximo 12 veces; con más, "Too many resursions"):
  - superficies con la bandera `0x10` en el resultado del trazado: sigue desde el punto de
    impacto, adelantado 0.25 unidades (con un umbral de 0.125);
  - vehículos que lleva la IA y compañeros de equipo de un actor que dispara (la IA no se hiere
    entre sí);
  - con bala de rifle, tras dañar a un actor, sigue con **la mitad del daño** si el resultado
    es ≥ 1.
- Daño: el `damage` del arma, por `G_Damage` con la zona del trazado.
- Efectos: evento de impacto en la carne o en la superficie, con su tipo de superficie (bits 20–24
  de las banderas de superficie) y la normal comprimida.

## Trazado contra personajes: cajas por hueso (`CoDSP.exe`)

Confianza **confirmado** (binario) salvo lo indicado.

- La llamada del juego `0x3b` (y `0x32`/`0x33`, que comparten la función `0x44e6c0`) traza
  primero contra el mundo y luego contra las entidades del árbol de áreas (`0x420390`; recorre
  nodos con eje y distancia, como `sv_world.c` de Quake 3).
- **Por entidad** (`0x44e110`), si el trazado pide zonas (lleva tabla de prioridad) y la entidad
  tiene modelo animado y la bandera de servidor `0x8` (actor) o `0x10` (cliente):
  1. **Rechazo rápido** contra una caja. Actores: (−64, −64, −32)–(64, 64, 72) alrededor del
     origen (`0x5689e0`). Clientes: los límites del modelo.
  2. Se pide al juego que actualice la pose del modelo en este instante (mensaje 0x15 de
     `vmMain`) y se calculan las matrices de los huesos.
  3. **Trazado contra los huesos** (`0x4776b0`):
     - Cada hueso tiene una caja en su espacio local y una zona de impacto. Las dos salen de
       `xmodelparts`, ver `docs/formats/xmodel.md`.
     - Se descarta el hueso si su prioridad (tabla de prioridad de la bala, indexada por la zona)
       es menor que la mejor encontrada hasta ahora; se empieza en 2, así que la zona `none`, de
       prioridad 1, nunca recibe impactos.
     - También se descarta si la esfera envolvente (centro = mitad de la caja,
       radio² = |máx − centro|²) no corta el rayo, y si el radio² es 0.
     - Si no, se hace la prueba de *slabs* contra la caja en el espacio del hueso.
     - **Gana la prioridad más alta aunque esté más lejos**; a igual prioridad, el impacto más
       cercano. Con bala de rifle (cabeza 9 > torso 8 > ...), un rayo que atraviesa torso y
       cabeza da en la cabeza. Con bala normal (todo 3), da en lo primero que toca.
     - Si el origen está dentro de la caja, la fracción es 0.
  4. El resultado lleva la zona y el hueso.
- Sin zonas, la entidad se trata como caja o modelo de brushes (`0x41d3b0`), como en Quake 3.
- Trazado con extensión (caja) contra entidades: `0x41fe80`; no usa los huesos.

## Pendiente (hito 5)

- Dispersión del jugador: cómo combinan `hipSpread*`, `adsSpread`, postura y movimiento (está
  en la parte compartida `bg_*`/pmove).
- Precisión de la IA: `accuracy` del actor, `ai_player{Near,Far}{Accuracy,Range}`,
  `g_accuracyStance*Mod` (ver `dvars.md` e `ia.md`).
- Granadas y proyectiles: trayectorias, `fuseTime`, `explosionRadius`, `explosion{Inner,Outer}Damage`
  y `G_RadiusDamage`.
- Cuerpo a cuerpo: `meleeDamage`, `meleeTime`, alcance.
- Estados del arma (disparo, recarga, cambio) en el pmove: tiempos `*Time` y `*Delay`.
