// Paper: seeded randomness so every city's landscape is cut the same way on
// every visit, colour arithmetic for lighting it, and the grain texture every
// sheet shares.

export type Random = () => number;

export function seeded(key: string): Random {
	let h = 1779033703 ^ key.length;
	for (let i = 0; i < key.length; i++) {
		h = Math.imul(h ^ key.charCodeAt(i), 3432918353);
		h = (h << 13) | (h >>> 19);
	}
	let state = h >>> 0;
	// mulberry32
	return () => {
		state = (state + 0x6d2b79f5) | 0;
		let t = Math.imul(state ^ (state >>> 15), 1 | state);
		t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
		return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
	};
}

export const between = (random: Random, lo: number, hi: number) => lo + random() * (hi - lo);

type RGB = [number, number, number];

export function parse(hex: string): RGB {
	const v = parseInt(hex.replace('#', ''), 16);
	return [(v >> 16) & 255, (v >> 8) & 255, v & 255];
}

export function hex([r, g, b]: RGB): string {
	const c = (x: number) => Math.round(Math.min(255, Math.max(0, x))).toString(16).padStart(2, '0');
	return `#${c(r)}${c(g)}${c(b)}`;
}

export function mix(a: string, b: string, t: number): string {
	const x = parse(a);
	const y = parse(b);
	const k = Math.min(1, Math.max(0, t));
	return hex([x[0] + (y[0] - x[0]) * k, x[1] + (y[1] - x[1]) * k, x[2] + (y[2] - x[2]) * k]);
}

/**
 * Paper of a given colour as it looks under the scene's light: dimmed toward
 * night, tinted by the colour of the sky, and faded toward the horizon the
 * further back it stands.
 */
export function lit(base: string, sky: { ambient: string; horizon: string; light: number }, distance = 0): string {
	const hazed = mix(base, sky.horizon, distance * 0.55);
	const tinted = mix(hazed, sky.ambient, 0.25 + (1 - sky.light) * 0.35);
	const [r, g, b] = parse(tinted);
	const l = 0.25 + sky.light * 0.75;
	return hex([r * l, g * l, b * l]);
}

/** A data URL for an SVG, for use as a CSS mask. */
export function svgUrl(width: number, height: number, body: string): string {
	const svg = `<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 ${width} ${height}' preserveAspectRatio='none'>${body}</svg>`;
	return `url("data:image/svg+xml,${encodeURIComponent(svg)}")`;
}

let grain: string | null = null;

/**
 * A tileable texture of paper fibre, drawn once: fine speckle, a few long
 * fibres, and soft blotches where the pulp was thicker. Laid over every sheet
 * with a multiply blend.
 */
export function paperGrain(): string {
	if (grain) return grain;
	const size = 256;
	const canvas = document.createElement('canvas');
	canvas.width = canvas.height = size;
	const ctx = canvas.getContext('2d');
	if (!ctx) return '';
	const random = seeded('paper');

	ctx.fillStyle = '#ffffff';
	ctx.fillRect(0, 0, size, size);

	for (let i = 0; i < 26; i++) {
		const x = random() * size;
		const y = random() * size;
		const r = 20 + random() * 50;
		const g = ctx.createRadialGradient(x, y, 0, x, y, r);
		g.addColorStop(0, `rgba(120,110,100,${0.035 + random() * 0.035})`);
		g.addColorStop(1, 'rgba(120,110,100,0)');
		for (const dx of [-size, 0, size]) {
			for (const dy of [-size, 0, size]) {
				ctx.save();
				ctx.translate(dx, dy);
				ctx.fillStyle = g;
				ctx.fillRect(x - r, y - r, r * 2, r * 2);
				ctx.restore();
			}
		}
	}

	const image = ctx.getImageData(0, 0, size, size);
	for (let i = 0; i < image.data.length; i += 4) {
		const n = (random() - 0.5) * 26;
		image.data[i] += n;
		image.data[i + 1] += n;
		image.data[i + 2] += n;
	}
	ctx.putImageData(image, 0, 0);

	ctx.lineCap = 'round';
	for (let i = 0; i < 70; i++) {
		const x = random() * size;
		const y = random() * size;
		const a = random() * Math.PI * 2;
		const len = 6 + random() * 22;
		ctx.strokeStyle = random() < 0.5 ? `rgba(90,80,70,${0.08 + random() * 0.1})` : `rgba(255,255,255,${0.2 + random() * 0.3})`;
		ctx.lineWidth = 0.4 + random() * 0.6;
		ctx.beginPath();
		ctx.moveTo(x, y);
		ctx.quadraticCurveTo(x + Math.cos(a + 0.6) * len * 0.5, y + Math.sin(a + 0.6) * len * 0.5, x + Math.cos(a) * len, y + Math.sin(a) * len);
		ctx.stroke();
	}

	grain = `url(${canvas.toDataURL('image/png')})`;
	return grain;
}
