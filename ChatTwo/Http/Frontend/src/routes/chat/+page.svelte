<script lang="ts">
    import { page } from '$app/state'
    import {Alert} from "@sveltestrap/sveltestrap";
    import { onMount } from 'svelte';
    import { ChatTwoWeb } from '$lib/chat'
    import {addGfdStylesheet} from "$lib/gfd";

    let data: App.Warning | null = null;
    $effect.pre(() => {
        if (page.url.searchParams.has('message')) {
            data = {
                hasWarning: true,
                content: page.url.searchParams.get('message') ?? '',
            };
        } else {
            data = {
                hasWarning: false,
                content: '',
            };
        }
    });

    onMount(() => {
        console.log('the component has mounted');

        // Populate the stylesheet with gfd data
        addGfdStylesheet('/files/gfdata.gfd', '/files/fonticon_ps5.tex');

        // Load all web functions in the background
        const _ = new ChatTwoWeb();
    });
</script>

<main class="chat">
    <section id="messages">
        <div class="scroll-container">
            <ol id="messages-list"></ol>
        </div>

        <div id="more-messages-indicator">
            <!-- "arrow-down" icon from https://github.com/feathericons/feather, under MIT license -->
            <svg xmlns="http://www.w3.org/2000/svg" width="50" height="50" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="5" x2="12" y2="19"/><polyline points="19 12 12 19 5 12"/></svg>
        </div>
    </section>

    {#if data?.hasWarning }
        <section id="warnings">
            <Alert content={data.content} color="warning" dismissible={true}/>
        </section>
    {/if}

    <section id="input">
        <form>
            <div class="select-container">
                <select id="channel-select"></select>
            </div>

            <div class="input-container">
                <input type="text" id="chat-input" autocomplete="off" placeholder="Message" enterkeyhint="send" maxlength="500">
                <div id="channel-hint"></div>
            </div>

            <button type="submit">Send</button>
        </form>
    </section>
</main>

<div id="timestamp-width-probe"></div>