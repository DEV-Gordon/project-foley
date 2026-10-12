# Red: tablas de campos (`netField_t`)

## Qué son

El estado del jugador (`playerState_t`) y el de cada entidad (`entityState_t`) viajan por red
y en las demos **como delta** respecto al anterior, campo a campo, según una tabla fija:
nombre, offset en la estructura y bits. Es el mecanismo de Quake 3
(`code/qcommon/msg.c`: `playerStateFields`, `entityStateFields`), pero las tablas de CoD1 son
propias (otros campos, orden y anchos).

## Evidencia

- Fuente: imagen en memoria de `CoDMP.exe` 1.5 (el ejecutable está cifrado en disco; se leyó
  con el juego arrancado, ver `docs/re/version.md` para los hashes). Imagen guardada fuera del
  repo, en `project-foley-trabajo/re/`.
- Cada entrada son 12 bytes: puntero al nombre, `int offset`, `int bits`.
- `playerStateFields`: dirección `0x580778`, **103 entradas**, terminada en puntero nulo.
  La referencia una sola función, en `0x44a8a8`.
- `entityStateFields`: dirección `0x580080`, **59 entradas**. Referenciada desde
  `0x40f4e5`, `0x416bb1`, `0x4179f2`, `0x448e23`, `0x4492b1`, `0x4580b2`, `0x46113e`, `0x461183`.
- Hay una tabla vecina de 67 entradas en `0x580348` (los campos de entidad más `absmin`,
  `absmax`, `svFlags`, `singleClient`: probablemente el estado de entidad del lado servidor) y
  otra antes de `0x580778` con `team`, `name[..]`, `attachModelIndex[..]` (probablemente
  `clientState`).
- Confianza: **confirmado**. La lectura del estado del jugador (`0x44a630`, analizada con REA
  sobre la imagen en memoria) recorre esta tabla; con ella se decodifican las 1533
  instantáneas de `Timedemo1.dm_2` y las posiciones caen dentro del mapa.
- `bits`: `0` = float (1 bit de modo + float completo o entero de 13 bits); distinto de 0 =
  entero de `|bits|` bits (el signo no se extiende al leer). El formato exacto está en
  `docs/formats/demo.md`.
- Otras funciones vistas: lectura de N bits de la reserva en `0x4475e0`; CL_ParseSnapshot en
  `0x4173e0` (sin máscara de áreas); campos de objetivos en `0x5bae80` (6 entradas) leídos por
  `0x448ec0`; HUD por `0x4494e0` (sin analizar).
- La demo incluida es del protocolo 2 (versión 1.2); estas tablas son de la 1.5.

## Campos de interés para el pmove

La tabla del jugador incluye, además de los de Q3, valores que el servidor envía al cliente y
que por tanto se pueden leer de las demos: `mins[0..2]`, `maxs[0..2]`, `standViewHeight`,
`crouchViewHeight`, `proneViewHeight`, `deadViewHeight`, `walkSpeedScale`, `runSpeedScale`,
`proneSpeedScale`, `crouchSpeedScale`, `strafeSpeedScale`, `backSpeedScale`,
`leanSpeedScale`, `friction`, `gravity`, `speed`, `jumpTime`, `fJumpOriginZ`, `leanf`,
`vLadderVec[0..2]`, `proneDirection`.

## `playerStateFields` (1.5)

