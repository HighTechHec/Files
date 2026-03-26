# CLAUDE.md

This file provides guidance to Claude Code when working in this repository.

## Repository Overview

`C:\Users\hjg42` is the root of the JAX PRIME AI automation and infrastructure ecosystem.
- **Primary storage backbone:** `G:\JAX_PRIME` (pivoted from F: after disk failure)
- **All prompts are part of the same ongoing project** — maintain full context across messages, never reset
- Every message is a continuation. Pick up from last known state.

---

## Directory Map

```
C:\Users\hjg42\
├── SOVEREIGNTY\jax-prime-core\     ← Docker Compose stack (Ollama, LiteLLM, n8n, WebUI, PG, Redis)
├── JAX_SCRIPTS\                    ← All JAX_ utility scripts (moved here from root)
├── JAX_CLI\                        ← JAX CLI Python project (cli.py, RAG, MCP config)
├── JAX_PRIME\                      ← Symlink / mirror reference (true store is G:\JAX_PRIME)
├── ANDROID_TOOLS\                  ← Frija, Odin, samloader, Samsung USB driver, FW_STAGING
├── TITAN_CORE\                     ← YouTube multi-channel publishing pipeline
├── PROJECT_AGGREGATOR_STACK\       ← Live publishing & revenue loop
├── FACTORY_ROOT\                   ← Kubernetes manifests (helm, kubeseal binaries included)
├── finance_ingest\                 ← PDF financial document extraction
├── autonomous_mesh\                ← Distributed services (docker compose)
├── strix_audit\                    ← Forensic system audit scripts
├── SOVEREIGNTY\                    ← jax-prime-core Docker stack
├── SOVEREIGN_NEXUS\                ← Nexus dashboard
├── SWARM_SYNC.md                   ← Agent handover log (Claude + Gemini)
├── SESSION_TRAIL.md                ← Running session log
└── bin\ / cmdAlias\                ← Custom CLI tools on PATH

G:\JAX_PRIME\
├── 00_CORE\                        ← K8s manifests, Vault
├── 01_ACTIVE\                      ← Live scripts: swarm_dashboard_v9_secure.py, sovereign_forge.py, oracle_synthesis.py
├── SOVEREIGN_MESH\                 ← n8n workflows, hydra_gateway, master_control, s22_edge_node
├── OPENCLAW_SWARM\                 ← WSL golden images, spawn scripts
└── VAULT\                         ← Knowledge vault (Hex vault intel docs)
```

---

## Key Services & Ports

| Service | Port | Location |
|---------|------|----------|
| Ollama | 11434 | Native Windows (GPU: RTX 5090) |
| LiteLLM gateway | 14000 | Docker (jax-prime-core) — port 4000 taken by Tailscale |
| n8n (jax-n8n) | 5680 | Docker — basic auth admin/sovereignty_2025_CHANGE_ME; owner: admin@jax.local/Sovereignty2025X; API key: see .env N8N_API_KEY (JWT, regen 2026-03-25) |
| n8n (strix_n8n) | 5678 | Standalone Docker — separate workflows at G:\JAX_PRIME\strix_compute\n8n_data |
| Open WebUI | 18080 | Docker — port 8080 taken by OpenClaw A2A broker |
| PostgreSQL | 5433 | Docker |
| Redis | 6379 | Docker |
| Nexus Dashboard | 5050 | Python (G:\JAX_PRIME\SOVEREIGN_MESH\master_control.py) |
| Pihole DNS | 53/80 | Docker |

**Docker Compose:** `C:\Users\hjg42\SOVEREIGNTY\jax-prime-core\docker-compose.yml`

```bash
cd C:/Users/hjg42/SOVEREIGNTY/jax-prime-core
docker compose up -d
```

---

## JAX CLI Scripts (in JAX_CLI\)

```powershell
python C:\Users\hjg42\JAX_SCRIPTS\JAX_REBOOT_WATCH.py      # ADB connectivity monitor
python C:\Users\hjg42\JAX_SCRIPTS\JAX_NEURAL_CORE.py      # Neural core agent
.\JAX_SCRIPTS\JAX_ARSENAL_LAUNCHER.ps1                     # Launch Steam/Discord/NSight
.\JAX_SCRIPTS\JAX_DOCKER_WATCH.ps1                         # Auto-deploy on Docker recovery
```

---

## Android / Device Management (in ANDROID_TOOLS\)

