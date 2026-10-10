#!/usr/bin/env bash
# Installs the anti-dance mod (mods/TavernCompass.NoDance) into the Hearthstone folder, from WSL.
#   tools/nodance-deploy.sh [--game-dir DIR] [--dry-run] [--yes]     install BepInEx and its corlibs if needed, copy the mod
#   tools/nodance-deploy.sh --uninstall [--purge] [--game-dir DIR] [--dry-run] [--yes]
#   tools/nodance-deploy.sh --restore BACKUP [--game-dir DIR] [--dry-run] [--yes]          undo one install run
# DIR defaults to $HEARTHSTONE_DIR, else /mnt/e/JEUX/Hearthstone. The script shows the target and what it will do,
# asks for 'y' (--yes answers it, for a run without a terminal), checks what it wrote (SHA-1), and prints the way back.
# --dry-run checks and shows, writes nothing. An install saves every file it replaces, and moves the preloader_*.log
# that BepInEx left in the game folder, into a dated folder next to the game (<DIR>.nodance-backups/<date>/), with the
# list of the files it added: --restore BACKUP undoes that run (what BepInEx writes later, config/ and its logs, stays).
# Hearthstone's mscorlib is stripped, and BepInEx 5 dies at its start without complete class libraries of the game's
# own Unity version (docs/journal/2026-10-10-bepinex-hearthstone.md): the script installs them into
# BepInEx/unstripped_corlib and points doorstop_config.ini's dll_search_path_override at them. They come from
# tools/nodance-corlibs.sh (Unity's official editor archive) and only files whose SHA-256 tools/nodance-corlibs.sha256
# lists for the game's Unity version (read in UnityPlayer.dll) are installed. A game update that changes that version
# leaves libraries of the old one in place: Mono may then refuse them and the game close at start. Run
# tools/nodance-corlibs.sh and this script again, or set 'enabled = false' in doorstop_config.ini (BepInEx off).
# BepInEx comes from the official archive kept in lib/bepinex/ by the mod's build, checked against the SHA-256
# written in the mod's .csproj; an existing BepInEx (or another winhttp.dll) is never overwritten.
# The mod must be built first (Release): dotnet build mods/TavernCompass.NoDance -c Release -warnaserror
#   -p:HearthstoneManagedDir=<game>/Hearthstone_Data/Managed/
set -euo pipefail
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

game="${HEARTHSTONE_DIR:-/mnt/e/JEUX/Hearthstone}"
dry_run=0
uninstall=0
purge=0
yes=0
restore=""
while [ $# -gt 0 ]; do
  case "$1" in
    --game-dir) game="${2:?--game-dir needs a folder}"; shift 2 ;;
    --dry-run) dry_run=1; shift ;;
    --uninstall) uninstall=1; shift ;;
    --purge) purge=1; shift ;;
    --yes) yes=1; shift ;;
    --restore) restore="${2:?--restore needs the backup folder an install printed}"; shift 2 ;;
    -h|--help) sed -n '2,21p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *) echo "unknown argument: $1 (see --help)" >&2; exit 2 ;;
  esac
done
[ "$purge" -eq 0 ] || [ "$uninstall" -eq 1 ] || { echo "--purge goes with --uninstall" >&2; exit 2; }
[ -z "$restore" ] || [ "$uninstall" -eq 0 ] || { echo "--restore and --uninstall are two different runs" >&2; exit 2; }
game="${game%/}"
restore="${restore%/}"

csproj="$repo/mods/TavernCompass.NoDance/TavernCompass.NoDance.csproj"
bepinex_version="$(sed -n 's:.*<BepInExVersion>\(.*\)</BepInExVersion>.*:\1:p' "$csproj")"
bepinex_sha256="$(sed -n 's:.*<BepInExZipSha256>\(.*\)</BepInExZipSha256>.*:\1:p' "$csproj")"
[ -n "$bepinex_version" ] && [ -n "$bepinex_sha256" ] || { echo "No BepInEx version or SHA-256 in $csproj" >&2; exit 1; }
zip="$repo/lib/bepinex/$bepinex_version/BepInEx_win_x64_$bepinex_version.zip"
dll="$repo/mods/TavernCompass.NoDance/bin/Release/net48/TavernCompass.NoDance.dll"
plugin_rel="BepInEx/plugins/TavernCompass.NoDance.dll"
plugin_dst="$game/$plugin_rel"
config="$game/BepInEx/config/com.tavern-compass.nodance.cfg"
# What BepInEx puts next to Hearthstone.exe, besides its BepInEx/ folder.
bepinex_root_files=(winhttp.dll doorstop_config.ini .doorstop_version changelog.txt)
corlib_manifest="$repo/tools/nodance-corlibs.sha256"
corlib_rel="BepInEx/unstripped_corlib"
# The value written in doorstop_config.ini: relative to the game folder, like BepInEx's own target_assembly.
corlib_override='BepInEx\unstripped_corlib'

