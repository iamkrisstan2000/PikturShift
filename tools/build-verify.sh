#!/bin/bash
# Generates a Wine-runnable verification build of the app: same XAML/logic, but with
# ASCII stand-ins for the Segoe Fluent icon glyphs and DejaVu Sans instead of Segoe UI
# (Wine's text stack hard-crashes on unresolved private-use glyphs).
set -euo pipefail
SRC=/home/user/src
DST=/var/tmp/verify
OUT=/var/tmp/verify-pub

sudo -n rm -rf "$DST" "$OUT" 2>/dev/null || rm -rf "$DST" "$OUT"
mkdir -p "$DST/PikturShift/Assets" "$DST/PikturShift.Core"
cd "$SRC"
cp PikturShift/*.cs PikturShift/*.xaml PikturShift/*.csproj PikturShift/*.manifest "$DST/PikturShift/"
cp -r PikturShift/Assets/. "$DST/PikturShift/Assets/"
cp -r PikturShift/Resources PikturShift/Theming PikturShift/ViewModels PikturShift/Interop PikturShift/Common "$DST/PikturShift/"
cp PikturShift.Core/*.csproj "$DST/PikturShift.Core/"
cp -r PikturShift.Core/Formats PikturShift.Core/Imaging PikturShift.Core/Settings "$DST/PikturShift.Core/"

python3 - <<'PY'
import os
mapping = {
 'E700':'≡','E710':'+','E713':'⚙','E711':'✕','E72B':'←','E921':'–','E922':'□','E923':'▢',
 'E8BB':'✕','E73E':'✓','EA39':'!','E91B':'▣','E8B9':'⚙','E70D':'▾','E7F4':'▤','E708':'☾',
 'E706':'☀','E768':'▶','E838':'▤','E8B7':'▤','E946':'ⓘ','E74D':'✕','E790':'◈',
}
for root, dirs, files in os.walk('/var/tmp/verify'):
    for f in files:
        if f.endswith(('.xaml','.cs')):
            p=os.path.join(root,f)
            s=open(p, encoding='utf-8').read()
            for code, ch in mapping.items():
                s=s.replace(f'&#x{code};', ch).replace(f'\\u{code}', ch)
            s=s.replace('Segoe UI Variable Text, Segoe UI','DejaVu Sans')
            s=s.replace('Segoe UI Variable Display, Segoe UI','DejaVu Sans')
            s=s.replace('Segoe Fluent Icons, Segoe MDL2 Assets','DejaVu Sans')
            open(p,'w',encoding='utf-8').write(s)
print("glyphs patched")
PY

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 NUGET_PACKAGES=/tmp/nuget
/opt/dotnet/dotnet publish "$DST/PikturShift/PikturShift.csproj" -c Release -r win-x64 \
  -p:EnableWindowsTargeting=true -p:SelfContained=true -p:PublishSingleFile=true -p:DebugType=None \
  -o "$OUT" -v q 2>&1 | grep -vE "NU190|^\s*$" | tail -3
echo "verification build ready: $OUT"
