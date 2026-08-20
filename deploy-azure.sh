#!/usr/bin/env bash
# Publish the ASP.NET Core app and zip-deploy it to Azure as a *proxy* to the LXC origin.
#
# Required app settings (set by this script):
#   Proxy__Enabled=true
#   Proxy__UpstreamBaseUrl=https://lunchmenu.debugthings.com
#
# The Azure resource must be an App Service (Linux) that can run ASP.NET Core / .NET 10.
# A Function App host cannot run this project as-is. Convert the site or create a Web App
# named stvrainlunchmenucalendar in resource group stvrainlunchmenucalendar.
#
# Portal: https://portal.azure.com/#@debugthings.com/resource/subscriptions/7c012b92-2b78-4cb4-ba6b-05729f4c8943/resourceGroups/stvrainlunchmenucalendar/providers/Microsoft.Web/sites/stvrainlunchmenucalendar
# Remote upload requires Azure CLI: `az login` (subscription 7c012b92-2b78-4cb4-ba6b-05729f4c8943).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

UPSTREAM="${PROXY_UPSTREAM_BASE_URL:-https://lunchmenu.debugthings.com}"
RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-stvrainlunchmenucalendar}"
APP_NAME="${AZURE_APP_NAME:-stvrainlunchmenucalendar}"
PUBLISH="$ROOT/artifacts/azure-linux-x64"

mkdir -p "$PUBLISH"

dotnet publish StVrainToICSFunctionApp.csproj -c Release \
  -r linux-x64 \
  --self-contained false \
  -o "$PUBLISH"

echo "Published to: $PUBLISH"

if ! command -v az >/dev/null 2>&1; then
  echo "Install Azure CLI and run 'az login', then re-run this script to upload the zip."
  exit 0
fi

if ! az account show >/dev/null 2>&1; then
  echo "Run 'az login' first, then re-run this script."
  exit 1
fi

az webapp config appsettings set \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_NAME" \
  --settings \
    "Proxy__Enabled=true" \
    "Proxy__UpstreamBaseUrl=${UPSTREAM}" \
    "Cache__Enabled=false" \
    "ASPNETCORE_ENVIRONMENT=Production"

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

if az webapp deployment source config-zip \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_NAME" \
  --src "$ZIP"; then
  echo "Deployed (webapp): https://${APP_NAME}.azurewebsites.net"
else
  echo "az webapp deploy failed. If this resource is still a Function App, convert it to App Service or create a Linux Web App, then re-run." >&2
  exit 1
fi
