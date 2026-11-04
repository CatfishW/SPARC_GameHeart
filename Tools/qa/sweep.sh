#!/bin/bash
# Autoplay every chapter end-to-end on a build; prints chapter_end lines and any console errors.
BASE=${BASE:-http://127.0.0.1:18791/}
cd "$(dirname "$0")/../.."
for spec in tutorial:faster:normal cold:faster:hard exercise:faster:normal sickness:faster:easy sleep:slower:normal finale:faster:hard; do
  IFS=: read ch choice diff <<< "$spec"
  echo "=== $ch ($diff)"
  node Tools/qa/drive.mjs "qa=1&fast=1&auto=1&nosave=1&jump=$ch&choose=$choice&quiz=right&diff=$diff" sweep/$ch --base=$BASE --timeout=260 --every=20000 --until=chapter_end 2>&1 \
    | grep -E 'fps|song_end|chapter_end|decision|ERRORS|Exception|error|PAGEERROR|SHOT' | sort -u
done
echo SWEEP DONE
