import { channelOptions, isChannelLocked, selectedTab, knownTabs } from "$lib/shared.svelte";
import { source, type Source } from "sveltekit-sse";

interface ChatElements {
    messagesContainer: Element | null,
    messagesList: HTMLElement | null,

    timestampWidthProbe: HTMLElement | null,

    inputForm: Element | null,
    chatInput: HTMLElement | null,
}

// ref `DataStructure.Messages`
interface Messages {
    messages: MessageResponse[]
}

// ref `DataStructure.MessageResponse`
interface MessageResponse {
    timestamp: string;
    templates: Template[];
}

// ref `DataStructure.MessageTemplate`
interface Template {
    id: number;
    payload: string;
    content: string;
    color: number;
}

// ref `DataStructure.SwitchChannel`
interface SwitchChannel {
    channelName: Template[];
    channelValue: number;
    channelLocked: boolean;
}

// ref `DataStructure.ChannelList`
interface ChannelList {
    channels: {[key: string]: number};
}

// ref `DataStructure.ChatTab`
export interface ChatTab {
    name: string;
    index: number;
}

// ref `DataStructure.ChatTabList`
interface ChatTabList {
    tabs: ChatTab[];
}

export class ChatTwoWeb {
    elements!: ChatElements;
    maxTimestampWidth: number = 0;
    scrolledToBottom: boolean = true;

    sse!: EventSource;
    connection!: Source;

    constructor() {
        this.setupDOMElements();
        this.setupSSEConnection();
    }

    setupDOMElements() {
        this.elements = {
            // channelHint: document.getElementById('channel-hint'),
            // channelSelect: document.getElementById('channel-select'),

            messagesContainer: document.querySelector('#messages > .scroll-container')!,
            messagesList: document.getElementById('messages-list'),

            timestampWidthProbe: document.getElementById('timestamp-width-probe'),

            inputForm: document.querySelector('#input > form'),
            chatInput: document.getElementById('chat-input')
        };

        // add indicator signaling more messages below
        this.elements.messagesContainer?.addEventListener('scroll', (event) => {
            if (event.currentTarget === null)
                return;

            let parentElement = (event.currentTarget as HTMLDivElement).parentElement;
            if (!this.messagesAreScrolledToBottom()) {
                parentElement?.classList.add('more-messages');
            } else {
                parentElement?.classList.remove('more-messages');
            }
        });

        // adjust scroll when the window size changes; mostly for mobile (opening/closing the keyboard)
        window.addEventListener('resize', () => {
            if (this.scrolledToBottom) {
                this.scrollMessagesToBottom();
            }
        })

        // handle message sending
        this.elements.inputForm?.addEventListener('submit', async (event) => {
            if (this.elements.chatInput === null)
                return;

            event.preventDefault();
            // @ts-ignore
            const message = this.elements.chatInput.value;
            if (message.length > 500) {
                return;
            }

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

            // @ts-ignore
            this.elements.chatInput.value = '';
        });
    }

