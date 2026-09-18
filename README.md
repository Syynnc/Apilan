# Apilan

Event registration and attendance for student organizations.

An organization publishes an event, students register online and get a stub with
a check-in code, an officer records attendance at the door, and the certificates
and the post-activity report come out of those same records. The point of the
system is that nothing is entered twice: the report an org files at the end of a
term is read back out of the check-ins, not retyped from a sign-in sheet.

ASP.NET Core 8 MVC · Entity Framework Core 8 · SQL Server · Razor views, no SPA.

---

## Before you start: the repo has no commits yet

`git log` is empty and there is no remote. So there is nothing to clone — the
code lives in this working folder only, and it is not backed up anywhere but
OneDrive. Making the first commit is the first useful thing you can do:

```bash
git add .
git commit -m "Initial commit"
```

Then, if you want it off this machine, create an empty repo on GitHub and:

```bash
git remote add origin <url>
git push -u origin master
```

The branch here is `master` while `main` is the more common default now; if you
care, rename before the first push with `git branch -M main`.

A standard .NET `.gitignore` is already in place, which is what keeps that
`git add .` honest — it drops `bin/`, `obj/` and Visual Studio's `.vs/`, about
68 MB of build output across 194 files that should never enter history. Two
things it deliberately does **not** ignore:

- **`wwwroot/lib/`** — Bootstrap and jQuery are vendored, and there is no npm or
  libman restore step, so those files have to be committed or the styling breaks
  for whoever clones next.
- **`appsettings.json`** — the connection string and config shape belong in the
  repo, which unavoidably means the seeded admin password is committed with
  them. Move it to [user secrets][secrets] or environment variables before this
  runs anywhere real.

[secrets]: https://learn.microsoft.com/aspnet/core/security/app-secrets

---

## Running it

You need the **.NET 8 SDK or newer** — the project targets `net8.0`, so the
.NET 8 *runtime* has to be present even if your SDK is 9 — and a **SQL Server**
instance. The connection string in `Apilan/appsettings.json` points at LocalDB,
which ships with Visual Studio:

```
Server=(localdb)\MSSQLLocalDB;Database=Apilan;Trusted_Connection=True
```

If you use a real SQL Server or a container, change that string and nothing else.

```bash
cd Apilan

dotnet tool restore        # installs dotnet-ef 8.0.31, pinned in .config/
dotnet restore
dotnet ef database update  # creates the database and applies both migrations
dotnet run
```

Then open the URL it prints — `https://localhost:7133` under the `https` profile,
or `http://localhost:5200` under `http`. In Visual Studio, F5 on `Apilan.sln`
does the same thing, but you still have to run `dotnet ef database update` (or
`Update-Database` in the Package Manager Console) at least once by hand.

**Migrations are never applied automatically.** The seeder checks for pending
migrations on startup and, if it finds any, logs a warning and does nothing
rather than half-populating a stale schema. If the app starts and every screen is
empty, read the console — that warning is almost always why.

### Signing in

With `Apilan:SeedDemoData` set to `true` (it is, in `appsettings.json`), the
first run creates three accounts, all with the password `Apilan!2026`:

| Email | Role | Sees |
|---|---|---|
| `admin@apilan.local` | Admin | Everything, including the activity log and role management |
| `officer@apilan.local` | Officer | The console: events, check-in, members, reports |
| `member@apilan.local` | Member | The calendar, their own stubs, their own feedback |

The login field takes either the email or the student number. Change those
credentials in `appsettings.json` before this goes anywhere real — they are
committed in plain text, which is fine for a demo and not fine otherwise.

Demo data is 4 organizations, 12 members, a handful of events including one
already finished with attendance and feedback on it, so the reports and
certificate screens have something true to show on a fresh machine. Set
`Apilan:SeedDemoData` to `false` for a clean install; the admin account is
created either way. Every seeding step is idempotent and skips itself if the
records already exist.

---

## What is where

```
Apilan/
  Controllers/      One per module. Thin — rules live in Services/.
  Models/
    Entities.cs     Every entity, in one file, grouped by lifecycle.
    Enums.cs        Roles and every status enum.
  Data/
    ApilanDbContext.cs   Relationships and delete behaviour, commented.
    DbSeeder.cs          Roles, first admin, demo records.
    Migrations/
  Services/         The rules two screens must not disagree about.
  ViewModels/       What each screen needs; entities are not bound to directly.
  Views/            Razor, plus four layouts (public, auth, admin, base).
  wwwroot/css/      tokens → base → components → screens → admin/landing/auth.
  wwwroot/js/       site, theme, console, landing, calendar, scanner.
```

`Services/` is the part worth reading first, because it is where the invariants
are kept rather than repeated:

- **`RegistrationService`** — capacity, the waitlist queue, and unique check-in
  codes. Both the public calendar and the officer console register people
  through it, so the two can never drift.
- **`AttendanceRules`** — turns attendance records into a certificate decision,
  and classifies an arrival as present or late. The certificates screen and the
  participation report both read from here.
- **`SessionSummaries`** — the per-session tally. The event screen, the door, and
  the report all show "how did this session go" and must agree to the person.
- **`AuditTrail`** — writes the log entry in the same unit of work as the change
  it describes, so a change and its record are saved together or not at all.
- **`OrgContext`** — which organization a screen is speaking for.
- **`ReportWorkbook`** — the Excel packs, built with ClosedXML.

---

## Things that will surprise you