- **Note 10+** (SN: RF8MC0S7B2L) — ADB via USB, lockdown protocol in JAX_CLI\JAX_REBOOT_WATCH.py
- **S22** (R5CTB2C858V) — Connected USB ADB, Ollama URL set to http://100.119.21.113:11434
- **Firmware staging:** `C:\Users\hjg42\ANDROID_TOOLS\` (Frija/Odin/samloader)
- **FW binaries:** `C:\Users\hjg42\FW_STAGING\`

```powershell
adb devices -l
adb -s R5CTB2C858V shell "..."
```

---

## Network / Mesh

| Node | Role | IP | OS |
|------|------|----|----|
| Strix Scar 18 (Crown) | Master compute | 100.119.21.113 | Windows 11 |
| m15r3 / Alienware (Vault) | Memory store | 100.68.114.40 | Windows (offline 19h) |
| ROG Ally (Scout) | Edge compute | 100.88.118.9 | Windows (relay sfo) — booted Windows not SteamOS |
| S22 (Watcher) | Mobile sensor | via hotspot | Android |
| jax-cloud-reflex | Cloud relay | 100.120.178.93 | OFFLINE |

- **LAN:** Strix=10.0.0.146, Xfinity=10.0.0.1, Android TV=10.0.0.114
- **Hotspot:** JAX-PRIME 5GHz, 4 clients — NAT persisted via `JAX-HotspotNAT-Persist` scheduled task
- **Pihole DNS:** Running in Docker, handles .jax.local resolution
- **IronClaw:** `.ironclaw/` — connects to `localhost:9999/v1` (Hydra Gateway), 5 parallel jobs

---

## Auth & Credentials

- **GWS (gws CLI):** LIVE — Drive/Gmail/Sheets/Calendar via gcloud client 32555940559. `gws auth status` to verify.
- **GitHub CLI:** Authenticated as HighTechHec
- **Gemini CLI:** hjg420@gmail.com — settings at `C:\Users\hjg42\.gemini\settings.json`
- **Anthropic API:** Set in Windows env + `.bashrc`
- **Tailscale API key:** `G:\JAX_PRIME\SOVEREIGN_MESH\.env`

---

## D: Drive — Exocortex (421GB)

```
D:\GLOBAL_MEMORY_MANIFEST.md     ← canonical mesh truth — READ THIS
D:\PROMETHEUS_PROTOCOL.md        ← permanent cognitive anchor — READ BEFORE MAJOR TASKS
D:\EXOCORTEX_ENTERPRISE\         ← enterprise projects (Team_Kappa_YouTube, infra)
D:\UNIFIED_BRAIN_5.0\01_SOVEREIGN\logs\  ← sovereign brain logs
D:\SOVEREIGN_NEXUS\01_CODE\GLOBAL_CACHE\ ← pip-cache (233K files, exclude from index)
D:\SOVEREIGN_NEXUS\02_MEMORY\JaxPrime\   ← brain_vector_db, Chats, Google Keep exports
D:\Technoticia_Nexus\            ← PARA knowledge (Inbox/Projects/Areas/Resources)
```

## Brain Index (JAX_BRAIN_INDEX.db)

- **Location:** `C:\Users\hjg42\JAX_BRAIN_INDEX.db` — 1.1GB FTS5 SQLite
- **Indexer:** `C:\Users\hjg42\JAX_SCRIPTS\JAX_NEXUS_INDEXER.py` — 278,580 files indexed
- **MESH_MAP:** `C:\Users\hjg42\MESH_MAP.md` + `G:\JAX_PRIME\MESH_MAP.md`
- **Query:** `SELECT path,preview FROM files WHERE tags LIKE '%todo%'` or use FTS: `SELECT * FROM files_fts WHERE files_fts MATCH 'ollama docker'`

## Swarm Protocol (Claude + Gemini)

- **On session start:** Read `SWARM_SYNC.md` + `D:\PROMETHEUS_PROTOCOL.md` for current mesh state
- **During session:** Append actions to `SESSION_TRAIL.md`
- **On handoff:** Update HANDOVER LOG in `SWARM_SYNC.md`

---

## Environment

- **Platform:** Windows 11, shell = bash (Git Bash). Use Unix paths unless calling `.ps1` directly.
- **Python:** prefer `pip install --user`
- **msr.exe / nin.exe** — custom search binaries in home root
- **G: drive** is the active backbone (F: is dead — never reference F:\JAX_PRIME or F:\MYCELIUM_HIVE)
