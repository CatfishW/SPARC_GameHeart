#!/bin/bash
# Capture every UI screen at one viewport. Usage: ui_audit.sh <tag> [--size=WxH | --phone] [--dpr=N]
TAG=$1; shift
BASE=${BASE:-http://127.0.0.1:18791/}
D="node Tools/qa/drive.mjs"
cd "$(dirname "$0")/../.."
Q="qa=1&nosave=1"
run() { $D "$1" "ui-$TAG/$2" --base=$BASE "$@" --timeout=${T:-40} --steps="$3" 2>&1 | grep -E 'ERRORS|PAGEERROR|Exception' ; }
run "nosave=1&qa=1"                         title    '[{"wait":2500},{"shot":"press"},{"key":"Space"},{"wait":1500},{"shot":"menu"}]' "$@"
run "$Q&unlock=1&jump=hub"                  hub      '[{"wait":2500},{"shot":"hub"}]' "$@"
run "$Q&fresh=1&jump=hub"                   hubnew   '[{"wait":2500},{"shot":"hubnew"}]' "$@"
run "$Q&jump=cold:intro"                    cut      '[{"wait":5500},{"shot":"cut"}]' "$@"
run "$Q&jump=cold:decision"                 dec      '[{"wait":3200},{"shot":"dec"}]' "$@"
run "$Q&jump=cold:song"                     card     '[{"wait":3200},{"shot":"card"}]' "$@"
run "$Q&jump=cold:song&auto=1&seek=40"      hud      '[{"waitFor":"song_begin"},{"wait":7000},{"shot":"hud"},{"key":"Escape"},{"wait":800},{"shot":"pause"}]' "$@"
run "$Q&jump=cold:results"                  res      '[{"wait":5500},{"shot":"res"}]' "$@"
run "$Q&jump=cold:quiz"                     quiz     '[{"wait":3500},{"shot":"quiz"}]' "$@"
run "$Q&title=settings"                     set      '[{"wait":5000},{"shot":"set"}]' "$@"
echo "audit $TAG done"
