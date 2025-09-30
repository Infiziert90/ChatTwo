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
export const tabBarState: { visible: boolean } = $state({ visible: false });
