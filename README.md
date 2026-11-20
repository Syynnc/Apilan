# Apilan

**Event and attendance management for the Alliance student organization, University of Cebu.**

Officers create events, take attendance at the door, and pull participation reports.
Members sign in to see upcoming events and their own attendance history.
Every report comes from the attendance records, so nothing has to be typed in twice.

**Stack:** ASP.NET Core MVC (.NET 9) · Razor views · SQL Server (`AllianceDB`) · stored procedures

---

## Features

**Officers**
- **Dashboard:** an overview of upcoming events and recent attendance.
- **Events:** create and edit events. Each event has a type, a required venue, a capacity and a time-in/time-out window. Arriving after the time-in window counts as late.
- **Event types:** manage categories, each with its own colour on the calendar.
- **Attendance:** record time-in and time-out for members at an event.
- **Members:** add, edit and deactivate members (year 1–5 or Non-blocked, plus a searchable course picker). Every member gets a sign-in automatically. A student number can belong to only one member, and the form says who already has it.
- **Accounts:** promote a member to officer or demote them, and reset passwords.
- **Reports:** participation broken into sections, with filters and an analytics export.
- **Audit log:** a record of who changed what, and when.

**Members**
- See the event calendar.
- See their own attendance history.
- Change their password. This is required at the first sign-in.

**Everywhere**
- **Phones:** under 960px the sidebar becomes a slide-out menu, and under 640px each table row turns into a card.
- **Clickable rows:** clicking a row in the Events, Members, Accounts or report tables opens that row's page. Ctrl/Cmd-click or middle-click opens it in a new tab.
- **Input checks:** student numbers must be `####-#####`, and names accept only letters (ñ and accents included), spaces, `-`, `.` and `'`. The checks run while typing and again on the server.
- **Fast on venue Wi-Fi:** fonts are served by the site itself (no Google Fonts), static files are cached by the browser, and CSS/JS are compressed.

---

## Project structure

```
Apilan.sln
├── ASI.Basecode.WebApp      → controllers, Razor views, wwwroot (startup project)
├── ASI.Basecode.Services    → business logic and view models
├── ASI.Basecode.Data        → database access (EF Core + stored procedures)
├── ASI.Basecode.Resources   → constants and settings
└── database/                → SQL change scripts (01–07)
```

### How data moves through the app

Every screen follows the same path. Each layer only talks to the one below it.

```
Browser ──► Controller ──► Service ──► Repository ──► AllianceDB
 (Razor     (WebApp)       (Services)  (Data)         (tables, views,
  view, JS)                                            stored procedures)
   ▲                                                        │
   └──────── view model ◄── view model ◄── entity / view row ┘
```

1. **Browser.** A Razor view renders a form whose inputs are named after a
   view-model property (`asp-for`). Plain forms post or GET normally. Add/edit
   forms open in a modal: `modal.js` fetches the form and posts it with
   `X-Requested-With`. Live search (`/Events/Suggest`) fetches JSON.
2. **Controller** (`ASI.Basecode.WebApp/Controllers`). MVC model-binds the
   request into a view model (e.g. `EventViewModel`) and validates it through
   `ModelState`. The controller adds who is signed in (`CurrentUserName` from
   the cookie) and calls a service. It never touches the database directly.
3. **Service** (`ASI.Basecode.Services/Services`). Applies the business rules
   (e.g. whether a scan is a time-in or time-out) and maps view models to
   entities with AutoMapper (`MappingProfile`). Writes are wrapped in
   `ServiceBase.Run`, which turns a database rule violation into an
   `InvalidDataException` carrying the database's message.
4. **Repository** (`ASI.Basecode.Data/Repositories`). **Reads** return
   `IQueryable` over tables and views (`vw_EventAttendanceSummary`, ...), so
   the service's filters run as SQL. **Writes** call stored procedures
   (`sp_Event_Create`, `sp_Attendance_TimeIn`, ...) with parameterized SQL.
5. **AllianceDB** enforces the hard rules (unique student numbers, the
   attendance window, capacity) and raises `THROW 50xxx` when one is broken.

On the way back, the service turns rows into a view model and the controller
returns one of these:
- a **view** (a full page, or just `_Form` for the modal);
- **JSON** (`{ redirect }` after a modal save, or search suggestions);
- a **redirect**, with a toast message carried in `TempData`.

