<script lang="ts">
    import { page } from '$app/state'
    import { Alert } from "@sveltestrap/sveltestrap";
    import { onMount } from 'svelte';
    import { ChatTwoWeb } from '$lib/chat.svelte'
    import { addGfdStylesheet } from "$lib/gfd";
    import DynamicTextArea from "../../components/DynamicTextArea.svelte";
    import ChannelSelector from "../../components/ChannelSelector.svelte";
    import TabPane from "../../components/TabPane.svelte";
    import TabPaneOpener from "../../components/TabPaneOpener.svelte";

    let data: App.Warning = $state({ hasWarning: false, content: '' });
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
    <TabPane />

    <div class="main-content">
        <TabPaneOpener />

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
                <div class="input-container">
                    <DynamicTextArea />
                    <ChannelSelector />
                </div>

                <button type="submit">Send</button>
            </form>
        </section>
    </div>
</main>

<div id="timestamp-width-probe"></div>
