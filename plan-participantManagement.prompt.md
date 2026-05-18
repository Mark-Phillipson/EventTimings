# Plan: Participant Management, Route Tracking & Wave Mass Start

## Requirements Summary

1. **Bulk import participants** from JSON file → populate database
2. **Add participants on the day** → ad-hoc UI form to register late arrivals
3. **Route assignment** → participants self-select/confirm their route on arrival
4. **Wave / group mass start** → pre-defined groups of ~20 riders, one button press records start time for all simultaneously

**Key decisions:**
- Participants = riders (same entity in the timing system)
- Route = simple name/ID (not full metadata)
- Route assignment = self-service on the day (participants choose when they arrive)
- Import format = JSON
- Wave start time = always `UtcNow` at button press (no manual override)
- Route is per-rider, not per-wave
- Duplicate bib # on import = warn + skip
- Already-started riders in a wave = warn + skip

---

## Phase 1: Data Model Enhancements

**Goal:** Extend domain models to support route tracking and wave grouping.

**Steps:**
1. Update `RiderSummary` record in `src/EventTimings.Contracts/Class1.cs`:
   - Add `AssignedRoute: string?`
   - Add `WaveId: string?`
   - Add `WaveName: string?`
2. Add `RiderImportDto` record (Id, BibNumber, FullName, Category) for JSON import schema
3. Add `ImportResults` DTO (SuccessCount, SkippedCount, Errors list) for import feedback
4. Add `WaveDto` record (WaveId, WaveName, RiderIds list)
5. Add `WaveStartRequest` DTO (WaveId or explicit rider ID list)
6. Add `WaveStartResult` DTO (StartedAt, StartedCount, SkippedRiders list)

---

## Phase 2: Import Endpoint & Logic

**Goal:** Add API endpoint for bulk importing participants from JSON.

**Steps:**
1. Add POST `/api/event/participants/import` endpoint in `src/EventTimings.Api/Program.cs`
   - Accept JSON body with participant array
   - Validate: required fields present, check for bib # duplicates
   - Return `ImportResults`
2. Add `ImportParticipants(IEnumerable<RiderImportDto>)` method to `src/EventTimings.Api/TimingStore.cs`
   - Thread-safe merge into existing riders
   - Skip duplicates by bib # with warning in results
3. Add validation helper: check for required fields and duplicate bib numbers

---

## Phase 3: On-the-Day Add Participant

**Goal:** Blazor form to add single participants at the event.

**Steps:**
1. Create new Razor component `src/EventTimings.App/Components/AddParticipantForm.razor`
   - Form fields: BibNumber, FullName, Category, Route
   - Validation: all required, bib # unique (check against current list)
   - Submit calls API
2. Add POST `/api/event/participants/add` endpoint in `Program.cs`
   - Accept single `RiderImportDto`
   - Reuse `TimingStore.AddParticipant()` method (new)
   - Return updated `EventSnapshot`
3. Add `EventTimingApiClient.AddParticipantAsync()` in `src/EventTimings.App/Services/EventTimingApiClient.cs`
4. Integrate `AddParticipantForm` into `src/EventTimings.App/Pages/Home.razor` or a dedicated page

---

## Phase 4: Route Capture & Display

**Goal:** Track and display route information alongside the participant list.

**Steps:**
1. Add route selection/confirmation to timing UI
   - Decide: pre-defined dropdown or free-text entry (TBD — see Open Questions)
   - Add `TimingStore.UpdateRiderRoute(riderId, route)` method
2. Expose `AssignedRoute` in API response (update `EventSnapshot` serialization)
3. Display route column/badge in rider list in `Home.razor`

---

## Phase 5: Wave / Group Mass Start

**Goal:** Pre-define waves of ~20 riders; trigger a simultaneous mass start with one button press.

**Steps:**

