# Deploy to https://ping.agaii.org/game/gameheart/

1. Release build: `Unity -batchmode -quit -projectPath . -executeMethod GameHeartEditor.GameHeartBuilder.BuildWebGL` → `Builds/WebGL`.
2. `python3 Tools/deploy/stage.py` → `Builds/stage` + `Builds/stage-manifest.json`.
3. Upload: `rsync -a --delete Builds/stage/ tang-server:~/gameheart-stage/`
4. Back up the live copy and swap it in (needs the tang sudo password, typed by you at the hidden prompt):
   `ssh -t tang-server 'sudo cp -a /www/wwwroot/ping.agaii.org/game/gameheart ~/gameheart-live-backup-$(date +%Y%m%d_%H%M%S) && sudo rsync -a --delete --chown=www:www ~/gameheart-stage/ /www/wwwroot/ping.agaii.org/game/gameheart/'`
5. Verify: `python3 Tools/deploy/verify_live.py` compares live sha256 hashes with the manifest.
