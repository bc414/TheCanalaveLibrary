# Slice 5 — Social (Features 18-19, 23-30, 38-40, 49)

Audited 2026-07-17 by the S5 slice agent. Read-only; no builds/tests — all `verify: [pending]`.
Scope: Comments (F23-26), Recommendations (F27-30), Following/Vouches (F18-19), Messaging (F49),
Groups (F38-40). Atoms (EditorView/RichTextView/UserCard/ConfirmDialog/InlineAlert/PaginationControls/
StoryDeck/ReportDialog) are prior-slice (S0/S2/S4); cited where consumed, not re-audited.

**HEADLINE — both Tier-1 security classes are CLEAN in the highest-risk slice.**
- **Sanitization (MA-201): 6/6 rich-text write paths sanitize.** Every EditorView-fed persist calls
  `IHtmlSanitizationService.Sanitize()` before save. No repeat of the Stories `LongDescription` miss.
- **Authorization (MA-301): every mutation is gated.** No ungated HTTP-reachable write exists. Comment/
  rec edit+delete are author-equality gated; vouch/follow are self-scoped; messaging is participant-gated;
  group admin ops use `RequireAdminAsync`/`RequireMemberAsync`; every write endpoint carries
  `.RequireAuthorization()`. A positive contrast with S2 (MA-202) and S3 (MA-301 5/7 miss).

Findings are second-order: an error-handling normalization (WU-ErrorHandling) that reached Comments but
not Recommendations/Messaging/Groups, one non-atomic counter, two unregistered silent catches, and
code-economy candidates.

## Sanitization coverage table (headline deliverable)

| # | Rich-text write path | Method | Injected sanitizer? | Sanitize call |
|---|---|---|---|---|
| 1 | Chapter comment | `ServerCommentWriteService.PostChapterCommentAsync` | yes (`sanitizer`) | `:46` `sanitizer.Sanitize(dto.CommentText)` |
| 2 | Blog-post comment | `.PostBlogPostCommentAsync` | yes | `:96` |
| 3 | Group comment | `.PostGroupCommentAsync` | yes | `:147` |
| 4 | Profile-wall comment | `.PostUserProfileCommentAsync` | yes | `:195` |
| 4b | Comment edit | `.EditCommentAsync` | yes | `:236` |
| 5 | Recommendation body | `ServerRecommendationWriteService.SubmitAsync` / `EditAsync` | yes | `:57` / `:107` |
| 6 | Vouch text | `ServerFollowingWriteService.VouchAsync` | yes | `:118` (null-tolerant) |
| 7 | Private message | `ServerMessagingWriteService.StartConversationAsync` / `SendMessageAsync` | yes | `:51` / `:112` |
| 8 | Group description | `ServerGroupWriteService.CreateGroupAsync` / `UpdateGroupAsync` | yes | `:34` / `:77` |

**Every path sanitizes.** Preview stripping in `ServerMessagingReadService.MakePreview` operates on
already-sanitized stored HTML (documented, no security implication).

## Authorization table

| Mutation | Server gate | Endpoint |
|---|---|---|
| Post{Chapter,BlogPost,Group,Profile}Comment | auth-guard (any authed user — correct) | `.RequireAuthorization()` |
| EditComment / DeleteComment | `comment.UserId != userId → UnauthorizedAccessException` | `.RequireAuthorization()` |
| ToggleLike (comment/rec) | auth-guard (correct) | `.RequireAuthorization()` |
| Recommendation Submit | auth-guard + `ContentCreate` throttle | `.RequireAuthorization()` |
| Recommendation Edit/Delete/SetHiddenGem | `rec.RecommenderId != userId → Unauthorized` | `.RequireAuthorization()` |
| SetHighlightedByAuthor | `Stories.Any(s.StoryId==rec.StoryId && s.AuthorId==userId)` | `.RequireAuthorization()` |
| Follow/Unfollow/SetAlerts/Vouch/RemoveVouch | self-scoped `actorId` (no cross-user write expressible) | `.RequireAuthorization()` |
| StartConversation | `EnforcePrivacyGateAsync` (AllowPrivateMessages 4-tier, fail-closed default) | `.RequireAuthorization()` |
| SendMessage / MarkRead / SetArchived | participant check (`ConversationParticipants` row) | `.RequireAuthorization()` |
| CreateGroup / Join / Leave | auth-guard / self-scoped | `.RequireAuthorization()` |
| UpdateGroup / RemoveStory / folder CRUD / Assign/Unassign | `RequireAdminAsync` | `.RequireAuthorization()` |
| AddStory | `RequireMemberAsync` + 3-tier rating waterfall | `.RequireAuthorization()` |

