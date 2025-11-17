import { channelOptions, isChannelLocked, selectedTab, knownTabs, chatInput, messagesList, scrollMessagesToBottom } from "$lib/shared.svelte";
import { WebPayloadType } from "$lib/payload";
import { source, type Source } from "sveltekit-sse";

interface ChatElements {
    messagesContainer: Element | null,
    messagesList: HTMLElement | null,

    timestampWidthProbe: HTMLElement | null,

    inputForm: Element | null,
}

// ref `DataStructure.Messages`
interface Messages {
    messages: MessageResponse[]
}

// ref `DataStructure.MessageResponse`
interface MessageResponse {
    id: string;
    timestamp: string;
    templates: Template[];
}

// ref `DataStructure.MessageTemplate`
interface Template {
    payloadType: WebPayloadType;
    content: string;
    iconId: number;
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
    unreadCount: number;
}

// ref `DataStructure.ChatTabList`
interface ChatTabList {
    tabs: ChatTab[];
}

// ref `DataStructure.ChatTabUnreadState`
interface ChatTabUnreadState {
    index: number;
    unreadCount: number;
}

export class ChatTwoWeb {
    elements!: ChatElements;
    maxTimestampWidth: number = 0;

    sse!: EventSource;
    connection!: Source;

    constructor() {
        this.setupDOMElements();
        this.setupSSEConnection();
    }

    setupDOMElements() {
        this.elements = {
            messagesContainer: document.querySelector('#messages > .scroll-container')!,
            messagesList: document.getElementById('messages-list'),

            timestampWidthProbe: document.getElementById('timestamp-width-probe'),

            inputForm: document.querySelector('#input > form'),
        };
        messagesList.element = this.elements.messagesList;

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
            if (messagesList.scrolledToBottom) {
                scrollMessagesToBottom();
            }
        })

        // handle message sending
        this.elements.inputForm?.addEventListener('submit', async (event) => {
            event.preventDefault();
            if (chatInput.content.length > 500) {
                return;
            }

            const rawResponse = await fetch('/send', {
                method: 'POST',
                headers: {
                    'Accept': 'application/json',
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ message: chatInput.content })
            });
            // const content = await rawResponse.json();
            // TODO: use the response

            chatInput.content = '';
        });
    }

    messagesAreScrolledToBottom() {
        if (this.elements.messagesContainer === null) {
            return messagesList.scrolledToBottom;
        }

        messagesList.scrolledToBottom =
            (
                this.elements.messagesContainer.scrollHeight -
                this.elements.messagesContainer.clientHeight -
                this.elements.messagesContainer.scrollTop
            ) < 1;

        return messagesList.scrolledToBottom;
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
            scrollMessagesToBottom();
        }
    }

    processTemplate(templates: Template[]) {
        const frag = document.createDocumentFragment();

        for( const template of templates ) {
            const spanElement = document.createElement('span');
            switch (template.payloadType) {
                case WebPayloadType.RawText:
                    this.processTextTemplate(template, spanElement);
                    break;
                case WebPayloadType.CustomUri:
                    this.processUrlTemplate(template, spanElement);
                    break;
                case WebPayloadType.CustomEmote:
                    this.processEmote(template, spanElement);
                    break;
                case WebPayloadType.Icon:
                    this.processIcon(template, spanElement);
                    break;
                default:
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
        spanElement.classList.add(`gfd-icon-hq-${template.iconId}`);
    }

    clearAllMessages() {
        if (this.elements.messagesList === null)
            return;

        this.elements.messagesList.innerHTML = '';
    }

    setupSSEConnection() {
        this.connection = source('/sse')

        this.connection.select('close').subscribe((data: string) => {
            console.log(`close: ${data}`)
            if (data) {
                console.log('Closing SSE connection.');
                this.connection.close();
            }
        });

        // new messages to be appended to the message list
        this.connection.select('new-message').subscribe((data: string) => {
            console.log(`new-message: ${data}`)
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
            console.log(`bulk-messages: ${data}`)
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
            console.log(`channel-switched: ${data}`)
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
            console.log(`channel-list: ${data}`)
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

        // the unread state of a specific tab has changed
        this.connection.select('tab-unread-state').subscribe((data: string) => {
            console.log(`tab-unread-state`, data)
            if (data) {
                try {
                    const chatTabUnreadState: ChatTabUnreadState = JSON.parse(data);
                    let tab = knownTabs.find((tab) => tab.index === chatTabUnreadState.index);
                    if (tab) {
                        tab.unreadCount = chatTabUnreadState.unreadCount;
                    }
                    else {
                        console.error("Unable to find tab!")
                        console.error(chatTabUnreadState)
                    }
                } catch (error) {
                    console.error(error);
                }
            }
        });
    }
}