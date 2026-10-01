# Audit — Spotlight/

**Feature:** 55 (Community Spotlight). Spec §5.26 (donation model — superseded in part, see below).

## Shared Context

**Intent settled 2026-07-11 (Brian, in chat over several rounds).** This was the least-defined
cluster in the project ("TBD throughout"); the long-standing donation-infrastructure gap is now
split: the **spotlight feature itself** (slot grants, booking, homepage display) is fully designed
and buildable, while the **donation/payment layer** is explicitly deferred (Phase-4 verdict
rendered — deferred past beta).

**Source-of-truth ruling:** the Gemini discussions (`GeminiDiscussions/MyActivity September to
November 2025_filtered.md`, §VIII) supply the *requirements spirit* only — their implementation
(weekly pledge drive, `OperatingCosts`/`Pledges`/`SpotlightCredits` credit economy, slot pricing)
is suspect and superseded. Spec §5.26's one-paragraph "direct donation model" stands, now given
concrete mechanics. Implementation is first-principles.

## Settled requirements (do not revisit without Brian)

- **Donation-funded in spirit; donations deferred in build.** A donation will earn the donor the
  right to spotlight *someone else's* story. Slot supply scales with site activity/cost, capped
  monthly by actual operating costs so slots stay meaningful and proportional. None of the
  money-handling exists yet — only the seam.
- **The allocator seam:** `ISpotlightSlotAllocator` grants a slot-entitlement to a user. The
  mod-grant implementation exists now; the donation pipeline becomes a second grant source later
  through the same contract. One redemption UI, two grant sources.
- **No algorithmic homepage.** Selection is always a human act (mod grant → user pick). This is a
  site-mission constraint, not an implementation shortcut.
- **Display = additive composition of existing things.** A spotlight placement shows a **Story**
  (required) beside an optional **Recommendation** of that story; the recommendation brings its
  recommender attribution for free (`RecommendationCard`). Blank rec half when none attached. Not
  a polymorphic target — a composition.
- **Booking model — discrete calendar blocks.** The homepage has **N concurrent positions**
  (`site_settings`, mod-editable; future: activity/cost-scaled formula). Time divides into
  fixed-duration blocks on a fixed grid; redemption = picking an available future block
  (schedulable start is required — awardees can't be expected to be online when a window opens,
  and the homepage shouldn't have empty positions). Availability is computed (bookings
  overlapping a block < N), never stored — N can change without data rewrites.
- **Eligibility (server-enforced in the write service):** no self-spotlight (story author ≠
  sponsor); story publicly visible (published/approved, not taken down); per-story cooldown after
  a spotlight ends. The attached recommendation may be **anyone's** — self-recommendation is fine
  (story author ≠ sponsor is the rule; rec author is unconstrained).
- **Notifications:** `SpotlightSlotGranted` → awardee, inline at grant; `StorySpotlighted` →
  story author and `RecommendationSpotlighted` → attached rec's recommender, both **at go-live**
  via a worker sweep (never at booking).
- **Tuning knobs are DB-backed mod-editable settings** (`site_settings`, the first consumer of the
  new cross-cutting SiteSettings cluster): block duration days, per-story cooldown days, position
  count N, booking horizon days, monthly grant cap.
- **Slot-grant expiry: deferred.** Grants persist until redeemed or mod-revoked.

## Deferred (Phase-4 verdict rendered 2026-07-11: past beta)

- Donation/payment pipeline (the second `ISpotlightSlotAllocator` source; `SpotlightSlot.PaymentId`
  population; provider choice).
- Activity/cost-proportional formula for N (mod-set constant stands in; `SiteDailyStat` is the
  documented future input).
- Patron/Spotlighter badge (`SpotlightCount` counter) — rewards patronage, rides with donations.
- Slot redemption deadlines/expiry.

## Feature 55 — Community Spotlight

Built as WU-Spotlight (2026-07-12, `workplan.md`). Conventions:
`canalave-conventions/layer2-services.md` §"Community Spotlight — Slot Allocator Seam + Block
Booking" and §"Site Settings (`ISiteSettingsService`)".

