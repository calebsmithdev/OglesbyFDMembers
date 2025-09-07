# Custom Instructions — Blazor/SQLite Project (Volunteer Fire Dept – Membership & Payments)

**Goal**
Track yearly membership fees **per property**, handle utility-bill payments that list only a payer’s name, keep **owner/property history**, manage mailing addresses & returns, and print clear **“who owes / who paid”** reports.

**High-level shape**

* **App type:** Blazor (Server or Hybrid/WinUI 3). One Windows PC, fully offline.
* **Data:** SQLite (single file). EF Core for schema, migrations, and queries.
* **Packaging:** Single EXE. Data file sits in a fixed folder; simple copy-based backups.
* **Git-friendly:** Everything source-controlled; dev uses a sandbox DB.

---

## Domain model (tables/entities)

Keep the proven schema but expressed as EF entities:

* `Person` – people who owe/pay.
* `PersonAddress` – many per person; one **primary**; `IsValidForMail` for returned mail.
* `PersonAlias` – name variants for matching utility notices.
* `Property` – parcels in jurisdiction (physical/situs address, Active flag).
* `Ownership` – **dated** join (Start/End) Person↔Property for history.
* `FeeSchedule` – Year, AmountPerProperty.
* `Assessment` – **Property × Year**: AmountDue, AmountPaid, Status.
* `Payment` – a payment made by a person (Cash/Check/Utility; `IsDonation`).
* `PaymentAllocation` – splits a Payment across one or more Assessments.
* `UtilityNotice` – monthly intake (payer name, amount; no address), matching target for aliases.

**Non‑negotiable rules**

1. Liability is **per property per year**; an owner should cover **all** owned properties.
2. Yearly reports use owner of record on **Jan 1** (configurable policy date).
3. **Utility** payments: match by Person/Alias. If `amount == (#owned × fee)` → auto allocate 1×fee per property; else mark **NeedsReview**.
4. **Donations** are normal credits (`IsDonation = true`) but separated in reporting.
5. **Mail returns:** set address invalid; if it was primary, promote next valid; if none → show on “no mailing address” report.
6. `Assessment (PropertyId, Year)` is **unique**.

---

## Stack & project layout

**Stack**: .NET 8, Blazor Server (or Blazor Hybrid), EF Core, SQLite. Optional libs: QuestPDF (PDFs/labels/receipts), CsvHelper (CSV import), ClosedXML (Excel export), Serilog (file logging).

**Layout**

```
src/
  OglesbyFDMembers.Domain/     // POCOs + validation
  OglesbyFDMembers.Data/       // DbContext, configurations, migrations, seed
  OglesbyFDMembers.App/        // Blazor UI (Server or Hybrid), pages, components
  OglesbyFDMembers.Tools/      // importers, data repair scripts (console)
```

**Connection** (SQLite): `Data Source=C:\\OglesbyFD\\Data\\oglesby.db;Cache=Shared;Foreign Keys=True;`

* Turn on **WAL** once on startup: `PRAGMA journal_mode=WAL;`
* Set a **busy timeout** (e.g., 5000ms) to avoid “database is locked”.

---

## EF Core configuration (essentials)

* No cascade deletes; use soft flags/guards.
* Unique index: `Assessment(PropertyId, Year)`.
* Helpful indexes: Ownership(PropertyId, StartDate, EndDate), PaymentAllocation(AssessmentId), PersonAddress(PersonId, IsPrimary, IsValidForMail), UtilityNotice(MatchedPersonId, IsAllocated).

**Example**

```csharp
protected override void OnModelCreating(ModelBuilder b)
{
  b.Entity<Assessment>()
    .HasIndex(x => new { x.PropertyId, x.Year })
    .IsUnique();

  b.Entity<PersonAddress>()
    .HasOne(p => p.Person)
    .WithMany(p => p.Addresses)
    .OnDelete(DeleteBehavior.Restrict);

  // … repeat for other relationships; prefer Restrict over Cascade
}
```

