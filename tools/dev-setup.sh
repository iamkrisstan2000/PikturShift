#!/bin/bash
# Installs the toolchain needed to build and verify this project on a fresh Linux box.
# (Only the workspace persists between sessions; /opt does not.)
set -euo pipefail

if [ ! -x /opt/dotnet/dotnet ]; then
    echo "installing .NET 8 SDK..."
    curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    sudo mkdir -p /opt/dotnet && sudo chown -R "$USER":"$USER" /opt/dotnet
    bash /tmp/dotnet-install.sh --channel 8.0 --install-dir /opt/dotnet --no-path >/dev/null
fi
echo ".NET: $(/opt/dotnet/dotnet --version)"

if ! command -v wine >/dev/null; then
    echo "installing wine + xdotool (for the UI verification pass)..."
    sudo dpkg --add-architecture i386
    sudo apt-get update -qq
    DEBIAN_FRONTEND=noninteractive sudo apt-get install -y -qq wine64 wine32:i386 xdotool
fi
echo "wine: $(wine --version 2>/dev/null || echo missing)"
