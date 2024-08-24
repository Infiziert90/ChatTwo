// websocket connection
class SSEConnection {
    constructor() {
        this.socket = new EventSource('/sse', );

        this.socket.addEventListener('close', (event) => {
            console.log("Closing SSE connection.")
            this.socket.close()
        });

        this.socket.addEventListener('switch-channel', (event) => {
            updateChannelHint(JSON.parse(event.data).channel)
        });

        this.socket.addEventListener('new-message', (event) => {
            for (let message of JSON.parse(event.data).messages)
            {
                addMessage(message);
            }
        });

        this.socket.addEventListener('channel-list', (event) => {
            updateChannelOptions(JSON.parse(event.data).channels)
        });
    }

    send(message) {
        this.socket.send(message);
    }
}

const sse = new SSEConnection();


// channel switcher
function updateChannelHint(label) {
    document.getElementById('channel-hint').innerHTML = label;
}

document.getElementById('channel-select').addEventListener('change', (event) => {
    // TODO: send new channel to "backend"
    // ws.send(...);
});

function updateChannelOptions(channels) {
    let select = document.getElementById('channel-select');

    // clear existing channels
    select.innerHTML = '';

    for (let [ name, channel ] of Object.entries(channels))
    {
        let option = document.createElement('option');
        option.text = name;
        option.value = channel;

        select.appendChild(option)
    }
}


// functions for handling the message list
function scrollMessagesToBottom() {
    const messagesContainer = document.querySelector('#messages > .scroll-container');
    messagesContainer.scrollTop = messagesContainer.scrollHeight - messagesContainer.offsetHeight;
}

function messagesContainerIsScrolledToBottom() {
    const messagesContainer = document.querySelector('#messages > .scroll-container');
    return messagesContainer.scrollTop == messagesContainer.scrollHeight - messagesContainer.offsetHeight;
}

function addMessage(messageData) {
    const scrolledToBottom = messagesContainerIsScrolledToBottom();

    const liMessage = document.createElement('li');
    const spanTimestamp = document.createElement('span');
    spanTimestamp.classList.add('timestamp');
    const spanMessage = document.createElement('span');
    spanMessage.classList.add('message');

    // suggestions, update with actual data model
    spanTimestamp.innerText = messageData.timestamp;
    spanMessage.innerHTML = messageData.messageHTML;  // or build HTML in here

    liMessage.appendChild(spanTimestamp);
    liMessage.appendChild(spanMessage);
    document.getElementById('messages-list').appendChild(liMessage);

    if (scrolledToBottom) {
        scrollMessagesToBottom();
    }
}

function clearMessages() {
    document.getElementById('messages-list').innerHTML = '';
}

//
document.querySelector('#messages > .scroll-container').addEventListener('scroll', () => {
    const messagesContainer = document.querySelector('#messages > .scroll-container');
    if (!messagesContainerIsScrolledToBottom()) {
        messagesContainer.classList.add('more-messages');
    } else {
        messagesContainer.classList.remove('more-messages');
    }
});


// handle message sending
document.querySelector('#input > form').addEventListener('submit', (event) => {
    event.preventDefault();

    const chatInput = document.getElementById('chat-input');
    const message = chatInput.value;

    fetch('/send', {
        method: 'POST',
        body: message,
        headers: {
            'Content-type': 'application/txt; charset=UTF-8'
        }
    });

    chatInput.value = '';
});


// calculate timestamp width
// to ensure that all timestamps have the same width. some typefaces have the same width across
// all number glyphs, others do not. the solution below is very rudimentary; at the very least,
// delaying it to account for font loading might make sense. perhaps there's an even better way?
window.setTimeout(() => {
    const widthProbe = document.getElementById('timestamp-width-probe');
    widthProbe.innerText = '88:88';  // assume 8 to be widest glyph
    document.body.style.setProperty('--timestamp-width', Math.ceil(widthProbe.clientWidth) + 'px');
}, 100);

// From kizer
async function AddGfdStylesheet(gfdPath, texPath) {
    const texPromise = LoadTexAsBlob(texPath);
    const gfdPromise = LoadGfd(gfdPath);
    const texUrl = URL.createObjectURL(await texPromise);
    const gfd = await gfdPromise;

    let stylesheets = [];
    for (const entry of gfd) {
        if (entry.width * entry.height <= 0)
            continue;

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
                `height: ${entry.height}px`,
                `margin-bottom: -20px`
            ].join(";"),
            [
                `background-position: -${entry.left * 2}px -${entry.top * 2 + 341}px`,
                `background-image: url('${texUrl}')`,
                `width: ${entry.width * 2}px`,
                `height: ${entry.height * 2}px`,
                `margin-bottom: -40px`
            ].join(";")
        ];
    }

    let stylesheet = ".gfd-icon::before { content: ''; display: inline-block; overflow: hidden; vertical-align: top; height:0; }";
    for (const entry of stylesheets) {
        if (!entry)
            continue;

        stylesheet += `\n${entry[0].map(x => `.gfd-icon.gfd-icon-${x}::before`).join(', ')}{${entry[1]};}`;
        stylesheet += `\n${entry[0].map(x => `.gfd-icon.gfd-icon-hq-${x}::before`).join(', ')}{${entry[2]};}`;
    }

    const styleNode = document.createElement("style");
    styleNode.type = "text/css";
    styleNode.appendChild(document.createTextNode(stylesheet));
    document.head.appendChild(styleNode);
}

async function LoadTexAsBlob(path) {
    const tex = ParseTex(await (await fetch(path)).arrayBuffer());
    if (tex.format !== 0x1450) // B8G8R8A8
        throw "Not supported";

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

AddGfdStylesheet("/files/gfdata.gfd", "/files/fonticon_ps5.tex");
