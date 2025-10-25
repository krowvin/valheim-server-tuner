# Valheim Server Send/Receive Limit Patch - Docker

> ⚠️ **Use at your own peril, traveler.** This setup modifies Valheim's managed server binaries (`assembly_valheim.dll`) at runtime to raise the internal packet send/receive limit. It is **not officially supported by Iron Gate** and may break or require maintenance after future Valheim updates.

---

## 🛡️ Overview

This project extends the [lloesche/valheim-server](https://github.com/lloesche/valheim-server-docker) Docker image with an automated patcher that raises the server's internal **send/receive queue size** (default `10240 bytes`) to a higher configurable value, typically **30720 bytes**.  
This can reduce desyncs, rubber-banding, and “lag spikes” caused by Valheim's small network packet buffer.

## 📜 Notes from the Mead Hall

There are mods that exist that do very similar things, if not the same thing. You might consider those first.
Namely:

- https://thunderstore.io/c/valheim/p/CW_Jesse/BetterNetworking_Valheim/
- https://thunderstore.io/c/valheim/p/Smoothbrain/Network/

I opted to go this route so I could have full control the server files and their modifications. I prefer to not use mods on my servers if possible! (Mods are great!)

### What It Does

- Automatically builds and includes a small .NET console app (`ValheimNetPatcher`) that:
  - Scans Valheim's `assembly_valheim.dll` for constants equal to `10240` (the network send/receive limits).
  - Replaces those constants with your configured value (default `30720`).
- Runs the patcher automatically **after every game update** and **before** the server launches, using the base image's `PRE_START_HOOK`.
- Keeps SteamCMD auto-updates enabled — so the game still updates normally, and the patch is automatically re-applied afterward.
- Requires **no external mods or clients** — this is a _server-only_ runtime patch.

### How to Use

You must include the `PRE_START_HOOK` to your environment variables to called the built CS program in the valheim-server-tuner docker image. To do this simply include the following in your environment variables for your Docker server:

```bash
# IMPORTANT: run the patch AFTER updates, BEFORE server start. You should see it in the logs like:
#   [pre-start-hook] Running pre-start hook: /opt/valheim-tools/ValheimNetPatcher /opt/valheim/server/valheim_server_Data/Managed/assembly_valheim.dll
#   [patcher] Patched 2 occurrence(s) (found 2) to 30720 in assembly_valheim.dll
PRE_START_HOOK: >
  /opt/valheim-tools/ValheimNetPatcher
  /opt/valheim/server/valheim_server_Data/Managed/assembly_valheim.dll
```

Example here:
[docker-compose.yml](/docker-compose.yml#39-44)

### Technical Reference

Original discovery and rationale for the patch:  
🔗 [James A. Chambers — Revisiting Fixing Valheim Lag (Modifying Send/Receive Limits)](https://jamesachambers.com/revisiting-fixing-valheim-lag-modifying-send-receive-limits/)

---

## Repository Layout

```
valheim-server-tuner/
├── Dockerfile
├── docker-compose.yml
├── patcher/
│ ├── Program.cs
│ └── ValheimNetPatcher.csproj
├── config/
│ └── adminlist.txt # optional, for server admin Steam64 IDs
└── data/ # world files, generated at runtime
```
