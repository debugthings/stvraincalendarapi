#!/usr/bin/env bash
# Install the lunch-menu calendar inside a Debian/Ubuntu LXC. Run as root.
# Usage: install.sh [tarball-or-publish-dir]
set -euo pipefail

APP_DIR="${APP_DIR:-/opt/stvrain-lunch-menu}"
ENV_FILE="${ENV_FILE:-/etc/stvrain-lunch-menu.env}"
SERVICE_NAME="stvrain-lunch-menu"
SOURCE="${1:-/tmp/stvrain-lunch-menu.tar.gz}"

if [[ "$(id -u)" -ne 0 ]]; then
  echo "Run as root." >&2
  exit 1
fi

export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y --no-install-recommends ca-certificates tzdata

icu_pkg="$(apt-cache search -n '^libicu[0-9]+$' | awk '{print $1}' | sort -V | tail -1 || true)"
if [[ -n "${icu_pkg}" ]]; then
  apt-get install -y --no-install-recommends "${icu_pkg}"
fi

if ! id -u stvrain >/dev/null 2>&1; then
  useradd --system --home "$APP_DIR" --shell /usr/sbin/nologin stvrain
fi

mkdir -p "$APP_DIR/data"
mkdir -p "$APP_DIR/data"
STAGE="$(mktemp -d)"
cleanup() { rm -rf "$STAGE"; }
trap cleanup EXIT

if [[ -f "$SOURCE" ]]; then
  tar -xzf "$SOURCE" -C "$STAGE"
elif [[ -d "$SOURCE" ]]; then
  cp -a "$SOURCE"/. "$STAGE"/
else
  echo "Source not found: $SOURCE" >&2
  echo "Pass a tarball from deploy/publish.sh or a publish directory." >&2
  exit 1
fi

if [[ ! -x "$STAGE/StVrainToICSFunctionApp" && ! -f "$STAGE/StVrainToICSFunctionApp.dll" ]]; then
  echo "Publish output is missing StVrainToICSFunctionApp in $SOURCE" >&2
  exit 1
fi

find "$APP_DIR" -mindepth 1 -maxdepth 1 ! -name 'data' -exec rm -rf {} +
cp -a "$STAGE"/. "$APP_DIR"/
mkdir -p "$APP_DIR/data"
chmod +x "$APP_DIR/StVrainToICSFunctionApp" 2>/dev/null || true
chown -R stvrain:stvrain "$APP_DIR"

if [[ -f "$APP_DIR/stvrain-lunch-menu.env.example" && ! -f "$ENV_FILE" ]]; then
  cp "$APP_DIR/stvrain-lunch-menu.env.example" "$ENV_FILE"
  chmod 0640 "$ENV_FILE"
fi

if [[ -f "$APP_DIR/stvrain-lunch-menu.service" ]]; then
  cp "$APP_DIR/stvrain-lunch-menu.service" "/etc/systemd/system/${SERVICE_NAME}.service"
else
  echo "Missing stvrain-lunch-menu.service in the payload." >&2
  exit 1
fi

systemctl daemon-reload
systemctl enable --now "$SERVICE_NAME"

sleep 1
systemctl --no-pager --full status "$SERVICE_NAME" || true
echo
echo "Listening on the URL in $ENV_FILE (default http://0.0.0.0:8080)."
echo "Try: curl -sS http://127.0.0.1:8080/healthz"
