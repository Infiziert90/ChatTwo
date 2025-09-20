<script lang="ts">
    import { page } from '$app/state'
    import { Alert } from '@sveltestrap/sveltestrap';

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
</script>

<main class="auth">
    <h1>Authcode</h1>
    {#if data?.hasWarning }
        <Alert content={data.content} color="warning" dismissible={true}/>
    {/if}
    <form action="/auth" method="POST">
        <label><input type="password" name="authcode"></label>
        <button type="submit" class="submitButton">Submit</button>
    </form>
    <div data-sveltekit-preload-data="false">
        <img src="/emote/Sure" alt=":Sure:" data-sveltekit-preload-data="off">
    </div>
</main>