| # | Campo | Offset | Bits |
|---:|---|---:|---:|
| 0 | `commandTime` | 0 | 32 |
| 1 | `origin[1]` | 24 | 0 |
| 2 | `origin[0]` | 20 | 0 |
| 3 | `origin[2]` | 28 | 0 |
| 4 | `viewangles[1]` | 196 | 0 |
| 5 | `viewangles[0]` | 192 | 0 |
| 6 | `iCompassFriendInfo` | 960 | 32 |
| 7 | `eFlags` | 128 | 24 |
| 8 | `bobCycle` | 8 | 8 |
| 9 | `viewHeightCurrent` | 208 | 0 |
| 10 | `eventSequence` | 132 | 8 |
| 11 | `legsAnim` | 112 | 10 |
| 12 | `pm_flags` | 12 | 21 |
| 13 | `delta_angles[1]` | 76 | 16 |
| 14 | `velocity[1]` | 36 | 0 |
| 15 | `velocity[0]` | 32 | 0 |
| 16 | `speed` | 68 | 16 |
| 17 | `mins[0]` | 804 | 0 |
| 18 | `mins[1]` | 808 | 0 |
| 19 | `maxs[0]` | 816 | 0 |
| 20 | `maxs[1]` | 820 | 0 |
| 21 | `maxs[2]` | 824 | 0 |
| 22 | `proneViewHeight` | 828 | -8 |
| 23 | `crouchViewHeight` | 832 | -8 |
| 24 | `standViewHeight` | 836 | -8 |
| 25 | `deadViewHeight` | 840 | -8 |
| 26 | `walkSpeedScale` | 844 | 0 |
| 27 | `runSpeedScale` | 848 | 0 |
| 28 | `proneSpeedScale` | 852 | 0 |
| 29 | `crouchSpeedScale` | 856 | 0 |
| 30 | `strafeSpeedScale` | 860 | 0 |
| 31 | `backSpeedScale` | 864 | 0 |
| 32 | `leanSpeedScale` | 868 | 0 |
| 33 | `friction` | 892 | 0 |
| 34 | `groundEntityNum` | 84 | 10 |
| 35 | `viewHeightTarget` | 204 | -8 |
| 36 | `events[0]` | 136 | 8 |
| 37 | `weapons[0]` | 780 | 32 |
| 38 | `weaponslots[0]` | 788 | 32 |
| 39 | `gravity` | 60 | 16 |
| 40 | `serverCursorHintString` | 908 | -8 |
| 41 | `viewmodelIndex` | 188 | 8 |
| 42 | `events[1]` | 140 | 8 |
| 43 | `events[2]` | 144 | 8 |
| 44 | `events[3]` | 148 | 8 |
| 45 | `delta_angles[0]` | 72 | 16 |
| 46 | `weapon` | 176 | 6 |
| 47 | `movementDir` | 124 | 8 |
| 48 | `viewHeightLerpTarget` | 216 | -8 |
| 49 | `weaponslots[4]` | 792 | 32 |
| 50 | `proneDirection` | 872 | 0 |
| 51 | `aimSpreadScale` | 984 | 0 |
| 52 | `weapAnim` | 980 | 10 |
| 53 | `jumpTime` | 100 | 32 |
| 54 | `velocity[2]` | 40 | 0 |
| 55 | `weaponTime` | 44 | -16 |
| 56 | `proneTorsoPitch` | 880 | 0 |
| 57 | `proneDirectionPitch` | 876 | 0 |
| 58 | `fTorsoPitch` | 968 | 0 |
| 59 | `fWaistPitch` | 972 | 0 |
| 60 | `fTorsoHeight` | 964 | 0 |
| 61 | `weaponstate` | 180 | 4 |
| 62 | `torsoTimer` | 116 | 16 |
| 63 | `torsoAnim` | 120 | 10 |
| 64 | `eventParms[0]` | 152 | 8 |
| 65 | `vLadderVec[0]` | 88 | 0 |
| 66 | `eventParms[3]` | 164 | 8 |
| 67 | `viewHeightLerpDown` | 220 | 1 |
| 68 | `weaponDelay` | 48 | -16 |
| 69 | `eventParms[1]` | 156 | 8 |
| 70 | `viewHeightLerpTime` | 212 | 32 |
| 71 | `eventParms[2]` | 160 | 8 |
| 72 | `vLadderVec[1]` | 92 | 0 |
| 73 | `fWeaponPosFrac` | 184 | 0 |
| 74 | `pm_type` | 4 | 8 |
| 75 | `legsTimer` | 108 | 16 |
| 76 | `fJumpOriginZ` | 104 | 0 |
| 77 | `leanf` | 64 | 0 |
| 78 | `damageEvent` | 228 | 8 |
| 79 | `damageYaw` | 232 | 8 |
| 80 | `damagePitch` | 236 | 8 |
| 81 | `damageCount` | 240 | 7 |
| 82 | `weaponrechamber[0]` | 796 | 32 |
| 83 | `grenadeTimeLeft` | 52 | -16 |
| 84 | `pm_time` | 16 | -16 |
| 85 | `iFoliageSoundTime` | 56 | 32 |
| 86 | `deltaTime` | 8392 | 32 |
| 87 | `serverCursorHint` | 900 | 8 |
| 88 | `serverCursorHintVal` | 904 | 8 |
| 89 | `shellshockIndex` | 988 | 4 |
| 90 | `shellshockTime` | 992 | 32 |
| 91 | `shellshockDuration` | 996 | 16 |
| 92 | `delta_angles[2]` | 80 | 16 |
| 93 | `vLadderVec[2]` | 96 | 0 |
| 94 | `clientNum` | 172 | 8 |
| 95 | `weapons[1]` | 784 | 32 |
| 96 | `weaponrechamber[1]` | 800 | 32 |
| 97 | `viewangles[2]` | 200 | 0 |
| 98 | `viewHeightLerpPosAdj` | 224 | 0 |
| 99 | `mins[2]` | 812 | 0 |
| 100 | `viewlocked` | 884 | 8 |
| 101 | `viewlocked_entNum` | 888 | 16 |
| 102 | `gunfx` | 896 | 8 |