say() { printf '%s\n' "$*"; }
win() { wslpath -w "$1" 2>/dev/null || printf '%s' "$1"; }
fail() { say "STOP: $*" >&2; exit 1; }
sha1() { sha1sum "$1" | cut -d' ' -f1; }
confirm() {
  if [ "$yes" -eq 1 ]; then say "$1 [y/N] y (--yes)"; return 0; fi
  local answer
  read -r -p "$1 [y/N] " answer || answer=""
  [ "$answer" = "y" ]
}
# The Unity version the game runs, e.g. 6000.3.11f1: UnityPlayer.dll's ProductVersion, "6000.3.11f1 (3000ef702840)".
unity_version() {
  strings -el "$1/UnityPlayer.dll" | sed -n '/^ProductVersion$/{n;s/ .*//;p;q}'
}
# doorstop_config.ini's dll_search_path_override, as written (empty when unset).
doorstop_override() {
  sed -n 's/^dll_search_path_override *= *\([^\r]*\)\r\{0,1\}$/\1/p' "$1" | head -n 1
}
# doorstop_config.ini (on stdin) with dll_search_path_override set to ours; every other byte kept, CRLF included.
with_override() {
  sed 's/^dll_search_path_override *=[^\r]*/dll_search_path_override = BepInEx\\unstripped_corlib/'
}

# ---- pre-flight: everything checked before anything is asked or written -----------------------------------------
say "Target: $game ($(win "$game"))"
[ -f "$game/Hearthstone.exe" ] || fail "no Hearthstone.exe in $game: pass --game-dir or set HEARTHSTONE_DIR"
[ -d "$game/Hearthstone_Data/Managed" ] || fail "no Hearthstone_Data/Managed in $game"

tasklist=/mnt/c/Windows/System32/tasklist.exe
if [ -x "$tasklist" ]; then
  # </dev/null: a Windows process started from WSL reads the terminal (or the pipe) and would eat the answer to come.
  if "$tasklist" /FI "IMAGENAME eq Hearthstone.exe" /NH </dev/null 2>/dev/null | grep -qi 'Hearthstone.exe'; then
    fail "Hearthstone is running: close it first (winhttp.dll, the libraries and the plugins are loaded at its start)"
  fi
  say "Hearthstone is not running."
else
  say "WARNING: $tasklist not found, cannot check that Hearthstone is closed."
fi

has_winhttp=0; [ -f "$game/winhttp.dll" ] && has_winhttp=1
has_core=0; [ -d "$game/BepInEx/core" ] && has_core=1

