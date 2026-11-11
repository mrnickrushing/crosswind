#!/usr/bin/env bash
# Runs the Core tests (Assets/Scripts/Core without Unity) with a .NET 8 or newer SDK. The `dotnet` on PATH may be
# older (this machine's /usr/bin/dotnet is 6.0, which can't build net8.0), so this looks for one that can:
# $DOTNET, ~/.dotnet8, ~/.dotnet, then PATH. Extra arguments go to `dotnet test` (e.g. --filter Name~Trip).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

has8() { "$1" --list-sdks 2>/dev/null | grep -Eq '^([89]|[1-9][0-9])\.'; }
DN=""
for c in "${DOTNET:-}" "$HOME/.dotnet8/dotnet" "$HOME/.dotnet/dotnet" "$(command -v dotnet || true)"; do
  if [ -n "$c" ] && [ -x "$c" ] && has8 "$c"; then DN="$c"; break; fi
done
if [ -z "$DN" ]; then
  echo "No .NET 8 SDK found (the Core tests target net8.0). Install one for this user, no sudo needed:"
  echo "  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh && bash /tmp/dotnet-install.sh --channel 8.0 --install-dir ~/.dotnet8"
  exit 1
fi
export DOTNET_ROOT="$(dirname "$(readlink -f "$DN")")"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
echo "using $DN ($("$DN" --version))"
exec "$DN" test "$ROOT/Tools/CoreTests" "$@"