---

## Services layer (keep UI thin)

Create small, testable services that mirror workflows:

* `RolloverService`

    * Ensure FeeSchedule\[year] exists → create missing Assessments for active/in‑jurisdiction properties → Recalc totals & Status.
* `PaymentService`

    * Create Payment; allocate to open Assessments (current year default); donations flagged; call Recalc.
* `UtilityService`

    * Import CSV → fuzzy/alias match to Person → auto‑allocate exact cover → else mark NeedsReview.
* `OwnershipService`

    * End seller, start buyer; supports policy “owner as of Jan 1”.
* `AddressService`

    * Mark invalid; promote next valid primary.
* `ReportService`

    * Owners who owe (grouped); Unpaid properties; Paid owners; No valid primary; Mailing labels.
* `RecalcService`

    * Compute `Assessment.AmountPaid` (sum allocations) and `Status` (Unpaid/Partial/Paid/Overpaid).

**Status logic (pseudocode)**

```csharp
var paid = allocations.Sum(a => a.Amount);
if (paid <= 0) Status = Unpaid;
else if (paid < AmountDue) Status = Partial;
else if (paid == AmountDue) Status = Paid;
else Status = Overpaid;
```

---

## Blazor UI (pages & components)

* **People**: detail + addresses subview; buttons → Make Primary, Mark Returned (invalid), Add Alias.
* **Properties**: ownership timeline; open assessments.
* **Payments**: entry form (Cash/Check/Utility/Donation), allocation table (current year default).
* **Utility Intake**: import viewer → suggested matches → auto vs manual allocation → finalize.
* **Year Rollover**: fee for year, create assessments, run recalc.
* **Reports**: on-screen grids + Export PDF; Avery labels; receipts (QuestPDF).

**UX notes**

* Use `EditForm` + validation; block double‑submit with a busy flag; show toast results.
* Idempotent actions (rerun safe): e.g., rollover creates only **missing** assessments.
* All destructive ops must be guarded by checks; prefer disabling to deleting.

---

## Testing & environments

* Two DBs: `oglesby.db` (live) and `oglesby_sandbox.db` (test). A dropdown in the appsettings picker chooses one.
* Seed minimal fixtures for dev: 3 persons, 4 properties, fee schedule for current year, 6 utility notices.
* Unit tests on services; page tests can mock services.

---

## Backups & maintenance

* **Backup** button uses `VACUUM INTO 'C:\\OglesbyFD\\Backups\\oglesby_yyyyMMdd_HHmm.db';`
* Log to file (Serilog) under `C:\\OglesbyFD\\Logs`.

---

## Import formats

* **Utility CSV**: `PayerFirst,PayerLast,Amount,StatementMonth` (+ optional Notes). Keep raw rows in `UtilityNotice`.
* Person/Alias matching should be case‑insensitive; allow manual override.

---

## Policies and tie‑breakers (encode as constants or settings)

* Owner of record date (default **Jan 1**).
* Utility exact‑cover auto‑allocation; otherwise NeedsReview queue.
* If payer is also an owner with multiple properties, **prioritize matching their own property** first, then others.
* Donations are allowed everywhere; separated in reporting by flag.

---

## How Agents should answer in this project

* Provide **EF Core** entity configs, LINQ, and migrations.
* Provide **Blazor** components/pages (Razor) with working `EditForm`/validation.
* Provide **service methods** with clear signatures and transaction boundaries.
* Prefer **idempotent** SQL or guarded updates.
* Keep snippets **runnable** (no pseudo‑namespaces).
* If policy changes (fees, dates, proration), propose the **minimal** schema/query changes + a one‑time migration.

---

## Packaging & deployment

* Publish single file: `dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true`.
* On first run, ensure data folder, set WAL, create DB if missing, seed FeeSchedule prompt.
* Optional: Blazor Hybrid (WinUI 3) if you prefer a real window instead of a browser.