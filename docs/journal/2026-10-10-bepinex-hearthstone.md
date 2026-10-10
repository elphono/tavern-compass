<!-- visuel : dérogation — note de journal -->
# Journal — 2026-10-10 : faire démarrer BepInEx dans Hearthstone (issue #1)

Le mod anti-danse, installé dans l'après-midi, n'avait jamais été chargé : pas de `BepInEx/LogOutput.log`, et à chaque
lancement un `preloader_*.log` à côté de `Hearthstone.exe`. **Corrigé : BepInEx 5.4.23.5 démarre et le mod pose ses
correctifs** (vu à 17:12, jeu lancé par Battle.net, puis fermé).

```
[Info   :Tavern Compass — No Dance] 5/5 required patches applied, 0/1 optional (game 1.0, Unity 6000.3.11f1, BepInEx 5.4.23.5, Tavern Compass — No Dance 0.4.0, flight timeout 3,0 s)
[Message:   BepInEx] Chainloader startup complete
```

## Cause, mesurée

| Mesure | Résultat |
|---|---|
| `preloader_*.log` (5 lancements) | `MissingMethodException: void System.Reflection.Module.GetPEKind(…)` à l'offset IL 0 de `PreloaderRunner.PreloaderPreMain` : à la compilation JIT de la méthode |
| `BepInEx.Preloader.dll` 5.4.23.5 décompilé (`ilspycmd` 8.2) | `PreloaderPreMain` appelle d'abord `PlatformUtils.SetPlatform()`, qui appelle `typeof(object).Module.GetPEKind(…)` |
| `mscorlib.dll` du jeu (`Hearthstone_Data/Managed`, 2,7 Mo), lu en métadonnées | **dépouillé** par l'éditeur de liens d'Unity : `Module` a 23 méthodes, sans `GetPEKind` ; 2 034 types. Témoin, même lecteur : le `mscorlib` de référence net48 a `GetPEKind` (54 méthodes) |
| Version d'Unity (`UnityPlayer.dll`, ProductVersion) | `6000.3.11f1 (3000ef702840)` ; runtime `MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll` **identique au bit près** (SHA-1 `3d49cbf8…`) à celui du lecteur Windows 64 bits Mono de cette version publiée par Unity |
| Ticket officiel BepInEx #1312 (Unity 6000.3.0f1, même trace) | fermé « not planned » le 2026-05-03 : « The game is stripped » ; la réponse est d'installer BepInEx avec des **corlibs non dépouillés** |

Ce n'est donc ni la version de BepInEx, ni celle de Doorstop (4.5.0, déjà la dernière publiée) : c'est le `mscorlib` du jeu.

## Ce qui a été essayé

| Piste | Verdict, et la mesure |
|---|---|
| corlibs de `unity.bepinex.dev` (archive officielle de BepInEx, `corlibs/6000.3.11.zip`, SHA-256 `6c1e78b0…14b0`, mesurée : aucune empreinte publiée) | **inutilisable** : c'est la version **Unix** (`Interop.Sys`, 28 P/Invoke vers `System.Native`, chemins macOS) ; celui du jeu est la version Windows (`Interop.Kernel32`, aucun `System.Native`). UnityDataMiner les prend dans le profil `4.5` de l'éditeur Linux ; ticket UnityDataMiner #3, ouvert : le jeu Windows plante alors au démarrage (`DllNotFoundException: System.Native`) |
| Installeur de l'éditeur Windows (`UnitySetup64-6000.3.11f1.exe`, 4,18 Go, MD5 conforme à l'API d'Unity) | NSIS à longueurs 64 bits : 7-Zip 25.01 refuse de l'ouvrir. Abandonné |
| Module « Windows Mono Support » pour éditeurs Mac/Linux (`.pkg`, 380 Mo) | runtime seulement, aucune bibliothèque de classes ; a servi à prouver l'identité du runtime (ci-dessus) |
| **Éditeur Linux** (`Unity-6000.3.11f1.tar.xz`, 4,46 Go, MD5 `Eb19GJTSaJMe2ExISQJMOA==` conforme, SHA-256 `0ff52b69…0078`) | **retenu** : il contient `Editor/Data/MonoBleedingEdge/lib/mono/unityjit-win32`, version Windows (`Interop.Kernel32`, aucun `System.Native`, `Module` à 59 méthodes avec `GetPEKind`). Appariement au jeu : tous les types et méthodes du `mscorlib`, du `System` et du `System.Core` dépouillés y sont (seuls écarts : types imbriqués homonymes et souches `$__Stripped*`, artefacts du lecteur) ; le profil voisin `net_4_x-win32` apparie moins bien (un type, 35 méthodes manquent) |
| BepInEx 6 | non essayé, inutile : la cause est le `mscorlib` du jeu, et BepInEx 5 démarre une fois ce `mscorlib` remplacé |

