// from kizer, gfd icons
async function AddGfdStylesheet(gfdPath, texPath) {
    const texPromise = LoadTexAsBlob(texPath);
    const gfdPromise = LoadGfd(gfdPath);
    const texUrl = URL.createObjectURL(await texPromise);
    const gfd = await gfdPromise;

    const stylesheets = [];
    for (const entry of gfd) {
        if (entry.width * entry.height <= 0)
            continue;

        width = entry.width;
        height = entry.height;

        if (entry.redirect !== 0) {
            stylesheets[entry.redirect][0].push(entry.id);
            continue;
        }

        stylesheets[entry.id] = [
            [entry.id],
            [
                `background-position: -${entry.left}px -${entry.top}px`,
                `background-image: url('${texUrl}')`,
                `width: ${entry.width}px`,
                `height: ${entry.height}px`
            ].join(';'),
            [
                `background-position: -${entry.left * 2}px -${entry.top * 2 + 341}px`,
                `background-image: url('${texUrl}')`,
                `width: ${entry.width * 2}px`,
                `height: ${entry.height * 2}px`
            ].join(';'),
            entry.width
        ];
    }

    let stylesheet = '';
    for (const entry of stylesheets) {
        if (!entry)
            continue;

        stylesheet += `\n${entry[0].map(x => `.gfd-icon.gfd-icon-${x}::before`).join(', ')}{${entry[1]};}`;
        stylesheet += `\n${entry[0].map(x => `.gfd-icon.gfd-icon-hq-${x}::before`).join(', ')}{${entry[2]};}`;
        stylesheet += `\n${entry[0].map(x => `.gfd-icon.gfd-icon-${x}`).join(', ')}{width:${entry[3]}px;}`;
        stylesheet += `\n${entry[0].map(x => `.gfd-icon.gfd-icon-hq-${x}`).join(', ')}{width:${entry[3] * 2}px;}`;
    }

    const styleNode = document.createElement('style');
    styleNode.appendChild(document.createTextNode(stylesheet));
    document.head.appendChild(styleNode);
}

async function LoadTexAsBlob(path) {
    const tex = ParseTex(await (await fetch(path)).arrayBuffer());
    if (tex.format !== 0x1450) // B8G8R8A8
        throw 'Not supported';

    const dataArray = new Uint8ClampedArray(tex.buffer, tex.offsetToSurface[0], tex.width * tex.height * 4);
    for (let i = 0; i < dataArray.length; i += 4) {
        const t = dataArray[i];
        dataArray[i] = dataArray[i + 2];
        dataArray[i + 2] = t;
    }
    const imageData = new ImageData(dataArray, tex.width, tex.height);
    const bitmap = await createImageBitmap(imageData);

    const canvas = new OffscreenCanvas(tex.width, tex.height);
    canvas.getContext('bitmaprenderer').transferFromImageBitmap(bitmap);
    return await canvas.convertToBlob();
}

async function LoadGfd(path) {
    const buffer = new DataView(await (await fetch(path)).arrayBuffer());
    const count = buffer.getInt32(8, true);
    const entries = new Array(count);
    for (let i = 0; i < count; i++) {
        const offset = 0x10 + (i * 0x10);
        entries[i] = {
            id: buffer.getInt16(offset, true),
            left: buffer.getInt16(offset + 2, true),
            top: buffer.getInt16(offset + 4, true),
            width: buffer.getInt16(offset + 6, true),
            height: buffer.getInt16(offset + 8, true),
            unk0A: buffer.getInt16(offset + 10, true),
            redirect: buffer.getInt16(offset + 12, true),
            unk0E: buffer.getInt16(offset + 14, true),
        };
    }

    return entries;
}

function ParseTex(arrayBuffer) {
    const buffer = new DataView(arrayBuffer);
    const type = buffer.getInt32(0, true);
    const format = buffer.getInt32(4, true);
    const width = buffer.getInt16(8, true);
    const height = buffer.getInt16(10, true);
    const depth = buffer.getInt16(12, true);
    const mipsAndFlag = buffer.getInt8(14, true);
    const arraySize = buffer.getInt8(15, true);
    const lodOffsets = [buffer.getInt32(16, true), buffer.getInt32(20, true), buffer.getInt32(24, true)];
    const offsetToSurface = [buffer.getInt32(28, true), buffer.getInt32(32, true), buffer.getInt32(36, true), buffer.getInt32(40, true), buffer.getInt32(44, true), buffer.getInt32(48, true), buffer.getInt32(52, true), buffer.getInt32(56, true), buffer.getInt32(60, true), buffer.getInt32(64, true), buffer.getInt32(68, true), buffer.getInt32(72, true), buffer.getInt32(76, true)];
    return {
        buffer: arrayBuffer,
        type,
        format,
        width,
        height,
        depth,
        mipsAndFlag,
        arraySize,
        lodOffsets,
        offsetToSurface,
    };
}

AddGfdStylesheet('/files/gfdata.gfd', '/files/fonticon_ps5.tex');
