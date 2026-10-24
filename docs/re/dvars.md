# Dvars del juego (gamex86.dll 1.5, un jugador)

Apéndice generado con `tools/re/gen_docs.py` desde la tabla `cvarTable` de `gamex86.dll` (registros de 24 bytes `{vmCvar*, nombre, valor, flags, modificationCount, trackChange}`, como `g_main.c` de Quake 3), 99 entradas. Los flags son los de `cvar_t` del motor; su significado exacto en CoD1 está por confirmar (`0x200` aparece en todas las de depuración y `0x8` en `pmove_*`).

| Dvar | Valor por defecto | Flags | Avisa al cambiar |
|---|---|---|---|
| `sv_cheats` | `` | `0x0` |  |
| `gamename` | `main` | `0x44` |  |
| `gamedate` | `Mar  2 2004` | `0x40` |  |
| `sv_mapname` | `` | `0x44` |  |
| `g_gameskill` | `1` | `0x64` |  |
| `g_reloading` | `0` | `0x40` |  |
| `g_speed` | `190` | `0x0` | sí |
| `g_gravity` | `800` | `0x0` | sí |
| `g_knockback` | `1000` | `0x0` | sí |
| `g_weaponrespawn` | `5` | `0x0` | sí |
| `g_weaponAmmoPools` | `0` | `0x0` | sí |
| `g_debugMove` | `0` | `0x0` |  |
| `g_debugProneCheck` | `0` | `0x0` |  |
| `g_debugProneCheckDepthCheck` | `1` | `0x0` |  |
| `g_debugDamage` | `0` | `0x0` |  |
| `g_debugBullets` | `0` | `0x200` |  |
| `g_vehicleDrawPath` | `0` | `0x200` |  |
| `g_vehicleDebug` | `0` | `0x200` |  |
| `g_vehicleTexScrollScale` | `0` | `0x200` |  |
| `g_listEntity` | `0` | `0x0` |  |
| `developer` | `0` | `0x100` |  |
| `pmove_fixed` | `0` | `0x8` |  |
| `pmove_msec` | `8` | `0x8` |  |
| `g_entinfo` | `0` | `0x200` |  |
| `g_entinfo_maxdist` | `2048` | `0x200` |  |
| `g_entinfo_scale` | `1` | `0x200` |  |
| `g_drawEntBBoxes` | `0` | `0x200` |  |
| `g_ai` | `1` | `0x200` |  |
| `g_spawnai` | `1` | `0x220` |  |
| `ai_corpseCount` | `5` | `0x1` |  |
| `ai_showNodes` | `0` | `0x200` |  |
| `ai_showNodesDist` | `384` | `0x200` |  |
| `ai_showNearestNode` | `0` | `0x200` |  |
| `ai_showFriendlyChains` | `0` | `0x200` |  |
| `ai_showVisData` | `0` | `0x200` |  |
| `ai_showVisDataDist` | `1000` | `0x200` |  |
| `ai_debugCoverEntityNum` | `-6969` | `0x200` |  |
| `ai_showBadPlaces` | `0` | `0x200` |  |
| `ai_showDodge` | `0` | `0x200` |  |
| `ai_noDodge` | `0` | `0x200` |  |
| `ai_debugMayMove` | `0` | `0x200` |  |
| `ai_showPaths` | `0` | `0x200` |  |
| `ai_debugFindPath` | `0` | `0x200` |  |
| `ai_debugAnimDeltas` | `0` | `0x200` |  |
| `ai_ShowCanshootChecks` | `0` | `0x1` |  |
| `ai_debugThreat` | `0` | `0x200` |  |
| `ai_badPathSpam` | `0` | `0x1` |  |
| `ai_playerNearAccuracy` | `0.5` | `0x0` |  |
| `ai_playerNearRange` | `800` | `0x0` |  |
| `ai_playerFarAccuracy` | `0.1` | `0x0` |  |
| `ai_playerFarRange` | `2000` | `0x0` |  |
| `com_dumpingdxf` | `0` | `0x0` |  |
| `bg_viewheight_standing` | `60` | `0x200` |  |
| `bg_viewheight_crouched` | `40` | `0x200` |  |
| `bg_viewheight_prone` | `11` | `0x200` |  |
| `bg_duck2prone_time` | `400` | `0x200` |  |
| `bg_prone2duck_time` | `400` | `0x200` |  |
| `bg_ladder_yawcap` | `100` | `0x200` |  |
| `bg_prone_yawcap` | `85` | `0x200` |  |
| `bg_prone_softyawedge` | `1` | `0x200` |  |
| `bg_foliagesnd_minspeed` | `40` | `0x200` |  |
| `bg_foliagesnd_maxspeed` | `180` | `0x200` |  |
| `bg_foliagesnd_slowinterval` | `1500` | `0x200` |  |
| `bg_foliagesnd_fastinterval` | `500` | `0x200` |  |
| `bg_foliagesnd_resetinterval` | `500` | `0x200` |  |
| `bg_fallDamageMinHeight` | `256` | `0x208` |  |
| `bg_fallDamageMaxHeight` | `480` | `0x208` |  |
| `bg_debugWeaponAnim` | `0` | `0x200` |  |
| `bg_debugWeaponState` | `0` | `0x200` |  |
| `g_bounds_width` | `30` | `0x200` |  |
| `g_bounds_height_standing` | `70` | `0x200` |  |
| `g_player_maxhealth` | `600` | `0x60` |  |
| `g_no_script_spam` | `0` | `0x0` |  |
| `g_connectpaths` | `0` | `0x0` |  |
| `g_ignorePathErrors` | `0` | `0x100` |  |
| `g_playerTrailTime` | `500` | `0x200` |  |
| `g_accuracyStanceProneMod` | `0.5` | `0x200` |  |
| `g_accuracyStanceCrouchMod` | `0.75` | `0x200` |  |
| `g_dumpAnims` | `-1` | `0x200` |  |
| `ai_NoCriticalSections` | `1` | `0x100` |  |
| `ai_disableSpawn` | `0` | `0x200` |  |
| `cg_weaponSelect` | `0` | `0x40` |  |
| `cg_weaponSelectTime` | `0` | `0x40` |  |
| `cl_stance` | `0` | `0x0` |  |
| `cl_run` | `1` | `0x0` |  |
| `cg_deadscreen_backdrop` | `` | `0x0` |  |
| `cg_deadscreen_levelname` | `` | `0x0` |  |
| `cg_victoryscreen_backdrop` | `` | `0x0` |  |
| `cg_victoryscreen_levelname` | `` | `0x0` |  |
| `g_changelevel_time` | `4` | `0x0` |  |
| `g_lastSaveGame` | `` | `0x1` |  |
| `g_internalSaveGame` | `` | `0x40` |  |
| `g_friendlyfireDist` | `256` | `0x0` |  |
| `g_friendlyfireUseDist` | `256` | `0x0` |  |
| `g_friendlyfireUseTime` | `500` | `0x0` |  |
| `g_useholdtime` | `10` | `0x1` | sí |
| `g_useGear` | `1` | `0x21` |  |
| `cl_languagewarnings` | `1` | `0x0` |  |
| `cl_languagewarningsaserrors` | `0` | `0x0` |  |
