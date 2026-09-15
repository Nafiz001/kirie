// Cutting silhouettes. Every shape is SVG markup in a 1000-wide box of a given
// height, filled solid; it becomes the mask of a paper sheet, so solid is paper
// and empty is cut away. Holes (windows, stars) are cut with even-odd fills.

import { between, type Random } from './paper';

export const W = 1000;

export type Cut = { height: number; body: string };

const f = (n: number) => n.toFixed(1);

function ridge(points: Array<[number, number]>, h: number): string {
	return `M0 ${h} ` + points.map(([x, y]) => `L${f(x)} ${f(y)}`).join(' ') + ` L${W} ${h} Z`;
}

function smooth(points: Array<[number, number]>, h: number): string {
	let d = `M0 ${h} L${f(points[0][0])} ${f(points[0][1])}`;
	for (let i = 1; i < points.length; i++) {
		const [x0, y0] = points[i - 1];
		const [x1, y1] = points[i];
		const mx = (x0 + x1) / 2;
		d += ` C${f(mx)} ${f(y0)} ${f(mx)} ${f(y1)} ${f(x1)} ${f(y1)}`;
	}
	return d + ` L${W} ${h} Z`;
}

export function mountains(random: Random, h = 500, { low = 0.25, high = 0.92 } = {}): Cut & { snow: string } {
	const points: Array<[number, number]> = [[0, h * (1 - between(random, low, high * 0.7))]];
	const peaks: Array<[number, number]> = [];
	let x = 0;
	let up = true;
	while (x < W) {
		x += between(random, 60, 170);
		const y = up ? h * (1 - between(random, (low + high) / 2, high)) : h * (1 - between(random, low, (low + high) / 2));
		points.push([Math.min(x, W), y]);
		if (up) peaks.push([Math.min(x, W), y]);
		// A little crag on the way down.
		if (random() < 0.5) points.push([Math.min(x + between(random, 15, 35), W), y + between(random, 10, 40)]);
		up = !up;
	}
	const snow = peaks
		.filter(([, y]) => y < h * (1 - high * 0.72))
		.map(([px, py]) => {
			const w = between(random, 30, 55);
			const depth = between(random, 35, 70);
			return `M${f(px - w)} ${f(py + depth)} L${f(px)} ${f(py)} L${f(px + w)} ${f(py + depth)} L${f(px + w * 0.4)} ${f(py + depth * 0.7)} L${f(px)} ${f(py + depth)} L${f(px - w * 0.45)} ${f(py + depth * 0.65)} Z`;
		})
		.join(' ');
	return { height: h, body: `<path d='${ridge(points, h)}'/>`, snow: `<path d='${snow}'/>` };
}

export function volcano(random: Random, h = 500): Cut & { snow: string } {
	const cx = between(random, 380, 620);
	const top = h * 0.12;
	const crater = between(random, 40, 70);
	const foot = between(random, 480, 620);
	const d = `M0 ${h} L0 ${f(h * 0.82)} C${f(cx - foot)} ${f(h * 0.8)} ${f(cx - crater * 2)} ${f(top + 40)} ${f(cx - crater)} ${f(top)} L${f(cx - crater * 0.4)} ${f(top + 12)} L${f(cx + crater * 0.3)} ${f(top + 6)} L${f(cx + crater)} ${f(top)} C${f(cx + crater * 2)} ${f(top + 40)} ${f(cx + foot)} ${f(h * 0.8)} ${W} ${f(h * 0.84)} L${W} ${h} Z`;
	const snow = `M${f(cx - crater * 2.1)} ${f(top + 70)} L${f(cx - crater)} ${f(top)} L${f(cx + crater)} ${f(top)} L${f(cx + crater * 2.2)} ${f(top + 75)} L${f(cx + crater * 1.2)} ${f(top + 55)} L${f(cx + crater * 0.3)} ${f(top + 80)} L${f(cx - crater * 0.8)} ${f(top + 50)} Z`;
	return { height: h, body: `<path d='${d}'/>`, snow: `<path d='${snow}'/>` };
}

