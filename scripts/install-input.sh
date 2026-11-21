#!/bin/sh
set -eu
# Installs only our own file; never overwrites KWin or a native task manager.
project_dir=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
plugin_dir=/usr/lib/qt6/plugins/kwin/plugins
target="$plugin_dir/meta_cue_input.so"
if [ "$(id -u)" -ne 0 ]; then
    echo "Run with sudo: sudo $0 ${1:-}" >&2
    exit 1
fi
if [ "${1:-}" = --remove ]; then
    rm -f -- "$target"
    echo 'Input plugin file removed. Run scripts/meta-cue stop in your user session if still loaded.'
    exit 0
fi
[ -f "$project_dir/build/meta_cue_input.so" ] || { echo 'Build first' >&2; exit 1; }
# Atomic replacement avoids truncating an mmap-ed library. A new login is needed
# to use an updated binary already loaded by Qt.
install -d -- "$plugin_dir"
install -m 755 -- "$project_dir/build/meta_cue_input.so" "$target.new"
mv -f -- "$target.new" "$target"
echo 'Installed. As your normal user run: ./scripts/meta-cue start'
