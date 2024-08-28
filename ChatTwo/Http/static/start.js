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
                this.elements.messagesList.lastChild.scrollIntoView();
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

    updateChannelHint(labelHTML) {
        this.elements.channelHint.innerHTML = labelHTML;
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
        this.scrolledToBottom =
            this.elements.messagesContainer.scrollTop >= this.elements.messagesContainer.scrollHeight - this.elements.messagesContainer.offsetHeight;
        return this.scrolledToBottom;
    }

    addMessage(messageData) {
        const scrolledToBottom = this.messagesAreScrolledToBottom();
        this.calculateTimestampWidth(messageData.timestamp);

        const liMessage = document.createElement('li');
        const spanTimestamp = document.createElement('span');
        spanTimestamp.classList.add('timestamp');
        const spanMessage = document.createElement('span');
        spanMessage.classList.add('message');

        spanTimestamp.innerText = messageData.timestamp;
        spanMessage.innerHTML = messageData.messageHTML;

        liMessage.appendChild(spanTimestamp);
        liMessage.appendChild(spanMessage);
        this.elements.messagesList.appendChild(liMessage);

        if (scrolledToBottom) {
            liMessage.scrollIntoView();
        }
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
                this.updateChannelHint(JSON.parse(event.data).channel);
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
