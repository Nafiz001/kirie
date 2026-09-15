// The live connection to the book: which page this reader has open, how many
// others are reading, fresh scenes as the weather and the sun move, and cranes
// folded by anyone.

import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import type { Crane, Scene } from './api';

type Handlers = {
	scene: (scene: Scene) => void;
	crane: (cityId: string, crane: Crane, total: number) => void;
};

class Live {
	connected = $state(false);
	onPage = $state(0);
	inBook = $state(0);

	#page = 'contents';
	#handlers: Handlers | null = null;
	#connection = new HubConnectionBuilder()
		.withUrl('/hubs/book')
		.withAutomaticReconnect([0, 1000, 3000, 8000, 15000, 30000])
		.configureLogging(LogLevel.Warning)
		.build();

	constructor() {
		const c = this.#connection;
		c.on('Scene', (scene: Scene) => this.#handlers?.scene(scene));
		c.on('CraneFolded', (cityId: string, crane: Crane, total: number) => this.#handlers?.crane(cityId, crane, total));
		c.on('Readers', (page: string, onPage: number, inBook: number) => {
			this.inBook = inBook;
			if (page === this.#page) this.onPage = onPage;
		});
		c.onreconnecting(() => (this.connected = false));
		c.onreconnected(async () => {
			this.connected = true;
			await this.open(this.#page);
		});
		c.onclose(() => (this.connected = false));
	}

	async start(handlers: Handlers) {
		this.#handlers = handlers;
		if (this.#connection.state !== HubConnectionState.Disconnected) return;
		try {
			await this.#connection.start();
			this.connected = true;
			await this.open(this.#page);
		} catch {
			this.connected = false;
			setTimeout(() => this.start(handlers), 5000);
		}
	}

	async open(page: string) {
		this.#page = page;
		if (this.#connection.state !== HubConnectionState.Connected) return;
		try {
			await this.#connection.invoke('Open', page);
		} catch {
			// The next reconnect opens it again.
		}
	}
}

export const live = new Live();
