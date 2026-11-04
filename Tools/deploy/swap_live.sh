#!/bin/bash
# Back up the live /game/gameheart folder and copy ~/gameheart-stage over it (asks for the tang sudo password).
set -euo pipefail
ssh -t tang-server 'set -e; ts=$(date +%Y%m%d_%H%M%S); sudo cp -a /www/wwwroot/ping.agaii.org/game/gameheart ~/gameheart-live-backup-$ts && sudo rsync -a --chown=www:www ~/gameheart-stage/ /www/wwwroot/ping.agaii.org/game/gameheart/ && echo "DEPLOYED (backup: ~/gameheart-live-backup-$ts)"'
