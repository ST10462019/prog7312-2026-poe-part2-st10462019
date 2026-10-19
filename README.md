# Smart-X Part 2 – Real-Time Processing & Device Management

ASP.NET Core 10 Minimal API + single-page dashboard (no build step) + xUnit tests.

## Run it

```bash
dotnet run --project src/SmartX.Part2.Api          # then open the URL it prints (e.g. http://localhost:5000)
dotnet test                                          # runs all tests
```

## demo script for the video

1. **Home** – three pillars; Topology is disabled.
2. Open **Real-Time Command Stream**. Click **Start simulator** – every reading is a real `POST /api/telemetry`.
3. Click **Moisture B drop** – packet goes to the **priority queue**, card turns red, and a **Suggested action: Turn Pump A ON** appears (learned from history, with confidence %). Click **Apply**.
4. Click **Undo last** – the **Stack** reverts the pump.
5. Click **Disconnect a node** – after ~6 s it turns Offline; the *Duplicate alerts blocked* counter climbs (**HashSet**).
6. Click **Burst 600 packets** – FIFO queue depth jumps and drains.
7. Filter by category / status chips / type `pump a`; click a card to see the sorted timeline chart.
8. Switch between Home / Ingestion / Stream – state, filters and simulator keep running (views are hidden, never destroyed).

## Mark map

| Requirement | Where |
|---|---|
| Menu + async navigation, state kept (30) | `wwwroot/index.html` – hash router, views hidden not destroyed, `sessionStorage` filters, 1 s async polling |
| Queue&lt;T&gt; FIFO | `TelemetryProcessor._standard` |
| PriorityQueue critical bypass | `TelemetryProcessor._critical` (drained immediately on arrival) |
| Stack&lt;T&gt; undo | `CommandService._undo` |
| Dictionary live devices | `DeviceRegistry._devices` (key = MAC) |
| SortedDictionary history | `DeviceRegistry._history` (per device, by timestamp) |
| HashSet unique alerts | `AlertTracker._active` (+ duplicate counter) |
| Search / filter | `GET /api/devices?q=&category=&status=` + toolbar |
| Manual overrides | `POST /api/commands`, `/api/commands/undo` |
| Predictive engine (30) | `PredictionEngine` (Markov transition table `Dictionary<string,Dictionary<string,int>>`) + `InsightService` → "Suggested actions & automated insights" panel |

## API

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/telemetry` | `{ "mac": "...", "value": 42.5 }` or `{ "mac": "...", "state": true }` |
| POST | `/api/telemetry/batch` | array of the above |
| GET/POST | `/api/devices` | list (filters) / register |
| GET | `/api/devices/{mac}/history?take=120` | ordered readings |
| POST | `/api/commands`, `/api/commands/undo` | `{ "macs": [...], "action": "on|off|toggle" }` |
| GET | `/api/commands`, `/api/stats`, `/api/alerts`, `/api/insights` | dashboard data |
| POST | `/api/search-log` | records a search for the predictor |

Hit it like a device from a terminal:

```bash
curl -X POST http://localhost:5000/api/telemetry -H "Content-Type: application/json" \
     -d '{"mac":"AA:BB:CC:00:00:02","value":12}'
```