# ---- restore: undo one install run from its backup ----------------------------------------------------------------
if [ -n "$restore" ]; then
  [ -f "$restore/added.txt" ] && [ -d "$restore/files" ] || fail "$restore is not a backup written by this script (no added.txt and files/)"
  [ "$(cat "$restore/game.txt" 2>/dev/null)" = "$game" ] || fail "$restore was made for $(cat "$restore/game.txt" 2>/dev/null || echo '?'), not $game"
  to_remove=(); kept=()
  while read -r sum rel; do
    [ -n "$rel" ] || continue
    if [ ! -e "$game/$rel" ]; then continue; fi
    if [ "$(sha1 "$game/$rel")" = "$sum" ]; then to_remove+=("$rel"); else kept+=("$rel"); fi
  done < "$restore/added.txt"
  to_put_back="$(cd "$restore/files" && find . -type f | sed 's:^\./::' | sort)"
  say "Will delete what that run added:"; for f in "${to_remove[@]}"; do say "  $f"; done
  [ "${#to_remove[@]}" -gt 0 ] || say "  (nothing)"
  for f in "${kept[@]}"; do say "Will keep: $f (changed since that run: not deleted)"; done
  say "Will put back from $restore/files:"; [ -z "$to_put_back" ] || say "$to_put_back" | sed 's:^:  :'
  [ -n "$to_put_back" ] || say "  (nothing)"
  if [ "$dry_run" -eq 1 ]; then say "Dry run: nothing written."; exit 0; fi
  confirm "Restore?" || { say "Nothing written."; exit 1; }
  for f in "${to_remove[@]}"; do rm -f -- "$game/$f"; done
  # The folders that run created and left empty go too: an empty BepInEx/core reads as half an install.
  while read -r d; do
    while [ -n "$d" ] && [ "$d" != "." ] && [ -d "$game/$d" ]; do
      rmdir -- "$game/$d" 2>/dev/null || break
      d="$(dirname "$d")"
    done
  done < <(for f in "${to_remove[@]}"; do dirname "$f"; done | sort -ru)
  cp -a -- "$restore/files/." "$game/"
  while read -r rel; do
    [ -n "$rel" ] || continue
    [ "$(sha1 "$restore/files/$rel")" = "$(sha1 "$game/$rel")" ] || fail "differs after restore: $rel"
  done <<< "$to_put_back"
  say "Restored as before the run of $(basename "$restore") (SHA-1 of every file put back checked)."
  exit 0
fi

# ---- uninstall ----------------------------------------------------------------------------------------------------
if [ "$uninstall" -eq 1 ]; then
  to_delete=()
  [ -f "$plugin_dst" ] && to_delete+=("$plugin_dst")
  keep=()
  if [ "$purge" -eq 1 ]; then
    [ -f "$zip" ] || fail "purge compares the files with BepInEx's archive, missing: $zip (build the mod once to get it)"
    for f in "${bepinex_root_files[@]}"; do
      [ -e "$game/$f" ] || continue
      ours="$(unzip -p "$zip" "$f" | sha1sum | cut -d' ' -f1)"
      # doorstop_config.ini is ours too when the only change is the dll_search_path_override this script writes.
      ours_set="$( [ "$f" != doorstop_config.ini ] || unzip -p "$zip" "$f" | with_override | sha1sum | cut -d' ' -f1)"
      actual="$(sha1 "$game/$f")"
      if [ "$actual" = "$ours" ] || [ "$actual" = "$ours_set" ]; then
        to_delete+=("$game/$f")
      else
        keep+=("$game/$f (differs from BepInEx $bepinex_version's: not ours, kept)")
      fi
    done
    if [ -d "$game/BepInEx" ]; then
      others="$(find "$game/BepInEx/plugins" -type f ! -name 'TavernCompass.NoDance.dll' 2>/dev/null | sed "s:^$game/::" || true)"
      [ -z "$others" ] || say "BepInEx/plugins holds other plugins, deleted with BepInEx/:"$'\n'"$others"
      to_delete+=("$game/BepInEx")
    fi
  fi
  say "Will delete:"; for f in "${to_delete[@]}"; do say "  $f"; done
  [ "${#to_delete[@]}" -gt 0 ] || say "  (nothing: the mod is not installed)"
  for k in "${keep[@]}"; do say "Will keep: $k"; done
  [ "$purge" -eq 1 ] || [ ! -f "$config" ] || say "Kept: $config (the mod's settings; --purge removes it with BepInEx/)"
  [ "$purge" -eq 1 ] || [ ! -d "$game/$corlib_rel" ] || say "Kept: $game/$corlib_rel (the libraries BepInEx needs; --purge removes them with BepInEx/)"
  if [ "$dry_run" -eq 1 ]; then say "Dry run: nothing deleted."; exit 0; fi
  [ "${#to_delete[@]}" -gt 0 ] || exit 0
  confirm "Delete these?" || { say "Nothing deleted."; exit 1; }
  for f in "${to_delete[@]}"; do rm -rf -- "$f"; done
  for f in "${to_delete[@]}"; do [ ! -e "$f" ] || fail "still there: $f"; done
  say "Deleted. Back: tools/nodance-deploy.sh --game-dir '$game'"
  exit 0
