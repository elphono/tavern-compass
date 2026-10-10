# Sourced by tools/deploy.sh and tools/release.sh: where the Windows install of Hearthstone Deck Tracker lives, seen
# from WSL. Builds and deployments always target the newest HDT installed (the highest app-<version>/ by `sort -V`;
# HDT's auto-updater keeps the previous one next to it), never a version written down in a script.
#   hdt_local_root          the Squirrel install, .../AppData/Local/HearthstoneDeckTracker (env HDT_LOCAL_ROOT forces it)
#   hdt_latest_app_dir      the newest .../app-<version>/ under it
#   hdt_version_of DIR      1.58.10 for .../app-1.58.10/
#   hdt_plugins_dir         .../AppData/Roaming/HearthstoneDeckTracker/Plugins/BronzebeardHud, same Windows profile
# Each function prints its answer on stdout, or explains on stderr and returns 1.

hdt_local_root() {
    if [ -n "${HDT_LOCAL_ROOT:-}" ]; then
        printf '%s\n' "${HDT_LOCAL_ROOT%/}"
        return 0
    fi
    local candidates=() exe
    for exe in /mnt/c/Users/*/AppData/Local/HearthstoneDeckTracker/HearthstoneDeckTracker.exe; do
        [ -f "$exe" ] && candidates+=("${exe%/HearthstoneDeckTracker.exe}")
    done
    if [ "${#candidates[@]}" -ne 1 ]; then
        echo "Found ${#candidates[@]} HDT installs under /mnt/c/Users/*/AppData/Local/ (${candidates[*]:-none}):" \
            "set HDT_LOCAL_ROOT or pass the app-<version>/ directory." >&2
        return 1
    fi
    printf '%s\n' "${candidates[0]}"
}

hdt_latest_app_dir() {
    local root latest
    root="$(hdt_local_root)" || return 1
    [ -d "$root" ] || { echo "No HDT install at $root" >&2; return 1; }
    latest="$(find "$root" -mindepth 1 -maxdepth 1 -type d -name 'app-*' -printf '%f\n' | sort -V | tail -n 1)"
    if [ -z "$latest" ]; then
        echo "No app-<version>/ directory in $root" >&2
        return 1
    fi
    if [ ! -f "$root/$latest/HearthstoneDeckTracker.exe" ]; then
        echo "No HearthstoneDeckTracker.exe in $root/$latest/ (an update still unpacking?)" >&2
        return 1
    fi
    printf '%s\n' "$root/$latest/"
}

hdt_version_of() {
    local name
    name="$(basename "${1%/}")"
    printf '%s\n' "${name#app-}"
}

hdt_plugins_dir() {
    local root
    root="$(hdt_local_root)" || return 1
    # The profile is the one that holds the install: <profile>/AppData/Local/HearthstoneDeckTracker.
    printf '%s\n' "${root%/AppData/Local/HearthstoneDeckTracker}/AppData/Roaming/HearthstoneDeckTracker/Plugins/BronzebeardHud"
}
