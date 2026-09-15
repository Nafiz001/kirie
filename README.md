# kirie

A pop-up book of the sky over 36 places, cut from paper and lit by the weather
there right now. ASP.NET Core and SignalR on the back, SvelteKit and CSS 3D on
the front.

切り絵 (kirie) is Japanese paper cutting.

The book lies open on a cutting mat. Each spread is a city. As you turn to it,
its landscape folds up out of the page: a sky card, mountains, a skyline, the
sea. The paper is coloured by what the sky over that city is doing at this
moment.

- The sun and moon stand on paper rods at their real altitude and bearing.
- Clouds are cut-paper shapes drifting with the real wind.
- Rain falls in paper strips and snow in punched confetti.
- Fog is a sheet of vellum.
- At night the windows in the skyline light up and stars show through pinholes
  in the sky card.

The contents page is a volvelle, the rotating paper wheel old astronomy books
used to tell the time around the world. It shows the Earth from above the North
Pole, turned so the sun is always at the top, with night shaded across it and
every city pricked through.

Readers see each other: every page says how many people have it open. Anyone
can fold a paper crane for a city, and it flies across that page for everyone
reading it, for a day.

```console
$ make dev
server  http://localhost:5080
web     http://localhost:5173
```

Nothing is drawn with WebGL or canvas except the day and night shading on the
wheel. Every piece of the book is a DOM element: a coloured sheet with an SVG
silhouette as its CSS mask, hinged to the page with a 3D transform.

## How the server works

### The sky is computed, not fetched

Only the weather comes from outside. Everything about light and time is worked
out on the server from where each city is.

- **Sun and moon.** `Sky/Astronomy.cs` computes the altitude and azimuth of both
  from the low-precision formulas in Meeus' *Astronomical Algorithms*, plus the
  moon's illumination and phase.
- **Sunrise and sunset.** Found by scanning the local day and bisecting each
  horizon crossing down to 15 seconds. The tests check them against published
  times for London's summer solstice and New York's winter solstice, to within
  three minutes.
- **Polar days.** At midwinter in Tromsø the sun never rises, and at midsummer
  it never sets. Both come back as a missing sunrise or sunset, and the page
  says "under the midnight sun" or "in the polar night".
- **The contents wheel.** It needs the subsolar point, the spot on Earth where
  the sun is directly overhead. The wheel shades night across the Earth from
  that point, pixel by pixel.

### Weather becomes paper

`Scenes/SceneComposer.cs` turns a city, its latest reading and the current
moment into a scene: everything the client needs to cut and light the page.

- **Sky colours** come from a table of clear-sky colours keyed by the sun's
  altitude, from deep night through blue hour and sunset to noon. Dusk is warmer
  than dawn. Cloud cover then greys it toward overcast, and storms darken it
  further. The mixing happens in OKLab, so a sunset fading into cloud passes
  through believable colours instead of RGB's muddy midpoints.
- **Light** is how brightly the paper is lit. It falls with the sun, rises a
  little under a bright moon, and drops under heavy cloud. The client multiplies
  every sheet's colour by it, tinted by the sky and faded toward the horizon the
  further back the sheet stands.
- **Effects** come from the WMO weather code and the measurements:
  - Precipitation has a kind and an intensity. A rain code with nothing measured
    still rains.
  - Fog comes from visibility.
  - Wind comes from speed and gusts.
  - Lightning comes from thunderstorm codes.
  - How many windows are lit follows the sun and the gloom.

### Keeping the sky current

`Live/SkyKeeper.cs` is a background service with two clocks:

