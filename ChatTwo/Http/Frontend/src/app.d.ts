// See https://svelte.dev/docs/kit/types#app.d.ts
// for information about these interfaces
declare global {
	namespace App {
		interface Error {
            code: string;
            id: string;
        }
		// interface Locals {}
		// interface PageData {}
		// interface PageState {}
		// interface Platform {}

        interface Warning {
            hasWarning: boolean;
            content: string;
        }
	}

    interface Element { scrollTopMax: number } // Firefox only property
}

export {};
