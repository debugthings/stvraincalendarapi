# St. Vrain lunch menu calendar

HTTP service that turns [LINQ Connect](https://linqconnect.com) family menus into iCalendar feeds (`Lunchmenu.ics`, `Breakfastmenu.ics`, `Academicmenu.ics`).

It runs in two modes:

| Mode | Where | Behavior |
| --- | --- | --- |
| **Origin** (default) | Proxmox LXC at `https://lunchmenu.debugthings.com` | Fetches LINQ, caches menu JSON in SQLite, generates `.ics` |
| **Proxy** | Azure App Service `stvrainlunchmenucalendar.azurewebsites.net` | Forwards `*menu.ics` to the origin. No SQLite, no LINQ |

## Endpoints

| Path | Description |
| --- | --- |
| `GET /Lunchmenu.ics` | Elementary & PK lunch |
| `GET /Breakfastmenu.ics` | Breakfast |
| `GET /Academicmenu.ics` | Academic calendar notes |
| `GET /healthz` | Liveness (`Healthy` in origin mode, `Proxy` in proxy mode) |
| `GET /` | Short usage text |

The `/api/...` prefix from the old Azure Functions host still works (for example `/api/Lunchmenu.ics`).

Query parameters: `buildingId`, `districtId`, `startDate`, `endDate`.

Calendar apps can keep using:

`https://stvrainlunchmenucalendar.azurewebsites.net/api/Lunchmenu.ics`

Azure proxies that to:

`https://lunchmenu.debugthings.com/api/Lunchmenu.ics`

## SQLite cache (origin)

Raw LINQ `Menu` JSON is stored in SQLite, keyed by building, district, and date range. Lunch, breakfast, and academic calendars share the same cached payload.

| Setting | Default | Meaning |
| --- | --- | --- |
| `Cache:Enabled` / `Cache__Enabled` | `true` | Use SQLite |
| `Cache:TtlMinutes` / `Cache__TtlMinutes` | `360` | Fresh window (6 hours) |
| `Cache:DatabasePath` / `Cache__DatabasePath` | `data/menu-cache.db` | Relative to the app content root |

On a cache miss or expired row the app calls LINQ and upserts. If LINQ fails and a row exists, it serves **stale** JSON.

## Run locally

Requires the .NET 10 SDK.

```bash
dotnet run --project StVrainToICSFunctionApp.csproj
```

Listens on `http://localhost:7163` (see `Properties/launchSettings.json`).

```bash
curl -sS http://localhost:7163/healthz
curl -sS http://localhost:7163/Lunchmenu.ics | head
```

Tests (cache/proxy unit tests always run; live LINQ is marked Integration):

```bash
dotnet test
FUNCTIONS_E2E_BASE_URL=http://localhost:7163 dotnet test --filter Function_Lunch_menu_ics_returns_calendar
```

## Deploy origin on Proxmox (LXC)

### 1. Publish on a machine with the .NET 10 SDK

```bash
./deploy/publish.sh
```

That writes `artifacts/stvrain-lunch-menu.tar.gz` (self-contained `linux-x64`, no .NET install needed in the guest).

Copy the tarball to the Proxmox host if you did not build there.

### 2. Create the container (on the Proxmox host)

```bash
# DHCP on vmbr0, CTID 150
CTID=150 ./deploy/lxc/create-ct.sh /root/stvrain-lunch-menu.tar.gz

# Static IP
CTID=150 IP=192.168.1.50/24,gw=192.168.1.1 BRIDGE=vmbr0 \
  ./deploy/lxc/create-ct.sh /root/stvrain-lunch-menu.tar.gz
```

Useful environment variables for `create-ct.sh`:

| Variable | Default | Meaning |
| --- | --- | --- |
| `CTID` | `150` | Container ID |
| `HOSTNAME` | `stvrain-lunch` | Guest hostname |
| `STORAGE` | `local-lvm` | Root disk storage |
| `TEMPLATE_STORAGE` | `local` | Where CT templates live |
| `BRIDGE` | `vmbr0` | Linux bridge |
| `IP` | `dhcp` | `dhcp` or `addr/cidr,gw=...` |
| `MEMORY` | `768` | RAM in MiB |
| `CORES` | `1` | vCPUs |
| `DISK` | `4` | Root disk GiB |
| `CT_PASSWORD` | unset | Root password (optional) |
| `SSH_PUBKEY` | `~/.ssh/authorized_keys` | Injected if the file exists |
| `TEMPLATE` | latest `debian-12-standard` amd64 | Override template name |

Point `lunchmenu.debugthings.com` at the guest (or a reverse proxy in front of **port 8080**). Direct calendar URL:

`https://lunchmenu.debugthings.com/api/Lunchmenu.ics`

### 3. Install into an existing Debian/Ubuntu LXC

If the CT already exists:

```bash
pct push <CTID> artifacts/stvrain-lunch-menu.tar.gz /tmp/stvrain-lunch-menu.tar.gz
pct exec <CTID> -- tar -xzf /tmp/stvrain-lunch-menu.tar.gz -C /tmp install.sh
pct exec <CTID> -- bash /tmp/install.sh /tmp/stvrain-lunch-menu.tar.gz
```

`install.sh` keeps `/opt/stvrain-lunch-menu/data` across upgrades so the SQLite cache survives.

### Origin configuration

Edit `/etc/stvrain-lunch-menu.env` in the guest (see `deploy/lxc/stvrain-lunch-menu.env.example`), then:

```bash
pct exec <CTID> -- systemctl restart stvrain-lunch-menu
```

| Variable | Default | Meaning |
| --- | --- | --- |
| `ASPNETCORE_URLS` | `http://0.0.0.0:8080` | Bind address |
| `DefaultStartOffset` | `-7` | Days before today |
| `DefaultEndOffset` | `30` | Days after today |
| `Cache__TtlMinutes` | `360` | Menu cache lifetime |
| `Cache__DatabasePath` | `data/menu-cache.db` | SQLite file under the app dir |
| `APIEndpoint` | `https://api.linqconnect.com` | LINQ API |
| `LinqMinimalBrowserHeaders` | `false` | Set `true` if LINQ’s WAF rejects the client |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | unset | Optional Azure Monitor |

Keep `Proxy__Enabled=false` on the LXC.

### Logs and health

```bash
pct exec <CTID> -- journalctl -u stvrain-lunch-menu -f
pct exec <CTID> -- curl -sS http://127.0.0.1:8080/healthz
```

Suggested resources: **1 vCPU**, **768 MiB RAM**, **4 GiB disk**, Debian 12 unprivileged LXC.

## Deploy Azure proxy

The Azure site must be a **Linux App Service** running this ASP.NET Core app (not an Azure Functions worker). Then:

```bash
./deploy-azure.sh
```

That publishes `linux-x64` and sets:

- `Proxy__Enabled=true`
- `Proxy__UpstreamBaseUrl=https://lunchmenu.debugthings.com` (override with `PROXY_UPSTREAM_BASE_URL`)
- `Cache__Enabled=false`

`/healthz` on Azure stays local and returns `Proxy` so platform probes do not depend on the LXC.
