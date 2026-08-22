# St. Vrain lunch menu calendar

HTTP service that turns [LINQ Connect](https://linqconnect.com) family menus into iCalendar feeds (`Lunchmenu.ics`, `Breakfastmenu.ics`, `Academicmenu.ics`).

## Quick start

| Goal | Command |
| --- | --- |
| Run locally | `dotnet run --project StVrainToICSFunctionApp.csproj` |
| Deploy Azure (origin) | `./deploy-azure.sh` |
| Deploy Azure (proxy → LXC) | `DEPLOY_MODE=proxy ./deploy-azure.sh` |
| Build LXC tarball | `./deploy/publish.sh` |
| Install on existing CT | `pct push … && pct exec … bash /tmp/install.sh …` |
| Run tests | `dotnet test --filter "Category!=Integration"` |

**Subscribe URL (unchanged):** `https://stvrainlunchmenucalendar.azurewebsites.net/api/Lunchmenu.ics`

One codebase, two hosts, switchable by config:

| Host | Default mode | Config |
| --- | --- | --- |
| **Azure Function App** | Origin (LINQ + SQLite) | `Proxy__Enabled=false` |
| **Proxmox LXC** | Origin | `Proxy__Enabled=false` in `/etc/stvrain-lunch-menu.env` |

Either host can run **proxy mode** and forward `*menu.ics` to `Proxy__UpstreamBaseUrl` (typically `https://lunchmenu.debugthings.com`). Calendar URLs stay the same; only where `.ics` is generated changes.

Lunch menus use `FamilyMenuSession.ServingSession` (for example `"Lunch"`). Plan names vary by school level. **Super Snack** plans (`PK Super Snack 26/27`) and meals named Super Snack are excluded.

## Endpoints

| Path | Description |
| --- | --- |
| `GET /rhe/lunchmenu` | Redhawk Elementary lunch at 11:30 |
| `GET /ems/lunchmenu` | Erie Middle lunch at 12:00 |
| `GET /rhe/breakfastmenu` | Redhawk Elementary breakfast at 8:30 |
| `GET /ems/breakfastmenu` | Erie Middle breakfast at 8:30 |
| `GET /Lunchmenu.ics` | Default building lunch (query params optional) |
| `GET /Breakfastmenu.ics` | Breakfast |
| `GET /Academicmenu.ics` | Academic calendar notes |
| `GET /healthz` | Liveness (`Healthy` in origin mode, `Proxy` in proxy mode) |

`.ics` on the short URLs is optional (`/rhe/lunchmenu.ics` works too).

**Google Calendar** (short URLs — use these; long GUID paths often fail):

```
https://lunchmenu.debugthings.com/rhe/lunchmenu
https://lunchmenu.debugthings.com/ems/lunchmenu
https://lunchmenu.debugthings.com/rhe/breakfastmenu
https://lunchmenu.debugthings.com/ems/breakfastmenu
```

Calendar apps keep using:

`https://stvrainlunchmenucalendar.azurewebsites.net/api/Lunchmenu.ics`

That URL works in **origin** mode (Azure calls LINQ) or **proxy** mode (Azure forwards to the LXC).

## SQLite cache (origin)

Raw LINQ `Menu` JSON is stored in SQLite, keyed by building, district, and date range. Lunch, breakfast, and academic calendars share the same cached payload.

| Setting | Default | Meaning |
| --- | --- | --- |
| `Cache:Enabled` / `Cache__Enabled` | `true` | Use SQLite |
| `Cache:TtlMinutes` / `Cache__TtlMinutes` | `360` | Fresh window (6 hours) |
| `Cache:DatabasePath` / `Cache__DatabasePath` | `data/menu-cache.db` | Relative to the app content root |

On Azure, the relative path is stored under `$HOME` (writable). On the LXC it is under the app directory.

## Deploy Azure Function App

Same zip for both modes — only app settings change.

**Origin (default)** — Azure calls LINQ, caches in SQLite:

```bash
az login   # if needed
./deploy-azure.sh
# or explicitly:
DEPLOY_MODE=origin ./deploy-azure.sh
```

**Proxy** — Azure forwards to the LXC; no LINQ/SQLite on Azure:

```bash
DEPLOY_MODE=proxy PROXY_UPSTREAM_BASE_URL=https://lunchmenu.debugthings.com ./deploy-azure.sh
```

You can also flip modes in the Azure portal without redeploying:

| Setting | Origin | Proxy |
| --- | --- | --- |
| `Proxy__Enabled` | `false` | `true` |
| `Proxy__UpstreamBaseUrl` | (ignored) | `https://lunchmenu.debugthings.com` |
| `Cache__Enabled` | `true` | `false` |

Then verify:

```bash
curl -sS https://stvrainlunchmenucalendar.azurewebsites.net/api/Lunchmenu.ics | head
```

You should see `BEGIN:VEVENT`, not only a timezone.

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