export function hills(random: Random, h = 400, { low = 0.3, high = 0.75 } = {}): Cut {
	const points: Array<[number, number]> = [];
	for (let x = 0; x <= W; x += between(random, 120, 240)) points.push([x, h * (1 - between(random, low, high))]);
	points.push([W, h * (1 - between(random, low, high))]);
	return { height: h, body: `<path d='${smooth(points, h)}'/>` };
}

export function dunes(random: Random, h = 300): Cut {
	let d = `M0 ${h} L0 ${f(h * 0.6)}`;
	let x = 0;
	while (x < W) {
		const w = between(random, 180, 320);
		const peak = h * (1 - between(random, 0.45, 0.85));
		// Dunes rise gently on the windward side and drop steeply on the lee.
		d += ` Q${f(x + w * 0.55)} ${f(peak - 20)} ${f(x + w * 0.7)} ${f(peak)} Q${f(x + w * 0.8)} ${f(peak + 30)} ${f(Math.min(x + w, W))} ${f(h * between(random, 0.55, 0.7))}`;
		x += w;
	}
	return { height: h, body: `<path d='${d} L${W} ${h} Z'/>` };
}

export function waves(random: Random, h = 160, { count = 14, swell = 0.35 } = {}): Cut {
	const w = W / count;
	let d = `M0 ${h} L0 ${f(h * swell)}`;
	for (let i = 0; i < count; i++) {
		const x = i * w;
		const crest = h * (swell - between(random, 0.1, 0.25));
		d += ` Q${f(x + w * 0.35)} ${f(crest)} ${f(x + w * 0.6)} ${f(h * swell - 4)} Q${f(x + w * 0.8)} ${f(h * swell + 6)} ${f(x + w)} ${f(h * swell)}`;
	}
	return { height: h, body: `<path d='${d} L${W} ${h} Z'/>` };
}

export function ice(random: Random, h = 360): Cut {
	const blocks: string[] = [`M0 ${h} L0 ${f(h * 0.72)} L${W} ${f(h * 0.7)} L${W} ${h} Z`];
	let x = between(random, 0, 60);
	while (x < W) {
		const w = between(random, 60, 200);
		const top = h * (1 - between(random, 0.35, 0.9));
		const lean = between(random, -30, 30);
		blocks.push(
			`M${f(x)} ${f(h * 0.75)} L${f(x + 10)} ${f(top + 40)} L${f(x + w * 0.3 + lean)} ${f(top)} L${f(x + w * 0.75 + lean)} ${f(top + between(random, 0, 30))} L${f(x + w)} ${f(top + 70)} L${f(x + w + 10)} ${f(h * 0.75)} Z`
		);
		x += w + between(random, 40, 160);
	}
	return { height: h, body: blocks.map((d) => `<path d='${d}'/>`).join('') };
}

export function forest(random: Random, h = 260, { tropical = false } = {}): Cut {
	const parts: string[] = [`<rect x='0' y='${f(h * 0.85)}' width='${W}' height='${f(h * 0.15)}'/>`];
	let x = between(random, -20, 10);
	while (x < W + 20) {
		const size = between(random, 0.45, 1);
		const th = h * 0.85 * size;
		const base = h * 0.88;
		if (tropical || random() < 0.25) {
			const r = th * 0.32;
			parts.push(`<rect x='${f(x - 4)}' y='${f(base - th * 0.5)}' width='8' height='${f(th * 0.5)}'/>`);
			parts.push(`<circle cx='${f(x)}' cy='${f(base - th * 0.62)}' r='${f(r)}'/>`);
			parts.push(`<circle cx='${f(x - r * 0.6)}' cy='${f(base - th * 0.5)}' r='${f(r * 0.7)}'/>`);
			parts.push(`<circle cx='${f(x + r * 0.6)}' cy='${f(base - th * 0.52)}' r='${f(r * 0.72)}'/>`);
			x += r * 2.2 + between(random, 0, 12);
		} else {
			const w = th * 0.34;
			let d = `M${f(x)} ${f(base - th)}`;
			for (let tier = 1; tier <= 3; tier++) {
				const y = base - th + (th * 0.3 * tier);
				d += ` L${f(x + w * (0.45 + tier * 0.18))} ${f(y)} L${f(x + w * (0.2 + tier * 0.08))} ${f(y - th * 0.04)}`;
			}
			d += ` L${f(x + 5)} ${f(base - th * 0.12)} L${f(x + 5)} ${f(base)} L${f(x - 5)} ${f(base)} L${f(x - 5)} ${f(base - th * 0.12)}`;
			for (let tier = 3; tier >= 1; tier--) {
				const y = base - th + (th * 0.3 * tier);
				d += ` L${f(x - w * (0.2 + tier * 0.08))} ${f(y - th * 0.04)} L${f(x - w * (0.45 + tier * 0.18))} ${f(y)}`;
			}
			parts.push(`<path d='${d} Z'/>`);
			x += w * 1.3 + between(random, 0, 10);
		}
	}
	return { height: h, body: parts.join('') };
}

