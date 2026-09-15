<script lang="ts">
	import type { Book, CityCard, Phase } from '$lib/api';
	import { paperGrain } from '$lib/paper';
	import Layer from './Layer.svelte';
	import Sheet from './Sheet.svelte';

	// The contents page: a volvelle, the rotating paper wheel old astronomy
	// books used to tell the time around the world. It's the Earth seen from
	// above the North Pole, turned so the sun is always at the top, with night
	// shaded across it and every city in the book pricked through.
	let {
		book,
		pop,
		onopen
	}: {
		book: Book;
		pop: number;
		onopen: (city: CityCard) => void;
	} = $props();

	const RAD = Math.PI / 180;

	function spot(city: { latitude: number; longitude: number }) {
		const radius = (90 - city.latitude) / 180; // 0 at the pole, 1 at the South Pole
		const theta = -(city.longitude - book.subsolar.longitude) * RAD;
		return { x: 50 + Math.sin(theta) * radius * 46, y: 50 - Math.cos(theta) * radius * 46 };
	}

	// Day and night across the wheel, drawn pixel by pixel from where the sun is overhead.
	const shading = $derived.by(() => {
		const size = 420;
		const canvas = document.createElement('canvas');
		canvas.width = canvas.height = size;
		const ctx = canvas.getContext('2d');
		if (!ctx) return '';
		const image = ctx.createImageData(size, size);
		const dec = book.subsolar.latitude * RAD;
		const day = [206, 225, 233];
		const dusk = [232, 150, 112];
		const night = [33, 43, 82];

		for (let py = 0; py < size; py++) {
			for (let px = 0; px < size; px++) {
				const dx = (px + 0.5) / size - 0.5;
				const dy = (py + 0.5) / size - 0.5;
				const rho = Math.hypot(dx, dy) / 0.46;
				if (rho > 1.087) continue;
				const lat = (90 - Math.min(rho, 1) * 180) * RAD;
				const theta = Math.atan2(dx, -dy);
				const deltaLon = -theta;
				const elevation = Math.sin(lat) * Math.sin(dec) + Math.cos(lat) * Math.cos(dec) * Math.cos(deltaLon);

				let c: number[];
				if (elevation > 0.05) c = day;
				else if (elevation > -0.12) {
					const t = (0.05 - elevation) / 0.17;
					c = t < 0.5 ? day.map((v, i) => v + (dusk[i] - v) * t * 2) : dusk.map((v, i) => v + (night[i] - v) * (t - 0.5) * 2);
				} else c = night;

				// Faint rings for the equator and the tropics.
				const ringLat = 90 - Math.min(rho, 1) * 180;
				const onRing = [0, 23.44, -23.44].some((r) => Math.abs(ringLat - r) < (r === 0 ? 0.35 : 0.22));
				const k = (py * size + px) * 4;
				const shade = onRing ? (elevation > 0 ? 0.86 : 1.35) : 1;
				image.data[k] = c[0] * shade;
				image.data[k + 1] = c[1] * shade;
				image.data[k + 2] = c[2] * shade;
				image.data[k + 3] = 255;
			}
		}
		ctx.putImageData(image, 0, 0);
		return canvas.toDataURL();
	});

	const PHASE_COLOUR: Record<Phase, string> = {
		day: '#ffcf5a',
		'golden-hour': '#ff9748',
		'civil-twilight': '#e6849a',
		'nautical-twilight': '#b28ad0',
		'astronomical-twilight': '#8e9ce0',
		night: '#fff1c9'
	};

	let hovered = $state<CityCard | null>(null);
	// Clockwise from the top, where the sun is. Seen from above the pole the
	// Earth turns anticlockwise, so places on the right are turning into the
	// light and places on the left out of it.
	const marks = ['', 'Dawn', 'Midnight', 'Dusk'];

	// The wheel is 56% of the spread wide; the spread is 0.64 as deep as it is wide. Square it up.
	const WHEEL = 0.56 / 0.64;
</script>

