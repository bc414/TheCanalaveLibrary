# Content Safety & Moderation

The content-rating/audience visibility filters (the "zero visible trace" rule) and the moderation
tooling's deliberately-inverted-from-industry-standard model. Split out of `cross-cutting.md`
(2026-07-07) as its own coherent theme.

## Mature-Content Design Philosophy

The axioms below are *why* the filters and moderation model are shaped the way they are. They are
settled design intent — treat them as load-bearing when any decision touches mature-content
visibility, discovery, gating, or defaults. The concrete access model built on these axioms —
the three planes, the consent interstitial, reveals, and the Intentionality Doctrine — was
resolved 2026-07-19 (`middle_plan_v2.md` Resolved "Mature-content `noindex` (row 11)") and is
stated in §"The Three-Plane Access Model" below; the full derivation lives in
`.claude/design/access-gating-first-principles.md` (authoritative) and
`access-gating-audit.md` (surface inventory). Only the age-gate *wording* remains open (row 10,
counsel — interim: AO3-style willingness assertion).

**Rating model.** `Rating { E=0, T=1, M=2 }` (`Core/Lookups/ModelEnums.cs`). Three tiers only.
E (Everyone) and T (Teen) are functionally identical (both non-mature). **M (Mature) is the single
gated tier, and M *includes explicit content*** — there is no separate "Explicit" rating above M
because M already encompasses it. "Mature" here means **adult/explicit** — up to and including
explicit sexual content — not merely "mature themes." For all age-gating, SafeSearch/adult
labeling, and legal analysis, treat M as the explicit tier. Mature vs non-mature is therefore a clean binary, with M carrying the full
adult-content weight.

**No ads.** The site carries no advertising. Ad-safety/brand-pressure is *not* a driver of any
content or moderation decision here (this is why the moderation model can invert the industry
defaults — see "Mission-Driven Defaults" below). SEO/discoverability is therefore never monetized,
but still matters as the path by which readers find content.

**Filters are first-class, and serve three reader tiers — not a binary.** The whole product thesis
is that the mature/non-mature *binary* of existing platforms underserves people. The filter system
must serve all three tiers equally well:
1. **No-M readers** — want mature content gone entirely.
2. **M-native readers/writers** — here *for* it; want it first-class and discoverable.
3. **The quiet middle** — mixed, not full-embrace; quiet consumers who keep mature *off* as their
   comfortable default but occasionally read a specific M work. Existing platforms flatten this
   tier by forcing a self-declaration; this project deliberately designs for it.

A design that optimizes tier-1 comfort or tier-2 access *at the other's expense* is the flattening
this project exists to avoid. **Prefer granular, consent-based, per-work mechanisms over global
binary switches** — e.g. a middle-tier reader must be able to view one M work without being forced
to reclassify their whole account to tier-2. (This is the design constraint behind the still-open
direct-link interstitial in row 11: a temporary per-work reveal must never mutate the saved
`ShowMatureContent` setting.)

**The middle tier's mechanism is cross-tier author discovery.** Readers are often first drawn in by
one kind of content but retained by an author's *broader* body of work spanning rating tiers — the
bridge is an author being discoverable across ratings, not any single work in isolation. Design
implication: **preserve the discovery bridge while gating the M content itself, not the discovery.**
The gate sits on the M content a viewer reaches, not on their ability to find an author. The two
are not in tension. (M pages are indexed — never `noindex`d — with the interstitial as the
crawler-visible artifact; the count-line disclosure keeps the bridge on profile listings.)

## The Three-Plane Access Model (settled 2026-07-19)

Every surface belongs to exactly one plane; assign new surfaces deliberately:

