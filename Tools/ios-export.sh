#!/usr/bin/env bash
# Exports the iOS Xcode project from the running Editor (local Unity license) and publishes it as the
# `ios-latest` GitHub release asset that Codemagic's ironclad-testflight workflow (train app) builds and signs.
#   Tools/ios-export.sh            # needs the iOS Build Support module: unity install-modules -m ios
#   Tools/ios-export.sh --local    # export only, no upload
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LOG=~/.config/unity3d/Editor.log
# With a second Unity project open, Editor.log belongs to whichever Editor started last; read the log of the
# Editor that has this project open instead (its stdout, even if the file has since been renamed or deleted).
PID=$(unity status 2>/dev/null | awk -v r="$ROOT" '$3 == r { print $5 }' | head -1)
[ -n "$PID" ] && [ -r "/proc/$PID/fd/1" ] && LOG="/proc/$PID/fd/1"
unity command --project-path "$ROOT" editor.playmode stop >/dev/null 2>&1 || true
unity status --until-ready >/dev/null
# The Editor must be on the iOS platform: the capability and ad steps that finish the Xcode project are only
# compiled there (#if UNITY_IOS). Switching reloads the scripts, so it is its own step.
unity command --project-path "$ROOT" script eval --timeout 600 --code \
  'if (UnityEditor.EditorUserBuildSettings.activeBuildTarget != UnityEditor.BuildTarget.iOS) UnityEditor.EditorUserBuildSettings.SwitchActiveBuildTarget(UnityEditor.BuildTargetGroup.iOS, UnityEditor.BuildTarget.iOS); return 0;' >/dev/null 2>&1 || true
sleep 5; unity status --until-ready --project-path "$ROOT" >/dev/null
unity recompile --project-path "$ROOT" >/dev/null 2>&1 || true      # the editor scripts, now with UNITY_IOS
unity status --until-ready --project-path "$ROOT" >/dev/null
START=$(date +%s)
# The eval's own timeout is short; the build carries on in the Editor, so wait for its result line in the log.
unity command --project-path "$ROOT" script eval --timeout 3000 --code \
  'UnityEditor.PlayerSettings.iOS.appleDeveloperTeamID = "PH4AKDQ4Q7"; Ironclad.EditorTools.BuildScript.BuildIos(); return 0;' >/dev/null 2>&1 || true
for i in $(seq 1 360); do
  [ "$(stat -c %Y "$ROOT/ios/Info.plist" 2>/dev/null || echo 0)" -ge "$START" ] && grep -q "\[Ironclad\] iOS build" "$LOG" && break
  sleep 5
done
grep "\[Ironclad\] iOS build" "$LOG" | tail -1
grep "\[Ironclad\] iOS build" "$LOG" | tail -1 | grep -q "Succeeded"
# A project without these would crash at launch or be refused by the App Store: fail here instead.
grep -q "GADApplicationIdentifier" "$ROOT/ios/Info.plist" || { echo "EXPORT INCOMPLETE: no AdMob app id in Info.plist"; exit 1; }
grep -q "ubiquity-kvstore-identifier" "$ROOT/ios/Ironclad.entitlements" && grep -q "applesignin" "$ROOT/ios/Ironclad.entitlements" || { echo "EXPORT INCOMPLETE: entitlements are missing iCloud or Sign in with Apple"; exit 1; }
# The build rewrites these; the project's own copies are the ones to keep.
(cd "$ROOT" && git checkout -q Assets/Settings ProjectSettings/ProjectSettings.asset ProjectSettings/TimeManager.asset 2>/dev/null || true; rm -rf Assets/Resources/PerformanceTestRun*)
[ "${1:-}" = "--local" ] && exit 0
mkdir -p "${TMPDIR:-/tmp}/ironclad-export"
ZIP="${TMPDIR:-/tmp}/ironclad-export/ios-xcode.zip"   # the asset keeps this file name
rm -f "$ZIP"
(cd "$ROOT" && zip -qry "$ZIP" ios -x "ios/*_DoNotShip/*")
ls -lh "$ZIP"
cd "$ROOT"
gh release view ios-latest >/dev/null 2>&1 || gh release create ios-latest --prerelease --title "iOS Xcode export" --notes "Exported Xcode project for Codemagic (Tools/ios-export.sh)."
gh release upload ios-latest "$ZIP" --clobber
echo "uploaded; start the ironclad-testflight workflow in Codemagic (train app, branch codemagic/ironclad)"