<!-- The fixed outer ring, printed with the hours. -->
<Layer base={0.5} height={WHEEL} inset={0.22} {pop} delay={0}>
	<Sheet colour="#27305a" cut="<circle cx='500' cy='500' r='500'/>" viewHeight={1000}>
		<div class="ring">
			{#each marks as mark, i (i)}
				<span class="mark" style:transform="rotate({i * 90}deg) translateY(-44.2cqw)">
					<span style:transform="rotate({-i * 90}deg)">{mark}</span>
				</span>
			{/each}
			{#each Array.from({ length: 24 }) as _, i (i)}
				<i class="tick" class:major={i % 6 === 0} style:transform="rotate({i * 15}deg) translateY(-47.5cqw)"></i>
			{/each}
		</div>
	</Sheet>
</Layer>

<!-- The wheel itself, with the cities pricked through it. -->
<Layer base={0.496} height={WHEEL} inset={0.22} {pop} delay={140} shadow={false}>
	<div class="wheel" style:background-image="{paperGrain()}, url({shading})">
		<div class="sun"></div>
		{#each book.cities as city (city.id)}
			{@const p = spot(city)}
			<button
				class="city"
				class:active={hovered?.id === city.id}
				style:left="{p.x}%"
				style:top="{p.y}%"
				style:--dot={PHASE_COLOUR[city.phase]}
				onpointerenter={() => (hovered = city)}
				onpointerleave={() => (hovered = hovered?.id === city.id ? null : hovered)}
				onfocus={() => (hovered = city)}
				onblur={() => (hovered = null)}
				onclick={() => onopen(city)}
				aria-label="{city.name}, {city.clock}, {city.summary}"
			></button>
		{/each}
		{#if hovered}
			{@const p = spot(hovered)}
			<div class="label" style:left="{p.x}%" style:top="{p.y}%" class:below={p.y < 22}>
				<strong>{hovered.name}</strong>
				<span>{hovered.clock}, {hovered.summary.toLowerCase()}</span>
			</div>
		{/if}
		<div class="pin"></div>
	</div>
</Layer>

<style>
	.ring {
		position: absolute;
		inset: 0;
		container-type: inline-size;
		color: #e9e6d8;
	}

	.mark,
	.tick {
		position: absolute;
		left: 50%;
		top: 50%;
		transform-origin: 0 0;
	}

	.mark span {
		display: block;
		translate: -50% -50%;
		font-family: 'Stardos Stencil', serif;
		font-weight: 700;
		font-size: 3.4cqw;
		letter-spacing: 0.04em;
	}

	.tick {
		width: 0.5cqw;
		height: 1.6cqw;
		margin-left: -0.25cqw;
		background: rgb(233 230 216 / 0.55);
	}

	.tick.major {
		height: 2.6cqw;
		background: #e9e6d8;
	}

	.wheel {
		position: absolute;
		inset: 11.5%;
		border-radius: 50%;
		background-size:
			256px 256px,
			100% 100%;
		background-blend-mode: multiply;
		box-shadow:
			0 0 0 3px #f7f6f0,
			0 0 0 4px rgb(0 0 0 / 0.15);
	}

	.sun {
		position: absolute;
		left: 50%;
		top: -9%;
		width: 7%;
		aspect-ratio: 1;
		translate: -50% 0;
		border-radius: 50%;
		background: radial-gradient(circle, #fff3bf 0 35%, #ffc93c 36% 100%);
		box-shadow: 0 0 0 3px #27305a;
	}

	.pin {
		position: absolute;
		left: 50%;
		top: 50%;
		width: 3%;
		aspect-ratio: 1;
		translate: -50% -50%;
		border-radius: 50%;
		background: radial-gradient(circle at 35% 35%, #fff 0 20%, #b8b4a4 21% 100%);
		box-shadow: 0 1px 2px rgb(0 0 0 / 0.4);
	}

	.city {
		position: absolute;
		width: 3.2%;
		aspect-ratio: 1;
		translate: -50% -50%;
		padding: 0;
		border: 2px solid rgb(20 24 44 / 0.75);
		border-radius: 50%;
		background: var(--dot);
		box-shadow: 0 0 6px var(--dot);
		cursor: pointer;
		pointer-events: auto;
		transition: scale 0.2s;
	}

	.city:hover,
	.city.active,
	.city:focus-visible {
		scale: 1.6;
		outline: none;
	}

	.label {
		position: absolute;
		translate: -50% calc(-100% - 14px);
		padding: 4px 9px 5px;
		background: #fbfbf7;
		color: #1d2140;
		border-radius: 2px;
		box-shadow: 0 2px 6px rgb(0 0 0 / 0.3);
		white-space: nowrap;
		font-size: 13px;
		line-height: 1.3;
		text-align: center;
		pointer-events: none;
	}

	.label.below {
		translate: -50% 14px;
	}

	.label strong {
		display: block;
		font-family: 'Stardos Stencil', serif;
		font-size: 17px;
	}
</style>
