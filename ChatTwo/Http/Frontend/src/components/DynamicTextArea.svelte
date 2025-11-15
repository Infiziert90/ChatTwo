<script lang="ts">
    import { onMount } from "svelte";
    import { subscribe } from "$lib/utils.svelte";
    import { chatInput, messagesList, scrollMessagesToBottom } from "$lib/shared.svelte";

    let textarea: HTMLTextAreaElement;

    let skipNextCheck: boolean = $state(false);
    let requiresResize: boolean = $state(true);

    subscribe(
        () => chatInput,
        (v) => {
            if (skipNextCheck) {
                skipNextCheck = false;
                return;
            }

            // Input box has been reset to empty, so resize it back to smaller box
            if (v.content === '') {
                console.log("Empty chatbox, resize");
                requiresResize = true;
                return;
            }

            // Remove newline characters
            let original = v.content;
            v.content = v.content.replace(/(\r\n|\n|\r)/gm,"");

            console.log(`${original.length} vs ${v.content.length}`);
            let hasChanged = original.length != v.content.length;
            if (hasChanged) {
                skipNextCheck = true;
                requiresResize = true;
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

        const scrolledToBottom = messagesList.scrolledToBottom;
        textarea.style.height = '1px';
        textarea.style.height = `${textarea.scrollHeight + 10}px`; // with +10px extra padding
        if (scrolledToBottom)
            scrollMessagesToBottom();
    }

    $effect(() => {
        console.log(`Checking effect: ${requiresResize}`)
        if (requiresResize) {
            requiresResize = false;
            resize();
        }
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
