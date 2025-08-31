# GCColorer

CLI tool to recolor, copy and delete **Google Calendar** events by rules.  
Built with **.NET 10**, OAuth2, and the Google Calendar API.

---

## Why GCColorer?

Google Calendar has a major limitation:  
👉 **When importing events (e.g. from ICS), per-event colors are not preserved.**

This leads to:
- All imported events showing in the same default calendar color.
- No way to assign event colors in `.ics` files.
- A lot of manual work if you want to visually separate categories (work, sport, family, etc.).
- Workarounds like using multiple calendars, which adds complexity.

For personal productivity this is frustrating:  
You want to see **one calendar** where events are clearly color-coded by category.

**GCColorer solves this problem** by:
- Connecting to your Google Calendar via API (OAuth2).
- Applying **color rules** to events based on their titles (`SUMMARY`).
- Supporting bulk operations in a date range:
  - recolor events
  - delete events
  - copy events to another period

This is especially useful for **personal calendars** where you want to visually separate events into categories for better clarity.

---

## Features

- 🔹 **Recolor events** automatically by rules (`ColorRules`).
- 🔹 **Delete events** automatically by rules (`DeleteRules`).
- 🔹 **Copy events** from one period to another (`--copyTo`).
- 🔹 Works in a given date range (`start`–`end`).
- 🔹 Self-contained executable, no need to install .NET runtime.

---

## Example usage

Recolor all events between **Sept 1 – Sept 7, 2025**:

```bash
dotnet run --project GCColorer -- 2025-09-01 2025-09-07
```

Delete all events matching `DeleteRules` in the same period:

```bash
dotnet run --project GCColorer -- 2025-09-01 2025-09-07 --delete
```

Copy events from one period to a new period starting Sept 14, 2025:

```bash
dotnet run --project GCColorer -- 2025-09-01 2025-09-07 --copyTo 2025-09-14
```

---

## Configuration

Copy the example file:

```bash
cp GCColorer/appsettings.json.example GCColorer/appsettings.json
```

Edit `appsettings.json`:

```json
{
  "Google": {
    "ClientId": "YOUR_GOOGLE_OAUTH_CLIENT_ID",
    "ClientSecret": "YOUR_GOOGLE_OAUTH_CLIENT_SECRET",
    "CalendarId": "primary",
    "TimeZone": "Europe/Warsaw"
  },
  "ColorRules": [
    { "MatchType": "equals", "Pattern": "Main job",     "ColorId": "8" },
    { "MatchType": "equals", "Pattern": "Sport",        "ColorId": "6" },
    { "MatchType": "equals", "Pattern": "Family Time",  "ColorId": "5" },
    { "MatchType": "equals", "Pattern": "Meeting",      "ColorId": "9" },
    { "MatchType": "equals", "Pattern": "Lunch",        "ColorId": "2" }
  ],
  "DeleteRules": [
    { "MatchType": "equals", "Pattern": "Main job" },
    { "MatchType": "equals", "Pattern": "Sport" },
    { "MatchType": "equals", "Pattern": "Family Time" },
    { "MatchType": "equals", "Pattern": "Meeting" },
    { "MatchType": "equals", "Pattern": "Lunch" }
  ]
}
```

- `MatchType`: `equals` | `contains` | `regex`  
- `Pattern`: text to match in event title  
- `ColorId`: Google Calendar color (see table below)

---

## ColorId reference

Google Calendar supports only **11 predefined colors** (`colorId`).  
Here is the mapping:

| ColorId | Name       | Suggested use         |
|---------|----------- |---------------------- |
| 1       | Lavender   | — |
| 2       | Sage       | Lunch (green) |
| 3       | Grape      | Hobby, reading |
| 4       | Flamingo   | — |
| 5       | Banana     | Family Time (yellow) |
| 6       | Tangerine  | Sport (orange) |
| 7       | Peacock    | — |
| 8       | Graphite   | Main job (gray) |
| 9       | Blueberry  | Meeting (blue) |
| 10      | Basil      | — |
| 11      | Tomato     | Important tasks (red) |

---

## Google API setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/).  
2. Create a new project.  
3. Enable **Google Calendar API**.  
4. Create credentials → **OAuth Client ID** → **Desktop App**.  
5. Copy **Client ID** and **Client Secret** into `appsettings.json`.  
6. First run will open a browser for OAuth consent.  
7. A token will be saved locally (e.g. in `token_store/`).

---

## Build & Run

Requirements:
- .NET 10 SDK (preview or later)

Clone and build:

```bash
git clone https://github.com/<your-username>/GCColorer.git
cd GCColorer/src
dotnet restore
dotnet build -c Release
```

Run:

```bash
dotnet run --project GCColorer.csproj -- 2025-09-01 2025-09-07
```

---

## CI/CD

- **CI:** builds and tests on every push/PR (`.github/workflows/dotnet-ci.yml`).  
- **Release:** builds self-contained binaries for Windows, Linux, macOS on each version tag (`.github/workflows/release.yml`).  

To publish a release:

```bash
git tag v0.1.0
git push origin v0.1.0
```

---

## License

[MIT](./LICENSE)