    messagesAreScrolledToBottom() {
        if (this.elements.messagesContainer === null) {
            return this.scrolledToBottom;
        }

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

    updateChannelHint(channel: SwitchChannel) {
        // Set storage to the current lock state
        isChannelLocked.locked = channel.channelLocked;

        const channelElement = this.processTemplate(channel.channelName);
        if (!channelElement.firstChild)
            return;

        let channelName = (channelElement.firstChild as HTMLSpanElement).innerText;
        if (channel.channelLocked)
            channelName = `(Locked) ${channelName}`;

        channelOptions[0] = {text: channelName, value: 0, preview: true }
    }

    updateChannels(channelList: ChannelList) {
        channelOptions.length = 1;

        for (const [ label, channel ] of Object.entries(channelList.channels)) {
            channelOptions.push( { text: label, value: channel, preview: false } )
        }
    }

    // calculate timestamp width to ensure that all timestamps have the same width.
    // some typefaces have the same width across all number glyphs, others do not.
    // then there's AM/PM vs 24 hour, and so on
    calculateTimestampWidth(timestamp: string) {
        if (this.elements.timestampWidthProbe === null)
            return;

        this.elements.timestampWidthProbe.innerText = timestamp;
        if (this.elements.timestampWidthProbe.clientWidth > this.maxTimestampWidth) {
            this.maxTimestampWidth = this.elements.timestampWidthProbe.clientWidth;
            document.body.style.setProperty('--timestamp-width', (Math.ceil(this.maxTimestampWidth) + 1) + 'px');
        }
    }

    scrollMessagesToBottom() {
        if (this.elements.messagesContainer === null || this.elements.messagesList === null)
            return;

        if (this.elements.messagesContainer.scrollTopMax) {
            this.elements.messagesContainer.scrollTop = this.elements.messagesContainer.scrollTopMax;
        } else {
            if (this.elements.messagesList.lastElementChild === null)
                return;

            this.elements.messagesList.lastElementChild.scrollIntoView();
        }
    }

    addMessage(messageData: MessageResponse) {
        if (this.elements.messagesList === null)
            return;

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

    processTemplate(templates: Template[]) {
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

    processTextTemplate(template: Template, spanElement: HTMLSpanElement) {
        spanElement.innerText = template.content;
        if (template.color !== 0)
        {
            this.processColor(template, spanElement);
        }
    }

    processUrlTemplate(template: Template, spanElement: HTMLSpanElement) {
        const urlElement = document.createElement('a');
        urlElement.innerText = template.content;
        urlElement.href = encodeURI(template.content);
        urlElement.target = '_blank'

        if (template.color !== 0)
        {
            this.processColor(template, spanElement);
        }
    }

    // converts a RGBA uint number to components
    processColor(template: Template, spanElement: HTMLSpanElement) {
        const r = (template.color & 0xFF000000) >>> 24;
        const g = (template.color & 0xFF0000) >>> 16;
        const b = (template.color & 0xFF00) >>> 8;
        const a = (template.color & 0xFF) / 255.0;

        spanElement.style.color = `rgba(${r}, ${g}, ${b}, ${a})`;
    }

    processEmote(template: Template, spanElement: HTMLSpanElement) {
        const imgElement = document.createElement('img');
        imgElement.src = `/emote/${template.content}`;

        spanElement.classList.add('emote-icon');
        spanElement.appendChild(imgElement);
    }

    processIcon(template: Template, spanElement: HTMLSpanElement) {
        spanElement.classList.add('gfd-icon');
        spanElement.classList.add(`gfd-icon-hq-${template.id}`);
    }

    clearAllMessages() {
        if (this.elements.messagesList === null)
            return;

        this.elements.messagesList.innerHTML = '';
    }

    setupSSEConnection() {
        this.connection = source('/sse')

        this.connection.select('close').subscribe((data: string) => {
            console.log(`Data received: ${data}`)
            if (data) {
                console.log('Closing SSE connection.');
                this.connection.close();
            }
        });

        // new messages to be appended to the message list
        this.connection.select('new-message').subscribe((data: string) => {
            console.log(`Data received: ${data}`)
            if (data) {
                try {
                    let message: MessageResponse = JSON.parse(data);
                    this.addMessage(message);
                } catch (error) {
                    console.error(error);
                }
            }
        });

        // a bulk of new messages, with a clear of the message list beforehand
        this.connection.select('bulk-messages').subscribe((data: string) => {
            console.log(`Data received: ${data}`)
            if (data) {
                this.clearAllMessages();
                try {
                    let messages: Messages = JSON.parse(data);
                    for (const message of messages.messages) {
                        this.addMessage(message);
                    }
                } catch (error) {
                    console.error(error);
                }
            }
        });

        this.connection.select('channel-switched').subscribe((data: string) => {
            console.log(`Data received: ${data}`)
            if (data) {
                try {
                    let channel: SwitchChannel = JSON.parse(data);
                    this.updateChannelHint(channel);
                } catch (error) {
                    console.error(error);
                }
            }
        });

        // list of all channels
        this.connection.select('channel-list').subscribe((data: string) => {
            console.log(`Data received: ${data}`)
            if (data) {
                try {
                    let channelList: ChannelList = JSON.parse(data);
                    this.updateChannels(channelList);
                } catch (error) {
                    console.error(error);
                }
            }
        });

        // tab switched
        this.connection.select('tab-switched').subscribe((data: string) => {
            console.log(`tab-switched: ${data}`)
            if (data) {
                try {
                    const chatTab: ChatTab = JSON.parse(data);
                    selectedTab.index = chatTab.index;
                } catch (error) {
                    console.error(error);
                }
            }
        });

        // list of all tabs
        this.connection.select('tab-list').subscribe((data: string) => {
            console.log(`tab-list: ${data}`)
            if (data) {
                try {
                    const chatTabList: ChatTabList = JSON.parse(data);
                    knownTabs.length = 0;
                    for (const tab of chatTabList.tabs) {
                        knownTabs.push(tab);
                    }
                } catch (error) {
                    console.error(error);
                }
            }
        });
    }
}