## `entityStateFields` (1.5)

| # | Campo | Offset | Bits |
|---:|---|---:|---:|
| 0 | `pos.trTime` | 16 | 32 |
| 1 | `pos.trBase[0]` | 24 | 0 |
| 2 | `pos.trBase[1]` | 28 | 0 |
| 3 | `pos.trDelta[1]` | 40 | 0 |
| 4 | `pos.trDelta[0]` | 36 | 0 |
| 5 | `angles2[1]` | 108 | 0 |
| 6 | `apos.trBase[1]` | 64 | 0 |
| 7 | `apos.trBase[0]` | 60 | 0 |
| 8 | `pos.trBase[2]` | 32 | 0 |
| 9 | `pos.trDelta[2]` | 44 | 0 |
| 10 | `eventSequence` | 164 | 8 |
| 11 | `eType` | 4 | 8 |
| 12 | `eFlags` | 8 | 24 |
| 13 | `groundEntityNum` | 124 | 10 |
| 14 | `legsAnim` | 204 | 10 |
| 15 | `clientNum` | 144 | 8 |
| 16 | `apos.trBase[2]` | 68 | 0 |
| 17 | `events[0]` | 168 | 8 |
| 18 | `index` | 140 | 9 |
| 19 | `events[1]` | 172 | 8 |
| 20 | `events[2]` | 176 | 8 |
| 21 | `events[3]` | 180 | 8 |
| 22 | `eventParm` | 160 | 8 |
| 23 | `torsoAnim` | 208 | 10 |
| 24 | `surfType` | 136 | 8 |
| 25 | `scale` | 216 | 8 |
| 26 | `otherEntityNum` | 116 | 10 |
| 27 | `fWaistPitch` | 236 | 0 |
| 28 | `pos.trType` | 12 | 8 |
| 29 | `angles2[0]` | 104 | 0 |
| 30 | `fTorsoHeight` | 228 | 0 |
| 31 | `fTorsoPitch` | 232 | 0 |
| 32 | `apos.trType` | 48 | 8 |
| 33 | `solid` | 156 | 24 |
| 34 | `weapon` | 200 | 6 |
| 35 | `apos.trTime` | 52 | 32 |
| 36 | `apos.trDelta[0]` | 72 | 0 |
| 37 | `eventParms[0]` | 184 | 8 |
| 38 | `pos.trDuration` | 20 | 32 |
| 39 | `animMovetype` | 224 | 4 |
| 40 | `eventParms[1]` | 188 | 8 |
| 41 | `apos.trDelta[2]` | 80 | 0 |
| 42 | `eventParms[2]` | 192 | 8 |
| 43 | `eventParms[3]` | 196 | 8 |
| 44 | `leanf` | 212 | 0 |
| 45 | `apos.trDelta[1]` | 76 | 0 |
| 46 | `loopSound` | 132 | 8 |
| 47 | `attackerEntityNum` | 120 | 10 |
| 48 | `iHeadIcon` | 148 | 4 |
| 49 | `iHeadIconTeam` | 152 | 2 |
| 50 | `apos.trDuration` | 56 | 32 |
| 51 | `time` | 84 | 32 |
| 52 | `time2` | 88 | 32 |
| 53 | `origin2[0]` | 92 | 0 |
| 54 | `origin2[1]` | 96 | 0 |
| 55 | `origin2[2]` | 100 | 0 |
| 56 | `angles2[2]` | 112 | 0 |
| 57 | `constantLight` | 128 | 32 |
| 58 | `dmgFlags` | 220 | 32 |

