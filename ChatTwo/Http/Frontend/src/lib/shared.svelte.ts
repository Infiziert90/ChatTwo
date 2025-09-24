export const isChannelLocked: { locked: boolean } = $state({ locked: false });
export const channelOptions: ChannelOption[] = $state([ { text: 'Invalid', value: 0, preview: true } ]);

export interface ChannelOption {
    text: string;
    value: number;
    preview: boolean;
}