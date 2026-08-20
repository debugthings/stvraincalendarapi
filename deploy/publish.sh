#!/usr/bin/env bash
# Build a self-contained linux-x64 payload for the Proxmox LXC.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/artifacts/linux-x64"
TARBALL="$ROOT/artifacts/stvrain-lunch-menu.tar.gz"

cd "$ROOT"
mkdir -p "$ROOT/artifacts"

dotnet publish "$ROOT/StVrainToICSFunctionApp.csproj" \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -o "$OUT" \
  -p:DebugType=None \
  -p:DebugSymbols=false

cp "$ROOT/deploy/lxc/install.sh" "$OUT/"
cp "$ROOT/deploy/lxc/stvrain-lunch-menu.service" "$OUT/"
cp "$ROOT/deploy/lxc/stvrain-lunch-menu.env.example" "$OUT/"

cd "$OUT"
tar -czf "$TARBALL" *

echo "Published to: $OUT"
echo "Tarball:      $TARBALL"
