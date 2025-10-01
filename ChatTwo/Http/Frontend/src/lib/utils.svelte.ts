import {writable} from "svelte/store";

// https://stackoverflow.com/a/79696571
export const subscribe = <T>(functionToState: () => T, callback: (v: T) => void) => {
    let value = writable<T>(functionToState());
    value.subscribe(callback);

    $effect(() => {
        value.set(functionToState());
    });
};