- **L1 — Stage 5.** `WU_Spotlight_SlotsAndSiteSettings` migration: two-table split —
  `SpotlightSlot` (entitlement) + reshaped `CommunitySpotlight` (placement: unique `SlotId` FK
  Restrict, `RecommendationId` SetNull, `GoLiveNotifiedUtc`, composite `(start_date, end_date)`
  index; `SponsorComment` dropped — the attached recommendation *is* the endorsement; `PaymentId`
  moved to the slot); `SiteSetting` string-key table + 5 seeded knobs; `NotificationType` rows
  90–92; entity migrated out of legacy `Core/Models/`. Migration-verified: generated clean and
  applied cleanly on startup to the standing dev DB (SeedTool-volume). No L6 pass — the composite
  index shipped with L1 by design (low-volume table); L6 stays N/A.
- **L2 — Stage 5.** Allocator seam + read/write services + go-live worker/sweeper split +
  SiteSettings cluster. Redemption is advisory-lock-serialized under `CreateExecutionStrategy()`.
  Covered by the Integration tier (`SpotlightServiceTests`, 20 tests: grant cap/roles/donation-seam
  NotSupported, every redemption rejection, a two-racers-one-opening concurrency test, sweep
  fires-once idempotency, FK cascade/SetNull, settings round-trip + non-mod write rejection) and
  the Unit tier (`SpotlightBlocksTests`, 12 tests: grid floor/pre-epoch/on-grid, bookable-block
  tiling/horizon bounds).
- **L3-Logic / L3.5-Structure — Stage 5** (behavior changed beneath them 2026-09-30 by
  WU-InertFeatures and its review fixes — Stage note at the end of this feature).
  `CommunitySpotlightDisplay` (coordination composite,
  sanctioned injection — NotificationBell precedent), `SpotlightRedemptionPage` (`/spotlight`:
  two pick paths, any-of-the-story's-recs attach with own-rec preselect, occupancy calendar),
  `ModSpotlightPage` (`/mod/spotlight`: grant/revoke/knobs). Covered by the RazorComponents tier
  (12 tests across the three components: empty states, blank-rec half, both pick paths, composed
  redeem DTO, disabled full blocks, revoke-only-for-Available).
- **L4-Style — Stage 3.** Functional and token-clean (`check-design-tokens.ps1` green for these
  files), composed from existing roled components; not design-reviewed — rides the Phase-3 freeze
  sweep like the rest.
- **L4.5-Browser — Stage 5 (flipped 5→1 by the WU-InertFeatures review fixes, 2026-09-30, and back to 5
  by the WU-InertFeatures browser pass the same day — tracker H14 closed).** The recommendation half
  gained Read It Later / Read now, and a signed-in viewer's `StoryCard` now shows their real flags. The
  WU shipped those with no browser; its browser pass drove them on both phases (both notes at the end
  of this feature). *History:* Verified 2026-07-12 in a real browser against the standing dev DB
  (kept, not wiped): grant as AdminUser (capacity 12→11) → redeem as TestUser via the primary
  pick path into the current block → placement live on `/` (StoryCard + RecommendationCard) →
  psql ground truth: slot Redeemed, worker stamped `GoLiveNotifiedUtc` within its 1-min cadence
  unprompted, notifications 90 (awardee) + 91 (story author) present and 92 correctly drop-self
  suppressed (sponsor attached their own rec). One runtime bug found + fixed same-session:
  unbreakable rec text blew the homepage grid past the viewport (grid `min-width:auto`) —
  `min-w-0` + inherited `break-words` wrappers.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; homepage display, redemption page, AND the mod page all
  verified in a real WASM runtime during the flip's browser wave — incl. the new
  `ISpotlightSlotAllocator` L5 surface (`SpotlightSlotAllocatorEndpoints` +
  `ClientSpotlightSlotAllocator`), minted at the flip because `ModSpotlightPage` injects the
  allocator directly. Full wave narrative + the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.
- **L6 / L8 — N/A.** Index shipped with L1 (above); the go-live worker is an L2-style hosted
  service, not a mart.

### WU-StatBadgeProducers — ModSpotlightPage retrofitted onto UserPicker (2026-07-31)

No grid cells changed — F55 was already Stage 5 (L4 stays 3, unrelated to this WU) and remains so.
`ModSpotlightPage`'s "Exact username" free-text grant-recipient input is replaced with `UserPicker`.
This also **drops the page's `@inject IMessagingReadService`** — the page's own header comment used
to document that it "reuses `IMessagingReadService.FindUserByUsernameAsync` … rather than minting a
new user-search service"; that service now exists
(`IUserProfileReadService.SearchUsersByNameAsync`), so the cross-cluster dependency (a Spotlight
mod page depending on Messaging purely for a lookup) is gone. `ModSpotlightPageTests` (bUnit)
updated: the `FakeUserLookup : IMessagingReadService` fake is replaced with a minimal
`FakeUserProfileReadService : IUserProfileReadService`; the two tests that drove the old text
input + button click (`Grant_KnownUsername_CallsAllocator`,
`Grant_UnknownUsername_ShowsInlineError_AndDoesNotGrant`) could not be ported — picking a result
from `UserPicker`'s typeahead needs JS-level interaction bUnit doesn't drive reliably, the same
documented limitation as `StoryTitlePickerTests`. Replaced with what bUnit *can* assert: the picker
renders with the right placeholder, and the Grant button starts disabled with no recipient picked.
The full grant flow is covered by Integration (`SpotlightServiceTests`'s existing
`GrantSlotAsync` coverage, unaffected — only the recipient-resolution UI changed) and was
browser-verified end to end: search "TestUser" → pick → Grant slot → toast "Slot granted to
TestUser." → row appears in Recent grants, `psql`-confirmed. Full context: `audit/Badges.md`
§"WU-StatBadgeProducers".

### WU-InertFeatures and its review fixes — Stage note (2026-09-30): the placement gained Feature 30's entry points and real interaction state

**Cells:** L3-Logic/L3.5 stay 5; **L4.5 flips 5→1** (behavior a browser should see changed with no
browser available — tracker **H14**, step 1). The WU itself left this file and this cell untouched; the
review fixes caught the omission and made the flip.

**What changed.** `CommunitySpotlightDisplay` (owner ruling D3, `audit/Recommendations.md` F30):
- The recommendation half is a `ReadItLaterRecommendationCard` (Read It Later + "Read now"; an
  anonymous click is a login nudge) instead of a read-only `RecommendationCard`.
- The display takes the auth cascade and, for a signed-in viewer, batch-loads the spotlight stories'
  interaction states in one call and passes them (and `IsOwnStory`) to each `StoryCard`. The card's
  panel used to get **no** state, so its first flush wrote all-false over the viewer's real flags (a
  pre-existing bug this fixes).
