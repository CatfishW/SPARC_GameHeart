#!/bin/bash
# Stage the release build and upload it to tang-server:~/gameheart-stage (no sudo needed).
# Then run the printed sudo command yourself to back up the live copy and swap the new build in.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 Tools/deploy/stage.py
rsync -az --delete Builds/stage/ tang-server:~/gameheart-stage/
ssh tang-server 'du -sh ~/gameheart-stage; ls ~/gameheart-stage/Build'
echo
echo "Now run (types your tang sudo password at the hidden prompt):"
echo "ssh -t tang-server 'sudo cp -a /www/wwwroot/ping.agaii.org/game/gameheart ~/gameheart-live-backup-\$(date +%Y%m%d_%H%M%S) && sudo rsync -a --chown=www:www ~/gameheart-stage/ /www/wwwroot/ping.agaii.org/game/gameheart/ && echo DEPLOYED'"
