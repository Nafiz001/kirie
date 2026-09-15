<script lang="ts">
	import type { Scene } from '$lib/api';
	import { CRANE_COLOURS } from '$lib/api';
	import { between, lit, mix, seeded } from '$lib/paper';
	import * as cut from '$lib/shapes';
	import Falling from './Falling.svelte';
	import Layer from './Layer.svelte';
	import Sheet from './Sheet.svelte';

	// One city's sky and land, cut from paper and lit by its weather right now.
	let { scene, pop }: { scene: Scene; pop: number } = $props();

	const PAPER = {
		mountains: '#5f7291',
		snow: '#f3f6fa',
		volcano: '#6d5c66',
		hills: '#6f9a5f',
		dunes: '#d9a45e',
		ice: '#dbe7ef',
		forest: '#2f5e41',
		tropical: '#3d7b47',
		meadow: '#8fb069',
		sea: '#2f6e9e',
		river: '#4c8cb8',
		towers: '#2f3b52',
		city: '#46526a',
		town: '#7b6553',
		glass: '#8ea8c2',
		lamplight: '#ffcf6e',
		cloud: '#ffffff',
		storm: '#5a616e'
	};

	const city = $derived(scene.city);
	// Dyed paper can't be as saturated as a screen: soften the sky toward the
	// page a touch.
	const sky = $derived({
		...scene.sky,
		top: mix(scene.sky.top, '#f4f1e8', 0.1),
		middle: mix(scene.sky.middle, '#f4f1e8', 0.12),
		horizon: mix(scene.sky.horizon, '#f4f1e8', 0.14)
	});
	const fx = $derived(scene.effects);
	const has = (t: string) => city.terrain.includes(t);
	const tropical = $derived(Math.abs(city.latitude) < 24);

	// What the land is cut from, back to front. Shapes depend only on the city,
	// so a page looks the same every time it's opened; only the light changes.
	const land = $derived.by(() => {
		const r = (part: string) => seeded(`${city.id}:${part}`);
		const cold = Math.abs(city.latitude) > 30;

		let far: { cut: cut.Cut; colour: string; snow?: string } | null = null;
		if (has('volcano')) {
			const v = cut.volcano(r('far'));
			far = { cut: v, colour: PAPER.volcano, snow: cold ? v.snow : undefined };
		} else if (has('mountains')) {
			const m = cut.mountains(r('far'));
			far = { cut: m, colour: PAPER.mountains, snow: m.snow };
		} else if (has('ice')) {
			far = { cut: cut.mountains(r('far'), 500, { low: 0.2, high: 0.55 }), colour: PAPER.ice };
		} else if (has('hills')) {
			far = { cut: cut.hills(r('far'), 500, { low: 0.3, high: 0.6 }), colour: PAPER.hills };
		} else if (has('desert')) {
			far = { cut: cut.dunes(r('far'), 400), colour: PAPER.dunes };
		} else {
			far = { cut: cut.hills(r('far'), 400, { low: 0.15, high: 0.35 }), colour: has('forest') ? PAPER.forest : PAPER.meadow };
		}

		let mid: { cut: cut.Cut; colour: string };
		if (has('ice')) mid = { cut: cut.ice(r('mid')), colour: PAPER.ice };
		else if (has('desert')) mid = { cut: cut.dunes(r('mid'), 300), colour: mix(PAPER.dunes, '#b5773a', 0.25) };
		else if (has('forest') && (has('mountains') || has('hills')))
			mid = { cut: cut.forest(r('mid'), 300, { tropical }), colour: tropical ? PAPER.tropical : PAPER.forest };
		else if (has('hills')) mid = { cut: cut.hills(r('mid'), 360, { low: 0.2, high: 0.5 }), colour: mix(PAPER.hills, '#4d7a45', 0.4) };
		else if (tropical || has('forest')) mid = { cut: cut.forest(r('mid'), 300, { tropical }), colour: tropical ? PAPER.tropical : PAPER.forest };
		else mid = { cut: cut.hills(r('mid'), 340, { low: 0.2, high: 0.45 }), colour: PAPER.meadow };

		let fore: { cut: cut.Cut; colour: string; water: boolean };
		if (has('sea')) fore = { cut: cut.waves(r('fore'), 160), colour: PAPER.sea, water: true };
		else if (has('river')) fore = { cut: cut.waves(r('fore'), 120, { count: 20, swell: 0.55 }), colour: PAPER.river, water: true };
		else if (has('forest')) fore = { cut: cut.forest(r('fore'), 220, { tropical }), colour: tropical ? PAPER.tropical : PAPER.forest, water: false };
		else if (has('desert')) fore = { cut: cut.dunes(r('fore'), 180), colour: mix(PAPER.dunes, '#fff1d6', 0.2), water: false };
		else fore = { cut: cut.meadow(r('fore')), colour: PAPER.meadow, water: false };

		const buildings = city.skyline === 'none' ? null : cut.skyline(r('skyline'), city.skyline);
		const heights = { towers: 0.34, city: 0.25, town: 0.22, none: 0 };

		return { far, mid, fore, buildings, buildingHeight: heights[city.skyline] };
	});

	const stars = $derived(cut.prickedSky(seeded(`${city.id}:stars`), 90));

	const clouds = $derived.by(() => {
		const random = seeded(`${city.id}:clouds`);
		const count = Math.round(fx.clouds * 7);
		let body = '';
		for (let i = 0; i < count; i++) {
			const width = between(random, 120, 260);
			body += cut.cloud(random, (i / Math.max(1, count)) * 1000 + between(random, -40, 40), between(random, 70, 300), width);
		}
		return body;
	});

	const cloudColour = $derived(lit(mix(PAPER.cloud, PAPER.storm, fx.cloudDarkness), sky, 0.15));

	// Sun and moon on paper rods. The viewer faces the equator.
	function place(altitude: number, azimuth: number) {
		const facing = city.latitude >= 0 ? 180 : 0;
		const relative = ((azimuth - facing + 540) % 360) - 180;
		const x = 50 + Math.max(-1, Math.min(1, relative / 110)) * 40;
		const y = 20 + (altitude / 70) * 72;
		return { x, y, visible: altitude > -4 };
	}

	const sun = $derived(place(scene.sun.altitude, scene.sun.azimuth));
	const moon = $derived(place(scene.moon.altitude, scene.moon.azimuth));
	const sunColour = $derived(
		scene.sun.altitude < 8 ? mix('#ff7b3a', '#ffcf5a', Math.max(0, scene.sun.altitude + 2) / 10) : '#ffdf7e'
	);
	const moonShape = $derived(cut.moon(scene.moon.illumination, scene.moon.phase < 0.5));

	// Lightning: a flash now and then while a storm is overhead.
	let flash = $state(false);
	$effect(() => {
		if (!fx.lightning) return;
		let timer: ReturnType<typeof setTimeout>;
		const strike = () => {
			flash = true;
			setTimeout(() => (flash = false), 140 + Math.random() * 120);
			timer = setTimeout(strike, 2500 + Math.random() * 7000);
		};
		timer = setTimeout(strike, 1200);
		return () => clearTimeout(timer);
	});

	const bolt = $derived.by(() => {
		const random = seeded(`${city.id}:bolt:${flash}`);
		let x = between(random, 300, 700);
		let d = `M${x} 120`;
		for (let y = 120; y < 620; y += between(random, 50, 90)) {
			x += between(random, -60, 60);
			d += ` L${x.toFixed(0)} ${y.toFixed(0)} L${(x + 14).toFixed(0)} ${(y + 8).toFixed(0)}`;
		}
		return `<path d='${d} L${x + 30} 640 L${x + 6} 600 Z' />`;
	});

	const flying = $derived(
		scene.cranes.map((crane) => {
			const random = seeded(`crane:${crane.id}`);
			return {
				...crane,
				top: between(random, 8, 42),
				duration: between(random, 38, 70),
				delay: -between(random, 0, 70),
				scale: between(random, 0.7, 1.1),
				bob: between(random, 3, 6)
			};
		})
	);

	const wind = $derived(fx.wind);
	const d = (i: number) => i * 60;

	// How tall the sky card stands, as a fraction of the spread's depth.
	const SKY = 0.72;
