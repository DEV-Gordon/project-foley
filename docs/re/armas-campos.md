# Archivo de arma: campos (gamex86.dll 1.5, un jugador)

Apéndice generado con `tools/re/gen_docs.py`. Tabla `{clave, desplazamiento, tipo}` en `0x200a63b0`, 248 campos (se pasan 0xf8 = 248 al parser). La prosa está en `armas.md`.

| Clave | Desp. | Tipo |
|---|---|---|
| `displayName` | 8 | cadena |
| `AIOverlayDescription` | 12 | cadena |
| `modeName` | 108 | cadena |
| `gunModel` | 16 | cadena |
| `handModel` | 20 | cadena |
| `idleAnim` | 28 | cadena |
| `emptyIdleAnim` | 32 | cadena |
| `fireAnim` | 36 | cadena |
| `holdFireAnim` | 40 | cadena |
| `lastShotAnim` | 44 | cadena |
| `rechamberAnim` | 48 | cadena |
| `meleeAnim` | 52 | cadena |
| `reloadAnim` | 56 | cadena |
| `reloadEmptyAnim` | 60 | cadena |
| `reloadStartAnim` | 64 | cadena |
| `reloadEndAnim` | 68 | cadena |
| `raiseAnim` | 72 | cadena |
| `dropAnim` | 76 | cadena |
| `altRaiseAnim` | 80 | cadena |
| `altDropAnim` | 84 | cadena |
| `adsFireAnim` | 88 | cadena |
| `adsLastShotAnim` | 92 | cadena |
| `adsRechamberAnim` | 96 | cadena |
| `adsUpAnim` | 100 | cadena |
| `adsDownAnim` | 104 | cadena |
| `script` | 1040 | cadena |
| `weaponType` | 112 | enum weaponType |
| `weaponClass` | 116 | enum weaponClass |
| `weaponSlot` | 120 | enum weaponSlot |
| `slotStackable` | 124 | bool |
| `viewFlashEffect` | 132 | cadena |
| `worldFlashEffect` | 136 | cadena |
| `pickupSound` | 140 | cadena |
| `ammoPickupSound` | 144 | cadena |
| `projectileSound` | 148 | cadena |
| `pullbackSound` | 152 | cadena |
| `fireSound` | 156 | cadena |
| `loopFireSound` | 160 | cadena |
| `stopFireSound` | 164 | cadena |
| `fireEchoSound` | 168 | cadena |
| `lastShotSound` | 172 | cadena |
| `rechamberSound` | 176 | cadena |
| `reloadSound` | 180 | cadena |
| `reloadEmptySound` | 184 | cadena |
| `reloadStartSound` | 188 | cadena |
| `reloadEndSound` | 192 | cadena |
| `raiseSound` | 196 | cadena |
| `altSwitchSound` | 200 | cadena |
| `putawaySound` | 204 | cadena |
| `noteTrackSoundA` | 208 | cadena |
| `noteTrackSoundB` | 212 | cadena |
| `noteTrackSoundC` | 216 | cadena |
| `noteTrackSoundD` | 220 | cadena |
| `shellEjectEffect` | 224 | cadena |
| `lastShotEjectEffect` | 228 | cadena |
| `reticleCenter` | 232 | cadena |
| `reticleSide` | 236 | cadena |
| `reticleCenterSize` | 240 | int |
| `reticleSideSize` | 244 | int |
| `reticleMinOfs` | 248 | int |
| `standMoveF` | 252 | float |
| `standMoveR` | 256 | float |
| `standMoveU` | 260 | float |
| `standRotP` | 264 | float |
| `standRotY` | 268 | float |
| `standRotR` | 272 | float |
| `duckedOfsF` | 276 | float |
| `duckedOfsR` | 280 | float |
| `duckedOfsU` | 284 | float |
| `duckedMoveF` | 288 | float |
| `duckedMoveR` | 292 | float |
| `duckedMoveU` | 296 | float |
| `duckedRotP` | 300 | float |
| `duckedRotY` | 304 | float |
| `duckedRotR` | 308 | float |
| `proneOfsF` | 312 | float |
| `proneOfsR` | 316 | float |
| `proneOfsU` | 320 | float |
| `proneMoveF` | 324 | float |
| `proneMoveR` | 328 | float |
| `proneMoveU` | 332 | float |
| `proneRotP` | 336 | float |
| `proneRotY` | 340 | float |
| `proneRotR` | 344 | float |
| `posMoveRate` | 348 | float |
| `posProneMoveRate` | 352 | float |
| `standMoveMinSpeed` | 356 | float |
| `duckedMoveMinSpeed` | 360 | float |
| `proneMoveMinSpeed` | 364 | float |
| `posRotRate` | 368 | float |
| `posProneRotRate` | 372 | float |
| `standRotMinSpeed` | 376 | float |
| `duckedRotMinSpeed` | 380 | float |
| `proneRotMinSpeed` | 384 | float |
| `radiantName` | 388 | cadena |
| `worldModel` | 392 | cadena |
| `hudIcon` | 396 | cadena |
| `modeIcon` | 400 | cadena |
| `ammoIcon` | 404 | cadena |
| `startAmmo` | 408 | int |
| `ammoName` | 412 | cadena |
| `clipName` | 420 | cadena |
| `maxAmmo` | 428 | int |
| `clipSize` | 432 | int |
| `sharedAmmoCapName` | 436 | cadena |
| `sharedAmmoCap` | 444 | int |
| `damage` | 448 | int |
| `meleeDamage` | 452 | int |
| `fireDelay` | 460 | ms (segundos × 1000, truncado) |
| `meleeDelay` | 464 | ms (segundos × 1000, truncado) |
| `fireTime` | 468 | ms (segundos × 1000, truncado) |
| `rechamberTime` | 472 | ms (segundos × 1000, truncado) |
| `rechamberBoltTime` | 476 | ms (segundos × 1000, truncado) |
| `holdFireTime` | 480 | ms (segundos × 1000, truncado) |
| `meleeTime` | 484 | ms (segundos × 1000, truncado) |
| `reloadTime` | 488 | ms (segundos × 1000, truncado) |
| `reloadEmptyTime` | 492 | ms (segundos × 1000, truncado) |
| `reloadAddTime` | 496 | ms (segundos × 1000, truncado) |
| `reloadStartTime` | 500 | ms (segundos × 1000, truncado) |
| `reloadStartAddTime` | 504 | ms (segundos × 1000, truncado) |
| `reloadEndTime` | 508 | ms (segundos × 1000, truncado) |
| `dropTime` | 512 | ms (segundos × 1000, truncado) |
| `raiseTime` | 516 | ms (segundos × 1000, truncado) |
| `altDropTime` | 520 | ms (segundos × 1000, truncado) |
| `altRaiseTime` | 524 | ms (segundos × 1000, truncado) |
| `fuseTime` | 528 | ms (segundos × 1000, truncado) |
| `moveSpeedScale` | 532 | float |
| `idleCrouchFactor` | 632 | float |
| `idleProneFactor` | 636 | float |
| `gunMaxPitch` | 640 | float |
| `gunMaxYaw` | 644 | float |
| `swayMaxAngle` | 648 | float |
| `swayLerpSpeed` | 652 | float |
| `swayPitchScale` | 656 | float |
| `swayYawScale` | 660 | float |
| `swayHorizScale` | 664 | float |
| `swayVertScale` | 668 | float |
| `swayShellShockScale` | 672 | float |
| `adsSwayMaxAngle` | 676 | float |
| `adsSwayLerpSpeed` | 680 | float |
| `adsSwayPitchScale` | 684 | float |
| `adsSwayYawScale` | 688 | float |
| `adsSwayHorizScale` | 692 | float |
| `adsSwayVertScale` | 696 | float |
| `rifleBullet` | 704 | bool |
| `twoHanded` | 700 | bool |
| `semiAuto` | 708 | bool |
| `boltAction` | 712 | bool |
| `aimDownSight` | 716 | bool |
| `clipOnly` | 724 | bool |
| `cookOffHold` | 720 | bool |
| `wideListIcon` | 728 | bool |
| `adsFire` | 732 | bool |
| `killIcon` | 736 | cadena |
| `wideKillIcon` | 740 | bool |
| `noPartialReload` | 744 | bool |
| `segmentedReload` | 748 | bool |
| `reloadAmmoAdd` | 752 | int |
| `reloadStartAdd` | 756 | int |
| `altWeapon` | 760 | cadena |
| `dropAmmoMin` | 768 | int |
| `dropAmmoMax` | 772 | int |
| `explosionRadius` | 776 | int |
| `explosionInnerDamage` | 780 | int |
| `explosionOuterDamage` | 784 | int |
| `projectileSpeed` | 788 | int |
| `projectileSpeedUp` | 792 | int |
| `projectileModel` | 796 | cadena |
| `projExplosionType` | 800 | enum projExplosionType |
| `projExplosionEffect` | 804 | cadena |
| `projExplosionSound` | 808 | cadena |
| `projImpactExplode` | 812 | bool |
| `projTrailEffect` | 816 | cadena |
| `projectileDLight` | 820 | int |
| `projectileRed` | 824 | float |
| `projectileGreen` | 828 | float |
| `projectileBlue` | 832 | float |
| `adsTransInTime` | 616 | ms (segundos × 1000, truncado) |
| `adsTransOutTime` | 620 | ms (segundos × 1000, truncado) |
| `adsIdleAmount` | 624 | float |
| `adsZoomFov` | 536 | float |
| `adsZoomInFrac` | 540 | float |
| `adsZoomOutFrac` | 544 | float |
| `adsOverlayShader` | 548 | cadena |
| `adsOverlayReticle` | 552 | enum adsOverlayReticle |
| `adsOverlayWidth` | 556 | float |
| `adsOverlayHeight` | 560 | float |
| `adsBobFactor` | 564 | float |
| `adsViewBobMult` | 568 | float |
| `adsAimPitch` | 836 | float |
| `adsCrosshairInFrac` | 840 | float |
| `adsCrosshairOutFrac` | 844 | float |
| `adsReloadTransTime` | 980 | ms (segundos × 1000, truncado) |
| `adsTransBlendTime` | 984 | ms (segundos × 1000, truncado) |
| `adsGunKickPitchMin` | 848 | float |
| `adsGunKickPitchMax` | 852 | float |
| `adsGunKickYawMin` | 856 | float |
| `adsGunKickYawMax` | 860 | float |
| `adsGunKickAccel` | 864 | float |
| `adsGunKickSpeedMax` | 868 | float |
| `adsGunKickSpeedDecay` | 872 | float |
| `adsGunKickStaticDecay` | 876 | float |
| `adsViewKickPitchMin` | 880 | float |
| `adsViewKickPitchMax` | 884 | float |
| `adsViewKickYawMin` | 888 | float |
| `adsViewKickYawMax` | 892 | float |
| `adsViewKickCenterSpeed` | 896 | float |
| `adsSpread` | 908 | float |
| `hipSpreadStandMin` | 572 | float |
| `hipSpreadDuckedMin` | 576 | float |
| `hipSpreadProneMin` | 580 | float |
| `hipSpreadMax` | 584 | float |
| `hipSpreadDecayRate` | 588 | float |
| `hipSpreadFireAdd` | 592 | float |
| `hipSpreadTurnAdd` | 596 | float |
| `hipSpreadMoveAdd` | 600 | float |
| `hipSpreadDuckedDecay` | 604 | float |
| `hipSpreadProneDecay` | 608 | float |
| `hipReticleSidePos` | 612 | float |
| `hipIdleAmount` | 628 | float |
| `hipGunKickPitchMin` | 912 | float |
| `hipGunKickPitchMax` | 916 | float |
| `hipGunKickYawMin` | 920 | float |
| `hipGunKickYawMax` | 924 | float |
| `hipGunKickAccel` | 928 | float |
| `hipGunKickSpeedMax` | 932 | float |
| `hipGunKickSpeedDecay` | 936 | float |
| `hipGunKickStaticDecay` | 940 | float |
| `hipViewKickPitchMin` | 944 | float |
| `hipViewKickPitchMax` | 948 | float |
| `hipViewKickYawMin` | 952 | float |
| `hipViewKickYawMax` | 956 | float |
| `hipViewKickCenterSpeed` | 960 | float |
| `leftArc` | 988 | float |
| `rightArc` | 992 | float |
| `topArc` | 996 | float |
| `bottomArc` | 1000 | float |
| `accuracy` | 1004 | float |
| `vertTurnSpeed` | 1008 | float |
| `horTurnSpeed` | 1012 | float |
| `convergenceTime` | 1016 | float |
| `maxRange` | 1020 | float |
| `animHorRotateInc` | 1024 | float |
| `playerPositionDist` | 1028 | float |
| `stance` | 128 | enum stance |
| `useHintString` | 1032 | cadena |
| `aiEffectiveRange` | 972 | float |
| `aiMissRange` | 976 | float |
