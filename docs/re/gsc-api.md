# API de GSC: built-ins y campos (gamex86.dll 1.5, un jugador)

Apéndice generado con `tools/re/gen_docs.py` a partir de las tablas de registro de `gamex86.dll` (hash en `version.md`). La prosa está en `gsc.md`. Solo nombres, direcciones y tipos.

Cada tabla es una lista `{nombre, función[, developer]}` terminada en `NULL`. Las funciones marcadas con `*` solo existen con `developer 1` (el compilador las ignora si no).

Total: **360** built-ins en 7 tablas.

## Funciones globales

Tabla en `0x200a4a00`, 121 entradas.

|   |   |   |   |   |   |
|---|---|---|---|---|---|
| `print*` | `println*` | `iprintln` | `iprintlnbold` | `print3d` | `line` |
| `getent` | `getentarray` | `getnode` | `getnodearray` | `getallnodes` | `getvehiclenode` |
| `getvehiclenodearray` | `getallvehiclenodes` | `getnumvehicles` | `precachevehicle` | `precacheturret` | `spawn` |
| `spawnvehicle` | `spawnturret` | `spawnstruct` | `assert*` | `isdefined` | `isalive` |
| `isplayer` | `isai` | `issentient` | `getcvar` | `getcvarint` | `getcvarfloat` |
| `setcvar` | `gettime` | `getdifficulty` | `getentbynum` | `getaiarray` | `getspawnerarray` |
| `getspawnerteamarray` | `getweaponmodel` | `getweaponclassname` | `getanimlength` | `animhasnotetrack` | `getbrushmodelcenter` |
| `getkeybinding` | `objective_add` | `objective_delete` | `objective_state` | `objective_string` | `objective_string_nomessage` |
| `objective_position` | `objective_current` | `objective_ring` | `bullettrace` | `getstartorigin` | `getstartangles` |
| `getcycleoriginoffset` | `getmovedelta` | `getangledelta` | `randomint` | `randomfloat` | `randomintrange` |
| `randomfloatrange` | `sin` | `cos` | `tan` | `asin` | `acos` |
| `atan` | `distance` | `distancesquared` | `length` | `lengthsquared` | `closer` |
| `vectordot` | `vectornormalize` | `vectortoangles` | `anglestoup` | `anglestoright` | `anglestoforward` |
| `musicplay` | `musicstop` | `soundfade` | `ambientplay` | `ambientstop` | `precachemodel` |
| `precacheshellshock` | `precacheitem` | `precacheshader` | `precachestring` | `precachemenu` | `savegame` |
| `loadfx` | `playfx` | `playfxontag` | `playloopedfx` | `setcullfog` | `setexpfog` |
| `setculldist` | `radiusdamage` | `setplayerignoreradiusdamage` | `changelevel` | `missionsuccess` | `missionfailed` |
| `cinematic` | `earthquake` | `drawcompassfriendlies` | `bullettracer` | `magicbullet` | `prof_begin` |
| `prof_end` | `getnumparts` | `getpartname` | `weaponfiretime` | `weaponclipsize` | `weaponissemiauto` |
| `weaponisboltaction` | `weapontype` | `badplace_delete` | `badplace_cylinder` | `badplace_arc` | `newhudelem` |
| `resettimeout` |  |  |  |  |  |

## Métodos de entidad (`self` = cualquier entidad)

Tabla en `0x2008a828`, 101 entradas.

|   |   |   |   |   |   |
|---|---|---|---|---|---|
| `moveto` | `movex` | `movey` | `movez` | `movegravity` | `rotateto` |
| `rotatepitch` | `rotateyaw` | `rotateroll` | `rotatevelocity` | `solid` | `notsolid` |
| `animscripted` | `stopanimscripted` | `attach` | `detach` | `detachall` | `getattachsize` |
| `getattachmodelname` | `getattachtagname` | `linkto` | `playerlinkto` | `unlink` | `enablelinkto` |
| `dospawn` | `stalingradspawn` | `getorigin` | `geteye` | `useby` | `istouching` |
| `lockdoor` | `unlockdoor` | `isdoorlocked` | `playsound` | `playloopsound` | `stoploopsound` |
| `iswaitingonsound` | `delete` | `setmodel` | `getnormalhealth` | `setnormalhealth` | `dodamage` |
| `show` | `hide` | `setcontents` | `disconnectpaths` | `connectpaths` | `startfiring` |
| `stopfiring` | `shootturret` | `setmode` | `getturretowner` | `settargetentity` | `cleartargetentity` |
| `setturretteam` | `maketurretusable` | `maketurretunusable` | `setturretaccuracy` | `getturrettarget` | `setcursorhint` |
| `sethintstring` | `clearanim` | `setanimknob` | `setanimknobrestart` | `setanimknoball` | `setanimknoballrestart` |
| `setanim` | `setanimrestart` | `getanimtime` | `setflaggedanimknob` | `setflaggedanimknobrestart` | `setflaggedanimknoball` |
| `setflaggedanimknoballrestart` | `setflaggedanim` | `setflaggedanimrestart` | `useanimtree` | `stopuseanimtree` | `dumpanims` |
| `getstance` | `magicgrenade` | `magicgrenademanual` | `isfiringturret` | `setfriendlychain` | `gettagorigin` |
| `gettagangles` | `shellshock` | `stopshellshock` | `viewkick` | `getentnum` | `locklightvis` |
| `unlocklightvis` | `launch` | `setsoundblend` | `localtoworldcoords` | `getentitynumber` | `enablegrenadetouchdamage` |
| `disablegrenadetouchdamage` | `enablegrenadebounce` | `disablegrenadebounce` | `makefakeai` | `setspawnerteam` |  |

