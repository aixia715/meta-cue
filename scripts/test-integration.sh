#!/bin/sh
set -eu
project_dir=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
build_dir="$project_dir/build"
test_dir=$(mktemp -d /tmp/meta-cue-test.XXXXXX)
trap 'rm -rf -- "$test_dir"' EXIT HUP INT TERM
mkdir -p "$test_dir/plugins/kwin/plugins" "$test_dir/config" "$test_dir/runtime"
chmod 700 "$test_dir/runtime"
cp "$build_dir/meta_cue_input.so" "$build_dir/test-plugins/kwin/plugins/meta_cue_test_driver.so" "$test_dir/plugins/kwin/plugins/"
# Private bus, virtual framebuffer and runtime prevent input from reaching the real desktop.
XDG_RUNTIME_DIR="$test_dir/runtime" XDG_CONFIG_HOME="$test_dir/config" \
QT_PLUGIN_PATH="$test_dir/plugins" QT_QPA_PLATFORM=offscreen KWIN_COMPOSE=Q \
timeout 20 dbus-run-session kwin_wayland --virtual --width 800 --height 600 \
    --no-lockscreen --no-kactivities --socket meta-cue-test \
    --exit-with-session "$build_dir/test_integration"