export function meadow(random: Random, h = 140): Cut {
	let d = `M0 ${h} L0 ${f(h * 0.55)}`;
	for (let x = 0; x < W; x += between(random, 6, 14)) {
		const blade = h * between(random, 0.25, 0.6);
		d += ` L${f(x + 3)} ${f(h * 0.62 - blade)} L${f(x + 6)} ${f(h * 0.6)}`;
	}
	return { height: h, body: `<path d='${d} L${W} ${f(h * 0.6)} L${W} ${h} Z'/>` };
}

export type Skyline = Cut & { windows: string };

/** Buildings with their windows cut out. Returns the windows separately, for the light behind them. */
export function skyline(random: Random, kind: 'towers' | 'city' | 'town', h = 440): Skyline {
	const outlines: string[] = [];
	const windows: string[] = [];
	let x = between(random, -30, 0);
	const tall = kind === 'towers' ? [0.35, 1] : kind === 'city' ? [0.3, 0.75] : [0.35, 0.8];
	const wide = kind === 'towers' ? [40, 95] : kind === 'city' ? [45, 110] : [70, 130];

	while (x < W) {
		const w = between(random, wide[0], wide[1]);
		const bh = h * between(random, tall[0], tall[1]) * (random() < 0.15 && kind === 'towers' ? 0.55 : 1);
		const top = h - bh;
		let d: string;
		if (kind === 'town' || (kind === 'city' && random() < 0.35)) {
			const roof = Math.min(w * 0.45, 50);
			d = `M${f(x)} ${h} L${f(x)} ${f(top + roof)} L${f(x + w / 2)} ${f(top)} L${f(x + w)} ${f(top + roof)} L${f(x + w)} ${h} Z`;
			if (random() < 0.4) d += ` M${f(x + w * 0.7)} ${f(top + roof * 0.6)} L${f(x + w * 0.7)} ${f(top + roof * 0.1)} L${f(x + w * 0.82)} ${f(top + roof * 0.1)} L${f(x + w * 0.82)} ${f(top + roof * 0.9)} Z`;
		} else if (random() < 0.3) {
			const step = w * 0.2;
			d = `M${f(x)} ${h} L${f(x)} ${f(top + 40)} L${f(x + step)} ${f(top + 40)} L${f(x + step)} ${f(top)} L${f(x + w - step)} ${f(top)} L${f(x + w - step)} ${f(top + 40)} L${f(x + w)} ${f(top + 40)} L${f(x + w)} ${h} Z`;
			if (random() < 0.5) d += ` M${f(x + w / 2 - 1.5)} ${f(top)} L${f(x + w / 2 - 1.5)} ${f(top - 50)} L${f(x + w / 2 + 1.5)} ${f(top - 50)} L${f(x + w / 2 + 1.5)} ${f(top)} Z`;
		} else {
			d = `M${f(x)} ${h} L${f(x)} ${f(top)} L${f(x + w)} ${f(top)} L${f(x + w)} ${h} Z`;
		}
		outlines.push(d);

		// A grid of windows, some of them dark.
		const cols = Math.max(1, Math.floor((w - 12) / 16));
		const rows = Math.max(1, Math.floor((bh - (kind === 'town' ? 50 : 30)) / 22));
		const cw = (w - 12) / cols;
		for (let r = 0; r < rows; r++) {
			for (let c = 0; c < cols; c++) {
				if (random() < 0.35) continue;
				const wx = x + 6 + c * cw + cw * 0.25;
				const wy = h - 18 - r * 22 - 12;
				if (wy < top + (kind === 'town' ? 40 : 14)) continue;
				windows.push(`M${f(wx)} ${f(wy)} h${f(cw * 0.5)} v10 h${f(-cw * 0.5)} Z`);
			}
		}
		x += w + between(random, 2, kind === 'town' ? 30 : 10);
	}

	const windowPath = windows.join(' ');
	return {
		height: h,
		body: `<path fill-rule='evenodd' d='${outlines.join(' ')} ${windowPath}'/>`,
		windows: `<path d='${windowPath}'/>`
	};
}