- After a card save the display re-reads that story's state, so the panel cannot flush the save back.
  Since the review fixes it also takes the panel's `OnStateSaved`, so a Read It Later cleared in the
  panel re-enables the card (`audit/UserStoryInteractions.md` F16's review-fixes note).

**How verified:** RazorComponents `CommunitySpotlightDisplayTests` (+4 across the WU and its review
fixes: states batch-loaded and handed to the card; anonymous loads none; card save → panel adopts it;
panel clear → card re-enabled). **Not browser-driven** — H14.

### WU-InertFeatures browser verification (2026-09-30) — L4.5 1→5 (tracker H14 closed)

No bug found. Aspire path, `psql` after every write; phase method in `audit/Recommendations.md`
F30's browser note.
- **Fixture, driven through the UI.** AdminUser granted AuthorAlpha a slot on `/mod/spotlight`.
  AuthorAlpha redeemed it on `/spotlight` for AuthorBeta's story 5, attaching AuthorAlpha's own rec 3,
  in the current block. This wrote notifications 90 and 91 and their emails.
- **Circuit (ReaderGamma, no interaction row).** The card's Read It Later wrote the bit and the
  attribution. The `StoryCard` panel showed Read It Later active after the debounce. A panel clear
  deleted the row and the attribution, and the card offered Read It Later again.
- **WASM (TestUser).** The panel showed TestUser's real Read It Later from the batch
  `by-ids?storyIds=5` read, and the card read "Saved for later". A panel clear re-enabled the card.
  The card save wrote a fresh attribution.
- **Anonymous.** The card sends you to `/Account/Login?ReturnUrl=%2F`.
- The placement stays live through the booked block (dev-DB state: the workplan entry).
