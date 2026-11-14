#!/usr/bin/env bash
# App Store screenshot of the running game (Play mode): Tools/store-shot.sh <iphone|ipad> <out.png>
# Sets the Game view to half the App Store size and captures at twice that (ScreenCapture's superSize), so the
# picture is exactly what the game draws, interface included: iphone = 2868x1320 (6.9"), ipad = 2752x2064 (13").
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
case "$1" in
  iphone) GW=1434; GH=660 ;;
  ipad)   GW=1376; GH=1032 ;;
  *) echo "device: iphone or ipad"; exit 1 ;;
esac
OUT="$(cd "$(dirname "$2")" && pwd)/$(basename "$2")"
U=(unity command --project-path "$ROOT")
"${U[@]}" script eval --result-only "UnityEditor.PlayModeWindow.SetCustomRenderingResolution($GW,$GH,\"store\"); return 0;" >/dev/null
sleep 1.5  # a few frames for the HUD to lay out at the new size
rm -f "$OUT"
"${U[@]}" script eval --result-only "UnityEngine.ScreenCapture.CaptureScreenshot(\"$OUT\", 2); return 0;" >/dev/null
for i in 1 2 3 4 5 6 7 8 9 10; do [ -s "$OUT" ] && break; sleep 0.5; done
python3 -I -c "from PIL import Image; print(Image.open('$OUT').size)"
