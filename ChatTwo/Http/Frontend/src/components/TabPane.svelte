<script lang="ts">
    import { selectedTab, knownTabs, tabPaneState, tabPaneAnimationState, closeTabPane, messagesList, scrollMessagesToBottom } from "$lib/shared.svelte";

    async function selectTab(index: number) {
        const rawResponse = await fetch('/tab', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ index })
        });
        // const content = await rawResponse.json();
        // TODO: use the response
    }

    function handleClose() {
        tabPaneAnimationState.noAnimation = false;
        closeTabPane();
    }

    let scrolledToBottom = true;
    function ontransitionstart() {
        scrolledToBottom = messagesList.scrolledToBottom;
    }

    function ontransitionend() {
        if (scrolledToBottom)
            scrollMessagesToBottom();
    }
</script>

<aside
    id="tabs"
    class:no-animation={tabPaneAnimationState.noAnimation}
    class:hidden={!tabPaneState.visible}
    {ontransitionstart}
    {ontransitionend}
>
    <div class="inner">
        <header>
            <span>Tabs</span>
            <button type="button" onclick={() => handleClose()}>
                <!-- "chevron-left" icon from https://github.com/feathericons/feather, under MIT license -->
                <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="15 18 9 12 15 6"/></svg>
            </button>
        </header>

        <hr>

        <ol id="tabs-list">
            {#each knownTabs as tab}
                <li class:active={selectedTab.index == tab.index}>
                    <button type="button" onclick={() => selectTab(tab.index)}>
                        { tab.name }
                    </button>
                </li>
            {/each}
        </ol>
    </div>
</aside>
