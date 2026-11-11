#!/usr/bin/env bash
# Leave Play mode, recompile scripts in the running Editor and print any compile errors.
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
unity command --project-path "$ROOT" editor.playmode stop >/dev/null 2>&1 || true
unity command --project-path "$ROOT" script eval --result-only 'UnityEditor.AssetDatabase.Refresh(); return 0;' >/dev/null 2>&1 || true
unity recompile --project-path "$ROOT" --format json 2>/dev/null | python3 -c "
import sys,json
d=json.JSONDecoder().raw_decode(sys.stdin.read())[0]['data']
print('recompile:',d['status'],d['summary'])
for e in d['errors']: print('  %s(%s): %s'%(e.get('file'),e.get('line'),e.get('message')))
sys.exit(1 if d['errors'] else 0)"