fi

# ---- install --------------------------------------------------------------------------------------------------------
[ -f "$dll" ] || fail "the mod is not built: dotnet build mods/TavernCompass.NoDance -c Release -warnaserror -p:HearthstoneManagedDir='$game/Hearthstone_Data/Managed/'"
newer="$(find "$repo/mods/TavernCompass.NoDance" "$repo/src/TavernCompass.NoDance.Core" -maxdepth 1 \( -name '*.cs' -o -name '*.csproj' \) -newer "$dll" | head -n 3)"
[ -z "$newer" ] || fail "sources newer than the built DLL, rebuild first:"$'\n'"$newer"
say "Mod: $dll"
say "     SHA-1 $(sha1 "$dll"), built $(date -r "$dll" '+%Y-%m-%d %H:%M:%S'), repository at $(git -C "$repo" rev-parse --short HEAD)$( [ -z "$(git -C "$repo" status --porcelain)" ] || printf ' (uncommitted changes)')"

install_bepinex=0
if [ "$has_winhttp" -eq 0 ] && [ "$has_core" -eq 0 ]; then
  [ -f "$zip" ] || fail "BepInEx's archive is missing: $zip (the mod's build downloads it)"
  actual="$(sha256sum "$zip" | cut -d' ' -f1)"
  [ "$actual" = "$bepinex_sha256" ] || fail "$zip: SHA-256 $actual, expected $bepinex_sha256"
  say "BepInEx: not installed; will extract BepInEx $bepinex_version (SHA-256 checked) into the game folder:"
  unzip -Z1 "$zip" | grep -v '/$' | sed 's:^:  :'
  install_bepinex=1
elif [ "$has_winhttp" -eq 1 ] && [ "$has_core" -eq 1 ]; then
  say "BepInEx: already installed (BepInEx/core/BepInEx.dll SHA-1 $(sha1 "$game/BepInEx/core/BepInEx.dll" 2>/dev/null || echo '?')), left as is."
else
  fail "half an install: winhttp.dll $([ "$has_winhttp" -eq 1 ] && echo present || echo absent), BepInEx/core $([ "$has_core" -eq 1 ] && echo present || echo absent). Not touching it: check the folder by hand."
fi

# The complete class libraries of the game's Unity version, checked against the manifest before anything is shown.
command -v strings > /dev/null || fail "strings (binutils) is needed to read the game's Unity version"
unity="$(unity_version "$game")"
[[ "$unity" =~ ^[0-9]+\.[0-9]+\.[0-9]+[abfp][0-9]+$ ]] || fail "cannot read the Unity version in $game/UnityPlayer.dll (read: '$unity')"
corlib_src="$repo/lib/unity/$unity/unstripped_corlib"
mapfile -t corlib_lines < <(awk -v p="$unity/unstripped_corlib/" 'index($2, p) == 1 { print $1, substr($2, length(p) + 1) }' "$corlib_manifest")
[ "${#corlib_lines[@]}" -gt 0 ] || fail "the game runs Unity $unity, and $corlib_manifest lists no libraries for it: run tools/nodance-corlibs.sh"
corlib_names=()
for line in "${corlib_lines[@]}"; do
  sum="${line%% *}"; name="${line#* }"
  [ -f "$corlib_src/$name" ] || fail "missing $corlib_src/$name: run tools/nodance-corlibs.sh"
  actual="$(sha256sum "$corlib_src/$name" | cut -d' ' -f1)"
  [ "$actual" = "$sum" ] || fail "$corlib_src/$name: SHA-256 $actual, the manifest says $sum"
  [ -f "$game/Hearthstone_Data/Managed/$name" ] || say "WARNING: the game has no Managed/$name; installed anyway (Mono loads it only if asked)"
  corlib_names+=("$name")
done
say "Libraries: Unity $unity (the game's UnityPlayer.dll), ${#corlib_names[@]} files from $corlib_src (SHA-256 checked against the manifest)"
corlib_todo=()
for name in "${corlib_names[@]}"; do
  dst="$game/$corlib_rel/$name"
  if [ -f "$dst" ] && [ "$(sha1 "$dst")" = "$(sha1 "$corlib_src/$name")" ]; then continue; fi
  corlib_todo+=("$name")
  if [ -f "$dst" ]; then say "Will replace: $corlib_rel/$name (SHA-1 $(sha1 "$dst"))"; else say "Will copy to: $corlib_rel/$name"; fi
