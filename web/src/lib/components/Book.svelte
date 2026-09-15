<script lang="ts">
	import { paperGrain } from '$lib/paper';
	import type { Snippet } from 'svelte';

	// The open book, lying on the mat, tilted away from the reader. What stands
	// on the spread is passed in as `spread`; what's printed on the pages as
	// `left` and `right`. A turning leaf sweeps across the gutter while `turning`.
	let {
		width,
		tiltX,
		tiltY,
		turning,
		direction,
		closed,
		cover,
		onopen,
		spread,
		left,
		right
	}: {
		width: number;
		tiltX: number;
		tiltY: number;
		turning: boolean;
		direction: 1 | -1;
		closed: boolean;
		cover: boolean;
		onopen: () => void;
		spread: Snippet;
		left: Snippet;
		right: Snippet;
	} = $props();

	const depth = $derived(Math.round(width * 0.64));
</script>

<div class="stage" style:--spread-width="{width}px" style:--spread-depth="{depth}px" style:--grain={paperGrain()}>
	<div class="book" class:closed style:transform="rotateX({tiltX}deg) rotateY({tiltY}deg) translateX({closed ? -25 : 0}%)">
		<div class="boards"></div>
		<div class="block"></div>
		<div class="page left">{@render left()}</div>
		<div class="page right">{@render right()}</div>
		<div class="gutter"></div>
		<div class="spread">{@render spread()}</div>
		{#key turning}
			{#if turning}
				<div class="leaf" class:back={direction === -1}>
					<div class="face front"></div>
					<div class="face reverse"></div>
				</div>
			{/if}
		{/key}
		{#if cover}
			<button class="cover" class:open={!closed} onclick={onopen} aria-label="Open the book" disabled={!closed}>
				<span class="cloth">
					<span class="frame"></span>
					<span class="title">kirie</span>
					<span class="subtitle">The sky over thirty-six places,<br />as it is right now</span>
				</span>
				<span class="endpaper"></span>
			</button>
		{/if}
	</div>
</div>

<style>
	.stage {
		position: relative;
		width: var(--spread-width);
		height: var(--spread-depth);
		perspective: calc(var(--spread-width) * 2.1);
		perspective-origin: 50% -30%;
	}

	.book {
		position: absolute;
		inset: 0;
		transform-style: preserve-3d;
		transform-origin: 50% 70%;
		transition: transform 0.9s cubic-bezier(0.2, 0.7, 0.2, 1);
	}

	/* Cloth-covered boards, a little larger than the pages. */
	.boards {
		position: absolute;
		inset: -2.2% -1.6%;
		border-radius: 6px;
		background:
			repeating-linear-gradient(45deg, rgb(255 255 255 / 0.025) 0 2px, transparent 2px 5px),
			repeating-linear-gradient(-45deg, rgb(0 0 0 / 0.05) 0 2px, transparent 2px 5px),
			#27305a;
		box-shadow:
			0 40px 60px -10px rgb(4 20 14 / 0.55),
			0 12px 18px rgb(4 20 14 / 0.35);
		transform: translateZ(-14px);
	}

	/* The edges of the stacked pages. */
	.block {
		position: absolute;
		inset: -0.2% 0.2%;
		background: repeating-linear-gradient(to bottom, #e9e8e1 0 1px, #fbfbf7 1px 3px);
		transform: translateZ(-7px);
		border-radius: 3px;
	}

	.page {
		position: absolute;
		top: 0;
		bottom: 0;
		width: 50%;
		background-color: #fbfbf7;
		background-image: var(--grain);
		background-blend-mode: multiply;
		overflow: hidden;
	}

	.page.left {
		left: 0;
		border-radius: 3px 0 0 3px;
		box-shadow: inset -40px 0 50px -30px rgb(30 30 50 / 0.3);
	}

	.page.right {
		right: 0;
		border-radius: 0 3px 3px 0;
		box-shadow: inset 40px 0 50px -30px rgb(30 30 50 / 0.3);
	}

	.gutter {
		position: absolute;
		left: 50%;
		top: 0;
		bottom: 0;
		width: 2px;
		margin-left: -1px;
		background: rgb(40 40 60 / 0.25);
	}

	.spread {
		position: absolute;
		inset: 0;
		transform-style: preserve-3d;
	}

	.leaf {
		position: absolute;
		top: 0;
		bottom: 0;
		left: 50%;
		width: 50%;
		transform-origin: 0 50%;
		transform-style: preserve-3d;
		animation: turn 0.9s cubic-bezier(0.45, 0.05, 0.3, 1) forwards;
		z-index: 5;
	}

	.leaf.back {
		animation-name: turn-back;
	}

	.face {
		position: absolute;
		inset: 0;
		background-color: #fbfbf7;
		background-image: var(--grain);
		background-blend-mode: multiply;
		backface-visibility: hidden;
		border-radius: 0 3px 3px 0;
		box-shadow: inset 40px 0 60px -30px rgb(30 30 50 / 0.25);
	}

	.face.reverse {
		transform: rotateY(180deg);
	}

	/* Closed, only the right half of the book is on the mat. */
	.book.closed .boards {
		left: 48.6%;
	}

	.book.closed .block {
		left: 50%;
	}

	.boards,
	.block,
	.page.left,
	.gutter,
	.spread {
		transition:
			left 1s cubic-bezier(0.2, 0.7, 0.2, 1),
			opacity 0.4s 0.7s;
	}

	.book.closed .page.left,
	.book.closed .gutter,
	.book.closed .spread {
		opacity: 0;
		transition-delay: 0s;
	}

	.cover {
		all: unset;
		position: absolute;
		top: -2.2%;
		bottom: -2.2%;
		left: 50%;
		width: 51.6%;
		transform-origin: 0 50%;
		transform-style: preserve-3d;
		transform: translateZ(3px);
		transition: transform 1.3s cubic-bezier(0.55, 0.05, 0.25, 1);
		cursor: pointer;
		z-index: 10;
	}

	.cover.open {
		transform: translateZ(3px) rotateY(-180deg);
		cursor: default;
		pointer-events: none;
	}

	.cover:focus-visible .cloth {
		outline: 3px solid #f2a900;
		outline-offset: 4px;
	}

	.cloth,
	.endpaper {
		position: absolute;
		inset: 0;
		backface-visibility: hidden;
		border-radius: 0 6px 6px 0;
	}

	.cloth {
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		gap: 3%;
		background:
			repeating-linear-gradient(45deg, rgb(255 255 255 / 0.03) 0 2px, transparent 2px 5px),
			repeating-linear-gradient(-45deg, rgb(0 0 0 / 0.06) 0 2px, transparent 2px 5px),
			linear-gradient(90deg, rgb(0 0 0 / 0.25), transparent 6%),
			#27305a;
		color: #fbfbf7;
		box-shadow: inset 0 0 0 1px rgb(255 255 255 / 0.05);
	}

	/* A foil-stamped rule around the title. */
	.frame {
		position: absolute;
		inset: 7% 9% 7% 11%;
		border: 1.5px solid rgb(242 190 70 / 0.75);
		outline: 1px solid rgb(242 190 70 / 0.35);
		outline-offset: 5px;
	}

	/* Cut out of the cloth, so the white board shows through. */
	.title {
		font-family: 'Stardos Stencil', serif;
		font-weight: 700;
		font-size: calc(var(--spread-width) * 0.1);
		line-height: 1;
		color: #fbfbf7;
		text-shadow:
			0 -2px 0 rgb(0 0 0 / 0.45),
			0 1px 0 rgb(255 255 255 / 0.2);
	}

	.subtitle {
		font-family: 'Familjen Grotesk', sans-serif;
		font-size: calc(var(--spread-width) * 0.018);
		line-height: 1.5;
		text-align: center;
		color: rgb(242 190 70 / 0.9);
		letter-spacing: 0.02em;
	}

	.endpaper {
		transform: rotateY(180deg);
		border-radius: 6px 0 0 6px;
		background:
			radial-gradient(circle at 25% 25%, rgb(242 169 0 / 0.55) 0 7%, transparent 7.5%),
			radial-gradient(circle at 75% 75%, rgb(242 169 0 / 0.55) 0 7%, transparent 7.5%),
			#2f3a6b;
		background-size: 12% 12%;
	}

	@keyframes turn {
		from {
			transform: translateZ(1px) rotateY(0deg);
		}
		to {
			transform: translateZ(1px) rotateY(-180deg);
		}
	}

	@keyframes turn-back {
		from {
			transform: translateZ(1px) rotateY(-180deg);
		}
		to {
			transform: translateZ(1px) rotateY(0deg);
		}
	}
</style>
