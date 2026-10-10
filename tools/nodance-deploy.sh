#!/usr/bin/env bash
# Installs the anti-dance mod (mods/TavernCompass.NoDance) into the Hearthstone folder, from WSL.
#   tools/nodance-deploy.sh [--game-dir DIR] [--dry-run]                 install BepInEx if absent, copy the mod
#   tools/nodance-deploy.sh --uninstall [--purge] [--game-dir DIR] [--dry-run]
# DIR defaults to $HEARTHSTONE_DIR, else /mnt/e/JEUX/Hearthstone. The script shows the target and what it will do,
# asks for 'y', checks what it wrote (SHA-1), and prints the way back. --dry-run checks and shows, writes nothing.
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
while [ $# -gt 0 ]; do
  case "$1" in
    --game-dir) game="${2:?--game-dir needs a folder}"; shift 2 ;;
    --dry-run) dry_run=1; shift ;;
    --uninstall) uninstall=1; shift ;;
    --purge) purge=1; shift ;;
    -h|--help) sed -n '2,11p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *) echo "unknown argument: $1 (see --help)" >&2; exit 2 ;;
  esac
done
[ "$purge" -eq 0 ] || [ "$uninstall" -eq 1 ] || { echo "--purge goes with --uninstall" >&2; exit 2; }
game="${game%/}"

csproj="$repo/mods/TavernCompass.NoDance/TavernCompass.NoDance.csproj"
bepinex_version="$(sed -n 's:.*<BepInExVersion>\(.*\)</BepInExVersion>.*:\1:p' "$csproj")"
bepinex_sha256="$(sed -n 's:.*<BepInExZipSha256>\(.*\)</BepInExZipSha256>.*:\1:p' "$csproj")"
[ -n "$bepinex_version" ] && [ -n "$bepinex_sha256" ] || { echo "No BepInEx version or SHA-256 in $csproj" >&2; exit 1; }
zip="$repo/lib/bepinex/$bepinex_version/BepInEx_win_x64_$bepinex_version.zip"
dll="$repo/mods/TavernCompass.NoDance/bin/Release/net48/TavernCompass.NoDance.dll"
plugin_dst="$game/BepInEx/plugins/TavernCompass.NoDance.dll"
config="$game/BepInEx/config/com.tavern-compass.nodance.cfg"
# What BepInEx puts next to Hearthstone.exe, besides its BepInEx/ folder.
bepinex_root_files=(winhttp.dll doorstop_config.ini .doorstop_version changelog.txt)

say() { printf '%s\n' "$*"; }
win() { wslpath -w "$1" 2>/dev/null || printf '%s' "$1"; }
fail() { say "STOP: $*" >&2; exit 1; }
sha1() { sha1sum "$1" | cut -d' ' -f1; }

# ---- pre-flight: everything checked before anything is asked or written -----------------------------------------
say "Target: $game ($(win "$game"))"
[ -f "$game/Hearthstone.exe" ] || fail "no Hearthstone.exe in $game: pass --game-dir or set HEARTHSTONE_DIR"
[ -d "$game/Hearthstone_Data/Managed" ] || fail "no Hearthstone_Data/Managed in $game"

tasklist=/mnt/c/Windows/System32/tasklist.exe
if [ -x "$tasklist" ]; then
  # </dev/null: a Windows process started from WSL reads the terminal (or the pipe) and would eat the answer to come.
  if "$tasklist" /FI "IMAGENAME eq Hearthstone.exe" /NH </dev/null 2>/dev/null | grep -qi 'Hearthstone.exe'; then
    fail "Hearthstone is running: close it first (winhttp.dll and the plugins are loaded at its start)"
  fi
  say "Hearthstone is not running."
else
  say "WARNING: $tasklist not found, cannot check that Hearthstone is closed."
fi

has_winhttp=0; [ -f "$game/winhttp.dll" ] && has_winhttp=1
has_core=0; [ -d "$game/BepInEx/core" ] && has_core=1

# ---- uninstall ----------------------------------------------------------------------------------------------------
if [ "$uninstall" -eq 1 ]; then
  to_delete=()
  [ -f "$plugin_dst" ] && to_delete+=("$plugin_dst")
  keep=()
  if [ "$purge" -eq 1 ]; then
    [ -f "$zip" ] || fail "purge compares the files with BepInEx's archive, missing: $zip (build the mod once to get it)"
    for f in "${bepinex_root_files[@]}"; do
      [ -e "$game/$f" ] || continue
      if [ "$(unzip -p "$zip" "$f" | sha1sum | cut -d' ' -f1)" = "$(sha1 "$game/$f")" ]; then
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
  if [ "$dry_run" -eq 1 ]; then say "Dry run: nothing deleted."; exit 0; fi
  [ "${#to_delete[@]}" -gt 0 ] || exit 0
  read -r -p "Delete these? [y/N] " answer || answer=""
  [ "$answer" = "y" ] || { say "Nothing deleted."; exit 1; }
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
if [ -f "$plugin_dst" ]; then
  say "Will replace: $plugin_dst (SHA-1 $(sha1 "$plugin_dst"))"
else
  say "Will copy to: $plugin_dst"
fi
others="$(find "$game/BepInEx/plugins" -type f -name '*.dll' ! -name 'TavernCompass.NoDance.dll' 2>/dev/null | sed "s:^$game/::" || true)"
if [ -n "$others" ]; then
  say "WARNING: other plugins in BepInEx/plugins run alongside this one; another fix of the minions' order there"
  say "         (Nomi's Kitchen's FixMinionDance, say) would fight it. Disable it before comparing:"
  say "$others" | sed 's:^:  :'
fi

if [ "$dry_run" -eq 1 ]; then say "Dry run: nothing written."; exit 0; fi
read -r -p "Proceed? [y/N] " answer || answer=""
[ "$answer" = "y" ] || { say "Nothing written."; exit 1; }

if [ "$install_bepinex" -eq 1 ]; then
  unzip -q -n "$zip" -d "$game"
  while read -r member; do
    [ "$(unzip -p "$zip" "$member" | sha1sum | cut -d' ' -f1)" = "$(sha1 "$game/$member")" ] || fail "BepInEx file differs after extraction: $member"
  done < <(unzip -Z1 "$zip" | grep -v '/$')
  say "BepInEx $bepinex_version extracted and checked (SHA-1 of every file)."
fi
mkdir -p "$game/BepInEx/plugins"
cp -f -- "$dll" "$plugin_dst"
src_sha1="$(sha1 "$dll")"; dst_sha1="$(sha1 "$plugin_dst")"
say "SHA-1 source      $src_sha1"
say "SHA-1 destination $dst_sha1"
[ "$src_sha1" = "$dst_sha1" ] || fail "the copy differs from the build"
say "Installed. Start Hearthstone; BepInEx writes $(win "$game/BepInEx/LogOutput.log"):"
say "  '5/5 required patches applied' says the mod is on, '... disabled (missing: ...)' that a game update changed a target."
say "Way back: tools/nodance-deploy.sh --uninstall --game-dir '$game'   (the mod only)"
say "          tools/nodance-deploy.sh --uninstall --purge --game-dir '$game'   (the mod and BepInEx)"
say "Without uninstalling: set 'enabled = false' in $(win "$game/doorstop_config.ini") (BepInEx off) or 'Enabled = false' in $(win "$config") (the mod off)."
