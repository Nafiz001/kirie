<script lang="ts">
	import { paperGrain, svgUrl } from '$lib/paper';
	import type { Snippet } from 'svelte';

	// A sheet of coloured paper cut to a silhouette. `cut` is SVG markup for a box
	// 1000 wide and `viewHeight` tall; where it's filled, there's paper.
	let {
		colour,
		cut = null,
		viewWidth = 1000,
		viewHeight = 500,
		gradient = null,
		opacity = 1,
		style = '',
		className = '',
		children
	}: {
		colour: string;
		cut?: string | null;
		viewWidth?: number;
		viewHeight?: number;
		gradient?: string | null;
		opacity?: number;
		style?: string;
		className?: string;
		children?: Snippet;
	} = $props();

	const mask = $derived(cut ? svgUrl(viewWidth, viewHeight, cut) : null);
	const image = $derived(gradient ? `${paperGrain()}, ${gradient}` : paperGrain());
</script>

<div
	class="sheet {className}"
	style:background-color={colour}
	style:background-image={image}
	style:mask-image={mask}
	style:opacity
	{style}
>
	{@render children?.()}
</div>

<style>
	.sheet {
		position: absolute;
		inset: 0;
		background-size:
			256px 256px,
			100% 100%;
		background-blend-mode: multiply;
		mask-size: 100% 100%;
		mask-repeat: no-repeat;
		transition:
			background-color 2.5s ease,
			opacity 1.5s ease;
	}
</style>
