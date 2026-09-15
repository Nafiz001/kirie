<script lang="ts">
	import { replaceState } from '$app/navigation';
	import { api, type Book as BookData, type CityCard, type Scene } from '$lib/api';
	import { live } from '$lib/live.svelte';
	import { onMount } from 'svelte';
	import Book from './Book.svelte';
	import Contents from './Contents.svelte';
	import Diorama from './Diorama.svelte';
	import Tag from './Tag.svelte';

	// Page 0 is the contents wheel; pages 1 to n are the cities in book order.
	let book = $state<BookData | null>(null);
	let page = $state(0);
	let scene = $state<Scene | null>(null);
	let pop = $state(0);
	let turning = $state(false);
	let direction = $state<1 | -1>(1);
	let error = $state<string | null>(null);
	// The book starts closed, unless the link points straight at a page.
	let closed = $state(true);
	let cover = $state(true);

	let viewport = $state({ width: 1440, height: 900 });
	let pointer = $state({ x: 0, y: 0 });
	let reducedMotion = $state(false);

	const narrow = $derived(viewport.width < 900);
	const width = $derived(
		narrow ? Math.min(viewport.width * 0.9, viewport.height * 0.75) : Math.min(1040, (viewport.width - 400) * 0.78, viewport.height * 1.02)
	);

	const pages = $derived(book ? book.cities.length + 1 : 1);
	const cityAt = (p: number) => (book && p > 0 ? book.cities[p - 1] : null);
	const previous = $derived(page > 0 ? (cityAt(page - 1)?.name ?? 'Contents') : null);
	const next = $derived(page < pages - 1 ? cityAt(page + 1)?.name : null);

	const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, reducedMotion ? 0 : ms));
	const raise = () => requestAnimationFrame(() => requestAnimationFrame(() => (pop = 1)));

	let busy = false;

	async function go(target: number, { instant = false } = {}) {
		if (!book || busy || target < 0 || target >= pages || (target === page && (scene || target === 0) && !instant)) return;
		busy = true;
		error = null;
		try {
			const city = cityAt(target);
			const nextScene = city ? await api.scene(city.id) : null;

			if (!instant) {
				direction = target > page ? 1 : -1;
				pop = 0;
				await sleep(620);
				turning = true;
				await sleep(430);
			}

			page = target;
			scene = nextScene;
			live.open(city ? city.id : 'contents');
			replaceState(city ? `#${city.id}` : location.pathname, {});

			if (!instant) await sleep(470);
			turning = false;
			raise();
		} catch (e) {
			error = e instanceof Error ? e.message : String(e);
		} finally {
			busy = false;
		}
	}

	const openCity = (city: CityCard) => go(book!.cities.findIndex((c) => c.id === city.id) + 1);

	async function openBook() {
		if (!closed) return;
		closed = false;
		await sleep(1050);
		raise();
		await sleep(500);
		cover = false;
	}

	function fromHash() {
		if (!book) return;
		const id = location.hash.slice(1);
		const index = book.cities.findIndex((c) => c.id === id);
		if (index >= 0 && closed) {
			closed = false;
			cover = false;
			go(index + 1, { instant: true });
			return;
		}
		go(index >= 0 ? index + 1 : 0);
	}

	onMount(() => {
		const resize = () => (viewport = { width: innerWidth, height: innerHeight });
		resize();
		addEventListener('resize', resize);
		addEventListener('hashchange', fromHash);
		const motion = matchMedia('(prefers-reduced-motion: reduce)');
		reducedMotion = motion.matches;

		live.start({
			scene: (s) => {
				if (scene && s.city.id === scene.city.id) scene = s;
			},
			crane: (cityId, crane, total) => {
				if (scene && scene.city.id === cityId && !scene.cranes.some((c) => c.id === crane.id)) {
					scene = { ...scene, cranes: [crane, ...scene.cranes], craneCount: total };
				}
			}
		});

		const refreshBook = () =>
			api
				.book()
				.then((b) => (book = b))
				.catch((e) => (error = e.message));

		refreshBook().then(() => {
			const id = location.hash.slice(1);
			const index = book ? book.cities.findIndex((c) => c.id === id) : -1;
			if (index >= 0) {
				closed = false;
				cover = false;
				go(index + 1, { instant: true });
			}
		});
		// The contents wheel turns with the Earth.
		const timer = setInterval(refreshBook, 60_000);

		return () => {
			removeEventListener('resize', resize);
			removeEventListener('hashchange', fromHash);
			clearInterval(timer);
		};
	});

	function onkey(event: KeyboardEvent) {
		if ((event.target as HTMLElement | null)?.closest?.('input, textarea')) return;
		if (closed) {
			if (event.key === 'ArrowRight' || event.key === 'Enter') openBook();
			return;
		}
		if (event.key === 'ArrowRight' || event.key === 'PageDown') go(page + 1);
		else if (event.key === 'ArrowLeft' || event.key === 'PageUp') go(page - 1);
		else if (event.key === 'Home') go(0);
	}

	const utc = $derived(book ? new Date(book.now).toISOString().slice(11, 16) : '');