</script>

<!-- Starlight behind the sky card shows through its pinholes; by day it matches the sky. -->
<Layer base={0.955} height={SKY * 0.97} inset={0.035} {pop} delay={d(0)} shadow={false}>
	<Sheet colour={mix(sky.top, '#fff4d6', sky.stars)} />
</Layer>

<Layer base={0.95} height={SKY} {pop} delay={d(0)}>
	<Sheet
		colour={sky.middle}
		cut={stars}
		viewHeight={900}
		gradient="linear-gradient(to top, {sky.horizon} 0%, {sky.middle} 42%, {sky.top} 100%)"
	>
		<div class="flash" class:on={flash}></div>
	</Sheet>
</Layer>

<Layer base={0.915} height={SKY} {pop} delay={d(1)} shadow={false}>
	{#if sun.visible}
		<div class="orb" style:left="{sun.x}%" style:bottom="{sun.y}%" style:opacity={1 - fx.clouds * 0.8}>
			<Sheet colour={mix(sunColour, '#ffffff', 0.2)} cut={cut.sunburst()} viewWidth={100} viewHeight={100} style="inset:-55%" />
			<Sheet colour={sunColour} cut="<circle cx='50' cy='50' r='50'/>" viewWidth={100} viewHeight={100} />
		</div>
	{/if}
	{#if moon.visible && scene.sun.altitude < 10}
		<div class="orb moon" style:left="{moon.x}%" style:bottom="{moon.y}%" style:--glow={1 - sky.light}>
			<Sheet colour={mix('#2b3350', sky.top, 0.5)} cut="<circle cx='50' cy='50' r='50'/>" viewWidth={100} viewHeight={100} opacity={0.35} />
			<Sheet colour="#f4f0df" cut={moonShape} viewWidth={100} viewHeight={100} />
		</div>
	{/if}
</Layer>

{#if fx.clouds > 0.05}
	<Layer base={0.88} height={SKY} {pop} delay={d(1)} sway={wind * 2}>
		<div class="clip">
			<div class="drift" style:--drift-duration="{180 / (0.25 + wind)}s">
				<Sheet colour={cloudColour} cut={clouds} viewHeight={900} style="width:50%" />
				<Sheet colour={cloudColour} cut={clouds} viewHeight={900} style="left:50%;width:50%" />
			</div>
		</div>
	</Layer>
{/if}

{#if fx.lightning && flash}
	<Layer base={0.86} height={SKY} pop={1} shadow={false}>
		<Sheet colour="#fff6c8" cut={bolt} viewHeight={900} />
	</Layer>
{/if}

<Layer base={0.8} height={0.5 * (land.far.cut.height / 500)} {pop} delay={d(2)}>
	<Sheet colour={lit(land.far.colour, sky, 0.55)} cut={land.far.cut.body} viewHeight={land.far.cut.height} />
	{#if land.far.snow}
		<Sheet colour={lit(PAPER.snow, sky, 0.4)} cut={land.far.snow} viewHeight={land.far.cut.height} />
	{/if}
</Layer>

{#if fx.fog > 0.05}
	<Layer base={0.74} height={0.3} {pop} delay={d(3)} shadow={false} sway={wind}>
		<Sheet
			colour={mix('#f2f5f8', sky.horizon, 0.3)}
			cut={cut.hills(seeded(`${city.id}:fog1`), 300, { low: 0.45, high: 0.75 }).body}
			viewHeight={300}
			opacity={Math.min(0.78, fx.fog * 0.7)}
		/>
	</Layer>
{/if}

<Layer base={0.68} height={0.32 * (land.mid.cut.height / 320)} {pop} delay={d(3)} sway={wind * 1.5}>
	<Sheet colour={lit(land.mid.colour, sky, 0.3)} cut={land.mid.cut.body} viewHeight={land.mid.cut.height} />
</Layer>

{#if flying.length}
	<Layer base={0.62} height={0.78} {pop} delay={d(4)} shadow={false}>
		{#each flying as crane (crane.id)}
			<svg
				class="crane"
				viewBox="0 0 {cut.CRANE.width} {cut.CRANE.height}"
				style:top="{crane.top}%"
				style:animation-duration="{crane.duration}s"
				style:animation-delay="{crane.delay}s"
				style:--scale={crane.scale}
				style:--bob="{crane.bob}px"
				style:filter="brightness({0.35 + sky.light * 0.65})"
			>
				<path d={cut.CRANE.wingFar} fill={mix(CRANE_COLOURS[crane.colour], '#000000', 0.25)} />
				<path d={cut.CRANE.body} fill={CRANE_COLOURS[crane.colour]} />
				<path d={cut.CRANE.wingNear} fill={mix(CRANE_COLOURS[crane.colour], '#ffffff', 0.25)} />
			</svg>
		{/each}
	</Layer>
{/if}

{#if fx.precipitation !== 'none'}
	<Layer base={0.58} height={0.82} {pop} delay={d(4)} shadow={false}>
		<Falling kind={fx.precipitation} intensity={fx.intensity * 0.7} wind={fx.wind} windDirection={fx.windDirection} light={sky.light} seed="{city.id}-back" />
	</Layer>
{/if}

{#if land.buildings}
	<Layer base={0.505} height={land.buildingHeight * 1.02} {pop} delay={d(5)} shadow={false}>
		<Sheet
			colour={mix(lit(PAPER.glass, sky, 0.1), PAPER.lamplight, fx.windowsLit)}
			cut={land.buildings.windows}
			viewHeight={land.buildings.height}
			style="transform: scale(1.02, 1.01); transform-origin: 50% 100%"
		/>
	</Layer>
	<Layer base={0.5} height={land.buildingHeight} {pop} delay={d(5)}>
		<Sheet colour={lit(PAPER[city.skyline as 'towers' | 'city' | 'town'], sky, 0.1)} cut={land.buildings.body} viewHeight={land.buildings.height} />
	</Layer>
{/if}

{#if fx.fog > 0.3}
	<Layer base={0.42} height={0.2} {pop} delay={d(6)} shadow={false}>
		<Sheet
			colour={mix('#f5f7fa', sky.horizon, 0.2)}
			cut={cut.hills(seeded(`${city.id}:fog2`), 200, { low: 0.4, high: 0.8 }).body}
			viewHeight={200}
			opacity={Math.min(0.7, (fx.fog - 0.3) * 0.9)}
		/>
	</Layer>
{/if}

<Layer base={0.33} height={0.17 * (land.fore.cut.height / 180)} {pop} delay={d(7)} sway={land.fore.water ? 0 : wind * 3}>
	<div class:bob={land.fore.water} class="fore" style:--wave-speed="{3.5 - wind * 2}s">
		<Sheet colour={lit(land.fore.colour, sky, 0)} cut={land.fore.cut.body} viewHeight={land.fore.cut.height} />
		{#if land.fore.water}
			<Sheet
				colour={lit(mix(land.fore.colour, '#ffffff', 0.35), sky, 0)}
				cut={cut.waves(seeded(`${city.id}:foam`), land.fore.cut.height, { count: 11, swell: 0.62 }).body}
				viewHeight={land.fore.cut.height}
				style="top: 22%"
			/>
		{/if}
	</div>
</Layer>

{#if fx.precipitation !== 'none'}
	<Layer base={0.26} height={0.5} inset={0.09} {pop} delay={d(8)} shadow={false}>
		<Falling kind={fx.precipitation} intensity={fx.intensity} wind={fx.wind} windDirection={fx.windDirection} light={sky.light} seed="{city.id}-front" />
	</Layer>
{/if}

<style>
	.flash {
		position: absolute;
		inset: 0;
		background: #fffbe8;
		opacity: 0;
		transition: opacity 0.35s ease-out;
	}

	.flash.on {
		opacity: 0.75;
		transition-duration: 0.04s;
	}

	.orb {
		position: absolute;
		width: 7.5%;
		aspect-ratio: 1;
		transform: translate(-50%, 50%);
		transition:
			left 3s ease,
			bottom 3s ease;
	}

	.orb.moon {
		width: 5.5%;
		filter: drop-shadow(0 0 calc(18px * var(--glow)) rgb(255 248 220 / 0.55));
	}

	.clip {
		position: absolute;
		inset: 0;
		overflow: hidden;
	}

	.drift {
		position: absolute;
		inset: 0;
		width: 200%;
		animation: drift var(--drift-duration) linear infinite;
	}

	@keyframes drift {
		to {
			transform: translateX(-50%);
		}
	}

	.fore {
		position: absolute;
		inset: 0;
	}

	.bob {
		animation: bob var(--wave-speed) ease-in-out infinite alternate;
	}

	@keyframes bob {
		from {
			transform: translate(-1.5%, 2%);
		}
		to {
			transform: translate(1.5%, -2%);
		}
	}

	.crane {
		position: absolute;
		left: 0;
		width: 9%;
		overflow: visible;
		animation: fly linear infinite;
	}

	@keyframes fly {
		0% {
			transform: translate(-120%, 0) scale(var(--scale));
		}
		25% {
			transform: translate(270%, var(--bob)) scale(var(--scale));
		}
		50% {
			transform: translate(560%, calc(var(--bob) * -1)) scale(var(--scale));
		}
		75% {
			transform: translate(840%, var(--bob)) scale(var(--scale));
		}
		100% {
			transform: translate(1150%, 0) scale(var(--scale));
		}
	}

	@media (prefers-reduced-motion: reduce) {
		.drift,
		.bob,
		.crane {
			animation: none;
		}
	}
</style>
