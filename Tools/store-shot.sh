#!/usr/bin/env bash
# App Store screenshot of the running game (Play mode): Tools/store-shot.sh <iphone|ipad> <out.png>
# Sets the Game view to the device's aspect (so the HUD lays out for it), then renders Tools/store-shot.cs at the
# exact App Store size: iphone = 2868x1320 (6.9"), ipad = 2752x2064 (13").
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
case "$1" in
  iphone) W=2868; H=1320; GW=1800; GH=828 ;;
  ipad)   W=2752; H=2064; GW=1440; GH=1080 ;;
  *) echo "device: iphone or ipad"; exit 1 ;;
esac
OUT="$(cd "$(dirname "$2")" && pwd)/$(basename "$2")"
U=(unity command --project-path "$ROOT")
"${U[@]}" script eval --result-only "UnityEditor.PlayModeWindow.SetCustomRenderingResolution($GW,$GH,\"store\"); return 0;" >/dev/null
sleep 2  # a few frames for the HUD to lay out at the new size
"${U[@]}" script eval --result-only "$(sed "s/__W__/$W/; s/__H__/$H/; s#__OUT__#$OUT#" "$ROOT/Tools/store-shot.cs")" | grep -o '"result": "[^"]*"'
