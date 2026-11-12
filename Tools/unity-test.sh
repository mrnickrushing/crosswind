#!/usr/bin/env bash
# Runs the EditMode tests and prints the totals and any failures (exit 1 if any failed or none ran).
#  - With the project open in an Editor: runs them in that Editor. Unity's runner can't start during Play mode
#    ("This cannot be used during play mode"), so this leaves Play mode first and waits until the Editor is idle.
#  - With no Editor on the project: runs them in batch mode (`unity test --mode EditMode`), which needs the project
#    closed (an open Editor holds its lock).
# Usage: Tools/unity-test.sh [timeout-seconds]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
U=(unity command --project-path "$ROOT")
TIMEOUT="${1:-600}"

editor_listed() {
  unity status --json --project-path "$ROOT" 2>/dev/null | python3 -I -c '
import json, os, sys
try: d = json.load(sys.stdin)["data"]["instances"]
except Exception: d = []
root = os.path.realpath(sys.argv[1])
print(any(os.path.realpath(i.get("projectPath") or "") == root for i in d))' "$ROOT" || echo False
}
open_editor=$(editor_listed)
# An Editor that is reloading its scripts (just after a recompile) drops off that list for a few seconds while it is
# still running and still holding the project: wait for it to come back instead of trying batch mode against its lock.
if [ "$open_editor" != "True" ] && pgrep -f -- "-projectpath $ROOT( |\$)" >/dev/null 2>&1; then
  for _ in $(seq 1 60); do sleep 2; open_editor=$(editor_listed); [ "$open_editor" = "True" ] && break; done
fi

if [ "$open_editor" = "True" ]; then
  RESULTS="$HOME/.config/unity3d/Rushing Technologies/Crosswind/TestResults.xml"
  unity status --until-ready --project-path "$ROOT" >/dev/null 2>&1 || true
  "${U[@]}" script eval --result-only 'UnityEditor.EditorApplication.isPlaying = false; return 0;' >/dev/null
  idle=0
  for _ in $(seq 1 120); do
    if "${U[@]}" script eval --result-only 'return UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating;' | grep -Eq '("result": false|^false$)'; then idle=1; break; fi
    sleep 1
  done
  if [ "$idle" != 1 ]; then echo "the Editor didn't leave Play mode / finish compiling in 2 minutes"; exit 1; fi
  before=$(stat -c %Y "$RESULTS" 2>/dev/null || echo 0)
  "${U[@]}" test run editor --timeout "$TIMEOUT" --result-only >/dev/null
  after=$(stat -c %Y "$RESULTS" 2>/dev/null || echo 0)
  if [ "$after" = "$before" ]; then echo "tests did not run (no new $RESULTS): see the Editor log"; exit 1; fi
else
  OUT="$(mktemp -d)"; trap 'rm -rf "$OUT"' EXIT
  RESULTS="$OUT/editmode.xml"
  echo "no Editor has the project open: running EditMode tests in batch mode"
  timeout "$TIMEOUT" unity test "$ROOT" --mode EditMode --output "$RESULTS" --non-interactive >"$OUT/log.txt" 2>&1 || true
  if [ ! -s "$RESULTS" ]; then echo "tests did not run (no results report); last lines of the log:"; tail -20 "$OUT/log.txt"; exit 1; fi
fi

python3 -I - "$RESULTS" <<'PY'
import re, sys, html
t = open(sys.argv[1]).read()
run = re.search(r'<test-run[^>]*>', t).group(0)
get = lambda k: re.search(k + r'="([^"]*)"', run).group(1)
print("tests: %s passed, %s failed, %s skipped (of %s)" % (get("passed"), get("failed"), get("skipped"), get("total")))
for case in re.finditer(r'<test-case[^>]*fullname="([^"]*)"[^>]*result="Failed"[^>]*>(.*?)</test-case>', t, re.S):
    msg = re.search(r'<message>\s*(?:<!\[CDATA\[)?(.*?)(?:\]\]>)?\s*</message>', case.group(2), re.S)
    print("  FAILED", case.group(1))
    if msg: print("    " + html.unescape(msg.group(1)).strip().splitlines()[0][:300])
sys.exit(0 if get("result") == "Passed" and int(get("total")) > 0 else 1)
PY