## Ce qui marche

- `tools/nodance-corlibs.sh` : lit la version d'Unity du jeu, télécharge une fois l'éditeur Linux de cette version
  dans `lib/unity/<version>/` (ignoré par git), le vérifie contre le MD5 publié par l'API d'Unity, extrait du profil
  `unityjit-win32` les 12 bibliothèques que le jeu livre parmi les corlibs de BepInEx (UnityDataMiner) — `mscorlib`,
  `System`, `System.Core`, `System.Configuration`, `System.Data`, `System.Net.Http`, `System.Numerics`,
  `System.Runtime.Serialization`, `System.Security`, `System.Xml`, `System.Xml.Linq`, `Mono.Security` —, refuse une
  version Unix et compare les SHA-256 à `tools/nodance-corlibs.sha256` (versionné ; pour une nouvelle version, il
  imprime les lignes à ajouter et rend 3). Éprouvé par mutation d'une empreinte du manifeste (refus, écart nommé).
- `tools/nodance-deploy.sh` les installe dans `BepInEx/unstripped_corlib/` (avec `UNITY_VERSION`) et règle
  `dll_search_path_override = BepInEx\unstripped_corlib` dans `doorstop_config.ini` (une seule ligne changée, CRLF
  gardés ; une autre valeur déjà présente n'est jamais écrasée). Doorstop 4.5.0 (source lue) met ce dossier en tête du
  chemin de Mono et substitue les images chargées en mémoire. Nouveau : `--yes` (confirmation sans terminal),
  sauvegarde datée à côté du jeu (`E:\JEUX\Hearthstone.nodance-backups\<date>\` : fichiers remplacés, `preloader_*.log`
  déplacés, liste des ajouts) et `--restore`. 19 contrôles sur de faux dossiers de jeu (installation puis restauration =
  inventaire identique au SHA-1 près, purge, valeur étrangère, sauvegarde jamais partagée), 8 mutations toutes tuées.
- Lancement par Battle.net (`--exec="launch WTCG"`, comme Skip combat) : `LogOutput.log` 3 s après le démarrage,
  aucun `preloader_*.log`. Le jeu atteint le menu comme la session précédente (`Player.log` : mêmes étapes
  `Startup stage LaunchGame`, `Initial download done`, aucune erreur de chargement), puis se ferme sur un simple
  `taskkill` sans `/F`. HDT n'a pas été touché.

Mesuré au passage (question laissée ouverte par `2026-10-10-anti-danse.md`) : `Application.version` rend « 1.0 »,
pas la version du jeu.

## Risque, et retour arrière

- **Une mise à jour du jeu qui change la version d'Unity** laisse en place les bibliothèques de l'ancienne : Mono peut
  alors les refuser et le jeu se fermer au démarrage. Rien ne le détecte tout seul. Remède : `tools/nodance-corlibs.sh`
  puis `tools/nodance-deploy.sh` (le script refuse d'installer des bibliothèques d'une autre version que celle du
  jeu), ou, tout de suite, `enabled = false` dans `doorstop_config.ini`.
- Retour arrière de la séance : `tools/nodance-deploy.sh --restore /mnt/e/JEUX/Hearthstone.nodance-backups/20261010-171116`
  (remet `doorstop_config.ini`, l'ancien plugin et les `preloader_*.log`, retire les bibliothèques) ;
  `--uninstall --purge` retire le mod, BepInEx et ses bibliothèques.
- Pas vu : une partie de Battlegrounds avec le mod actif (les correctifs sont posés, leur effet sur la danse reste à
  voir par Ali, `2026-10-10-anti-danse.md` § 7, « Le scénario de vérification d'Ali »). L'installation décrite au § 6
  de ce journal demande désormais `tools/nodance-corlibs.sh` d'abord ; sans lui, `tools/nodance-deploy.sh` s'arrête et
  le dit.
