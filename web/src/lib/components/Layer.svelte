<script lang="ts">
	import type { Snippet } from 'svelte';

	// One piece of a pop-up: a sheet hinged to the page along its bottom edge.
	// Folded (pop 0) it lies flat on the page; open (pop 1) it stands upright.
	// `base` is how far from the front edge of the spread its hinge sits, 0 to 1,
	// and `height` is its height as a fraction of the spread's depth.
	let {
		base,
		height,
		inset = 0.03,
		pop = 1,
		delay = 0,
		sway = 0,
		swaySpeed = 5,
		shadow = true,
		lift = 0,
		children
	}: {
		base: number;
		height: number;
		inset?: number;
		pop?: number;
		delay?: number;
		sway?: number;
		swaySpeed?: number;
		shadow?: boolean;
		lift?: number;
		children: Snippet;
	} = $props();
</script>

<div
	class="stand"
	class:folding={pop === 0}
	style:--base={base}
	style:--height={height}
	style:--inset={inset}
	style:--pop={pop}
	style:--delay="{delay}ms"
	style:--sway="{sway}deg"
	style:--sway-speed="{swaySpeed}s"
	style:--lift="{lift}px"
>
	{#if shadow}
		<div class="cast" style:opacity={pop}></div>
	{/if}
	<div class="hinge">
		<div class="flutter" class:still={sway === 0}>
			{@render children()}
		</div>
	</div>
</div>

<style>
	.stand {
		position: absolute;
		left: calc(var(--inset) * 100%);
		right: calc(var(--inset) * 100%);
		bottom: calc(var(--base) * 100%);
		height: calc(var(--height) * var(--spread-depth));
		transform-style: preserve-3d;
		pointer-events: none;
	}

	/* The shadow the standing sheet throws back across the page. */
	.cast {
		position: absolute;
		left: 2%;
		right: 2%;
		bottom: calc(-1 * var(--spread-depth) * 0.04);
		height: calc(var(--spread-depth) * 0.12);
		background: linear-gradient(to top, transparent, rgb(20 24 40 / 0.22) 40%, transparent);
		filter: blur(6px);
		transform: translateY(calc(var(--spread-depth) * -0.05));
		transition: opacity 0.6s var(--delay);
	}

	.hinge {
		position: absolute;
		inset: 0;
		transform-origin: 50% 100%;
		transform: translateZ(var(--lift)) rotateX(calc(-90deg * var(--pop)));
		transform-style: preserve-3d;
		transition: transform 0.85s cubic-bezier(0.34, 1.45, 0.52, 1) var(--delay);
		filter: drop-shadow(0 -1px 0 rgb(255 255 255 / 0.35)) drop-shadow(0 6px 5px rgb(10 14 30 / 0.28));
	}

	/* Folding down happens all at once, and quickly, before the page turns. */
	.folding .hinge {
		transition: transform 0.5s cubic-bezier(0.5, 0, 0.75, 0.3);
	}

	.folding .cast {
		transition: opacity 0.3s;
	}

	.flutter {
		position: absolute;
		inset: 0;
		transform-origin: 50% 100%;
		animation: flutter var(--sway-speed) ease-in-out infinite alternate;
	}

	.flutter.still {
		animation: none;
	}

	@keyframes flutter {
		from {
			transform: rotateX(calc(var(--sway) * -0.6)) skewX(calc(var(--sway) * 0.4));
		}
		to {
			transform: rotateX(var(--sway)) skewX(calc(var(--sway) * -0.4));
		}
	}

	@media (prefers-reduced-motion: reduce) {
		.hinge {
			transition-duration: 0.01ms;
		}
		.flutter {
			animation: none;
		}
	}
</style>
