#!/bin/bash

MAGICA=~/Downloads/Magica

mkdir -p ../art/vox/{source,exports,palettes}

cp "$MAGICA"/vox/* ../art/vox/source/
cp "$MAGICA"/export/* ../art/vox/exports/
cp "$MAGICA"/palette/pal1.png ../art/vox/palettes/palette.png

echo "Copied Magica files to ../art/vox/"