| Plane | Definition | M-content rule |
|---|---|---|
| **Discovery** | The site offers content the viewer didn't ask for: browse, search, tag listings, recommendations, random batch, tree-search results, homepage sections, group listings, marts | **Zero-trace.** Mature-off (or anonymous, no cookie) viewers see no M content, no M counts, no M ids rendered. Enforced by the named query filters + manual ceilings below. Reveals never widen this plane. |
| **Direct navigation** | The viewer asks for a specific thing by URL: story page, chapter page, group page, blog-post page | **Consent gate.** Existence acknowledged, content withheld: a server-side interstitial shows title/author/rating only (no cover, no description — they can themselves be explicit) with actions "View this story"/"View this group" (grants a durable per-item **reveal**) and "Always show mature content". The body is absent from the HTML until consent — gate the *fetch*, never CSS-hide (`[PersistentState]` embeds fetched DTOs in prerendered HTML). Taken-down content stays a true 404 — takedown is enforcement (Class A), not consent. |
| **Personal** | The viewer's own interaction graph: bookshelves, reading history, notifications, own custom lists, hidden-gem slots, own-authored stories (read *and* edit) | **Never rating-filtered.** Protected by auth/ownership (Class A), not by rating. A user's own favorites/history/notifications show their M items regardless of their Discovery setting — their interactions were deliberate acts. This is what prevents ghost rows (invisible, un-deletable M favorites) and vanishing reading history. |

`ShowMatureContent` is the **Discovery-plane setting only**. Reveals (durable, per-item:
`user_content_reveals` rows for accounts; the anon prefs cookie for guests) are the
**Direct-navigation consent record** — they also unlock the item's own subtree on deliberate use
(chapters/TOC/versions/export of a revealed story; folders/comments/blog posts of a revealed
group; a revealed story may seed its own tree-search page, results still ceiling-filtered).
There is no separate "strict no-M" setting — the interstitial *is* the strict experience (two
deliberate acts before anything mature renders).

**Class A vs Class B — know which one you're enforcing.**
- **Class A (access control, real security):** auth, ownership, `ProfileVisibility`, drafts,
  DMs, mod surfaces, `IsTakenDown`. Enforced server-side at **every** reachable endpoint; the
  adversary picks the path.
- **Class B (consent UX):** the mature-rating ceiling and interstitial. M content is public
  content behind a consent checkpoint — there is no adversary, only accidental exposure to
  prevent. The load-bearing enforcement is the *page-serving* path.

