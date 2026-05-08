#!/bin/bash

set -e

PROJECT="./jaeminlang/jaeminlang.csproj"
CONFIG="Release"

RIDS=(
    "win-x64"
    "win-x86"
    "win-arm64"

    "osx-x64"
    "osx-arm64"

    "linux-x64"
    "linux-arm64"
    "linux-arm"

    "linux-musl-x64"
    "linux-musl-arm64"
)

for RID in "${RIDS[@]}"
do
    echo "========================================"
    echo "Publishing for $RID"
    echo "========================================"

    dotnet publish "$PROJECT" \
        -c "$CONFIG" \
        -r "$RID" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -o "./publish/$RID"

    echo ""
done

echo "Done!"