export type Phase =
	| 'day'
	| 'golden-hour'
	| 'civil-twilight'
	| 'nautical-twilight'
	| 'astronomical-twilight'
	| 'night';

export type CityView = {
	id: string;
	name: string;
	country: string;
	latitude: number;
	longitude: number;
	timeZone: string;
	terrain: string[];
	skyline: 'towers' | 'city' | 'town' | 'none';
};

export type Crane = { id: number; colour: string; foldedAt: string };

export type Scene = {
	city: CityView;
	localTime: string;
	clock: string;
	phase: Phase;
	rising: boolean;
	summary: string;
	conditions: {
		code: number;
		description: string;
		temperatureC: number | null;
		humidity: number | null;
		cloudCover: number | null;
		precipitationMm: number | null;
		windKph: number | null;
		windDirection: number | null;
		visibilityM: number | null;
	};
	sun: {
		altitude: number;
		azimuth: number;
		sunrise: string | null;
		sunset: string | null;
		alwaysUp: boolean;
		alwaysDown: boolean;
	};
	moon: { altitude: number; azimuth: number; illumination: number; phase: number; name: string };
	sky: { top: string; middle: string; horizon: string; ambient: string; light: number; stars: number };
	effects: {
		clouds: number;
		cloudDarkness: number;
		precipitation: 'none' | 'drizzle' | 'rain' | 'sleet' | 'snow' | 'hail';
		intensity: number;
		fog: number;
		wind: number;
		windDirection: number;
		lightning: boolean;
		windowsLit: number;
	};
	observedAt: string | null;
	source: 'open-meteo' | 'synthetic' | 'none';
	cranes: Crane[];
	craneCount: number;
};

export type CityCard = {
	id: string;
	name: string;
	country: string;
	latitude: number;
	longitude: number;
	clock: string;
	phase: Phase;
	summary: string;
	temperatureC: number | null;
	code: number;
};

export type Book = {
	now: string;
	subsolar: { latitude: number; longitude: number };
	cities: CityCard[];
	readers: number;
	weather: 'open-meteo' | 'synthetic' | 'none';
};

export const CRANE_COLOURS: Record<string, string> = {
	red: '#d64541',
	orange: '#ee8a3a',
	yellow: '#f2c230',
	green: '#4f9d5d',
	teal: '#2a9d8f',
	blue: '#3b6fc2',
	violet: '#7b5ea7',
	white: '#f7f7f2'
};

async function json<T>(response: Response): Promise<T> {
	if (!response.ok) {
		if (response.status === 429) throw new Error('You’ve folded a lot of cranes. Rest your hands for a minute.');
		const problem = await response.json().catch(() => null);
		throw new Error(problem?.title ?? 'The book couldn’t reach the server. Is it running?');
	}
	return response.json();
}

export const api = {
	book: () => fetch('/api/book').then((r) => json<Book>(r)),
	scene: (id: string) => fetch(`/api/cities/${encodeURIComponent(id)}/scene`).then((r) => json<Scene>(r)),
	fold: (id: string, colour: string) =>
		fetch(`/api/cities/${encodeURIComponent(id)}/cranes`, {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify({ colour })
		}).then((r) => json<{ crane: Crane; total: number }>(r))
};
