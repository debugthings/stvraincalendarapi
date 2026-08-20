#!/usr/bin/env bash
# Create a Debian LXC on a Proxmox VE host and install the lunch-menu calendar.
# Run on the Proxmox host as root after deploy/publish.sh has produced the tarball.
#
# Examples:
#   CTID=150 ./create-ct.sh
#   CTID=150 IP=192.168.1.50/24,gw=192.168.1.1 BRIDGE=vmbr0 ./create-ct.sh /path/to/stvrain-lunch-menu.tar.gz
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
TARBALL="${1:-$ROOT/artifacts/stvrain-lunch-menu.tar.gz}"
CTID="${CTID:-150}"
HOSTNAME="${HOSTNAME:-stvrain-lunch}"
STORAGE="${STORAGE:-local-lvm}"
TEMPLATE_STORAGE="${TEMPLATE_STORAGE:-local}"
BRIDGE="${BRIDGE:-vmbr0}"
MEMORY="${MEMORY:-768}"
SWAP="${SWAP:-128}"
CORES="${CORES:-1}"
DISK="${DISK:-4}"
UNPRIVILEGED="${UNPRIVILEGED:-1}"
ONBOOT="${ONBOOT:-1}"
# dhcp  or  192.168.1.50/24,gw=192.168.1.1
IP="${IP:-dhcp}"
CT_PASSWORD="${CT_PASSWORD:-}"
SSH_PUBKEY="${SSH_PUBKEY:-${HOME}/.ssh/authorized_keys}"

if [[ "$(id -u)" -ne 0 ]]; then
  echo "Run as root on the Proxmox VE host." >&2
  exit 1
fi
if ! command -v pct >/dev/null 2>&1; then
  echo "pct not found. This script must run on a Proxmox VE host." >&2
  exit 1
fi
if [[ ! -f "$TARBALL" ]]; then
  echo "Tarball not found: $TARBALL" >&2
  echo "On your build machine run: deploy/publish.sh" >&2
  echo "Then copy artifacts/stvrain-lunch-menu.tar.gz to this host." >&2
  exit 1
fi
if pct status "$CTID" >/dev/null 2>&1; then
  echo "CT $CTID already exists. Choose another CTID or destroy it first." >&2
  exit 1
fi

if [[ -z "${TEMPLATE:-}" ]]; then
  pveam update >/dev/null
  TEMPLATE="$(pveam available --section system | awk '/debian-12-standard.*amd64/ {print $2}' | tail -1)"
fi
if [[ -z "$TEMPLATE" ]]; then
  echo "Could not find a debian-12-standard amd64 template. Set TEMPLATE=..." >&2
  exit 1
fi

if ! pveam list "$TEMPLATE_STORAGE" | grep -q "$TEMPLATE"; then
  echo "Downloading $TEMPLATE to $TEMPLATE_STORAGE ..."
  pveam download "$TEMPLATE_STORAGE" "$TEMPLATE"
fi

NET="name=eth0,bridge=${BRIDGE},ip=${IP}"
CREATE_ARGS=(
  "$CTID"
  "${TEMPLATE_STORAGE}:vztmpl/${TEMPLATE}"
  --hostname "$HOSTNAME"
  --memory "$MEMORY"
  --swap "$SWAP"
  --cores "$CORES"
  --rootfs "${STORAGE}:${DISK}"
  --net0 "$NET"
  --unprivileged "$UNPRIVILEGED"
  --onboot "$ONBOOT"
  --features nesting=0
  --ostype debian
  --start 0
)

if [[ -n "$CT_PASSWORD" ]]; then
  CREATE_ARGS+=(--password "$CT_PASSWORD")
fi
if [[ -f "$SSH_PUBKEY" ]]; then
  CREATE_ARGS+=(--ssh-public-keys "$SSH_PUBKEY")
fi

echo "Creating CT $CTID ($HOSTNAME) from $TEMPLATE ..."
pct create "${CREATE_ARGS[@]}"
pct start "$CTID"

echo "Waiting for network ..."
for _ in $(seq 1 40); do
  if pct exec "$CTID" -- ping -c1 -W1 1.1.1.1 >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

pct push "$CTID" "$TARBALL" /tmp/stvrain-lunch-menu.tar.gz
# install.sh is inside the tarball; extract just that script then run it.
pct exec "$CTID" -- tar -xzf /tmp/stvrain-lunch-menu.tar.gz -C /tmp install.sh
pct exec "$CTID" -- bash /tmp/install.sh /tmp/stvrain-lunch-menu.tar.gz

echo
echo "CT $CTID is installed. From the host:"
echo "  pct exec $CTID -- curl -sS http://127.0.0.1:8080/healthz"
echo "  pct exec $CTID -- curl -sS -o /dev/null -w '%{http_code}\\n' http://127.0.0.1:8080/Lunchmenu.ics"
echo
if [[ "$IP" == "dhcp" ]]; then
  echo "Guest IPv4:"
  pct exec "$CTID" -- hostname -I || true
else
  echo "Configured address: $IP"
fi