## `playerStateFields` del modo un jugador (CoDSP.exe 1.5)

Imagen en memoria de `CoDSP.exe` 1.5, tabla en `0x5379b0`, **99 entradas**. Los
offsets son los de `playerState_t` del modo un jugador, que es lo que usa `gamex86.dll`
(pmove, `docs/re/pmove.md`); difieren de los de multijugador.

| # | Campo | Offset | Bits |
|---:|---|---:|---:|
| 0 | `commandTime` | 0 | 32 |
| 1 | `pm_type` | 4 | 8 |
| 2 | `bobCycle` | 8 | 8 |
| 3 | `pm_flags` | 12 | 24 |
| 4 | `pm_time` | 16 | -16 |
| 5 | `origin[0]` | 20 | 0 |
| 6 | `origin[1]` | 24 | 0 |
| 7 | `origin[2]` | 28 | 0 |
| 8 | `velocity[0]` | 32 | 0 |
| 9 | `velocity[1]` | 36 | 0 |
| 10 | `velocity[2]` | 40 | 0 |
| 11 | `weaponTime` | 44 | -16 |
| 12 | `weaponDelay` | 48 | -16 |
| 13 | `grenadeTimeLeft` | 52 | -16 |
| 14 | `iFoliageSoundTime` | 56 | 32 |
| 15 | `gravity` | 60 | 16 |
| 16 | `leanf` | 64 | 0 |
| 17 | `speed` | 68 | 16 |
| 18 | `delta_angles[0]` | 72 | 16 |
| 19 | `delta_angles[1]` | 76 | 16 |
| 20 | `delta_angles[2]` | 80 | 16 |
| 21 | `groundEntityNum` | 84 | 11 |
| 22 | `vLadderVec[0]` | 88 | 0 |
| 23 | `vLadderVec[1]` | 92 | 0 |
| 24 | `vLadderVec[2]` | 96 | 0 |
| 25 | `jumpTime` | 100 | 32 |
| 26 | `fJumpPeak` | 104 | 0 |
| 27 | `movementDir` | 108 | 8 |
| 28 | `eFlags` | 112 | 24 |
| 29 | `eventSequence` | 116 | 32 |
| 30 | `events[0]` | 120 | 8 |
| 31 | `events[1]` | 124 | 8 |
| 32 | `events[2]` | 128 | 8 |
| 33 | `events[3]` | 132 | 8 |
| 34 | `eventParms[0]` | 136 | 8 |
| 35 | `eventParms[1]` | 140 | 8 |
| 36 | `eventParms[2]` | 144 | 8 |
| 37 | `eventParms[3]` | 148 | 8 |
| 38 | `clientNum` | 156 | 8 |
| 39 | `weapons[0]` | 756 | 32 |
| 40 | `weapons[1]` | 760 | 32 |
| 41 | `weaponslots[0]` | 764 | 32 |
| 42 | `weaponslots[4]` | 768 | 32 |
| 43 | `weaponrechamber[0]` | 772 | 32 |
| 44 | `weaponrechamber[1]` | 776 | 32 |
| 45 | `weapon` | 160 | 7 |
| 46 | `weapAnim` | 972 | 10 |
| 47 | `weaponstate` | 164 | 4 |
| 48 | `fWeaponPosFrac` | 168 | 0 |
| 49 | `viewangles[0]` | 176 | 0 |
| 50 | `viewangles[1]` | 180 | 0 |
| 51 | `viewangles[2]` | 184 | 0 |
| 52 | `viewHeightTarget` | 188 | -8 |
| 53 | `viewHeightCurrent` | 192 | 0 |
| 54 | `viewHeightLerpTime` | 196 | 32 |
| 55 | `viewHeightLerpTarget` | 200 | -8 |
| 56 | `viewHeightLerpDown` | 204 | 1 |
| 57 | `viewHeightLerpPosAdj` | 208 | 0 |
| 58 | `damageEvent` | 212 | 8 |
| 59 | `damageYaw` | 216 | 8 |
| 60 | `damagePitch` | 220 | 8 |
| 61 | `damageCount` | 224 | 7 |
| 62 | `mins[0]` | 780 | 0 |
| 63 | `mins[1]` | 784 | 0 |
| 64 | `mins[2]` | 788 | 0 |
| 65 | `maxs[0]` | 792 | 0 |
| 66 | `maxs[1]` | 796 | 0 |
| 67 | `maxs[2]` | 800 | 0 |
| 68 | `proneViewHeight` | 804 | -8 |
| 69 | `crouchViewHeight` | 808 | -8 |
| 70 | `standViewHeight` | 812 | -8 |
| 71 | `deadViewHeight` | 816 | -8 |
| 72 | `walkSpeedScale` | 820 | 0 |
| 73 | `runSpeedScale` | 824 | 0 |
| 74 | `proneSpeedScale` | 828 | 0 |
| 75 | `crouchSpeedScale` | 832 | 0 |
| 76 | `strafeSpeedScale` | 836 | 0 |
| 77 | `backSpeedScale` | 840 | 0 |
| 78 | `leanSpeedScale` | 844 | 0 |
| 79 | `proneDirection` | 848 | 0 |
| 80 | `proneDirectionPitch` | 852 | 0 |
| 81 | `proneTorsoPitch` | 856 | 0 |
| 82 | `friction` | 880 | 0 |
| 83 | `viewlocked` | 860 | 8 |
| 84 | `viewlocked_entNum` | 864 | 16 |
| 85 | `linkAngles[0]` | 868 | 0 |
| 86 | `linkAngles[1]` | 872 | 0 |
| 87 | `linkAngles[2]` | 876 | 0 |
| 88 | `gunfx` | 884 | 8 |
| 89 | `aimSpreadScale` | 976 | 0 |
| 90 | `serverCursorHint` | 888 | 8 |
| 91 | `serverCursorHintVal` | 892 | 8 |
| 92 | `serverCursorHintString` | 896 | -8 |
| 93 | `iCompassFriendInfo` | 948 | 32 |
| 94 | `iCompassTankInfo` | 952 | 32 |
| 95 | `viewmodelIndex` | 172 | 8 |
| 96 | `shellshockIndex` | 980 | 4 |
| 97 | `shellshockTime` | 984 | 32 |
| 98 | `shellshockDuration` | 988 | 16 |