## Métodos de actor (IA)

Tabla en `0x20089540`, 59 entradas.

|   |   |   |   |   |   |
|---|---|---|---|---|---|
| `startscriptedanim` | `shoot` | `melee` | `interruptpoint` | `setaimanims` | `aimatpos` |
| `enterprone` | `exitprone` | `setproneanimnodes` | `updateprone` | `setlookatanimnodes` | `setlookat` |
| `setlookatyawlimits` | `stoplookat` | `canshoot` | `cansee` | `canseeenemy` | `cantattackfromcover` |
| `dropweapon` | `maymovetopoint` | `teleport` | `entercriticalsection` | `leavecriticalsection` | `isincriticalsection` |
| `approxpathdist` | `ispathdirect` | `allowedstances` | `isstanceallowed` | `reacquire` | `issuppressed` |
| `checkgrenadethrow` | `throwgrenade` | `pickupgrenade` | `useturret` | `stopuseturret` | `canuseturret` |
| `traversemode` | `animmode` | `orientmode` | `getmotionangle` | `setturretanim` | `getturret` |
| `beginprediction` | `endprediction` | `lerpposition` | `predictoriginandangles` | `predictanim` | `gethitenttype` |
| `gethityaw` | `getgroundenttype` | `isdeflected` | `trackscriptstate` | `dumphistory` | `animcustom` |
| `canattackenemynode` | `getstandingnode` | `getnegotiationendpos` | `checkprone` | `pushplayer` |  |

## Métodos de "sentient" (actores y jugador)

Tabla en `0x200891d8`, 5 entradas.

|   |   |   |   |   |   |
|---|---|---|---|---|---|
| `setgoalpos` | `setgoalnode` | `setgoalentity` | `getenemysqdist` | `getclosestenemysqdist` |  |

## Métodos de jugador

Tabla en `0x2008b1f8`, 37 entradas.

|   |   |   |   |   |   |
|---|---|---|---|---|---|
| `giveweapon` | `takeweapon` | `takeallweapons` | `getcurrentweapon` | `hasweapon` | `switchtoweapon` |
| `givestartammo` | `givemaxammo` | `getfractionstartammo` | `getfractionmaxammo` | `setorigin` | `setvelocity` |
| `setplayerangles` | `usebuttonpressed` | `attackbuttonpressed` | `meleebuttonpressed` | `isonground` | `setviewmodel` |
| `allowstand` | `allowcrouch` | `allowprone` | `allowleanleft` | `allowleanright` | `openmenu` |
| `openmenunomouse` | `closemenu` | `freezecontrols` | `setreverb` | `islookingat` | `playlocalsound` |
| `setautopickup` | `getweaponslotweapon` | `setweaponslotweapon` | `getweaponslotammo` | `setweaponslotammo` | `getweaponslotclipammo` |
| `setweaponslotclipammo` |  |  |  |  |  |

## Métodos de vehículo

Tabla en `0x2008a600`, 23 entradas.

|   |   |   |   |   |   |
|---|---|---|---|---|---|
| `attachpath` | `startpath` | `setswitchnode` | `setwaitnode` | `setwaitspeed` | `setspeed` |
| `resumespeed` | `joltbody` | `freevehicle` | `getwheelsurface` | `getspeedmph` | `getvehicleowner` |
| `startenginesound` | `stopenginesound` | `makevehicleusable` | `makevehicleunusable` | `addvehicletocompass` | `removevehiclefromcompass` |
| `setturrettargetvec` | `setturrettargetent` | `clearturrettarget` | `fireturret` | `isturretready` |  |

## Métodos de elemento de HUD (`newhudelem`)

Tabla en `0x2008b128`, 14 entradas.

