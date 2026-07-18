# Content Safety & Moderation

The content-rating/audience visibility filters (the "zero visible trace" rule) and the moderation
tooling's deliberately-inverted-from-industry-standard model. Split out of `cross-cutting.md`
(2026-07-07) as its own coherent theme.

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

**Moderator filter behavior (settled 2026-06-26).** A moderator's content-rating reach equals their
personal `ShowMatureContent` setting, **both** when browsing the site and when reviewing the report queue.
The report queue is a shared global pool scoped by the `ContentRating` filter — a T-only mod sees only E/T
Story reports, not M ones. Review/entity-load paths bypass **only** `"IsTakenDown"` (so already-taken-down
content stays reviewable); `ContentRating` and `GroupAudience` stay live. Reports on entities filtered out
by ContentRating are *dropped* from the queue (not shown as placeholders).

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
