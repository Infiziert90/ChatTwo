class ChatTwoWeb {
    constructor() {
        this.setupDOMElements();
        this.setupSSEConnection();
    }


    setupDOMElements() {
        this.elements = {
            channelHint: document.getElementById('channel-hint'),
            channelSelect: document.getElementById('channel-select'),

            messagesContainer: document.querySelector('#messages > .scroll-container'),
            messagesList: document.getElementById('messages-list'),

            timestampWidthProbe: document.getElementById('timestamp-width-probe'),

            inputForm: document.querySelector('#input > form'),
            chatInput: document.getElementById('chat-input')
        };
        this.maxTimestampWidth = 0;
        this.scrolledToBottom = true;


        // channel selector
        this.elements.channelSelect.addEventListener('change', async (event) => {
            const rawResponse = await fetch('/channel', {
                method: 'POST',
                headers: {
                    'Accept': 'application/json',
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ channel: event.target.value })
            });
            // const content = await rawResponse.json();
            // TODO: use the response
        });

        // add indicator signaling more messages below
        this.elements.messagesContainer.addEventListener('scroll', (event) => {
            if (!this.messagesAreScrolledToBottom()) {
                event.target.parentElement?.classList.add('more-messages');
            } else {
                event.target.parentElement?.classList.remove('more-messages');
            }
        });

        // adjust scroll when the window size changes; mostly for mobile (opening/closing the keyboard)
        window.addEventListener('resize', () => {
            if (this.scrolledToBottom) {
                this.scrollMessagesToBottom();
            }
        })

        // handle message sending
        this.elements.inputForm.addEventListener('submit', async (event) => {
            event.preventDefault();
            const message = this.elements.chatInput.value;

            const rawResponse = await fetch('/send', {
                method: 'POST',
                headers: {
                    'Accept': 'application/json',
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ message: message })
            });
            // const content = await rawResponse.json();
            // TODO: use the response

            this.elements.chatInput.value = '';
        });
    }

    updateChannelHint(templates) {
        this.elements.channelHint.innerHTML = '';
        this.elements.channelHint.appendChild(this.processTemplate(templates));
    }

    updateChannels(channels) {
        this.elements.channelSelect.innerHTML = '';
        for (const [ label, channel ] of Object.entries(channels)) {
            const option = document.createElement('option');
            option.value = channel;
            option.innerText = label;
            this.elements.channelSelect.appendChild(option);
        }
    }

    // calculate timestamp width
    // to ensure that all timestamps have the same width. some typefaces have the same width across
    // all number glyphs, others do not. then there's AM/PM vs 24 hour, and so on
    calculateTimestampWidth(timestamp) {
        this.elements.timestampWidthProbe.innerText = timestamp;
        if (this.elements.timestampWidthProbe.clientWidth > this.maxTimestampWidth) {
            this.maxTimestampWidth = this.elements.timestampWidthProbe.clientWidth;
            document.body.style.setProperty('--timestamp-width', (Math.ceil(this.maxTimestampWidth) + 1) + 'px');
        }
    }

    messagesAreScrolledToBottom() {
        if (this.elements.messagesContainer.scrollTopMax) {
            this.scrolledToBottom = this.elements.messagesContainer.scrollTop === this.elements.messagesContainer.scrollTopMax;
        } else {
            this.scrolledToBottom =
                (
                    this.elements.messagesContainer.scrollHeight -
                    this.elements.messagesContainer.clientHeight -
                    this.elements.messagesContainer.scrollTop
                ) < 1;
        }

        return this.scrolledToBottom;
    }

    scrollMessagesToBottom() {
        if (this.elements.messagesContainer.scrollTopMax) {
            this.elements.messagesContainer.scrollTop = this.elements.messagesContainer.scrollTopMax;
        } else {
            this.elements.messagesList.lastChild.scrollIntoView();
        }
    }

    addMessage(messageData) {
        const scrolledToBottom = this.messagesAreScrolledToBottom();
        this.calculateTimestampWidth(messageData.timestamp);

        const liMessage = document.createElement('li');
        const spanTimestamp = document.createElement('span');
        spanTimestamp.classList.add('timestamp');
        spanTimestamp.innerText = messageData.timestamp;

        const spanMessage = document.createElement('span');
        spanMessage.classList.add('message');
        spanMessage.appendChild(this.processTemplate(messageData.templates))

        liMessage.appendChild(spanTimestamp);
        liMessage.appendChild(spanMessage);
        this.elements.messagesList.appendChild(liMessage);

        if (scrolledToBottom) {
            this.scrollMessagesToBottom();
        }
    }

    processTemplate(templates) {
        const frag = document.createDocumentFragment();

        for( const template of templates ) {
            const spanElement = document.createElement('span');
            switch (template.payload) {
                case 'text':
                    this.processTextTemplate(template, spanElement);
                    break;
                case 'url':
                    this.processUrlTemplate(template, spanElement);
                    break;
                case 'emote':
                    this.processEmote(template, spanElement);
                    break;
                case 'icon':
                    this.processIcon(template, spanElement);
                    break;
                case 'empty':
                    continue;
            }

            frag.appendChild(spanElement);
        }

        return frag;
    }

    processTextTemplate(template, spanContent) {
        spanContent.innerText = template.content;
        if (template.color !== 0)
        {
            this.processColor(template, spanContent);
        }
    }

    processUrlTemplate(template, spanContent) {
        const urlElement = document.createElement('a');
        urlElement.innerText = template.content;
        urlElement.href = encodeURI(template.content);
        urlElement.target = '_blank'

        if (template.color !== 0)
        {
            this.processColor(template, spanContent);
        }
    }

    // converts a RGBA uint number to components
    processColor(template, spanContent) {
        const r = (template.color & 0xFF000000) >>> 24;
        const g = (template.color & 0xFF0000) >>> 16;
        const b = (template.color & 0xFF00) >>> 8;
        const a = (template.color & 0xFF) / 255.0;

        spanContent.style.color = `rgba(${r}, ${g}, ${b}, ${a})`;
    }

    processEmote(template, spanContent) {
        const imgElement = document.createElement('img');
        imgElement.src = `/emote/${template.content}`;

        spanContent.classList.add('emote-icon');
        spanContent.appendChild(imgElement);
    }

    processIcon(template, spanContent) {
        spanContent.classList.add('gfd-icon');
        spanContent.classList.add(`gfd-icon-hq-${template.id}`);
    }

    clearAllMessages() {
        this.elements.messagesList.innerHTML = '';
    }

    setupSSEConnection() {
        this.sse = new EventSource('/sse');

        this.sse.addEventListener('close', () => {
            console.log('Closing SSE connection.');
            this.sse.close();
        });

        this.sse.addEventListener('switch-channel', (event) => {
            try {
                this.updateChannelHint(JSON.parse(event.data).channelName);
            } catch (error) {
                console.error(error);
            }
        });

        // new messages to be appended to the message list
        this.sse.addEventListener('new-message', (event) => {
            try {
                for (const message of JSON.parse(event.data).messages) {
                    this.addMessage(message);
                }
            } catch (error) {
                console.error(error);
            }
        });

        // a bulk of new messages, with a clear of the message list beforehand
        this.sse.addEventListener('bulk-messages', (event) => {
            this.clearAllMessages();
            try {
                for (const message of JSON.parse(event.data).messages) {
                    this.addMessage(message);
                }
            } catch (error) {
                console.error(error);
            }
        });

        this.sse.addEventListener('channel-list', (event) => {
            try {
                this.updateChannels(JSON.parse(event.data).channels);
            } catch (error) {
                console.error(error);
            }
        });
    }
}

window.addEventListener('load', function() {
    this._app = new ChatTwoWeb();
});
