<script lang="ts">
    import { selectedTab, knownTabs, tabBarState } from "$lib/shared.svelte";

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

    function closeTabBar() {
        tabBarState.visible = false;
    }
</script>

<aside id="tabs" class:visible={tabBarState.visible}>
    <div class="inner">
        <header>
            <span>Tabs</span>
            <button type="button" onclick={() => closeTabBar()}>
                <!-- "x" icon from https://github.com/feathericons/feather, under MIT license -->
                <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
            </button>
        </header>

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
