<script lang="ts">
    import {isChannelLocked, channelOptions} from "$lib/shared.svelte";

    let selectElement: HTMLSelectElement;

    async function requestChannelSwitch(event: Event) {
        if (!event.currentTarget)
            return;

        let element = (event.currentTarget as HTMLSelectElement);
        let requestedChannel = element.value;

        console.log(element.value)
        element.value = '0';

        const rawResponse = await fetch('/channel', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ channel: requestedChannel })
        });
        // const content = await rawResponse.json();
        // TODO: use the response
    }

    let canvas: HTMLCanvasElement | null = null;
    function getTextWidth(text: string): number {
        // re-use canvas object for better performance
        if (canvas === null)
            canvas = document.createElement("canvas");

        const context: CanvasRenderingContext2D | null = canvas.getContext("2d");
        if (!context)
            return 0;

        context.font = getCanvasFont(selectElement);
        const metrics = context.measureText(text);
        return metrics.width;
    }

    function getCssStyle(element: Element, prop: string): string {
        return window.getComputedStyle(element, null).getPropertyValue(prop);
    }

    function getCanvasFont(el = document.body) {
        const fontWeight = getCssStyle(el, 'font-weight') || 'normal';
        const fontSize = getCssStyle(el, 'font-size') || '16px';
        const fontFamily = getCssStyle(el, 'font-family') || 'Times New Roman';

        return `${fontWeight} ${fontSize} ${fontFamily}`;
    }
</script>

<select
    bind:this={selectElement}
    id="channel-select"
    style="pointer-events: {isChannelLocked.locked ? 'none' : 'inherit'}; width: {(channelOptions.length > 1 ? getTextWidth(channelOptions[0].text) : 1) + 40}px"
    onchange={(e) => requestChannelSwitch(e)}>
    {#each channelOptions as channelOption}
        {#if channelOption.preview }
        <option selected disabled hidden value={channelOption.value}>
            {channelOption.text}
        </option>
        {:else}
        <option value={channelOption.value}>
            {channelOption.text}
        </option>
        {/if}
    {/each}
</select>

<style>
    select {
        border: none;
        background-color: transparent;
    }
</style>