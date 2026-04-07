#!/usr/bin/env bash
# Same build as Visual Studio: Publish → stvrainlunchmenucalendar (Zip Deploy).
# Portal: https://portal.azure.com/#@debugthings.com/resource/subscriptions/7c012b92-2b78-4cb4-ba6b-05729f4c8943/resourceGroups/stvrainlunchmenucalendar/providers/Microsoft.Web/sites/stvrainlunchmenucalendar/appServices
# Remote upload requires Azure CLI: `az login` (subscription 7c012b92-2b78-4cb4-ba6b-05729f4c8943).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

dotnet publish StVrainToICSFunctionApp.csproj -c Release \
  -p:PublishProfile="stvrainlunchmenucalendar - Zip Deploy"

PUBLISH="$ROOT/bin/Release/net10.0/win-x64/publish"
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

# Unique path only — do not leave an empty file: zip treats it as a corrupt archive ("Zip file structure invalid").
ZIP="$(mktemp /tmp/stvrain-deploy-XXXXXX.zip)"
rm -f "$ZIP"
trap 'rm -f "$ZIP"' EXIT

make_deploy_zip() {
  local src="$1" out="$2"
  if command -v zip >/dev/null 2>&1; then
    (cd "$src" && zip -qr "$out" .)
    return 0
  fi
  if command -v python3 >/dev/null 2>&1; then
    python3 -c "import shutil, sys; shutil.make_archive(sys.argv[1][:-4], 'zip', root_dir=sys.argv[2])" "$out" "$src"
    return 0
  fi
  echo "Need either 'zip' (sudo apt install zip) or python3 to build the deployment archive." >&2
  return 1
}
make_deploy_zip "$PUBLISH" "$ZIP"

az functionapp deployment source config-zip \
  --resource-group stvrainlunchmenucalendar \
  --name stvrainlunchmenucalendar \
  --src "$ZIP"

echo "Deployed: https://stvrainlunchmenucalendar.azurewebsites.net"