**The Intentionality Doctrine (Class-B APIs).** *If someone calls a JSON API directly, they are
doing it on purpose — the hand-built request is itself the consent the interstitial exists to
capture.* JSON APIs serving Class-B child data (comments, arcs, TOC lists, group members/children,
view counts, id lists) are **deliberately ungated by rating** — do not "fix" them, and do not
re-report them as leaks (they were reclassified 2026-07-19; see
`access-gating-first-principles.md` §5). This matches the whole content class (AO3's gate is a
query param; Fimfiction's is a cookie). Two boundaries hold regardless:
1. **Class-A checks are never relaxed** — auth, privacy, takedown, ownership apply to every API.
2. **The four page-backing detail endpoints** (story, chapter, group, blog post) return the gated
   envelope (`Visible | GatedMature(metadata) | NotFound`) — they are the page's data source
   across render modes, and full JSON would put the body in the response the UI then hides.
   Consent rides the prefs cookie / DB reveal, so consented callers (including deliberate API
   users who set the cookie) get full data.

**The invariant this doctrine costs:** M-safety of child data is enforced at *page* level. Any
NEW UI surface that composes Class-B child data (comments, arcs, TOCs, group children) **outside
its gated parent page** must itself apply the viewer's effective ceiling. Check this at review
time whenever a widget/preview/dashboard renders child content.

**UI copy convention:** it is always "story" / "group" / "blog post" — never "work". Interstitial
buttons: "View this story" / "View this group".

**Write paths stay rating-blind.** Favoriting/following/listing/recommending an M story needs no
ceiling check (the actor consented via the gate, has M on, or called the API deliberately). The
one legitimate write-path rating check is spotlight redemption validating story rating against
the slot's rating class — that is inventory integrity, not consent. Spotlight runs **dedicated
M and non-M slot pools**; mature-off/anon viewers see the non-M pool at full width.

**Person/collection-scoped listings get the count-line disclosure.** Profile tabs (authored,
favorites, recommendations), public custom lists, group story/folder sections, and series pages
show "K mature stories aren't shown · show them" (line absent at K=0); expanding renders minimal
gated cards (title/author/rating, no cover/description) that link to the story page, where the
interstitial handles consent. Global Discovery surfaces never get the line.

## Group Audience-Visibility Filter

Groups carry an **`AudienceRating`** property (renamed from `Rating` in the WU32 migration — column
renamed, enum mapping unchanged). It enforces the same "zero visible trace of mature content" rule
as the story `ContentRating` filter, applied to group listings:

- **`AudienceRating = E` (General):** group is visible to all users (mature on or off).
- **`AudienceRating = M` (Mature):** group is hidden from users where `ShowMatureContent = false`.

This is enforced as a **named EF Core query filter `"GroupAudience"`** on `Group`, registered in
`ReadOnlyApplicationDbContext.OnModelCreating` (not the base `ApplicationDbContext` — see "Content Rating
Filtering" below for the principle: display filters live only on the read context):

```csharp
modelBuilder.Entity<Group>()
    .HasQueryFilter("GroupAudience", g => _activeUser.ShowMatureContent || g.AudienceRating != Rating.M);
```

`_activeUser` is the `protected readonly IActiveUserContext` field on `ApplicationDbContext`, accessible to
the subclass. Anonymous and mature-disabled users cannot see Mature groups; mature-enabled users can.

**Write paths are unfiltered by design** — the write context (`ApplicationDbContext`) carries no
`GroupAudience` filter, so load-by-id on write paths sees all groups without any `IgnoreQueryFilters` call.
The mod/creator *read* path that legitimately needs to surface a taken-down or audience-gated group goes
through the read context with an explicit elevated-read bypass (annotated `// elevated read:`).

**Named filter applies to `Group` only.** `GroupStory`, `GroupFolder`, `GroupComment`, and
`GroupBlogPost` are accessed only in the context of their parent group — when the parent group is
invisible (filter applied), none of its children are reachable. No child-table filter needed.

**Three audience presets are a UI/write convention, not stored.**  
`GroupAudienceType { Standard, SfwOnly, Mature }` is a C# enum in `Core/Lookups/ModelEnums.cs`
used only at the write/display boundary. The DB stores just `(AudienceRating, MaxContentRating)`.
A static mapper in `Core/Groups/GroupAudienceTypeMapper` converts both ways:

| `GroupAudienceType` | `AudienceRating` | `MaxContentRating` |
|---------------------|------------------|--------------------|
| Standard | E | M |
| SfwOnly | E | T |
| Mature | M | M |

Non-M stories can be added to a Mature group — the audience rating defines the group's topic and
audience, not a floor on story content. A T-rated story that fits can always be added to a Mature
group; safe because mature-disabled users cannot see the Mature group at all (filtered at listing).

Group role model (open join, permanent membership, Member/Admin roles) lives with the rest of Groups'
service-layer conventions in `layer2-services.md` §"Group Membership and Role Model".

## Content Rating Filtering

Every read service returning story data must filter by content rating. The ceiling is a **global EF Core named query filter** sourced from `IActiveUserContext` (`identity-and-authorization.md`), registered on `ReadOnlyApplicationDbContext` only — not the base `ApplicationDbContext`.

### The principle: display filters live only on the read context

> **The write context (`ApplicationDbContext`) sees ground truth and is never filtered. All display/
> visibility filters — `ContentRating`, `GroupAudience`, `IsTakenDown` — live only on
> `ReadOnlyApplicationDbContext`.** A bypass (`IgnoreQueryFilters`) on the read context is therefore always
> a deliberate, auditable "this read intentionally sees more than a normal viewer." There is nothing to
> forget on a write.

This makes the content-safety boundary **structurally safe** instead of discipline-safe. Write services
load entities by id to mutate them; they must see ground truth. If a visibility filter sits on the write
context, every write path must *remember* to bypass it — a discipline that silently breaks (WU31_5b,
post-WU38 audit). Moving all filters to the read context eliminates that entire class of bug.

`ReadOnlyApplicationDbContext` inherits from `ApplicationDbContext` and overrides `OnModelCreating` to add
the four named filters, closing over the `protected readonly IActiveUserContext _activeUser` field exposed
by the base class:

```csharp
// ReadOnlyApplicationDbContext.OnModelCreating (after base.OnModelCreating):
modelBuilder.Entity<Story>().HasQueryFilter("ContentRating",
    s => s.Rating <= (_activeUser.ShowMatureContent ? Rating.M : Rating.T));

modelBuilder.Entity<Group>().HasQueryFilter("GroupAudience",
    g => _activeUser.ShowMatureContent || g.AudienceRating != Rating.M);

modelBuilder.Entity<Story>().HasQueryFilter("IsTakenDown", s => !s.IsTakenDown);
modelBuilder.Entity<BaseComment>().HasQueryFilter("IsTakenDown", c => !c.IsTakenDown);
modelBuilder.Entity<BaseBlogPost>().HasQueryFilter("IsTakenDown", b => !b.IsTakenDown);
modelBuilder.Entity<Recommendation>().HasQueryFilter("IsTakenDown", r => !r.IsTakenDown);
```

EF Core caches models per context *type*, so the read context gets a filtered model; the base type
(`ApplicationDbContext`) remains unfiltered. Query filters don't touch schema — no migration impact.

Anonymous viewers get the `Rating.T` ceiling (never `IsAuthenticated` ⇒ never `ShowMatureContent`).
`User.ShowMatureContent` is a hot boolean on the User table — direct column, not in jsonb.

### Elevated reads (the only surviving bypasses)

Legitimate "see more than a normal viewer" read paths call `IgnoreQueryFilters` **by name** and are
annotated `// elevated read:` so they read as deliberate:

```csharp
// elevated read: author always sees their own stories regardless of personal rating setting
await readDb.Stories
    .IgnoreQueryFilters(["ContentRating"])
    .Where(s => s.AuthorId == userId)
    .ToListAsync();

// elevated read: mod queue must see taken-down content to act on it
await readDb.Stories
    .IgnoreQueryFilters(["IsTakenDown"])
    .Where(s => s.StoryId == id)
    .FirstOrDefaultAsync();
```

EF Core 10 named filters allow per-filter opt-out, leaving other named filters active on the same query.

### TPT blog-post exception

The TPT-root hazard is narrower than originally recorded (corrected 2026-07-18 against
`ReadOnlyApplicationDbContext.cs`, which is the authority): a `HasQueryFilter` that **closes over
`_activeUser`** (the content-rating filter) generates broken EF Core 10 SQL on derived-entity
materialization, so the blog-post **content-rating** filter is *not* applied model-level to
`BaseBlogPost` — each blog-post read service enforces the ceiling via an explicit
`.Where(p => p.Rating <= max)` in the projection. The simple boolean `"IsTakenDown"` filter **is**
safe on TPT roots and *is* applied model-level to `BaseBlogPost` (confirmed by the full test suite
since WU34). Blog-post delete uses
the change-tracker stub: `writeDb.Remove(new ProfileBlogPost { BlogPostId = id });
await writeDb.SaveChangesAsync();` — EF issues child-then-base DELETE in one transaction. See
`audit/BlogPosts.md` §Feature 35 Stage-5 note (WU31.5) for full rationale.

## Moderation Model

### Mission-Driven Defaults — Opposite of Industry Standard

The moderation tooling is deliberately inverted from commercial/attention-economy defaults. Understanding
*why* prevents re-introducing industry patterns that are load-bearing only for scale, ad-safety, or
engagement-maximization — none of which apply here.

| Industry driver | Industry pattern | This site's inversion |
|---|---|---|
| Ad-safety/brand pressure | Fast automated removal; err toward takedown | Soft-hide default (reversible); author told why |
| Engagement maximization | Lax on borderline-toxic but engaging content | Deliberately anti-engagement design |
| Adversarial spam scale | Auto-hide on threshold | Human-in-the-loop (affordable at this volume) |
| Hard-delete for storage/legal finality | Opaque permanent takedowns | Soft-hide default; hard-delete only for illegal content |
| Appeals cost money → no real appeal | Shadowban (user doesn't know) | **No shadowban — ever** |

### Content Removal

**Soft-delete (takedown) default.** Normal mod action sets `IsTakenDown = true`, `TakedownDate`, and
`TakedownReason` on the target entity. All three share the "takedown" stem; do not confuse with
`IsHiddenFavorite` (UserStoryInteraction, private-favorites), `IsHiddenGem` (Recommendation, a curator
label), or the `ContentRating`/`GroupAudience` filters (which hide by rating/audience, not by mod action).
The content is invisible on public reads (named query filter `"IsTakenDown"`) but visible to the author
(who reads with `IgnoreQueryFilters(["IsTakenDown"])`) and to moderators (same). The author receives a
`NotifyContentRemovedAsync` notification with the stated reason so they can often fix it (e.g. re-rate
mature content) rather than just being punished. See `layer2-services.md` §"Notification Generation" for
the generation mechanism (semantic per-event methods, best-effort post-commit ordering, DAG rule); the
full moderation semantic-method list is below in "Notification Loop (§13 Transparency)".

Applicable targets: `Story`, `BaseComment`, `BaseBlogPost`, `Recommendation`. `User` is handled by account
actions (not a takedown column). `PrivateMessage` is not soft-hidable — DM reports go straight to the queue
for moderator judgment only.

**Narrow hard-delete escape hatch.** A separate explicit "illegal content" action (CSAM, piracy) hard-deletes
via a distinct `ApplyHardDeleteAsync(type, id)` path in `ServerModerationWriteService`. This is not the
default and is presented as a distinct moderator choice, not the same action as soft takedown.

**Named query filter `"IsTakenDown"`.** Each removable entity registers the `"IsTakenDown"` filter via
`HasQueryFilter` in `ReadOnlyApplicationDbContext.OnModelCreating` (composable alongside `"ContentRating"`
and `"GroupAudience"` — all display/visibility filters live on the read context only; see "Content Rating
Filtering" above). The write context sees ground truth; `IgnoreQueryFilters` is only needed on elevated
*read* paths (mod queue, author viewing own taken-down content).

**Moderator review surfaces are work surfaces, exempt from the personal rating filter (settled
2026-07-18, supersedes 2026-06-26).** The report queue (`GetReportQueueAsync`) and pending-submission
queue (`GetPendingSubmissionsAsync`) bypass `"IsTakenDown"`, `"ContentRating"`, and `"GroupAudience"`
alike — a moderator sees every open report and every pending submission regardless of their personal
`ShowMatureContent` setting. `ShowMatureContent` still gates ordinary *browsing* reads; it does not gate
*moderation* reads. Three reasons converged: (1) the setting is a personal comfort filter, not an access
boundary — reviewing a report is not the same act as browsing for pleasure; (2) the write/action path was
already unfiltered ground truth (`ServerModerationWriteService` acts on `writeDb`, no rating filter, ever)
— a mod could already resolve or remove a report the queue chose to hide, so scoping the read side created
an incoherent middle state, not a real access boundary; (3) rating-scoping a shared work queue creates a
coverage hole — a report on content above every active moderator's personal cap would be invisible to all
of them. The original 2026-06-26 framing only ever scoped `Story` targets in practice (every other
reportable type — Recommendation, BlogPost, Comment, User, Message — was already shown regardless of
rating, for lack of a query filter reaching them); "show all" resolves that inconsistency by deletion
rather than by building derived-rating joins (child-table BlogPost rating, 2-hop Comment rating) to extend
scoping outward. Full reasoning: `middle_plan_v2.md` Resolved "Non-story report-target rating routing" and
`audit/Moderation.md` Feature 46/47.

### Auto-Hide Policy — None

`ActiveReportCount` drives mod-only triage ordering (most-reported items first in the queue) and an inline
queue badge. It is **never an automatic action trigger**. No threshold-based auto-hide or auto-flag logic.

Rationale: auto-hide-on-threshold in a small fandom community becomes a brigading weapon (the mass-report
heckler's veto). Human review is affordable and necessary at this site's volume. The deliberations'
"3 distinct reporters in 24h" rule is permanently dropped.

**Report counts are mod-only.** No public-facing "reported N times" display — that gamifies reporting and
enables coordinated harassment. `ActiveReportCount` on entities and `User` is queried only by moderator
surfaces.

### Account Actions

**State model:** `User.AccountStatus` enum (Active / Warned / Suspended / Banned — **no Shadowbanned**) +
`User.SuspendedUntilUtc` (nullable `DateTime?`).

**Action workflow:** each action (warn / suspend / ban) sets `AccountStatus`, records on `Report`
(`ActionTaken` + `DateResolved` + `ModeratorUserId`), and sends the appropriate notification
(`NotifyAccountWarningAsync` / `Suspended` / `Banned`). The user is always told why — transparency is
non-negotiable per §13.

**Shadowban is permanently rejected.** Deception-as-moderation directly contradicts the site's §13
philosophy ("close the loop on ALL reports — the user is entitled to know they were heard"). A community
that shadowbans lies to its members. This is a design axiom, not a deferred decision.

**Login enforcement is staged.** WU34 ships the state columns and notifications. Actual login-blocking
(block Suspended users until `SuspendedUntilUtc`; block Banned users permanently; surface Warned banner
in layout chrome) is a dedicated follow-up WU — it's a security surface that deserves its own careful
slice. **Named and sequenced (2026-07-15): WU-AccountEnforcement**, already listed as
`middle_plan_v2.md` Phase 2 item 5 (predates this formalization pass — recorded here and in
`workplan.md` "Planned / not-yet-built named WUs" so every phase-sequenced WU has one ledger entry).

### Report Targets and `ActiveReportCount`

**Reportable set (WU34):** Story, User, Comment (covers all TPT subtypes coarsely), BlogPost, Recommendation,
PrivateMessage. `Report.ReportedEntityId` is `long` (widened from `int` in WU34 migration). `ReportedEntityType`
enum value `Message = 5` added.

**`ActiveReportCount` exists on:** Story, BaseComment, BaseBlogPost, Recommendation, **and User** (added WU34
for symmetry). **`PrivateMessage` has no counter** — DMs are 1:1 sensitive; no surface would display a
per-message report count.

**Uniform `AdjustActiveReportCount(type, id, delta)`** private switch in `ServerModerationWriteService`
increments on report submit, decrements on resolve. The switch skips `Message` targets (no counter).

### Notification Loop (§13 Transparency)

Every report outcome notifies the reporter — including no-action outcomes. This is more work per report
than industry standard, and it is affordable precisely because volume is low and the community is the
point. **The "close the loop" philosophy is a project axiom; do not trade it away for efficiency.**

Semantic methods (generation mechanism: `layer2-services.md` §"Notification Generation"):
- `NotifyReportReceivedAsync` — immediate confirmation on submit.
- `NotifyReportResolvedAsync` — action taken (links to affected entity if applicable).
- `NotifyReportResolvedNoActionAsync` — no action taken, still closes the loop.
- `NotifyContentRemovedAsync` — target author notified with reason.
- `NotifyStoryApprovedAsync` / `NotifyStoryRejectedAsync` — submission outcomes.
- `NotifyAccountWarningAsync` / `AccountSuspendedAsync` / `AccountBannedAsync` — disciplinary actions.

All are best-effort post-commit (try/catch swallows; notification failure never rolls back the primary
moderation action).
