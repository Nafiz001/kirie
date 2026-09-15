<script lang="ts">
	import { api, CRANE_COLOURS, type Book, type Scene } from '$lib/api';
	import { live } from '$lib/live.svelte';
	import { paperGrain } from '$lib/paper';

	// A paper tag taped to the mat beside the book, with everything the page
	// itself doesn't have room to print.
	let {
		scene,
		book,
		closed,
		onopenbook,
		onfolded
	}: {
		scene: Scene | null;
		book: Book;
		closed: boolean;
		onopenbook: () => void;
		onfolded: (scene: Scene) => void;
	} = $props();

	const compass = (degrees: number | null) =>
		degrees === null ? '' : ['north', 'north-east', 'east', 'south-east', 'south', 'south-west', 'west', 'north-west'][Math.round(degrees / 45) % 8];

	const round = (value: number | null, unit: string) => (value === null ? '–' : `${Math.round(value)}${unit}`);

	function ago(iso: string | null) {
		if (!iso) return '';
		const minutes = Math.round((Date.now() - Date.parse(iso)) / 60000);
		if (minutes < 2) return 'just now';
		if (minutes < 90) return `${minutes} minutes ago`;
		return `${Math.round(minutes / 60)} hours ago`;
	}

	const readers = $derived(
		!live.connected
			? 'Offline, so this page won’t change on its own'
			: live.onPage <= 1
				? 'Only you are reading this page'
				: `${live.onPage} people are reading this page`
	);

	const sunLine = $derived.by(() => {
		if (!scene) return '';
		const { sun } = scene;
		if (sun.alwaysUp) return 'The sun doesn’t set today';
		if (sun.alwaysDown) return 'The sun doesn’t rise today';
		return [sun.sunrise && `Sunrise ${sun.sunrise}`, sun.sunset && `sunset ${sun.sunset}`].filter(Boolean).join(', ');
	});

	const risingNow = $derived(book.cities.filter((c) => c.phase === 'golden-hour' || c.phase === 'civil-twilight'));

	let colour = $state('red');
	let folding = $state(false);
	let message = $state<string | null>(null);

	async function fold() {
		if (!scene || folding) return;
		folding = true;
		message = null;
		try {
			const { crane, total } = await api.fold(scene.city.id, colour);
			if (!scene.cranes.some((c) => c.id === crane.id)) {
				onfolded({ ...scene, cranes: [crane, ...scene.cranes], craneCount: total });
			}
			message = `Folded. It will fly over ${scene.city.name} for a day.`;
		} catch (e) {
			message = e instanceof Error ? e.message : String(e);
		} finally {
			folding = false;
		}
	}
</script>

