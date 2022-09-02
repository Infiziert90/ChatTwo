#!/bin/sh

for f in ChatTwo/Resources/Language.resx ChatTwo/Resources/Language.*.resx; do
    sed -i 's/ xml:space="preserve"//g' "$f"
    xmlstarlet fo -e utf-8 -s 4 "$f" | sponge "$f"
done