done
[ "${#corlib_todo[@]}" -gt 0 ] || say "Libraries: already in $corlib_rel, identical."
stale="$(find "$game/$corlib_rel" -maxdepth 1 -type f -name '*.dll' 2>/dev/null | sed 's:.*/::' | grep -vxF -f <(printf '%s\n' "${corlib_names[@]}") || true)"
[ -z "$stale" ] || say "WARNING: $corlib_rel holds other DLLs, left as is (Mono prefers them to the game's):"$'\n'"$(say "$stale" | sed 's:^:  :')"
version_file="$game/$corlib_rel/UNITY_VERSION"
installed_version=""
[ ! -f "$version_file" ] || installed_version="$(tr -d '\r\n' < "$version_file")"
write_version=0
if [ "$installed_version" != "$unity" ]; then
  write_version=1
  say "Will write: $corlib_rel/UNITY_VERSION = $unity$( [ -z "$installed_version" ] || printf ' (was %s)' "$installed_version")"
fi

doorstop_src="$game/doorstop_config.ini"
[ "$install_bepinex" -eq 0 ] || doorstop_src=""
current_override=""
[ -z "$doorstop_src" ] || current_override="$(doorstop_override "$doorstop_src")"
set_override=0
if [ "$install_bepinex" -eq 1 ]; then
  set_override=1
  say "Will set: doorstop_config.ini dll_search_path_override = $corlib_override"
elif [ "$current_override" != "$corlib_override" ]; then
  [ -z "$current_override" ] || fail "doorstop_config.ini already has dll_search_path_override = $current_override: not ours, not touching it"
  grep -q '^dll_search_path_override *=' "$doorstop_src" || fail "doorstop_config.ini has no dll_search_path_override line: not BepInEx $bepinex_version's file, not touching it"
  set_override=1
  say "Will set: doorstop_config.ini dll_search_path_override = $corlib_override (now empty)"
fi

mapfile -t preloader_logs < <(find "$game" -maxdepth 1 -type f -name 'preloader_*.log' -printf '%f\n' | sort)
for f in "${preloader_logs[@]}"; do say "Will move to the backup: $f (BepInEx's report of a failed start)"; done

if [ -f "$plugin_dst" ]; then
  say "Will replace: $plugin_rel (SHA-1 $(sha1 "$plugin_dst"))"
else
  say "Will copy to: $plugin_rel"
fi
others="$(find "$game/BepInEx/plugins" -type f -name '*.dll' ! -name 'TavernCompass.NoDance.dll' 2>/dev/null | sed "s:^$game/::" || true)"
if [ -n "$others" ]; then
  say "WARNING: other plugins in BepInEx/plugins run alongside this one; another fix of the minions' order there"
  say "         (Nomi's Kitchen's FixMinionDance, say) would fight it. Disable it before comparing:"
  say "$others" | sed 's:^:  :'
fi

# One folder per run, never shared: a second run in the same second would overwrite the first one's list of additions.
backup="$game.nodance-backups/$(date '+%Y%m%d-%H%M%S')"
run=1
while [ -e "$backup$( [ "$run" -eq 1 ] || printf -- '-%s' "$run")" ]; do run=$((run + 1)); done
[ "$run" -eq 1 ] || backup="$backup-$run"
say "Backup of what is replaced or moved: $backup"
if [ "$dry_run" -eq 1 ]; then say "Dry run: nothing written."; exit 0; fi
confirm "Proceed?" || { say "Nothing written."; exit 1; }