|   |   |   |   |   |   |
|---|---|---|---|---|---|
| `settext` | `setshader` | `settimer` | `settimerup` | `settenthstimer` | `settenthstimerup` |
| `setclock` | `setclockup` | `setvalue` | `fadeovertime` | `scaleovertime` | `moveovertime` |
| `reset` | `destroy` |  |  |  |  |

## Clases de entidad con función de spawn

Tabla `{classname, spawn}` en `0x2008a500`, 31 entradas. Las clases que no están aquí (`worldspawn`, `actor_*`, `node_*`, `item_*`, `weapon_*`, `misc_mg42`...) se tratan por otras vías (ver `entidades.md`).

|   |   |   |   |   |   |
|---|---|---|---|---|---|
| `info_player_start` | `info_null` | `info_notnull` | `info_notnull_big` | `info_grenade_hint` | `func_door` |
| `func_static` | `func_rotating` | `func_bobbing` | `func_pendulum` | `func_group` | `func_door_rotating` |
| `trigger_use` | `trigger_multiple` | `trigger_friendlychain` | `trigger_hurt` | `trigger_once` | `trigger_damage` |
| `trigger_lookat` | `light` | `misc_model` | `misc_mg42` | `misc_turret` | `props_skyportal` |
| `corona` | `sound_blend` | `script_brushmodel` | `script_model` | `script_origin` | `script_vehicle` |
| `script_vehicle_collmap` |  |  |  |  |  |

## Campos accesibles desde GSC

Tipos (switch del lector de campos, `0x20041b60`, y del parser de claves del BSP, `0x20040f70`): 0 = int, 1 = short, 2 = byte, 3 = float, 4 = cadena, 5 = vector, 6 = entidad, 7 = entidad, 8 = entidad, 9 = ?, 10 = nodo, 11 = yaw → vector, 12 = modelo, 13 = (ignorado). El índice de campo que maneja la VM lleva la clase en los bits 14-15: `0x0000` entidad, `0x4000` sentient, `0x8000` actor, `0xc000` cliente (tabla vacía en un jugador).

### Entidad (`gentity_t`) — también son las claves que se leen del BSP

Tabla en `0x2008a200`, 38 campos. Columnas: desplazamiento en `gentity_t`, tipo, función propia de escritura (si no es 0).

| Campo | Desp. | Tipo | Funciones |
|---|---|---|---|
| `classname` | 302 | cadena | `0x20040e80` |
| `origin` | 224 | vector | `0x20038980` |
| `model` | 301 | modelo | `0x20040e80` |
| `spawnflags` | 304 | int | `0x20040e80` |
| `speed` | 392 | float |  |
| `closespeed` | 396 | float |  |
| `target` | 384 | cadena |  |
| `targetname` | 386 | cadena |  |
| `message` | 372 | cadena |  |
| `popup` | 372 | cadena |  |
| `book` | 372 | cadena |  |
| `teamname` | 388 | cadena |  |
| `wait` | 528 | float |  |
| `random` | 532 | float |  |
| `count` | 504 | int |  |
| `health` | 472 | int | `0x20038aa0` |
| `light` | 0 | (ignorado) |  |
| `dmg` | 480 | int |  |
| `angles` | 236 | vector | `0x20038a20` |
| `duration` | 548 | float |  |
| `rotate` | 552 | vector |  |
| `degrees` | 380 | float |  |
| `time` | 392 | float |  |
| `_color` | 580 | vector |  |
| `color` | 580 | vector |  |
| `key` | 592 | int |  |
| `delay` | 540 | float |  |
| `radius` | 536 | int |  |
| `start_size` | 600 | int |  |
| `end_size` | 604 | int |  |
| `shard` | 504 | int |  |
| `spawnitem` | 608 | cadena |  |
| `track` | 636 | cadena |  |
| `groupname` | 638 | cadena |  |
| `export` | 640 | cadena |  |
| `script_noteworthy` | 642 | cadena |  |
| `maxhealth` | 476 | int |  |
| `anglelerprate` | 700 | float |  |

### Actor

Tabla en `0x20089760`, 43 campos. Columnas: desplazamiento en `actor_t`, tipo, función de escritura, función de lectura.

