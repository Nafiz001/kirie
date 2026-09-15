import adapter from '@sveltejs/adapter-static';
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vite';

// The ASP.NET Core server. In development Vite proxies the API and the
// SignalR hub to it, so the browser only ever talks to one origin.
const server = process.env.KIRIE_SERVER ?? 'http://localhost:5080';

export default defineConfig({
	plugins: [
		sveltekit({
			compilerOptions: {
				// Force runes mode for the project, except for libraries. Can be removed in svelte 6.
				runes: ({ filename }) =>
					filename.split(/[/\\]/).includes('node_modules') ? undefined : true
			},
			// The book is a single client-rendered page; the server can host the
			// built files, or any static host can with the API elsewhere.
			adapter: adapter({ fallback: 'index.html' })
		})
	],
	server: {
		proxy: {
			'/api': server,
			'/hubs': { target: server, ws: true }
		}
	}
});