/**
 * A whole sheet with pinholes for stars pricked through its upper part. The
 * holes are subpaths of the same even-odd path, so they cut rather than add.
 */
export function prickedSky(random: Random, count: number, h = 900): string {
	// A gently scalloped top edge, the way a pop-up's backdrop is trimmed.
	let d = `M0 ${h} V18`;
	const scallops = 9;
	for (let i = 0; i < scallops; i++) {
		const x0 = (i / scallops) * W;
		const x1 = ((i + 1) / scallops) * W;
		d += ` Q${f((x0 + x1) / 2)} ${f(-6 + random() * 4)} ${f(x1)} 18`;
	}
	d += ` V${h} Z`;
	for (let i = 0; i < count; i++) {
		const r = random() < 0.08 ? between(random, 2.4, 3.4) : between(random, 1, 1.9);
		const cx = random() * W;
		const cy = Math.pow(random(), 1.4) * h * 0.62;
		d += ` M${f(cx - r)} ${f(cy)} a${f(r)} ${f(r)} 0 1 0 ${f(2 * r)} 0 a${f(r)} ${f(r)} 0 1 0 ${f(-2 * r)} 0 Z`;
	}
	return `<path fill-rule='evenodd' d='${d}'/>`;
}

/** A sunburst of long and short paper rays around a centre. */
export function sunburst(rays = 16, r = 50): string {
	let d = '';
	for (let i = 0; i < rays; i++) {
		const a = (i / rays) * Math.PI * 2;
		const long = i % 2 === 0 ? r : r * 0.78;
		const spread = Math.PI / rays * 0.55;
		const p = (angle: number, radius: number) => `${f(r + Math.cos(angle) * radius)} ${f(r + Math.sin(angle) * radius)}`;
		d += `M${p(a - spread, r * 0.52)} L${p(a, long)} L${p(a + spread, r * 0.52)} Z `;
	}
	return `<path d='${d}'/>`;
}

/** A cut-paper cloud: a flat base and a row of overlapping bumps. */
export function cloud(random: Random, x: number, y: number, width: number): string {
	const parts = [`<rect x='${f(x)}' y='${f(y)}' width='${f(width)}' height='${f(width * 0.12)}' rx='${f(width * 0.06)}'/>`];
	let cx = x + width * 0.12;
	while (cx < x + width * 0.9) {
		const r = width * between(random, 0.1, 0.22);
		parts.push(`<circle cx='${f(cx)}' cy='${f(y + width * 0.06 - r * 0.55)}' r='${f(r)}'/>`);
		cx += r * between(random, 0.9, 1.3);
	}
	return parts.join('');
}

/** A crescent, gibbous or full moon, by illumination and whether it's waxing. */
export function moon(illumination: number, waxing: boolean, r = 50): string {
	const k = 1 - 2 * illumination; // 1 new, 0 half, -1 full
	const sweepOuter = waxing ? 1 : 0;
	const sweepInner = k > 0 ? (waxing ? 0 : 1) : waxing ? 1 : 0;
	const rx = Math.abs(k) * r;
	return `<path d='M${r} 0 A${r} ${r} 0 0 ${sweepOuter} ${r} ${2 * r} A${f(rx)} ${r} 0 0 ${sweepInner} ${r} 0 Z'/>`;
}

/** An origami crane in flight, as three facets so the folds read. */
export const CRANE = {
	width: 120,
	height: 80,
	wingNear: 'M52 40 L28 4 L70 36 Z',
	wingFar: 'M56 38 L84 10 L66 40 Z',
	body: 'M4 30 L48 44 L60 34 L100 14 L108 18 L94 22 L70 48 L40 52 Z'
};