| Campo | Desp. | Tipo | Funciones |
|---|---|---|---|
| `accuracy` | 212 | float |  |
| `accuracystationarymod` | 216 | float |  |
| `lookforward` | 252 | vector | `0x2005b830` |
| `lookright` | 264 | vector | `0x2005b830` |
| `lookup` | 276 | vector | `0x2005b830` |
| `fovcosine` | 2008 | float | `0x2005b7a0` |
| `maxsightdistsqrd` | 2012 | float |  |
| `visibilitythreshold` | 2004 | float | `0x2005b7a0` |
| `defaultsightlatency` | 2016 | int |  |
| `followmin` | 1888 | int |  |
| `followmax` | 1892 | int |  |
| `chainfallback` | 1932 | short |  |
| `interval` | 1896 | float |  |
| `personalspace` | 1900 | float |  |
| `damagetaken` | 540 | int | `0x2005b830` |
| `damagedir` | 548 | vector | `0x2005b830` |
| `damageyaw` | 544 | int | `0x2005b830` |
| `damagelocation` | 560 | cadena | `0x2005b830` |
| `proneok` | 392 | int | `0x2005b830` |
| `walkdist` | 1876 | float |  |
| `desiredangle` | 328 | float | `0x2005b830` |
| `bravery` | 1940 | float |  |
| `pacifist` | 1944 | int |  |
| `pacifistwait` | 1948 | int | `0x2005b890` `0x2005b8e0` |
| `suppressionwait` | 4444 | int | `0x2005b890` `0x2005b8e0` |
| `name` | 236 | cadena |  |
| `weapon` | 238 | cadena |  |
| `dontavoidplayer` | 1928 | int |  |
| `grenadeawareness` | 4448 | float | `0x2005b7a0` |
| `grenade` | 4452 | entidad | `0x2005b830` |
| `grenadeweapon` | 4456 | int | `0x2005b900` `0x2005b930` |
| `grenadeammo` | 4468 | int |  |
| `favoriteenemy` | 4344 | entidad |  |
| `allowdeath` | 204 | int |  |
| `useable` | 4536 | byte |  |
| `goalradiusonly` | 1934 | byte |  |
| `dropweapon` | 4540 | int |  |
| `drawoncompass` | 4544 | int |  |
| `scriptstate` | 4552 | cadena | `0x2005b830` |
| `lastscriptstate` | 4554 | cadena | `0x2005b830` |
| `statechangereason` | 4556 | cadena | `0x2005b830` |
| `groundtype` | 660 | cadena | `0x2005b830` `0x2005b960` |
| `anim_pose` | 376 | cadena | `0x2005b990` |

### Sentient

Tabla en `0x20089200`, 9 campos. Columnas: desplazamiento en `sentient_t`, tipo, escritura, lectura.

| Campo | Desp. | Tipo | Funciones |
|---|---|---|---|
| `team` | 4 | int | `0x20074130` `0x200741d0` |
| `threatbias` | 40 | int |  |
| `scariness` | 44 | float |  |
| `node` | 64 | nodo | `0x20074100` |
| `chainnode` | 76 | nodo | `0x20074100` |
| `goalradius` | 24 | float | `0x20074230` |
| `goalangletolerance` | 36 | float | `0x200742a0` |
| `enemy` | 52 | entidad | `0x20074100` |
| `ignoreme` | 48 | int |  |

### Elemento de HUD

Tabla en `0x2008b048`, 10 campos. Columnas: desplazamiento, tipo, escritura, lectura.

| Campo | Desp. | Tipo | Funciones |
|---|---|---|---|
| `x` | 4 | int |  |
| `y` | 8 | int |  |
| `fontscale` | 12 | float | `0x20019c00` |
| `font` | 16 | int | `0x20019c60` `0x20019c90` |
| `alignx` | 20 | int | `0x20019cc0` `0x20019cf0` |
| `aligny` | 24 | int | `0x20019d20` `0x20019d50` |
| `color` | 28 | int | `0x200199a0` `0x20019af0` |
| `alpha` | 28 | int | `0x20019b50` `0x20019bd0` |
| `label` | 44 | int | `0x20019940` |
| `sort` | 108 | float |  |

### Nodo de caminos (`getnode`)

Tabla en `0x200a0098`, 9 campos. Columnas: desplazamiento en `pathnode_t`, tipo, escritura.

| Campo | Desp. | Tipo | Funciones |
|---|---|---|---|
| `targetname` | 44 | cadena |  |
| `target` | 48 | cadena |  |
| `animscript` | 50 | cadena |  |
| `script_noteworthy` | 46 | cadena |  |
| `origin` | 56 | vector |  |
| `angles` | 68 | yaw → vector |  |
| `radius` | 72 | float |  |
| `spawnflags` | 42 | short |  |
| `type` | 36 | int | `0x2006d6e0` |

### Nodo de vehículo (`getvehiclenode`)

Tabla en `0x200a4768`, 6 campos. Columnas: desplazamiento, tipo.

| Campo | Desp. | Tipo | Funciones |
|---|---|---|---|
| `targetname` | 0 | cadena |  |
| `target` | 2 | cadena |  |
| `origin` | 20 | vector |  |
| `angles` | 44 | vector |  |
| `speed` | 12 | float |  |
| `lookahead` | 16 | float |  |

