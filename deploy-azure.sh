#!/usr/bin/env bash
# Same build as Visual Studio: Publish → stvrainlunchmenucalendar (Zip Deploy).
# Remote upload requires Azure CLI: `az login` (subscription 7c012b92-… from your publish profile).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

dotnet publish StVrainToICSFunctionApp.csproj -c Release \
  -p:PublishProfile="stvrainlunchmenucalendar - Zip Deploy"

PUBLISH="$ROOT/bin/Release/net8.0/win-x64/publish"
echo "Published to: $PUBLISH"

if ! command -v az >/dev/null 2>&1; then
  echo "Install Azure CLI and run 'az login', then re-run this script to upload the zip."
  echo "Or publish from Visual Studio / Rider using the same profile (stores deploy creds locally)."
  exit 0
fi

if ! az account show >/dev/null 2>&1; then
  echo "Run 'az login' first, then re-run this script."
  exit 1
fi

ZIP="$(mktemp /tmp/stvrain-deploy-XXXXXX.zip)"
trap 'rm -f "$ZIP"' EXIT
(cd "$PUBLISH" && zip -qr "$ZIP" .)

az functionapp deployment source config-zip \
  --resource-group stvrainlunchmenucalendar \
  --name stvrainlunchmenucalendar \
  --src "$ZIP"

echo "Deployed: https://stvrainlunchmenucalendar.azurewebsites.net"