### API Additions
1. POST `/api/event/waves` — create a wave definition
2. PUT `/api/event/waves/{waveId}/riders` — assign/add riders to a wave (pre-event + on the day)
3. GET `/api/event/waves` — list all waves with their rider rosters
4. POST `/api/event/waves/{waveId}/start` — mass-start
   - Captures `UtcNow` as `startTime`
   - Calls `TimingStore.StartWave(waveId, startTime)`
   - Returns `WaveStartResult` (started count, skipped rider list)

### TimingStore Additions
- `CreateWave(WaveDto)` — add wave definition
- `AssignRiderToWave(riderId, waveId)` — assign/reassign a rider to a wave
- `StartWave(waveId, DateTime startTime)` — atomically sets `StartedAt` for all eligible riders in one lock pass; collects skipped riders (already started)

### UI Additions
1. Wave management section (list waves, assign riders via checkbox or search)
2. Inline "add rider to wave" picker (for on-the-day arrivals)
3. "Start Wave NOW" button → calls start endpoint → toast showing "X started, Y skipped"

---

## Phase 6: Testing & Validation

**Steps:**
1. Unit tests for `TimingStore` import/add/wave logic (edge cases: duplicates, already-started riders, invalid data)
2. Integration test: POST to import endpoint with sample JSON → verify riders added
3. Integration test: POST to wave start endpoint → verify `StartedAt` set, skipped riders reported
4. E2E test (Playwright): import JSON → add participant on the day → assign to wave → start wave → verify UI
5. Manual test on mobile device (PWA)

---

## Relevant Files

| File | Changes |
|------|---------|
| `src/EventTimings.Contracts/Class1.cs` | Add `AssignedRoute`, `WaveId`, `WaveName` to `RiderSummary`; new DTOs |
| `src/EventTimings.Api/Program.cs` | Add `/participants/import`, `/participants/add`, `/waves/*` endpoints |
| `src/EventTimings.Api/TimingStore.cs` | Add `ImportParticipants()`, `AddParticipant()`, `UpdateRiderRoute()`, `CreateWave()`, `AssignRiderToWave()`, `StartWave()` |
| `src/EventTimings.App/Services/EventTimingApiClient.cs` | Add `ImportParticipantsAsync()`, `AddParticipantAsync()`, wave management/start methods |
| `src/EventTimings.App/Pages/Home.razor` | Display routes, integrate add-participant form, wave management UI |
| `src/EventTimings.App/Components/AddParticipantForm.razor` *(new)* | Form for adding single participants on the day |
| `PlaywrightTests/` *(new tests)* | Import, add-participant, wave-start E2E scenarios |

---

## Verification Checklist

- [ ] Unit & integration tests pass for import/add/wave logic
- [ ] POST `/api/event/participants/import` with sample JSON → correct `ImportResults` returned
- [ ] POST `/api/event/participants/add` → new rider appears in `/api/event/current`
- [ ] Route persists: `/api/event/current` includes `AssignedRoute` in rider payload
- [ ] Wave create/assign endpoints work; riders appear in GET `/api/event/waves`
- [ ] POST `/api/event/waves/{waveId}/start` → all eligible riders get the same `StartedAt`; already-started riders reported in result
- [ ] `Home.razor` shows route column, add-participant form is functional, wave UI is usable on mobile
- [ ] Existing service-worker / PWA patterns not broken

---

## Open Questions

1. **Route sources:** Pre-defined dropdown (staff maintains route list) or free-text entry? Recommend dropdown to reduce data-entry errors.
2. **Data persistence:** In-memory store loses data on API restart. Is re-importing from JSON acceptable for now, or is Azure SQL migration a parallel workstream?
3. **Wave naming convention:** "Wave" or "Group"? Recommend "Wave" to distinguish from route-based groups.
4. **Import conflict handling:** Confirmed as warn + skip by bib #. Should import also deduplicate by rider ID, or bib # only?
5. **Wave membership across days:** Do waves persist between event days or reset per-event?
