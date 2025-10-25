#!/usr/bin/env bash
# pre_start.sh — Script to run ValheimNetPatcher before server start

set -euo pipefail

PATCHER="/opt/valheim-tools/ValheimNetPatcher"
TARGET="/opt/valheim/server/valheim_server_Data/Managed" # Directory to search for queue limit targets
LIMIT="${VALHEIM_SEND_QUEUE_LIMIT:-30720}"
LOG="/config/patcher.log"

echo "[$(date -u +'%F %T')] invoking patcher on ${TARGET} (limit=${LIMIT})" | tee -a "$LOG"
TMPDIR="/tmp" VALHEIM_SEND_QUEUE_LIMIT="${LIMIT}" "${PATCHER}" "${TARGET}" | tee -a "$LOG" || true