</script>

<svelte:window
	onkeydown={onkey}
	onpointermove={(e) => (pointer = { x: e.clientX / viewport.width - 0.5, y: e.clientY / viewport.height - 0.5 })}
/>

<main class="mat" class:narrow>
	<h1 class="sr-only">kirie, a pop-up book of the sky</h1>
	<p class="sr-only" aria-live="polite">{scene ? `${scene.city.name}: ${scene.summary}, ${scene.clock} local time` : 'Contents'}</p>

	{#if error}
		<p class="error" role="alert">{error}</p>
	{/if}

	{#if book}
		<div class="desk" style:--book-width="{width}px">
			<Book
				{width}
				tiltX={(narrow ? 44 : 54) - (reducedMotion ? 0 : pointer.y * 4)}
				tiltY={reducedMotion ? 0 : pointer.x * 5}
				{turning}
				{direction}
				{closed}
				{cover}
				onopen={openBook}
			>
				{#snippet spread()}
					{#if scene}
						{#key scene.city.id}
							<Diorama {scene} {pop} />
						{/key}
					{:else if book}
						<Contents {book} {pop} onopen={openCity} />
					{/if}
				{/snippet}
				{#snippet left()}
					<div class="print left">
						{#if scene}
							<h2>{scene.city.name}</h2>
							<p>{scene.city.country}</p>
						{:else}
							<h2>kirie</h2>
							<p>The sky over {book?.cities.length} places, as it is right now</p>
						{/if}
					</div>
					<span class="folio">{page === 0 ? '' : page * 2}</span>
				{/snippet}
				{#snippet right()}
					<div class="print right">
						{#if scene}
							<p class="clock">{scene.clock}</p>
							<p>{scene.summary}</p>
						{:else}
							<p class="clock">{utc}</p>
							<p>Greenwich</p>
						{/if}
					</div>
					<span class="folio right">{page === 0 ? '' : page * 2 + 1}</span>
				{/snippet}
			</Book>

			{#if previous && !closed}
				<button class="turn back" onclick={() => go(page - 1)} aria-label="Previous page: {previous}">
					<span>{previous}</span>
				</button>
			{/if}
			{#if next && !closed}
				<button class="turn forward" onclick={() => go(page + 1)} aria-label="Next page: {next}">
					<span>{next}</span>
				</button>
			{/if}
		</div>

		<div class="side">
			<Tag {scene} {book} {closed} onopenbook={openBook} onfolded={(s) => (scene = s)} />
			{#if page > 0}
				<button class="to-contents" onclick={() => go(0)}>Back to the wheel</button>
			{/if}
		</div>
	{:else if !error}
		<p class="loading">Opening the book…</p>
	{/if}
</main>

<style>
	:global(html, body) {
		margin: 0;
		height: 100%;
		overflow: hidden;
		background: #1f4a3b;
		font-family: 'Familjen Grotesk', system-ui, sans-serif;
		color: #f3f4ef;
		-webkit-font-smoothing: antialiased;
	}

	:global(*, *::before, *::after) {
		box-sizing: border-box;
	}

	.sr-only {
		position: absolute;
		width: 1px;
		height: 1px;
		overflow: hidden;
		clip-path: inset(50%);
		white-space: nowrap;
	}

	/* A self-healing cutting mat: centimetre grid, heavier every five, and a
	   45° guide. */
	.mat {
		position: fixed;
		inset: 0;
		background-color: #1f4a3b;
		background-image:
			linear-gradient(rgb(214 236 222 / 0.2) 1px, transparent 1px),
			linear-gradient(90deg, rgb(214 236 222 / 0.2) 1px, transparent 1px),
			linear-gradient(rgb(214 236 222 / 0.08) 1px, transparent 1px),
			linear-gradient(90deg, rgb(214 236 222 / 0.08) 1px, transparent 1px),
			linear-gradient(45deg, transparent calc(50% - 0.5px), rgb(214 236 222 / 0.07) 50%, transparent calc(50% + 0.5px)),
			radial-gradient(ellipse at 40% 45%, rgb(255 255 255 / 0.07), transparent 65%);
		background-size:
			190px 190px,
			190px 190px,
			38px 38px,
			38px 38px,
			100% 100%,
			100% 100%;
		background-position: -1px -1px;
	}

	.desk {
		position: absolute;
		left: calc((100% - 380px) / 2 + 10px);
		top: 57%;
		transform: translate(-50%, -50%);
	}

	.side {
		position: absolute;
		right: 34px;
		top: 50%;
		width: 310px;
		transform: translateY(-50%);
	}

	.narrow .desk {
		left: 50%;
		top: 36%;
	}

	.narrow .side {
		left: 12px;
		right: 12px;
		top: auto;
		bottom: 12px;
		width: auto;
		max-height: 44%;
		overflow: auto;
		transform: none;
		padding-top: 16px;
	}

	.print {
		position: absolute;
		bottom: 6%;
		color: #1d2140;
		max-width: 80%;
	}

	.print.left {
		left: 9%;
	}

	.print.right {
		right: 9%;
		text-align: right;
	}

	h2 {
		margin: 0;
		font-family: 'Stardos Stencil', serif;
		font-weight: 700;
		font-size: calc(var(--spread-width) * 0.058);
		line-height: 1;
	}

	.print p {
		margin: 0.35em 0 0;
		font-size: calc(var(--spread-width) * 0.018);
	}

	.clock {
		font-family: 'Stardos Stencil', serif;
		font-weight: 700;
		font-size: calc(var(--spread-width) * 0.045) !important;
		line-height: 1;
	}

	.folio {
		position: absolute;
		bottom: 2.4%;
		left: 4%;
		font-size: calc(var(--spread-width) * 0.012);
		color: #6b6f88;
	}

	.folio.right {
		left: auto;
		right: 4%;
	}

	/* Page tabs: strips of washi tape stuck to the mat either side of the book. */
	.turn {
		position: absolute;
		top: calc(100% + 6px);
		padding: 7px 16px 8px;
		border: 0;
		background: rgb(242 169 0 / 0.86);
		color: #2b1d00;
		font: inherit;
		font-size: 14px;
		font-weight: 600;
		white-space: nowrap;
		cursor: pointer;
		clip-path: polygon(0 10%, 5% 0, 95% 4%, 100% 20%, 97% 50%, 100% 85%, 94% 100%, 6% 96%, 0 80%, 3% 50%);
		transition: translate 0.2s;
	}

	.turn.back {
		left: 2%;
		rotate: -3deg;
	}

	.turn.back span::before {
		content: '‹ ';
	}

	.turn.forward {
		right: 2%;
		rotate: 2.5deg;
	}

	.turn.forward span::after {
		content: ' ›';
	}

	.turn:hover {
		translate: 0 -2px;
	}

	.turn:focus-visible,
	.to-contents:focus-visible {
		outline: 2px solid #fbfbf7;
		outline-offset: 3px;
	}

	.narrow .turn {
		font-size: 13px;
	}

	.to-contents {
		margin-top: 18px;
		padding: 6px 2px;
		border: 0;
		background: none;
		color: #e5efe8;
		font: inherit;
		font-size: 14px;
		text-decoration: underline;
		text-underline-offset: 4px;
		cursor: pointer;
	}

	.loading,
	.error {
		position: absolute;
		left: 50%;
		top: 50%;
		translate: -50% -50%;
		font-size: 18px;
	}

	.error {
		top: 24px;
		translate: -50% 0;
		padding: 10px 16px;
		background: #fbfbf7;
		color: #7a1f1f;
	}
</style>
