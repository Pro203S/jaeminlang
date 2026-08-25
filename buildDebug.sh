#!/bin/bash

set -e

PROJECT="./jaeminlang/jaeminlang.csproj"
CONFIG="Debug"

RIDS=(
    "osx-arm64"
)

for RID in "${RIDS[@]}"
do

    echo "========================================"
    echo "Publishing for $RID"
    echo "========================================"

    dotnet build "$PROJECT" \
        -c "$CONFIG"

done