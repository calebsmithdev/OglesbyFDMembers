# AGENTS.md (Context agent guidance)

> This file defines specialized “agent hats” you can put on ChatGPT (or multiple GPTs) to work on the app. Each agent has a mission, inputs, outputs, and a prompt template. All agents must respect the domain rules above.

## Global guardrails

* Never break the uniqueness of `Assessment(PropertyId, Year)`.
* No cascade deletes; use explicit flags/guards.
* Rerunnable (idempotent) operations wherever possible.
* Use transactions for Payment + Allocations + Recalc.
* Prefer small PR‑sized changes; include migration + rollback notes.

## Agents

### 1) Schema Guardian

**Purpose:** Keep EF entities + migrations aligned with policy.
**Inputs:** Proposed change or new policy.
**Outputs:** `*.cs` entity diffs, `Add-Migration` name, and migration code.
**Template:**

```
You are Schema Guardian. Given a change, produce:
1) Entity class edits
2) Fluent config diffs (OnModelCreating)
3) EF migration (Up/Down)
4) Data backfill script if needed
```

### 2) Rollover Captain

**Purpose:** Year rollover (create missing assessments, set fees, recalc).
**Inputs:** Target year.
**Outputs:** Service method, LINQ/SQL, page flow.
**Template:**

```
Design an idempotent RolloverService.CreateMissingAssessments(year) that:
- Ensures FeeSchedule[year]
- Creates Assessments only for active/in‑jurisdiction properties
- Leaves existing rows untouched
- Calls RecalcService.Recompute(year)
Provide tests.
```

### 3) Allocation Wizard

**Purpose:** Allocate Payments to open Assessments (and donations).
**Inputs:** Payment DTO (person, date, amount, type, memo).
**Outputs:** Allocation algorithm, edge‑case handling.
**Template:**

```
Implement PaymentService.Allocate(paymentId, policy) with:
- Current‑year default targeting
- Exact cover fast‑path
- Partial/overpay behavior
- Transaction + concurrency notes
- Unit tests for split allocations
```

### 4) Utility Clerk

**Purpose:** Import utility CSV, match aliases, auto‑allocate exact cover.
**Inputs:** CSV sample; fee for year.
**Outputs:** Parser, matcher (score), auto vs review workflow.
**Template:**

```
Create UtilityService.Import(csv) -> List<Notice>
Create UtilityService.SuggestMatches(noticeId) -> (PersonId, score)
Auto‑allocate when Amount == OwnedCount × Fee; else flag NeedsReview
```

### 5) Mailroom Clerk

**Purpose:** Handle returns, promote next valid primary.
**Inputs:** PersonId, AddressId.
**Outputs:** Update logic, UI actions, test.
**Template:**

```
AddressService.MarkReturned(addressId):
- Set IsValidForMail=false
- If Primary -> promote next IsValidForMail=true
- If none -> person appears on NoMailingAddress report
```

### 6) Report Builder

**Purpose:** Build on‑screen grids + PDF/labels.
**Inputs:** Report type + filters.
**Outputs:** LINQ/SQL, QuestPDF/Razor page, export method.
**Template:**

```
Produce OwnersWhoOwe(year): Person, Properties[], AmountDue, AmountPaid, Balance
Export to PDF and Avery labels (5160/8160)
```

### 7) QA Tester

**Purpose:** Fixtures + tests for services.
**Inputs:** Target service.
**Outputs:** xUnit tests, seed data, edge cases.
**Template:**

```
Given PaymentService, author tests for: exact cover, underpay, overpay, donation, multiple properties, concurrency.
```

## Prompt snippets

* **Use DB:**

```
Assume SQLite + EF Core. Provide migration code and a reversible Down(). Use Restrict deletes. Create unique index on Assessment(PropertyId, Year).
```

* **Use Blazor:**

```
Provide a Razor component with EditForm, DataAnnotations validation, and async service calls. Include minimal styling and error handling.
```

* **Backup pattern:**

```
Show a method that runs `VACUUM INTO` to a timestamped path, with try/catch and a success toast.
```

## Definition of Done (per PR)

* Compiles; migrations apply cleanly on a fresh DB.
* Unit tests pass; at least one PDF/labels export verified.
* Rerunning the action causes no duplicates or data corruption.
* Logging and basic error messages in place.

## Build Notes

* All `dotnet build` tasks need to run with escalated priviledges.
* Do not create migrations in code by hand. You should prompt the user to run `dotnet ef migrations add` instead.
* You are encouraged to run `dotnet build` after making code changes to verify it was built correctly. If there are errors, attempt to fix them before completing the task.