No ungated mutation. Group rating waterfall (tiers 2/3), AllowPrivateMessages gate, vouch-5/hidden-gem-5/
spotlight-5 limits are all enforced server-side (settled) — verified in the write services.

---

### MA-501 | Tier 2 | Bucket A | Slice 5
claim: `RecommendationSection` surfaces raw `ex.Message` to the UI, hand-rolls a danger `<p>` instead of
`InlineAlert`, and never logs unexpected exceptions — the exact pre-WU-ErrorHandling shape its sibling
`CommentSection` was normalized away from (2026-07-06). Two coordination composites in the same slice, one
normalized and one not. `error-handling.md` names raw `ex.Message` in UI a defect and `InlineAlert` the
ONLY validation channel; the unexpected-exception class here reaches users as framework text and is never
logged at Error.
evidence: `TheCanalaveLibrary.SharedUI/Recommendations/RecommendationSection.razor:97` — `<p class="mt-2 text-sm text-(--color-danger)">@_error</p>`; `:210` (HandleLike general catch) `_error = ex.Message;`; `:166`/`:236`/`:263`/`:289`/`:309` all `_error = ex.Message`. Contrast the normalized sibling `CommentSection.razor:118` `<InlineAlert Message="@_error" />` and `:275-284` `Translate(...)` which `Logger.LogError`s non-user-facing exceptions before `ExceptionPresenter.GetUserMessage`. Convention: `error-handling.md` §"Error Handling Strategy"; `audit/Comments.md` "WU-ErrorHandling note (2026-07-06)".
cells: F27 L3-Logic, F28 L3-Logic (RecommendationSection, Stage 5) — **proposes reopen**
effort: M | route: Stage-4 reconcile (adopt CommentSection's `InlineAlert` + `Translate`/`ExceptionPresenter` pattern; wrap consumer sites in a `CanalaveErrorBoundary` island as Comments already are)
verify: [pending]

### MA-502 | Tier 2 | Bucket A | Slice 5
claim: `ServerRecommendationWriteService.RecordSuccessAsync` increments the denormalized
`Recommendation.SuccessfulRecCount` with a **tracked read-modify-write** (`rec.SuccessfulRecCount++`), not
the atomic `ExecuteUpdateAsync` the codebase mandates for counters. Unlike the single-author-serialized
`VersionCount++` S3 waved through, this path is multi-reader: different readers who arrived via the same
recommendation trigger it concurrently (Feature 30, fired from the chapter reading page after 90% of Ch.1),
so two concurrent calls both load count=N, both `++` to N+1, both save → lost update. This is the exact
class WU-CounterAtomicity fixed for `LikeCount` in this very service's `ToggleLikeAsync`.
evidence: `TheCanalaveLibrary.Server/Recommendations/ServerRecommendationWriteService.cs:279` — `rec.SuccessfulRecCount++;` then `:281` `await writeDb.SaveChangesAsync();` (the surrounding `UserStats` counters at `:293-297` correctly use `ExecuteUpdateAsync`). Contrast `:171-173` `ToggleLikeAsync` — `ExecuteUpdateAsync(s => s.SetProperty(r => r.LikeCount, r => r.LikeCount + delta))`. Convention: `layer2-services.md` §"Counter mutation rule"; `audit/Recommendations.md` WU-CounterAtomicity note; hypotheses H-13.
cells: F30 L2 (Stage 5) — **proposes reopen**
effort: S | route: Stage-4 reconcile (move the increment to `ExecuteUpdateAsync` after the `RecommendationSuccess` insert; the composite-PK idempotency guard already prevents same-user double-count)
verify: [pending]

### MA-503 | Tier 3 | Bucket A | Slice 5
claim: Two unregistered silent catches (H-06 / MA-001/206/303 class). `MessagesNavLink` swallows **all**
exceptions on the unread-count read with a bare `catch` (prose comment only, no Warning log, no
`// sanctioned-silent:` + registry entry) — a genuine transient/anon error and a real query bug both
vanish, badge silently reads 0. `RecommendationEditor`'s char-sampling `PeriodicTimer` loop has the same
bare per-tick swallow (benign — mirrors S0's `DraftAutosave` MA-001).
evidence: `TheCanalaveLibrary.SharedUI/Messaging/MessagesNavLink.razor:59-63` — `catch { /* Swallow — unauthenticated or transient error; badge stays at 0. */ UnreadCount = 0; }`; `TheCanalaveLibrary.SharedUI/Recommendations/RecommendationEditor.razor:105-108` — `catch { /* Editor not yet initialised or component disposing — swallow. */ }`. Convention: `logging.md` §"No silent catches" (registry lists only `ServerActiveUserContext.ResolvePrincipal`); same class as MA-001.
cells: Messaging layout chrome; F27 L3-Logic (RecommendationEditor)
effort: S | route: mechanical sweep (log Warning per the level-semantics table, or annotate + register)
verify: [pending]

### MA-504 | Tier 3 | Bucket A | Slice 5
claim: Hand-rolled validation/error feedback instead of `InlineAlert` (MA-205 / MA-405 class) at three
sites — the entire Messaging error surface (`MessageThread` reply error, `ComposeConversationModal` compose
error) and the Groups create/edit form (`GroupCreateEditPage` server-validation errors). `error-handling.md`
names `InlineAlert` the ONLY channel for validation feedback; `CommentSection` (WU-ErrorHandling) is the
in-slice reference that does it right. The normalization reached Comments but skipped Messaging and Groups.
evidence: `TheCanalaveLibrary.SharedUI/Messaging/MessageThread.razor:70` — `<p class="mb-2 text-sm text-(--color-danger)" role="alert">@ErrorMessage</p>`; `Messaging/ComposeConversationModal.razor:75` — `<p class="text-sm text-(--color-danger)" role="alert">@ErrorMessage</p>`; `Groups/GroupCreateEditPage.razor:42` — `<div class="mb-4 rounded-lg bg-(--color-danger)/10 p-4 text-sm text-(--color-danger)" role="alert">`. Contrast `Comments/CommentSection.razor:118` `<InlineAlert Message="@_error" />`. (Static access-denied/not-found copy like `GroupPage.razor:32` `<p>Group not found.</p>` is empty-state text, not the validation channel — not filed.) Convention: `error-handling.md`; calibration seam record ("InlineAlert — the ONLY channel for validation feedback").
cells: F49 L3.5-Structure, F38 L3.5-Structure (Stage 5)
effort: S | route: mechanical sweep (replace with `<InlineAlert Message="@ErrorMessage" />`; bundle with MA-205/405)
verify: [pending]

### MA-505 | Tier 3 | Bucket A | Slice 5
claim: `EndpointHelpers.ExecuteWriteAsync` maps every `InvalidOperationException` to **401**, but several
S5 write methods throw it for genuine business-rule rejections, not auth failures — self-follow,
self-vouch, "you don't follow this user" (`SetReceiveAlertsAsync`), Hidden-Gem-limit-reached, and
spotlight-limit-reached all surface as 401 instead of the accurate 400. The message still crosses via
`ProblemDetails.Detail`, so the client isn't broken, but the status is semantically wrong. Self-documented
in two endpoint files as a known, deferred mismatch.
evidence: `TheCanalaveLibrary.Server/Http/EndpointHelpers.cs:61-69` — `catch (InvalidOperationException ex) { ... Status401Unauthorized }`; throw sites: `ServerFollowingWriteService.cs:34` self-follow, `:105` self-vouch, `:94` not-following; `ServerRecommendationWriteService.cs:199` hidden-gem limit, `:250` spotlight limit. Acknowledged: `FollowingEndpoints.cs:34-45` and `RecommendationEndpoints.cs:31-41` docstrings ("HTTP status itself (401) is semantically wrong … Left as-is per this sweep's mechanical, add-only scope"). Convention: `layer5-wasm.md` §"The Error-Translation Contract".
cells: F18/F19 L5, F29 L5 (Stage 5)
effort: S | route: seam — direction undetermined (give these business rules a typed `*ValidationException` so the existing name-suffix → 400 arm catches them, or add a dedicated 400 case; touches the shared helper)
verify: [pending]

### MA-506 | Tier 3 | Bucket A | Slice 5
claim: Comment-author notifications are unwired behind stale/inconsistent TODO markers (H-07 — this is the
calibration's origin site). `TODO(WU22)` references a **completed** work-unit (WU22 shipped follow/vouch
notifications, cited as done throughout `audit/Following.md`), so the "notify story author of new comment"
gap is untracked, not pending. `PostBlogPostCommentAsync`/`PostUserProfileCommentAsync` carry `TODO(WU33)`;
`PostGroupCommentAsync` carries **no** TODO and no notification at all — three of four contexts flag the gap,
one silently omits it.
evidence: `TheCanalaveLibrary.Server/Comments/ServerCommentWriteService.cs:65` — `// TODO(WU22): notify story author of new comment, and parent-comment author of reply.`; `:116` / `:214` `// TODO(WU33): ...`; `PostGroupCommentAsync` (`:121-167`) has no notification and no TODO. WU22-complete evidence: `audit/Following.md` F18 L2 ("Notification seams deferred: `// TODO(WU22)`" now shipped — `NotifyNewFollowerAsync`/`NotifyNewVouchAsync` live). Convention: hypotheses H-07 origin (`ServerCommentWriteService.cs:65`).
cells: F23 L2 (Stage 5)
effort: S | route: doc-touch decision (settle whether comment notifications are in scope; if deferred, retarget the TODO to a live tracker and make the four contexts consistent; if in scope, wire `NotifyNew*CommentAsync`)
verify: [pending]

### MA-507 | Tier 3 | Bucket A | Slice 5
claim: `SetHiddenGemAsync` reads the story author with the `(int?)s.AuthorId` projection that
`layer2-services.md` explicitly says to avoid, while `SubmitAsync` **in the same file** uses the correct
anonymous-reference-type projection — an intra-file inconsistency and the exact MA-409 (S4) class. Harmless
here (null author → notification skipped, which is the desired behavior), but a stated convention deviation.
evidence: `TheCanalaveLibrary.Server/Recommendations/ServerRecommendationWriteService.cs:212-215` — `int? storyAuthorId = await writeDb.Stories.Where(s => s.StoryId == rec.StoryId).Select(s => (int?)s.AuthorId).FirstOrDefaultAsync();`. Contrast the correct form `:49-52` `.Select(s => new { s.AuthorId }).FirstOrDefaultAsync()`. Convention: `layer2-services.md` §"Scalar projections on nullable FK columns — use anonymous-type, not `(int?)`"; matches MA-409.
cells: F29 L2 (Stage 5)
effort: S | route: mechanical sweep (project to `new { s.AuthorId }`; bundle with MA-409)
verify: [pending]

### MA-508 | Tier 3 | Bucket A | Slice 5
claim: `CreateGroupAsync` has no `IWriteRateLimitService` throttle, though group creation is exactly the
"creates content another user sees, or is unbounded" shape `security.md` says must add a rate-limit call.
Any authed user can spam-create groups that surface in `/groups`. `GroupEndpoints` documents the absence
("No RequireRateLimiting — ServerGroupWriteService doesn't call an IWriteRateLimitService token bucket") but
does not justify the exemption against the rule.
evidence: `TheCanalaveLibrary.Server/Groups/ServerGroupWriteService.cs:24-61` — `CreateGroupAsync` has an auth guard but no `rateLimit.EnsureAllowed(...)` call (contrast `ServerRecommendationWriteService.SubmitAsync:43` `rateLimit.EnsureAllowed(WriteActionKind.ContentCreate, userId)` and every comment post). `GroupEndpoints.cs:29-31` acknowledges no throttle. Convention: `security.md` §"Write Throttling" ("Every *new* abuse-prone write method … adds a call under an existing kind, or adds a kind").
cells: F38 L2 (Stage 5)
effort: S | route: doc-touch decision (add `ContentCreate` throttle to `CreateGroupAsync`, or record group-create as a deliberate exemption in `security.md`)
verify: [pending]

### MA-509 | Tier 3 | Bucket C | Slice 5
claim: The audience-badge static helpers `AudienceBadgeLabel` + `AudienceBadgeClasses` are
verbatim-duplicated across **three** components — `GroupCard`, `GroupDesktop`, `GroupMobile`. Pure functions
(viewer-independent switches over `GroupAudienceType`), byte-identical. The codebase's own "extract at the
third consumer" threshold (ConfirmDialog precedent) is met.
evidence: `TheCanalaveLibrary.SharedUI/Groups/GroupCard.razor:40-52`, `Groups/GroupDesktop.razor:251-263`, `Groups/GroupMobile.razor:193-205` — the two `static string ... => type switch { SfwOnly => "SFW Only", Mature => "Mature", _ => "Standard" }` / `{ Mature => "bg-(--color-danger)/10 ...", ... }` methods are identical in all three. Convention: `layer3.5-structure.md` ConfirmDialog "extract at third consumer" note.
cells: F40 L3.5 / F38 L3.5 (no cell change)
effort: S | route: seam — direction undetermined (move to a shared static, e.g. `GroupAudienceTypeMapper` or a `GroupAudienceVisuals` helper — pure win, ~14 LOC × 2 duplicate copies removed)
verify: [pending]

### MA-510 | Tier 3 | Bucket C | Slice 5
claim: `ServerCommentReadService`'s four read methods (`GetChapterCommentsAsync`, `GetGroupCommentsAsync`,
`GetUserProfileCommentsAsync`, `GetBlogPostCommentsAsync`) are ~55-line near-verbatim clones — the two-step
root-paging, the `rootIds.Contains` projection, the per-viewer like-EXISTS, and the entire in-memory
ordering block are identical; they differ only in the typed DbSet, the parent-id predicate column, and the
`IsSpoiler` projection (real value for chapters, literal `false` for the other three). ~165 LOC of the
264-LOC file is mechanical repetition. The settled "per-context method, not a generic enum" justification
(`layer2-services.md`) is about the **write-side verification** difference — the read side has no
verification step, so that rationale doesn't cover this duplication.
evidence: `TheCanalaveLibrary.Server/Comments/ServerCommentReadService.cs:25-89` vs `:91-147` vs `:149-205` vs `:207-263` — the `Dictionary<long,int> rootOrder = rootIds.Select((id, idx) => (id, idx)).ToDictionary(...)` + `OrderBy/ThenBy/ThenBy` tail (`:76-88`, `:134-146`, `:192-204`, `:250-262`) is byte-identical ×4. Convention: `layer2-services.md` §"Group Comments — Per-Context Method Pattern" (justifies the *write* split), §"Query Path"; H-13/N+1 lens.
cells: F24 L2 (no cell change)
effort: M | route: seam — direction undetermined (a private `PageCommentsAsync<TComment>(IQueryable<TComment> roots, Func<...> project)` generic over `BaseComment` children collapses the shared tail; the public per-context methods stay. Brian decides whether the indirection is worth ~120 LOC)
verify: [pending]

### MA-511 | Tier 3 | Bucket C | Slice 5
claim: `RecommendationCard.CardShadowClass` returns the identical string `"bg-(--color-surface)"` from both
switch arms — a vestigial conditional whose name ("Shadow") no longer matches its body (a background
color), left over from a superseded highlighted-card treatment.
evidence: `TheCanalaveLibrary.SharedUI/Recommendations/RecommendationCard.razor:162-164` — `private string CardShadowClass => Rec.IsHighlightedByAuthor ? "bg-(--color-surface)" : "bg-(--color-surface)";`. Applied at `:11`. Dead/vestigial member (lens 5).
cells: F28 L4 (no cell change)
effort: S | route: mechanical sweep (inline the constant, or restore the intended distinct shadow treatment)
verify: [pending]

---

## Hypothesis results (slice 5)

- **H-01** (`@key` on stateful list children): **clean** — `CommentSection.razor:34,52` keys root and
  reply `<CommentItem>` loops on `CommentId` (the WU-ComponentSoundness F3 spoiler-bleed fix, verified
  present); `RecommendationSection.razor:35` keys `<RecommendationCard>` on `RecommendationId`;
  `ConversationListItem`/`MessageItem`/`GroupCard` loops render leaves with no cached-param private state,
  but the message/conversation loops are keyed via their DTO identity through the list. No unkeyed
  `@foreach` over a param-caching child.
- **H-02** (route-param dispatcher reload discipline): **clean** — `MessagesPage.razor:139-162` guards
  `_loadedConversationId` sentinel + `_initialized`, resets transient thread state before reload, and the
  compose-modal-reset-before-navigate fix (`:287`) is present. `GroupPage.razor:146-152` guards
  `_loadedGroupId` + `_initialized`, plain-assigns on reload (`??=` only on the persisted first batch).
- **H-03** (unnamed `HasIndex` overwrite): **n/a** — EF configs are S1's; no `HasIndex` in slice product code.
- **H-04** (read-context factory-per-method): **clean** — all four read services
  (`ServerCommentReadService`, `ServerGroupReadService`, `ServerMessagingReadService`,
  `ServerFollowingReadService`, `ServerRecommendationReadService`) open `await using ...
  ReadDbFactory.CreateDbContextAsync()` per method; write services hold only `writeDb`; bases expose
  `protected ActiveUser`/`ReadDbFactory` (CS9107 idiom); `ServerGroupReadService.BuildFolderTreeAsync`
  helper opens its own context (documented). `MessagesNavLink` reaches `IMessagingReadService` from layout
  chrome — the factory-per-method fix (2026-07-01 circuit-concurrency) covers it.
- **H-05** (dead Tailwind classes): **clean** — paren-form tokens throughout; bare-name semantic tokens
  (`text-danger`, `text-text`) are the sanctioned dual style (S4). One intra-file inconsistency noted, not
  filed: `CommentItem.razor:133` `hover:text-(--color-danger)` vs `:144` `hover:text-danger` (paren vs bare
  for the same color). `text-white` on Control grounds only (`MessagesDesktop:14`, CI-green class).
- **H-06** (unregistered silent catches): **MA-503** — `MessagesNavLink:59` bare `catch` swallowing the
  unread-count read; `RecommendationEditor:105` bare per-tick sampler swallow. All best-effort notification
  catches in the write services (`ServerFollowingWriteService:60,133`, `ServerRecommendationWriteService:219,312`,
  `ServerGroupWriteService:184`) correctly `LogWarning` — not silent.
- **H-07** (stale/untracked TODO comments): **MA-506** — `ServerCommentWriteService` carries
  `TODO(WU22)`/`TODO(WU33)` for comment-author notifications; WU22 is complete, so the reference is stale
  and the gap untracked; group-comment context inconsistently has no TODO. This is the calibration's H-07
  origin site, confirmed still open.
- **H-08** (`Nav.NotFound()` vs manual): **mostly clean — first clean uses in the audit.**
  `MessagesPage.razor:187` calls `Nav.NotFound()` on `KeyNotFoundException` (non-participant/deleted
  conversation) — correct. `GroupPage.razor:29-33` renders inline `<p>Group not found.</p>` for `Group is
  null`, but that conflates "deleted" with "hidden Mature group the audience filter dropped" — the same
  deliberate missing-vs-hidden ambiguity S4 accepted for `CustomListPage` (a 404 must not reveal a hidden
  group's existence). Noted as defensible; the only nuance is groups are public SEO content, so a
  genuinely-deleted group returns 200 (minor F64 concern). Not filed, consistent with S4's precedent.
- **H-09** (dispatcher load parallelism): **clean** — `GroupPage.LoadGroupAsync:168-176` `Task.WhenAll`s
  role/stories/blog loads (in the documented parallelized set). `MessagesPage.OnInitializedAsync` awaits are
  a dependency-ish chain (conversations, then conditional compose-preset, then thread) — no StoryPage-style
  independent-await block.
- **H-10** (debounced writes lost on dispose): **clean/n-a** — no per-component debounce timer in the slice.
  `CommentSection`/`RecommendationSection` optimistic like paths write synchronously (await + reconcile/
  rollback), no deferred flush. `FollowButton`/`VouchButton` are immediate awaited writes. MA-401's class
  does not recur here.
- **H-11** (doc-vs-code staleness): **clean for new instances** — no convention-doc claim is contradicted
  by slice code beyond the already-filed items; MA-506's stale TODO is a code marker, not a doc claim.
- **H-12** (fire-and-forget without observation): **clean** — no `_ = SomeAsync(...)` launches in slice
  product code; the `RecommendationEditor` sampler loop is awaited within `SampleLengthLoopAsync` (its
  swallow is MA-503, not a fire-and-forget); `MessagesNavLink.OnLocationChangedAsync` is the sanctioned
  `async void` Blazor event-handler pattern.
- **H-13** (denormalized counter discipline): **MA-502** — `RecordSuccessAsync:279` `rec.SuccessfulRecCount++`
  tracked in a multi-reader path. All other counters (comment/rec `LikeCount`, every `UserStats` field,
  `FollowerCount`/`AuthorsFollowed`/`GroupsJoined`) use atomic `ExecuteUpdateAsync` with the transition-delta.
- **H-14** (elevated reads annotated): **n/a** — no `IgnoreQueryFilters` in slice product code. Write-service
  existence checks read the unfiltered `writeDb` directly (correct by construction); the group-rating
  waterfall tier-1 relies on the `ContentRating`/`GroupAudience` named filters being active on the read path.
- **H-15** (write-path by-id lookups bypass ContentRating): **clean by construction** — every write-service
  existence check (`AddStoryAsync` story/group, `SubmitAsync` story, comment parent checks, messaging
  recipient `FindAsync`) reads the unfiltered `writeDb`; no `readDb` PK fetch in a write path, so the
  phantom-`KeyNotFound` class can't occur. (`ServerGroupWriteService` comment `:90` notes the audience filter
  is active on `writeDb` for `JoinAsync` — deliberate: can't join what you can't see.)
- **H-16** (`[FromQuery]` on non-GET arrays): **clean** — no array/collection params on slice write
  endpoints. Bare-string body params (`vouchText`, `messageHtml`, folder `newName`) correctly carry
  `[FromBody]` with trap comments (`FollowingEndpoints:98`, `MessagingEndpoints:68`, `GroupEndpoints:132`);
  paging params are scalar query ints.
- **H-17** (nullable client reads use tolerant helpers): **clean** — every `Task<T?>` client read uses
  `GetNullableFromJsonAsync` (`ClientGroupReadService` GetById/role, `ClientRecommendationReadService`
  GetById/helpful-prompt, `ClientMessagingReadService.FindUserByUsernameAsync`); non-null shapes
  (`PagedResult`, `ConversationThreadDto`, relationship zero-state, `CommentPageDto`) use plain
  `GetFromJsonAsync`/`?? []`.
- **H-18** (aria-labels on icon-only + EditorView-adjacent buttons): **clean** — `CommentEditor`/
  `RecommendationEditor`/`MessageComposer` Save/Cancel carry `aria-label` (BlazoredTextEditor collision
  rule); `MessagesNavLink` badge, `RecommendationCard` like/gem/spotlight, `FollowButton` bell, `CommentItem`
  like/reply/edit/delete/report all labeled. No unlabeled icon-only control found.
- **H-19** (AuthorizeView-gated DI wrapper/inner split): **clean** — `MessagesNavLink` injects
  `IMessagingReadService` but the injection is at file scope on a **layout-chrome** component whose
  `<AuthorizeView>` gates only the markup, and its `RefreshCountAsync` catch tolerates the anonymous 401
  (the badge stays 0) — no anonymous-construction crash, unlike the WU43 NotificationBell class. Self-write
  composites (`FollowButton`/`VouchButton`) inject `IFollowingWriteService` but are rendered only inside
  already-authorized profile surfaces and fire only on user gesture. No leaf matches the WU43 wrapper/inner
  failure shape (contrast S4's MA-403).
- **H-20** (feedback-channel discipline): **MA-501 + MA-504** — `RecommendationSection` (raw `ex.Message` +
  hand-rolled `<p>`), `MessageThread`, `ComposeConversationModal`, `GroupCreateEditPage` hand-roll danger
  markup instead of `InlineAlert`; `CommentSection` is the in-slice reference doing it correctly.
  `GroupPage`/`GroupBlogPostEditorPage` static access/not-found copy is empty-state text, not the validation
  channel (not filed).

**MA-201 class (stored-XSS): CLEAN** — all 6 rich-text write paths sanitize (table above). Verified by
reading each write service's ctor deps + persist line, not assumed.

**MA-301 class (broken access control): CLEAN** — every mutation gated at the service layer, every write
endpoint `.RequireAuthorization()` (table above). No ungated HTTP-reachable write.
