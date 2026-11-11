#!/usr/bin/env bash
# Enter Play mode in the running Editor (if needed), let the world build, and save a Game view
# screenshot at iPhone landscape resolution. Requires the local-only com.unity.pipeline package
# (see CLAUDE.md). Usage: Tools/unity-capture.sh <out.png> [warmup-seconds] [csharp-to-eval-before-capture]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:?output png path}"; WARM="${2:-6}"; EVAL="${3:-}"
U=(unity command --project-path "$ROOT")
W=1800; H=830   # iPhone landscape aspect (2.17:1); must fit inside the maximized Game view

unity status --until-ready --project-path "$ROOT" >/dev/null 2>&1 || true  # e.g. after a recompile/domain reload
"${U[@]}" editor set_auto_tick --enabled true >/dev/null
"${U[@]}" script eval --result-only "UnityEditor.EditorPrefs.SetInt(\"ScriptCompilationDuringPlay\", 2); return 0;" >/dev/null  # stop Play before recompiling (a reload mid-play nulls statics)
"${U[@]}" console clear >/dev/null
"${U[@]}" script eval --result-only "UnityEditor.EditorSettings.asyncShaderCompilation = false; return 0;" >/dev/null  # no cyan placeholders
"${U[@]}" editor.playmode set_run_in_background >/dev/null
"${U[@]}" script eval --result-only "var gv=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.EditorWindow).Assembly.GetType(\"UnityEditor.GameView\")); gv.maximized=true; UnityEditor.PlayModeWindow.SetCustomRenderingResolution($W,$H,\"iPhone\"); return 0;" >/dev/null || true
if ! "${U[@]}" script eval --result-only 'return UnityEngine.Application.isPlaying;' | grep -q '"result": true'; then
  "${U[@]}" editor.playmode start >/dev/null
fi
end=$((SECONDS + WARM)); while [ $SECONDS -lt $end ]; do sleep 1; done
if [ -n "$EVAL" ]; then
  "${U[@]}" script eval --result-only "$EVAL" >/dev/null || echo "warning: pre-capture eval failed"
  end=$((SECONDS + 2)); while [ $SECONDS -lt $end ]; do sleep 1; done
fi
mkdir -p "$(dirname "$OUT")"
OUT="$(cd "$(dirname "$OUT")" && pwd)/$(basename "$OUT")"
"${U[@]}" capture game_view --width $W --height $H --output-path "$OUT" >/dev/null
"${U[@]}" console get --result-only | python3 -c "
import sys,json
d=json.JSONDecoder().raw_decode(sys.stdin.read())[0]
c=d.get('groundTruth',{}); print('console errors=%s warnings=%s'%(c.get('consoleErrors'),c.get('consoleWarnings')))
for e in d.get('entries',[]):
  if e.get('level') in ('error','warn','exception','assert'): print(' ',e.get('level'),str(e.get('message',''))[:300])"
echo "saved $OUT"
