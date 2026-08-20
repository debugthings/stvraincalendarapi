#!/usr/bin/env bash
# Publish the isolated Azure Functions worker. Same zip for origin or proxy — mode is app settings only.
#
#   DEPLOY_MODE=origin ./deploy-azure.sh   # LINQ + SQLite on Azure (default)
#   DEPLOY_MODE=proxy  ./deploy-azure.sh   # forward to LXC / lunchmenu.debugthings.com
#
# Proxy settings:
#   PROXY_UPSTREAM_BASE_URL=https://lunchmenu.debugthings.com
#
# Portal: https://portal.azure.com/#@debugthings.com/resource/subscriptions/7c012b92-2b78-4cb4-ba6b-05729f4c8943/resourceGroups/stvrainlunchmenucalendar/providers/Microsoft.Web/sites/stvrainlunchmenucalendar
# Remote upload requires Azure CLI: `az login` (subscription 7c012b92-2b78-4cb4-ba6b-05729f4c8943).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

DEPLOY_MODE="${DEPLOY_MODE:-origin}"
UPSTREAM="${PROXY_UPSTREAM_BASE_URL:-https://lunchmenu.debugthings.com}"
RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-stvrainlunchmenucalendar}"
APP_NAME="${AZURE_APP_NAME:-stvrainlunchmenucalendar}"
PUBLISH="$ROOT/artifacts/azure-functions"

mkdir -p "$PUBLISH"

dotnet publish StVrainToICSFunctionApp.csproj -c Release -o "$PUBLISH"

echo "Published to: $PUBLISH"
echo "Deploy mode: ${DEPLOY_MODE}"

if ! command -v az >/dev/null 2>&1; then
  echo "Install Azure CLI and run 'az login', then re-run this script to upload the zip."
  exit 0
fi

if ! az account show >/dev/null 2>&1; then
  echo "Run 'az login' first, then re-run this script."
  exit 1
fi

if [[ "$DEPLOY_MODE" == "proxy" ]]; then
  az functionapp config appsettings set \
    --resource-group "$RESOURCE_GROUP" \
    --name "$APP_NAME" \
    --settings \
      "FUNCTIONS_WORKER_RUNTIME=dotnet-isolated" \
      "Proxy__Enabled=true" \
      "Proxy__UpstreamBaseUrl=${UPSTREAM}" \
      "Cache__Enabled=false"
else
  az functionapp config appsettings set \
    --resource-group "$RESOURCE_GROUP" \
    --name "$APP_NAME" \
    --settings \
      "FUNCTIONS_WORKER_RUNTIME=dotnet-isolated" \
      "Proxy__Enabled=false" \
      "Cache__Enabled=true" \
      "Cache__TtlMinutes=360" \
      "Cache__DatabasePath=data/menu-cache.db" \
      "LinqMinimalBrowserHeaders=false" \
      "LinqUseHttp2=false"
fi

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
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_NAME" \
  --src "$ZIP"

echo "Deployed (${DEPLOY_MODE}): https://${APP_NAME}.azurewebsites.net/api/Lunchmenu.ics"
if [[ "$DEPLOY_MODE" == "proxy" ]]; then
  echo "Proxying to: ${UPSTREAM}"
fi