Example, saving an event: `Views/Events/_Form.cshtml` → `modal.js` →
`EventsController.Save` → `EventService.AddEvent` →
`EventRepository.AddEvent` → `dbo.sp_Event_Create`. Each step is commented in
the code.

---

## Setup

### 1. Install these first

| Tool | Notes |
|---|---|
| [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) | Check with `dotnet --version` (it should show 9.x) |
| SQL Server **LocalDB** | Comes with Visual Studio's *ASP.NET and web development* workload |
| [SSMS](https://aka.ms/ssms) | SQL Server Management Studio, used to create the database |
| Git | |
| Visual Studio 2022 (17.12 or newer) | Recommended. VS Code also works |

### 2. Get the code from the `development` branch

```powershell
git clone -b development https://github.com/Syynnc/Apilan.git
cd Apilan
```

If you already cloned it:

```powershell
git checkout development
git pull
```

### 3. Create the database

The database is **not** in this repo. Ask the team for **`Alliance.sql`**; it's sent separately.
It creates the whole `AllianceDB` with its tables, procedures and data, and it already includes every script in `database/`.

1. Open SSMS and connect to **`(localdb)\MSSQLLocalDB`** with *Windows Authentication*.
2. If you already have an `AllianceDB`, right-click it → **Delete**, and tick **Close existing connections**.
3. Open `Alliance.sql` with **File → Open → File…**
4. Near the top, find `CREATE DATABASE [AllianceDB]`. Delete the `ON PRIMARY ( … )` and `LOG ON ( … )` parts (they contain file paths from another PC), so the line reads only:
   ```sql
   CREATE DATABASE [AllianceDB]
   ```
5. Press **Execute** (F5). It should finish with no red errors.
6. Right-click **Databases → Refresh**. `AllianceDB` should appear with its tables.

### 4. Check the connection string

`ASI.Basecode.WebApp/appsettings.json` already points to LocalDB:

```json
"DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=AllianceDB;Trusted_Connection=True;TrustServerCertificate=True"
```

If you use SQL Server Express instead, change `(localdb)\\MSSQLLocalDB` to `.\\SQLEXPRESS` on your machine only, and **don't commit that change**.

### 5. Run the app

**Visual Studio:** open `Apilan.sln`, right-click **ASI.Basecode.WebApp → Set as Startup Project**, then press **F5**.

**Or use the terminal:**

```powershell
dotnet run --project ASI.Basecode.WebApp --launch-profile https
```

Then open **https://localhost:7133** (or http://localhost:5200).

### 6. Sign in

Sign in with your **ID** (student number).

| Who | ID | Password |
|---|---|---|
| Initial officer | `2000-00001` | `Password123` |
| Member | `2022-00101` | `Password123` |

New members added later sign in with their ID and the default password `Apilan@2026`. They're asked to choose a new one the first time.

---

## Database changes (`database/` folder)

When someone changes the database, they add a numbered script to `database/` (for example `08_Something.sql`).
After you `git pull`, run any **new** scripts on your `AllianceDB`:

1. In SSMS, open the script with **File → Open → File…**
2. In the toolbar's database dropdown, change **`master`** to **`AllianceDB`**.
3. Press **Execute** (F5).

The scripts are written so they're safe to run more than once. Always run them in number order.

---

## Working on the project

- Each person works on their own branch (for example `casquejo-dev`) and opens a pull request into **`development`**.
- Pull `development` often so you're working on the latest code:
  ```powershell
  git checkout development
  git pull
  git checkout your-branch
  git merge development
  ```
- **Never commit** database exports (`.sql` dumps, `.bak`, `.bacpac`), because they contain member data and password hashes.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| `Directory lookup for the file … failed` | Remove the `ON PRIMARY` / `LOG ON` parts (Setup step 3.4) |
| `Database 'AllianceDB' already exists` | Delete the old one first (Setup step 3.2) |
| `Cannot open database "AllianceDB"` | The database name must be exactly `AllianceDB` |
| `A network-related or instance-specific error` | LocalDB isn't running. Run `sqllocaldb start MSSQLLocalDB` |
| `Invalid object name …` / `Invalid column name …` | A script ran against `master`, or you missed a new script in `database/` |
| `The current .NET SDK does not support targeting .NET 9.0` | Install the .NET 9 SDK |
| Browser certificate warning | Run `dotnet dev-certs https --trust` once |
| Can't sign in at all | Your database has no data. Ask for a fresh `Alliance.sql` exported with **Schema and data** |
