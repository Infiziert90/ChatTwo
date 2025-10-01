import type { ChatTab } from "./chat.svelte";

export const isChannelLocked: { locked: boolean } = $state({ locked: false });
export const channelOptions: ChannelOption[] = $state([ { text: 'Invalid', value: 0, preview: true } ]);

export interface ChannelOption {
    text: string;
    value: number;
    preview: boolean;
}

export const selectedTab: { index: number } = $state({ index: 0 });
export const knownTabs: ChatTab[] = $state([]);
export const tabPaneState: { visible: boolean } = $state({ visible: false });
export const persistentTabPabeStateKey = 'chat2_tab_pane_visible';

export function openTabPane() {
    tabPaneState.visible = true;
    window.localStorage.setItem(persistentTabPabeStateKey, 'true');
}

export function closeTabPane() {
    tabPaneState.visible = false;
    window.localStorage.setItem(persistentTabPabeStateKey, 'false');
}

export const chatInput: { content: string } = $state({ content: ''} );