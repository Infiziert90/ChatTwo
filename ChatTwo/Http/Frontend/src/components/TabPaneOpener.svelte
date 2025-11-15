<script lang="ts">
    import { tabPaneState, tabPaneAnimationState, openTabPane } from "$lib/shared.svelte";

    function onclick() {
        tabPaneAnimationState.noAnimation = false;
        openTabPane();
    }
</script>

<button type="button" aria-label="Open tab pane" class:visible={!tabPaneState.visible} {onclick} disabled={tabPaneState.visible}>
    <!-- "chevron-right" icon from https://github.com/feathericons/feather, under MIT license -->
    <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 18 15 12 9 6"/></svg>
</button>

<style>
    button {
        position: absolute;
        top: 50%;
        left: 0;
        padding: 25px 0;
        border: none;
        background-color: transparent;
        transform: translateY(-50%);
        z-index: 100;
        opacity: 0;
        transition: opacity 250ms ease;
    }

    button.visible {
        opacity: 1;
    }

    button.unread svg {
        stroke: var(--unread-color);
        filter: drop-shadow(0 0 2px var(--unread-color));
    }
</style>