**A student cannot register until an officer links their account to a member
record.** Accounts and people are separate: `ApplicationUser` is a login,
`Member` is the organization's own record of a person. They are deliberately not
the same row, because officers keep member records for people who never sign up
online, and walk-ins get registered at the door with no account at all. A
self-registered account sits at `Pending` until someone approves it in
**Administration → Account requests** and links it. Until then My Events says so
and the register button does nothing.

**The role is called `Member`, not Student.** Three roles: `Admin`, `Officer`,
`Member`. Most of the console is behind the `StaffOnly` policy (Admin or
Officer); the activity log and role management are `AdminOnly`.

**Absence is a record, not a missing row.** An `AttendanceRecord` exists for
present, late *and* absent. Late is decided by comparing time-in against the
session's own `LateAfterMinutes` grace period, and **late counts as attended for
certificates** — arriving late is a punctuality problem, not an absence. An
officer closes a session to write off everyone still unaccounted for in one move.
A no-record is different from an absence: nobody has said anything either way.

**Deleting an event is deliberately hard.** Only a draft or a cancelled event
with zero attendance can be deleted, and you have to type the title to confirm.
Anything with attendance behind it is a fact the reports depend on, so it gets
cancelled instead — which voids the stubs and keeps the history. The audit entry
names the deleted event in its own text, so the trail outlives the row.

**The QR codes are real.** Stubs carry a scannable QR of the check-in code
itself, so a scanner produces exactly what an officer would have typed. The door
can also scan with a device camera where the browser supports `BarcodeDetector`
(Chrome and Edge); everywhere else the button never appears and the text field —
which a handheld scanner drives just as well — is unchanged.

**The landing page's calendar is live; its two other demos are not.** The month
calendar reads real published events from `/Calendar/Feed`. The arrivals ticker
and the door kiosk animation are canned illustrations, and `landing.js` says so
where they are defined. Don't read numbers off them.

**Organizations are not tenancy.** Every event belongs to one organization and
you can filter and report by it, but an officer can still see every org's
events. The event form just defaults to their own. If you want officers actually
scoped to their org, that is a deliberate change nobody has made yet.

**An organization named `ORG` may already exist.** The migration that introduced
organizations had to give the events that already existed somewhere to belong, so
it inserts a placeholder called "Our Organization" and points them at it. If you
see it, rename it in **Master data → Organizations**. A genuinely fresh database
never gets it.

---

## The modules

| Area | Route | Who |
|---|---|---|
| Landing page | `/` | Anyone |
| Public calendar — list and month grid | `/Calendar`, `/Calendar/Month` | Anyone |
| Register for an event | `/Calendar/Register` | Member |
| My stubs, my attendance, my certificates | `/MyEvents` | Member |
| Leave feedback on an event attended | `/Feedback/Form/{id}` | Member |
| Dashboard | `/Dashboard` | Staff |
| Events, sessions, roster, waitlist | `/Events` | Staff |
| Door check-in | `/Attendance/Door/{id}` | Staff |
| Certificates | `/Certificates` | Staff |
| Organizations and their events | `/Organizations` | Staff (admin to edit) |
| Members, event types, venues | `/Members`, `/EventTypes`, `/Venues` | Staff |
| Participation and per-event reports, Excel | `/Reports` | Staff |
| Feedback read-out | `/Feedback` | Staff |
| Account requests | `/AccountRequests` | Staff |
| Officers and roles | `/Officers` | Admin |
| Activity log | `/AuditLog` | Admin |

An event moves **Draft → Published → Ongoing → Completed**, or is cancelled.
Only a published event takes registrations; only closing one out settles its
certificates.

---

## Working on it

```bash
cd Apilan
dotnet build
dotnet run
```

There is no test project. Verification so far has been manual: exercising the
flows against a running instance. If you add tests, that gap is the obvious
place to start — `AttendanceRules` and `RegistrationService` are pure enough to
test directly and are where a mistake would be most expensive.

After changing anything in `Models/` or `Data/ApilanDbContext.cs`:

```bash
dotnet ef migrations add SomeDescriptiveName
dotnet ef database update
```

**Read the generated migration before applying it.** Two things here have needed
hand-editing already, and both will come up again:

1. **A new required foreign key breaks existing rows.** The scaffolder adds the
   column with a default of `0` and then adds a key that nothing satisfies. The
   organizations migration was rewritten to create the table, insert a
   placeholder, backfill, and only then add the key.
2. **SQL Server rejects multiple cascade paths.** Two `SET NULL` foreign keys
   from the same principal table will not build — `Event.LastEditedBy` had to be
   `NoAction` because `Event.CreatedBy` already claimed the one `SET NULL` path
   from `AspNetUsers`. `ReviewedBy` on `ApplicationUser` is `NoAction` for the
   same reason.

Also worth knowing: a scaffolded migration takes SQL's defaults (`0`, `false`),
not the model's. If a new property has a meaningful default, backfill it in the
migration or existing rows will quietly mean something different from new ones.

---

## Configuration

Everything lives under `Apilan` in `appsettings.json`:

| Key | Default | What it does |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | LocalDB | Where the database is |
| `Apilan:SeedDemoData` | `true` | Create demo orgs, members, events, feedback |
| `Apilan:DefaultAdminEmail` | `admin@apilan.local` | First admin's login |
| `Apilan:DefaultAdminPassword` | `Apilan!2026` | First admin's password |
| `Apilan:OrganizationName` | `CCS Student Council` | Legacy; orgs now live in the database |

`Apilan:OrganizationName` is no longer read by anything — the organization a
screen shows comes from `OrgContext` and the `Organizations` table. It is left in
place only so an older config file does not look broken.
