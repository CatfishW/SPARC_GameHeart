#!/bin/bash
# ~1 s out-of-editor compile of Assets/Scripts (runtime) and Assets/Editor against the last Bee response files.
cd "$(dirname "$0")/.."
ED=/Applications/Unity/Hub/Editor/6000.5.10f1/Unity.app/Contents/Resources/Scripting
CSC="$ED/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll"
DAG=$(ls -td Library/Bee/artifacts/*E.dag | head -1)
OUT=Logs/compile; mkdir -p $OUT
build() { # rsp, out dll, source glob
  grep -v '\.cs"\?$' "$DAG/$1" | grep -v '^-out:\|^-refout:\|^-doc:\|analyzer\|additionalfile\|ruleset' > $OUT/$2.rsp
  echo "-out:$OUT/$2.dll" >> $OUT/$2.rsp
  echo "-nowarn:0414,0169,0649,0618" >> $OUT/$2.rsp
  if [ -n "$4" ]; then sed -i '' "s#-r:\"[^\"]*Assembly-CSharp.dll\"#-r:\"$OUT/Assembly-CSharp.dll\"#; s#-r:[^ ]*Assembly-CSharp.ref.dll#-r:$OUT/Assembly-CSharp.dll#" $OUT/$2.rsp; fi
  find $3 -name '*.cs' | while read f; do echo "\"$f\"" >> $OUT/$2.rsp; done
  "$ED/DotNetSdk/dotnet" exec "$CSC" /noconfig @$OUT/$2.rsp 2>&1 | grep -E 'error|warning CS(0162|0219|1717|0108)' | sort -u | head -${LIMIT:-60}
}
OUTTXT=$( { build Assembly-CSharp.rsp Assembly-CSharp Assets/Scripts; [ -f "$DAG/Assembly-CSharp-Editor.rsp" ] && build Assembly-CSharp-Editor.rsp Assembly-CSharp-Editor Assets/Editor editor; } )
[ -n "$OUTTXT" ] && echo "$OUTTXT"
if echo "$OUTTXT" | grep -q "error"; then echo "compile-check FAILED"; exit 1; fi
echo "compile-check OK"