- **Every 10 minutes** it fetches current conditions for all 36 places in one
  request to [Open-Meteo](https://open-meteo.com), which needs no key. The HTTP
  client has the standard resilience pipeline: retries with backoff, a circuit
  breaker and timeouts. Readings are written through to SQLite, so a restart
  doesn't begin with an empty sky. Only cities whose sky *visibly* changed get
  redrawn; a temperature drift of a tenth of a degree doesn't count.
- **Every minute** it pushes fresh scenes to every page that someone has open,
  because the sun and moon keep moving whether or not the weather does.

If Open-Meteo can't be reached and nothing is stored, it falls back to plausible
synthetic weather so the book still opens offline. The page says so.

### Readers and cranes

Each reader holds one SignalR connection (`/hubs/book`) and opens one page at a
time. Opening a page joins that page's group. That's how the reader hears when
its sky changes, how many others are on it, and when someone folds a crane for
it.

Folding a crane is a plain `POST`, rate-limited per address. It's stored with a
salted hash of who folded it, never the address itself, and then broadcast to
the page's group.

### API

| | |
| --- | --- |
| `GET /api/book` | every city with its local time and summary, where the sun is overhead, how many readers |
| `GET /api/cities/{id}/scene` | everything needed to cut and light one page |
| `POST /api/cities/{id}/cranes` | fold a crane `{ "colour": "teal" }` |
| `GET /health` | |
| `WS /hubs/book` | `Open(page)`; receives `Scene`, `Readers`, `CraneFolded` |

## How the book works

**Pop-ups.** A piece of pop-up (`Layer.svelte`) is a sheet hinged to the page
along its bottom edge:

- `rotateX(-90deg × pop)` stands it up.
- Its position along the page sets its depth.
- A spring curve on the transition gives the overshoot of real paper popping up.
- Pieces rise back to front in a stagger. Before a page turns they fold down
  all at once, quickly, so the leaf never passes through a half-standing piece.

**Silhouettes.** `lib/shapes.ts` generates every silhouette as SVG: ridgelines,
volcanoes, dunes, waves, ice blocks, pine and broadleaf forests, skylines. Each
becomes the CSS mask of a sheet of coloured paper. Windows and stars are holes
in the same even-odd path, so they are cut out rather than drawn on.

**Paper.**
- Every shape is seeded from the city, so Tokyo is cut the same way on every
  visit; only its light changes.
- A generated fibre texture, laid over every sheet with a multiply blend, makes
  it read as paper.
- Behind the skyline sits a second sheet that shows through the window holes:
  grey glass by day, lamplight by night.
- Behind the sky card sits a starlight sheet. It matches the sky by day, so the
  pinholes vanish, and turns warm white by night, so they shine.

**Where the sun is.** The viewer always faces the equator. In the northern
hemisphere, east is on the left and the sun sets on the right. In the southern
hemisphere it's the other way round.

## Running it

You need the .NET 10 SDK and Node 20.19+.

```console
make setup   # restore, npm install
make dev     # server on :5080, book on :5173 (Vite proxies the API and the hub)
make test    # .NET tests, then svelte-check
make build   # the book as static files in web/build
make serve   # one process: the server hosting the built book on :5080
```

Or by hand:

```console
cd server && dotnet run --project Kirie.Api
cd web && npm install && npm run dev
```

Configuration, in `server/Kirie.Api/appsettings.json` or as environment variables
(`Sky__Offline=true` and so on):

| | |
| --- | --- |
| `ConnectionStrings:Kirie` | SQLite database, default `kirie.db` |
| `Sky:WeatherEvery`, `Sky:SkyEvery` | refresh intervals |
| `Sky:Offline` | never call Open-Meteo; use synthetic weather |
| `Kirie:Web` | a directory of built files to host, for single-process deployment |
| `Kirie:Salt` | salt for crane folders' address hashes |
| `Kirie:CranesPerMinute` | rate limit per address |

The tests never touch the network: they run the whole app with its own
database and synthetic weather, including a real SignalR client.

## Layout

```
server/
  Kirie.Api/
    Cities/        the 36 places, west to east
    Sky/           sun, moon, sunrise and sunset, subsolar point
    Weather/       Open-Meteo, synthetic weather, WMO codes, the store
    Scenes/        OKLab colour, the scene composer
    Live/          the SignalR hub, readers, the background sky keeper
    Cranes/
    Data/          EF Core context and migrations
  Kirie.Tests/     astronomy, scenes, API and hub tests
web/src/lib/
  shapes.ts        silhouettes to cut
  paper.ts         seeded randomness, colour, the paper texture
  live.svelte.ts   the SignalR connection
  components/
    Desk.svelte      the mat, the book's state, turning pages
    Book.svelte      the book in 3D: boards, pages, cover, turning leaf
    Layer.svelte     one hinged pop-up piece
    Sheet.svelte     one sheet of cut paper
    Diorama.svelte   a city's page
    Contents.svelte  the volvelle
    Falling.svelte   rain, snow and hail
    Tag.svelte       the tag beside the book
```
