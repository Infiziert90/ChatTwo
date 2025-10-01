<script lang="ts">
    import {onMount} from "svelte";
    import {subscribe} from "$lib/utils.svelte";
    import {chatInput} from "$lib/shared.svelte";

    let textarea: HTMLTextAreaElement;

    subscribe(
        () => chatInput,
        (v) => {
            // Input box has been reset to empty, so resize it back to smaller box
            if (v.content === '') {
                resize();
            }
        }
    );

    function preventNewlines(e: KeyboardEvent) {
        if (e.key === 'Enter') {
            // Prevent key from creating a newline
            e.preventDefault();

            // submit the data
            const newEvent = new Event('submit', {bubbles: true, cancelable: true});
            if (e.currentTarget !== null) {
                (e.currentTarget as HTMLTextAreaElement).closest('form')?.dispatchEvent(newEvent);
            }
        }
    }

    function resize() {
        if (!textarea)
            return;

        textarea.style.height = '1px';
        textarea.style.height = `${textarea.scrollHeight + 10}px`; // with +10px extra padding
    }

    onMount(() => {
        resize();
    })
</script>

<textarea
    bind:this={textarea}
    bind:value={chatInput.content}
    oninput={() => resize()}
    onkeydown={(e) => preventNewlines(e)}

    id="chat-input"
    autocomplete="off"
    placeholder="Message"
    enterkeyhint="send"
    maxlength="500">
</textarea>

<style>
    textarea {
        flex-grow: 0;

        font-size: 1rem;
        border: 3px solid transparent;
        border-radius: 20px;
        background-color: var(--bg-input);

        &:focus {
            outline: 2px solid var(--focus-color);
        }

        width: 100%;

        min-height: 2.5em;
        line-height: 1.25;
    }
</style>