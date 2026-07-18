# Part 1 — Calibration

Status: COMPLETE (2026-07-17). Contents: unwritten-pattern baseline, sampling observations, atom
seam records, proposed Part 2 slices. Companion outputs: `hypotheses.md`, `dimensions.md`,
`slices/0-atoms-findings.md`.

## Unwritten-pattern baseline (what the code consistently does that no doc fully states)

Observed across the calibration sample (~15 clusters touched; exemplars cited):

1. **Write-method skeleton is uniform**: auth guard (`UserId is not int userId` → InvalidOperationException) → `rateLimit.EnsureAllowed` → `dto.CanSave()` → existence checks via writeDb → `sanitizer.Sanitize` → construct entity with `DateTime.UtcNow` → `SaveChangesAsync` → `ExecuteUpdateAsync` counters → (best-effort notify). Exemplar: `Server/Comments/ServerCommentWriteService.cs:20-67`. Deviation-detection value: any write method deviating from this order is an anomaly worth a look.
2. **Rationale-comment culture**: nontrivial decisions carry WU-referenced comments citing convention docs. A file WITHOUT them is itself a (weak) staleness signal — likely pre-dates the convention era.
3. **Client service impls are mechanically uniform** (WU-L5Sweep): protected `Http` property, per-class `ThrowIfWriteFailedAsync` switch, `ClientHttpHelpers` for detail extraction. Exemplar: `Client/Comments/ClientCommentWriteService.cs`.
4. **Identity twins must not drift**: `ServerActiveUserContext` and `WasmActiveUserContext` implement identical claim-reading logic with matching anonymous defaults (code comment declares the invariant). Any IActiveUserContext property added to one side must land on both — nothing enforces this mechanically (B-flag candidate: analyzer/test gap).
5. **`DateTime.UtcNow` everywhere** (no `TimeProvider` injection) — testability trade accepted codebase-wide; consistent, so Bucket C at most.
6. **Loading text is inline `<p><em>Loading…</em>`/muted text** — no skeleton components (StoryDeck's upgrade documented as deferred-additive).

## Sampling observations (files read, notable results)

| Area | Files (exemplars) | Verdict |
|---|---|---|
| Data foundation | ApplicationDbContext, ReadOnlyApplicationDbContext | Clean; matches conventions precisely. Found content-safety.md ↔ code contradiction on BaseBlogPost IsTakenDown filter (H-11) |
| Comments write stack | ServerCommentWriteService, ClientCommentWriteService | Clean; textbook. Stale-looking `TODO(WU22)` notify comment (H-07) |
| Stories read svc | ServerStoryReadService (partial) | Clean two-step projection |
| Dispatcher | StoryPage.razor | Pattern-conformant; two leads: manual `/not-found` nav (H-08), sequential awaits (H-09) |
| Coordination composite | UserStoryInteractionPanel | Conformant incl. documented cache-guard; pending-flush-on-dispose question (H-10) |
| Signal buffering | ViewCountBuffer, ViewCountFlusher | Clean; telemetry + restore-on-failure per spec |
| Identity | ServerActiveUserContext, WasmActiveUserContext | Clean; the one registered sanctioned-silent site verified present |
| Notifications | ServerNotificationWriteService (partial) | Clean create-core discipline |
| Host composition | Server Program.cs, Client Program.cs | Clean; full L5 registration sweep verified present |
| Tests | CommentWriteServiceTests, CommentItemTests (partials) | High quality; convention-conformant seeding/selectors |

Clusters NOT calibration-read (slice agents do first full read): Import, Export, Spotlight,
Moderation, Groups, Discovery internals, Tags internals, Profiles, Badges, Messaging, BlogPosts,
Series, CustomLists, Following, Images, Sprites, SiteSettings, Seo, DevTools, marts detail.

## Atom seam records (DESCRIPTIVE — non-normative)

What each shared atom's contract *is*, recorded for consumer-slice comparison (lens 8). A
consumer/atom mismatch is a symmetric finding — neither side presumed correct
(reference-frame rule, plan v3).

| Atom | Contract (params → behavior) | Invariants consumers rely on |
|---|---|---|
| `RichTextView` | `HtmlContent` (EditorRequired, string?) + cascaded `ReaderDisplaySettings?` → renders trusted pre-sanitized HTML with reader typography inline styles | Renders nothing when null/empty; NO sanitization; must sit inside a `ContentSurface`; no font-* utilities apply inside |
| `ContentSurface` | `ChildContent` (required), `Variant` (Reading/Inline/Input, default Inline), cascaded Display → paper ground + side-rail frame; `ReadingBackground` override replaces ground/ink | Internal padding only (no outer margin); `Input` variant carries focus-within ring; `FrameStyle` param is gallery-only — production callers never set it |
| `EditorView` | `Html` (init seed only), `Placeholder`, cascaded Display → Quill wrapper | Pull-on-submit via `@ref.GetHtmlAsync()`; `SetHtmlAsync` to reset persistent composers (post-submit); ignores later `Html` param changes; never sanitizes; toolbar = sanitizer allow-list (13 tags), extend together; same-component route redirects on hosting pages need `forceLoad: true` |
| `IHtmlSanitizationService` | `Sanitize(string?)` → string | Called by every write service persisting EditorView output, before persist; null/empty → empty |
| `ConfirmDialog` | `@bind-IsOpen`, `Title?`, `Message?`/`ChildContent`, `ConfirmText`, `CancelText`, `IsDestructive`, `OnConfirm`, `OnCancel` | Backdrop click = Cancel; both paths auto-close + `IsOpenChanged(false)`; renders nothing closed |
| `InlineAlert` | `Messages?`/`Message?` (merged), `Variant` (default Danger) | Self-hides when empty — always safe to embed; the ONLY channel for validation feedback |
| `IToastService`/`ToastHost` | `Show(text, level, duration?)`; host mounted once per layout | No queue — Show with no subscriber (static SSR) silently drops; never for validation; auto+manual dismiss |
| `CanalaveErrorBoundary` | `Label` (low-cardinality island name), `Compact`, optional `ErrorContent` | Logs Error with `{Boundary}`/`{ErrorId}`; auto-Recover on navigation; catches subtree render/lifecycle/event faults only |
| `ExceptionPresenter` | `IsUserFacing(ex)`, `GetUserMessages(ex)` → list, `GetUserMessage(ex)` | Catch-site contract: typed→translate (no log); unexpected→log Error then show generic-with-trace-id; raw `ex.Message` in UI is a defect |
| `CanalaveTypeahead<TItem>` | `SearchMethod` (Func<string,Task<IEnumerable<TItem>>>), `OnSelected`, `ResultTemplate`, `MinimumLength`=2, `DebounceMilliseconds`=300 | Input clears after every pick — CALLER owns selection state; `data-typeahead-input` Enter suppression via typeahead.js; SearchMethod failures are NOT guarded (MA-005) |
| `PaginationControls` | `CurrentPage`/`PageSize`/`TotalCount` (all EditorRequired), `OnPageChanged` | Stateless; renders nothing when TotalPages ≤ 1 (consumers may pass degenerate values to self-hide); fixed 7-slot width |
| `UserCard` | `User` (UserCardDto, EditorRequired) + optional caret EventCallbacks gated by HasDelegate | View Profile always present; avatar = stored URL w/ default fallback; badges capped at 3 here; producers emit curated subset |
| `UserCardDto` | (UserId, Username, Tagline?, AvatarUrl?, Badges) | AvatarUrl copied verbatim by producing read service (never sprite-resolved); never cache across users |
| `DraftAutosave` | `DraftKey` (stable per target), `Capture` (pull, null=skip, throw tolerated), `OnRestore`, `IntervalSeconds`=10 | Host calls `ClearAsync` via `@ref` after successful submit; no-edit sessions never write; restore keeps backup until submit |
| `DraftStore` | `SaveAsync` (false = device refused), `LoadAsync` (null = absent/corrupt), `ClearAsync` | Injected by DraftAutosave only — editors never talk to it directly |
| `StatusBadges` | `Shell` + `ForStatus(StoryStatusEnum)` / `ForRating(Rating)` → literal class strings | Full-literal strings (JIT scan); single source for status/rating badge recipes |
| `AccountStatusBanner` | claims-only read of `canalave:account_status` | Renders only when Warned; staleness-by-design (next sign-in) |
| Layout chrome (`DeviceLayout`→`DesktopLayout`/`MobileLayout`) | boundary map: page island wraps `@Body`; chrome islands wrap bell/menus and banner; `ToastHost` mounted per layout | Chrome components init once per circuit (MA-003's staleness class); anything reachable from chrome must follow read-context factory rule |

## Proposed Part 2 slices (checkpoint input)

Product LOC = measured non-migration; test LOC = keyword-mapped approximation (first-match
grouping; exact assignment happens when each agent globs its scope). Atom clusters (~1.8k) were
audited in Part 1; their tests (~0.8k) fold into S1.

| # | Slice | Clusters | product | test | total |
|---|---|---|---|---|---|
| S1 | Foundation | Data, Program/root, Images, Sprites, Lookups, SiteSettings, Security, Diagnostics, DevTools, Components, Http, Telemetry, Seo, Home, legacy folders + atom TESTS | ~6.3k | ~5.4k | ~11.7k |
| S2 | Stories & structure | Stories, Series (Arcs, Lineage, ViewCount) | ~7.0k | ~4.8k | ~11.8k |
| S3 | Chapters & ingestion | Chapters, Import, Export, ReadingProgress | ~6.6k | ~3.1k | ~9.7k |
| S4 | Discovery & interaction | Discovery, Tags, UserStoryInteractions, Bookshelves, CustomLists | ~10.0k | ~6.6k | ~16.6k ⚠ |
| S5 | Social | Comments, Recommendations, Following, Messaging, Groups | ~8.3k | ~5.0k | ~13.3k |
| S6 | Identity & profiles | Identity, Profiles, Badges, Notifications | ~9.2k | ~2.4k | ~11.6k |
| S7 | Publishing & moderation | BlogPosts, Moderation, Spotlight, SiteDailyStat | ~7.5k | ~2.5k | ~10.0k |

**Amendment options for Brian at the checkpoint:**
- S4 is the heaviest (~16.6k). Option: split into S4a (Discovery + TreeSearch + CoOccurrence,
  ~8k+tests) and S4b (Tags + UserStoryInteractions + Bookshelves + CustomLists, ~8k+tests) → 8 slices.
- S5 alternative trim: move Groups (~2.4k product + ~1.5k test) to S7 (which is lightest),
  balancing S5→~9.4k / S7→~13.9k — roughly a wash; only worth it if S4 is NOT split.
- Recommended: split S4; keep everything else as-is (8 sequential agents).

Order: S1 first (foundation + atom tests), then S2→S7 dependency-ish. Frozen after sign-off.

## Global-Flip doc-staleness theme (feeds Bucket B agent + doc-touch list)

The 2026-07-13 Global Flip changed ground truth faster than the convention docs:
- `WasmActiveUserContext` exists; identity-and-authorization.md still says IActiveUserContext is server-only/never-in-SharedUI (MA-004).
- `WasmHostEnvironmentAdapter` exists; layer5-wasm.md still lists the DevLoginBar IHostEnvironment gap as open (MA-013).
- layer4-style.md pattern entries still carry v3 bracket syntax + `primary` alias names (MA-010).
Slice agents should flag (not rule on) any further doc claims contradicted by post-flip code.