<aside class="tag" style:background-image={paperGrain()}>
	<span class="tape" aria-hidden="true"></span>

	{#if scene}
		<header>
			<h2>{scene.city.name}</h2>
			<p class="country">{scene.city.country}</p>
		</header>

		<p class="now"><span class="clock">{scene.clock}</span> {scene.summary}</p>

		<dl class="facts">
			<div><dt>Air</dt><dd>{round(scene.conditions.temperatureC, ' °C')}</dd></div>
			<div><dt>Wind</dt><dd>{round(scene.conditions.windKph, ' km/h')} <small>{compass(scene.conditions.windDirection)}</small></dd></div>
			<div><dt>Cloud</dt><dd>{round(scene.conditions.cloudCover, '%')}</dd></div>
			<div><dt>Humidity</dt><dd>{round(scene.conditions.humidity, '%')}</dd></div>
		</dl>

		<p class="line">{sunLine}</p>
		<p class="line">{scene.moon.name}, {Math.round(scene.moon.illumination * 100)}% lit</p>

		<section class="cranes">
			<p>
				{#if scene.craneCount === 0}
					No one has folded a crane for {scene.city.name} yet.
				{:else}
					{scene.craneCount} {scene.craneCount === 1 ? 'crane has' : 'cranes have'} been folded for {scene.city.name}.
				{/if}
			</p>
			<div class="papers" role="radiogroup" aria-label="Paper colour">
				{#each Object.entries(CRANE_COLOURS) as [name, hex] (name)}
					<button
						class="paper"
						role="radio"
						aria-checked={colour === name}
						aria-label={name}
						style:--paper={hex}
						onclick={() => (colour = name)}
					></button>
				{/each}
			</div>
			<button class="fold" onclick={fold} disabled={folding}>{folding ? 'Folding…' : 'Fold a crane'}</button>
			{#if message}<p class="message" role="status">{message}</p>{/if}
		</section>

		<footer>
			<p>{readers}</p>
			<p>
				{#if scene.source === 'open-meteo'}
					Weather from Open-Meteo, observed {ago(scene.observedAt)}.
				{:else if scene.source === 'synthetic'}
					The weather service couldn’t be reached, so this sky is made up.
				{:else}
					No weather yet.
				{/if}
			</p>
		</footer>
	{:else}
		<header>
			<h2>kirie</h2>
			<p class="country">切り絵, paper cutting</p>
		</header>
		<p class="intro">
			The sky over {book.cities.length} places, cut from paper and lit by the weather there right now. Each page
			follows the real sun, moon, cloud and rain.
		</p>
		<p class="intro">
			The wheel is the Earth seen from above the North Pole, with the sun kept at the top. Pick a city on it, or turn
			the pages with the arrow keys.
		</p>
		{#if risingNow.length}
			<p class="line">
				The sun is rising or setting in {risingNow.map((c) => c.name).join(', ')}.
			</p>
		{/if}
		{#if closed}
			<button class="fold open" onclick={onopenbook}>Open the book</button>
		{/if}
		<footer>
			<p>
				{#if !live.connected}
					Offline
				{:else if live.inBook <= 1}
					Only you are reading the book
				{:else}
					{live.inBook} people are reading the book
				{/if}
			</p>
		</footer>
	{/if}
</aside>

<style>
	.tag {
		position: relative;
		width: 100%;
		padding: 34px 26px 22px;
		background-color: #fbfbf7;
		background-blend-mode: multiply;
		color: #1d2140;
		box-shadow:
			0 1px 0 rgb(0 0 0 / 0.06),
			0 18px 30px -8px rgb(0 0 0 / 0.45);
		rotate: -1.2deg;
		font-size: 15px;
		line-height: 1.45;
	}

	/* Washi tape holding the tag down, with torn ends. */
	.tape {
		position: absolute;
		top: -14px;
		left: 50%;
		width: 120px;
		height: 30px;
		translate: -50% 0;
		rotate: 3deg;
		background: rgb(242 169 0 / 0.82);
		clip-path: polygon(0 8%, 4% 0, 9% 10%, 14% 2%, 100% 0, 97% 30%, 100% 55%, 96% 78%, 100% 100%, 8% 100%, 3% 92%, 0 70%, 4% 45%);
		box-shadow: 0 1px 2px rgb(0 0 0 / 0.2);
	}

	h2 {
		margin: 0;
		font-family: 'Stardos Stencil', serif;
		font-weight: 700;
		font-size: 34px;
		line-height: 1;
	}

	.country {
		margin: 4px 0 0;
		color: #585d78;
	}

	.now {
		margin: 16px 0 14px;
		font-size: 17px;
	}

	.clock {
		font-family: 'Stardos Stencil', serif;
		font-weight: 700;
		font-size: 22px;
		margin-right: 4px;
	}

	.facts {
		display: grid;
		grid-template-columns: 1fr 1fr;
		gap: 10px 16px;
		margin: 0 0 14px;
		padding: 12px 0;
		border-block: 1px dashed rgb(29 33 64 / 0.25);
	}

	dt {
		font-size: 12.5px;
		color: #585d78;
	}

	dd {
		margin: 0;
		font-size: 17px;
		font-weight: 600;
		font-variant-numeric: tabular-nums;
	}

	dd small {
		font-weight: 400;
		font-size: 13px;
		color: #585d78;
	}

	.line,
	.intro {
		margin: 0 0 6px;
	}

	.intro {
		margin-top: 12px;
	}

	.cranes {
		margin-top: 16px;
		padding-top: 14px;
		border-top: 1px dashed rgb(29 33 64 / 0.25);
	}

	.cranes p {
		margin: 0 0 10px;
	}

	.papers {
		display: flex;
		gap: 6px;
		margin-bottom: 12px;
	}

	.paper {
		width: 24px;
		height: 24px;
		padding: 0;
		border: 0;
		background: var(--paper);
		box-shadow: inset 0 0 0 1px rgb(0 0 0 / 0.15);
		clip-path: polygon(0 0, 100% 0, 100% 100%, 0 100%);
		cursor: pointer;
		transition: translate 0.15s;
	}

	/* The chosen sheet has a folded corner. */
	.paper[aria-checked='true'] {
		clip-path: polygon(0 0, 62% 0, 100% 38%, 100% 100%, 0 100%);
		translate: 0 -3px;
		box-shadow:
			inset 0 0 0 1px rgb(0 0 0 / 0.25),
			0 3px 0 -1px #1d2140;
	}

	.fold {
		padding: 9px 16px 10px;
		border: 0;
		background: #1d2140;
		color: #fbfbf7;
		font: inherit;
		font-weight: 600;
		cursor: pointer;
		clip-path: polygon(0 0, calc(100% - 10px) 0, 100% 10px, 100% 100%, 0 100%);
	}

	.fold.open {
		margin-top: 12px;
	}

	.fold:disabled {
		opacity: 0.6;
	}

	.fold:focus-visible,
	.paper:focus-visible {
		outline: 2px solid #f2a900;
		outline-offset: 3px;
	}

	.message {
		margin-top: 10px !important;
		color: #3c4166;
	}

	footer {
		margin-top: 16px;
		font-size: 13px;
		color: #585d78;
	}

	footer p {
		margin: 0 0 3px;
	}
</style>
