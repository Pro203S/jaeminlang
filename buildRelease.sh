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

mkdir -p publish
mkdir -p artifacts

for RID in "${RIDS[@]}"
do
    OUTPUT_DIR="./publish/$RID"
    ZIP_PATH="./artifacts/$RID.zip"

    echo "========================================"
    echo "Publishing for $RID"
    echo "========================================"

    dotnet publish "$PROJECT" \
        -c "$CONFIG" \
        -r "$RID" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -o "$OUTPUT_DIR"

    echo "Compressing $RID..."

    rm -f "$ZIP_PATH"

    (
        cd "$OUTPUT_DIR"
        zip -r "../../artifacts/$RID.zip" .
    )

    echo "$RID done!"
    echo ""
done

echo "========================================"
echo "All builds completed!"
echo "Artifacts:"
echo "./artifacts"
echo "========================================"