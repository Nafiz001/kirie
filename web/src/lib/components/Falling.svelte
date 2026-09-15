<script lang="ts">
	import { between, seeded } from '$lib/paper';

	// Rain as falling strips of paper, snow and hail as punched confetti. The
	// strips lean with the wind.
	let {
		kind,
		intensity,
		wind,
		windDirection,
		light,
		seed
	}: {
		kind: 'drizzle' | 'rain' | 'sleet' | 'snow' | 'hail';
		intensity: number;
		wind: number;
		windDirection: number;
		light: number;
		seed: string;
	} = $props();

	const pieces = $derived.by(() => {
		const random = seeded(`${seed}-${kind}`);
		const snowy = kind === 'snow';
		const count = Math.round((snowy ? 26 : kind === 'drizzle' ? 22 : 30) + intensity * (snowy ? 70 : 110));
		return Array.from({ length: count }, () => ({
			left: random() * 104 - 2,
			delay: -random() * 10,
			duration: snowy
				? between(random, 5, 9)
				: kind === 'hail'
					? between(random, 0.5, 0.8)
					: between(random, 0.75, 1.3) * (kind === 'drizzle' ? 1.5 : 1),
			length: snowy || kind === 'hail' ? between(random, 5, 9) : between(random, 2.5, 6) * (kind === 'drizzle' ? 0.6 : 1),
			drift: between(random, -12, 12),
			tint: random()
		}));
	});

	// Wind from the west pushes strips to the right; from the east, to the left.
	const lean = $derived(Math.sin((windDirection * Math.PI) / 180) * -1 * Math.min(1, wind * 1.4) * 22);
	const colour = $derived(
		kind === 'snow' || kind === 'hail' ? `rgb(250 251 255 / ${0.55 + light * 0.45})` : `rgb(215 230 250 / ${0.35 + light * 0.5})`
	);
</script>

<div class="clip">
	<div class="curtain" style:--lean="{lean}deg" style:--colour={colour}>
		{#each pieces as p, i (i)}
			<i
				class={kind}
				style:left="{p.left}%"
				style:--length="{p.length}{kind === 'snow' || kind === 'hail' ? 'px' : '%'}"
				style:animation-duration="{p.duration}s"
				style:animation-delay="{p.delay}s"
				style:--drift="{p.drift}px"
				style:filter="brightness({0.9 + p.tint * 0.2})"
			></i>
		{/each}
	</div>
</div>

<style>
	/* Clipped square to the sheet, so leaning strips never fall outside it. */
	.clip {
		position: absolute;
		inset: 0;
		overflow: hidden;
	}

	.curtain {
		position: absolute;
		inset: 0 -20%;
		transform: skewX(var(--lean));
		transform-origin: 50% 100%;
	}

	i {
		position: absolute;
		top: 0;
		display: block;
		background: var(--colour);
		animation-name: fall;
		animation-timing-function: linear;
		animation-iteration-count: infinite;
		will-change: transform;
	}

	.rain,
	.drizzle,
	.sleet {
		width: 2px;
		height: var(--length);
		border-radius: 1px;
		box-shadow: 0.5px 0 0 rgb(60 80 110 / 0.25);
	}

	.drizzle {
		width: 1.5px;
	}

	.snow,
	.hail {
		width: var(--length);
		height: var(--length);
		border-radius: 50%;
		box-shadow: 0 1px 1px rgb(40 50 70 / 0.25);
		animation-name: flake;
	}

	.hail {
		border-radius: 2px;
	}

	@keyframes fall {
		from {
			transform: translateY(-10vh);
		}
		to {
			transform: translateY(calc(var(--spread-depth) * 0.9));
		}
	}

	@keyframes flake {
		0% {
			transform: translate(0, -10px) rotate(0deg);
		}
		50% {
			transform: translate(var(--drift), calc(var(--spread-depth) * 0.42)) rotate(180deg);
		}
		100% {
			transform: translate(0, calc(var(--spread-depth) * 0.85)) rotate(360deg);
		}
	}

	@media (prefers-reduced-motion: reduce) {
		i {
			animation-play-state: paused;
		}
	}
</style>