# ---- apply: every replaced file is saved first, every added file listed, so --restore can undo this run ----------
mkdir -p "$backup/files"
printf '%s\n' "$game" > "$backup/game.txt"
: > "$backup/added.txt"
save() {  # save <relative path>: keeps the current file in the backup before it changes
  [ -f "$game/$1" ] || return 0
  [ -f "$backup/files/$1" ] && return 0
  mkdir -p "$(dirname "$backup/files/$1")"
  cp -a -- "$game/$1" "$backup/files/$1"
}
put() {  # put <source> <relative path>: copies a file into the game folder and checks it
  if [ -f "$game/$2" ]; then save "$2"; else printf '%s %s\n' "$(sha1 "$1")" "$2" >> "$backup/added.txt"; fi
  mkdir -p "$(dirname "$game/$2")"
  cp -f -- "$1" "$game/$2"
  [ "$(sha1 "$1")" = "$(sha1 "$game/$2")" ] || fail "the copy differs from its source: $2"
}

if [ "$install_bepinex" -eq 1 ]; then
  unzip -q -n "$zip" -d "$game"
  while read -r member; do
    [ "$(unzip -p "$zip" "$member" | sha1sum | cut -d' ' -f1)" = "$(sha1 "$game/$member")" ] || fail "BepInEx file differs after extraction: $member"
    printf '%s %s\n' "$(sha1 "$game/$member")" "$member" >> "$backup/added.txt"
  done < <(unzip -Z1 "$zip" | grep -v '/$')
  say "BepInEx $bepinex_version extracted and checked (SHA-1 of every file)."
fi
for f in "${preloader_logs[@]}"; do
  mkdir -p "$backup/files"
  mv -- "$game/$f" "$backup/files/$f"
done
[ "${#preloader_logs[@]}" -eq 0 ] || say "Moved ${#preloader_logs[@]} preloader_*.log to $backup/files/"
for name in "${corlib_todo[@]}"; do put "$corlib_src/$name" "$corlib_rel/$name"; done
[ "${#corlib_todo[@]}" -eq 0 ] || say "Libraries copied and checked (SHA-1): ${#corlib_todo[@]} into $corlib_rel"
if [ "$write_version" -eq 1 ]; then
  printf '%s\r\n' "$unity" > "$backup/UNITY_VERSION"
  put "$backup/UNITY_VERSION" "$corlib_rel/UNITY_VERSION"
  rm -f -- "$backup/UNITY_VERSION"
fi
if [ "$set_override" -eq 1 ]; then
  if [ "$install_bepinex" -eq 1 ]; then
    # Freshly extracted: listed in added.txt with the archive's SHA-1; the edited file replaces that line.
    with_override < "$game/doorstop_config.ini" > "$backup/doorstop_config.ini.new"
    sed -i "/ doorstop_config\.ini$/d" "$backup/added.txt"
    printf '%s %s\n' "$(sha1 "$backup/doorstop_config.ini.new")" doorstop_config.ini >> "$backup/added.txt"
  else
    save doorstop_config.ini
    with_override < "$game/doorstop_config.ini" > "$backup/doorstop_config.ini.new"
  fi
  cp -f -- "$backup/doorstop_config.ini.new" "$game/doorstop_config.ini"
  rm -f -- "$backup/doorstop_config.ini.new"
  [ "$(doorstop_override "$game/doorstop_config.ini")" = "$corlib_override" ] || fail "doorstop_config.ini: dll_search_path_override not set after writing it"
  say "doorstop_config.ini: dll_search_path_override = $(doorstop_override "$game/doorstop_config.ini")"
fi
put "$dll" "$plugin_rel"
say "SHA-1 source      $(sha1 "$dll")"
say "SHA-1 destination $(sha1 "$plugin_dst")"
say "Installed. Start Hearthstone; BepInEx writes $(win "$game/BepInEx/LogOutput.log"):"
say "  '5/5 required patches applied' says the mod is on, '... disabled (missing: ...)' that a game update changed a target."
say "  A new preloader_*.log next to Hearthstone.exe says BepInEx failed to start (it is not loaded, the game runs without it)."
say "Way back: tools/nodance-deploy.sh --restore '$backup' --game-dir '$game'   (this run only)"
say "          tools/nodance-deploy.sh --uninstall --game-dir '$game'   (the mod only)"
say "          tools/nodance-deploy.sh --uninstall --purge --game-dir '$game'   (the mod, BepInEx and its libraries)"
say "Without uninstalling: set 'enabled = false' in $(win "$game/doorstop_config.ini") (BepInEx off) or 'Enabled = false' in $(win "$config") (the mod off)."
