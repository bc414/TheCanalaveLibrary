# bUnit Test-Hygiene Manifest (review gate — NO cuts applied yet)

**Status:** Re-adjudication complete. This is a review artifact only — nothing has been deleted or
edited. Approve/adjust the buckets below before any test is touched. Scope: `TheCanalaveLibrary.Tests.RazorComponents`
only (the Integration flake fix, Integration format-dupes, and Unit trim are separate phases, not in this file).

**Criterion applied** (the reframed rule now in `canalave-conventions/testing.md` §"What belongs in
RazorComponents"): a test earns its keep when a *plausible silent regression* in user-observable
behavior would NOT be caught by the compiler, another test tier, or `check-design-tokens.ps1` — AND
the assertion is on **semantic output**, not style. **Key correction vs. the earlier audit:** the
L4.5-Browser band is autonomous-only, not human-verified, so behavior-level bUnit assertions are the
*sole* automated net for render output. Conditional-visibility / data-driven / computed-output tests
are therefore **kept**, not cut as "readable." The earlier audit's 31–45% "bloat" was scored against
the old leaky rule and over-counted; under the corrected criterion the real cut is ~17%.

## Headline

| Bucket | Meaning | Count | Action |
|---|---|---:|---|
| **A** | Asserts CSS/Tailwind class presence | 11 | **Cut** — invalid evidence (bUnit applies no CSS); style owned by token CI + human pass |
| **B-redundant** | Inverse half of a bare-param pair / triplicated assert / mobile-mirror of desktop `@code` | 77 | **Cut / collapse** |
| **B-static** | Always-present static text/element, no condition behind it | 16 | **Cut** — change-detector, not bug-detector |
| **C-keep** | Conditional visibility, data-driven, computed value, EventCallback arg, service-call id | 471 | **Keep as-is** |
| **C-consolidate** | Keep coverage, but merge a fragmented cluster into fewer behavior-level tests | 27 | **Merge** (net −~15 methods, 0 coverage loss) |
| | **Total** | **602** | |

Net: **~104 deletions (17%)** + 27 consolidations. Zero behavioral coverage lost — every cut is a
class-string assert, a redundant restatement, a static change-detector, or a desktop-duplicated mirror.

---

## 1. Whole-file deletion

- **`StoryMobileTests.cs` — DELETE ENTIRELY (19 tests).** Every test mirrors `StoryDesktopTests`'
  identical `@code` (title/author/badges/word-count/tags/cover/description/chapters/rec-section). The
  only Desktop↔Mobile difference is Tailwind layout, which bUnit cannot verify and the human visual
  pass owns. Lines 82,94,104,117,128,144,157,172,184,202,214,224,236,246,258,275,286,300,312.

## 2. Bucket A — CSS/class-presence cuts (11)

| File:line | Test | Class string asserted |
|---|---|---|
| `ContentSurfaceTests.cs:30` | Default_UsesPaperGroundAndSideRailFrame | `bg-(--color-paper)`, `border-x-4` |
| `ContentSurfaceTests.cs:45` | Variants_CarryTheirDistinctTreatment | `py-8`/`py-3`/`focus-within:ring-2` |
| `ConversationListItemTests.cs:78` | WhenSelected_HasPrimaryBorderClass | `border-(--color-action-ink)` |
| `ConversationListItemTests.cs:90` | WhenNotSelected_NoPrimaryBorderClass | (absence of same) |
| `InlineAlertTests.cs:50` | DangerIsDefault_SuccessSwapsPalette | `--color-danger`/`--color-success` |
| `ManualTreeCanvasTests.cs:100` | UserChips_AreCircular_StoryChips_AreNot | `rounded-full` |
| `MessageItemTests.cs:40` | OwnMessage_UsesFlexRowReverse | `flex-row-reverse` |
| `MessageItemTests.cs:65` | OtherMessage_DoesNotUseFlexRowReverse | (absence of same) |
| `PaginationControlsTests.cs:165` | ActivePage_HasDistinctCssTokenFromInactivePage | `bg-(--color-action)` |
| `StoryDeckTests.cs:104` | PopulatedStories_GridContainerPresent | `[class*='grid']` |
| `TagChipTests.cs:51` | AppliesCorrectBackgroundClassForTagType | `bg-(--color-tagtype-*)` (5-case Theory) |

## 3. Bucket B-redundant — cut/collapse (77)

**Bare-parameter ShowsX/NoX inverse halves** (keep the richer half already listed as C-keep; cut the pure-absence one-liner):
- `BlogPostPropertiesFormTests.cs:94` (AuthorStories-null) · `CharacterEntryTests.cs:64` (AllowOCDetails-false), `:88` (IsOc-false)
- `CommentEditorTests.cs:110` (spoiler-toggle-false), `:156` (not-busy) · `ConversationListItemTests.cs:66` (archived-false)
- `CommentSectionTests.cs:335` (authenticated-shows-composer) · `FollowButtonTests.cs:65` (bell-shows-when-following)
- `MessageComposerTests.cs:128` (not-busy) · `RecommendationCardTests.cs:73` (not-highlighted), `:94` (not-hidden-gem), `:154` (not-own)
- `ResultsFilterPanelTests.cs:48` (ShowTextSearch-true), `:78` (ShowInteractionFilters-true)
- `SeriesCardTests.cs:79` (description-null), `:88` (no-EditHref) · `StoryCardTests.cs:120` (tags-empty), `:145` (description-null)
- `StoryDesktopTests.cs:133` (not-author), `:209` (no-tags), `:233` (cover-null), `:257` (description-null)
- `TagChipTests.cs:109` (sprite-null), `:162` (OnRemove-no-delegate) · `TagFilterTests.cs:41` (anonymous-hidden)
- `TagSelectorTests.cs:78` (no-initial-tags) · `UserCardTests.cs:53` (tagline-null) · `VouchListTests.cs:82` (vouchtext-null), `:100` (not-editable)
- `TreeSearchDesktopTests.cs:156` (not-truncated)

**"Present" half transitively covered by the click/interaction test** (cut the presence-only assert):
- `CommentEditorTests.cs:42` (default-save-label), `:65` (cancel-present) · `MessageComposerTests.cs:61` (cancel-present)
- `RecommendationCardTests.cs:105` (like-button-present) · `RecommendationEditorTests.cs:67` (cancel-present)
- `RecommendationHelpfulPromptTests.cs:28` (yes/no-buttons-present) · `RelatedStoriesSectionTests.cs:108` (auth-filter-present)

**Same-condition / same-value duplicates:**
- `ChapterNavigationTests.cs:216` (NoVersions == SingleVersion's Count≤1) · `PaginationControlsTests.cs:44` (zero-count == fits-one-page), `:150` (inactive-no-aria-current, implied by exactly-one aria-current)
- `BookshelvesDesktopTests.cs:87` (inactive-count, implied by HaveCount(1)) · `RecommendationHelpfulPromptTests.cs:79` (dup hide assertion)
- `StoryDeckTests.cs:137` (null-user no-edit == not-matching no-edit) · `TagDirectoryTests.cs:146` (Admin == Moderator mod-controls) · `TagEditorFormTests.cs:63` (CreateMode AllowOC, subsumed by the Theory)
- `MessageItemTests.cs:75` (other-side username == own username path), `:98` (other-side HTML == own HTML path)
- `SearchDesktopTests.cs:163` (mutation-sanity dup of 66+93) · `StoryExternalLinksRowTests.cs:103` (composition-level no-links, proven by leaf 39)
- `BookshelvesMobileTests.cs:167` (open-close mutation-sanity, dup of CloseButton test)

**Mobile files re-asserting desktop's identical computed logic** (keep only mobile-unique overlay/drawer behavior; cut the mirrors):
- `SearchMobileTests.cs:108, :120, :134, :145, :165` (give-me-more / sorted / StoryDeck / OnLoadMore / mutation-sanity — all == SearchDesktop)
- `TreeSearchMobileTests.cs:104` (degree-badge, == TreeSearchDesktop:129 + TreeSearchResultBadge)
- `CommentSectionGroupTests.cs:45` (empty-state is context-free, == CommentSectionTests:69)

## 4. Bucket B-static — cut (16)

- `BlogPostPropertiesFormTests.cs:36, :45, :54, :63` — `Form_Renders_{TitleInput,RatingSelect,HasSpoilersCheckbox,PublishToggle}` (unconditional element presence)
- `BookshelvesDesktopTests.cs:124` (sidebar panel always present) · `SearchDesktopTests.cs:121` (panel present) · `SearchMobileTests.cs:65` (filter button present)
- `CharacterEntryTests.cs:52` (priority select present) · `MessageComposerTests.cs:30` (default "Send" label)
- `RecommendationEditorTests.cs:107` (static "500" hint) · `RecommendationHelpfulPromptTests.cs:19` (static prompt text)
- `SavedTagSelectionLoadFlyoutTests.cs:47` ("Load saved" label) · `SavedTagSelectionSaveDialogTests.cs:38` ("Save current…" label)
- `StoryCardTests.cs:325` (ViewStory link always present, already asserted by :262) · `StoryTitlePickerTests.cs:46` ("renders without throwing" smoke, no assertion)
- `TagDirectoryTests.cs:66` (static section headings)

## 5. Bucket C-consolidate — merge, no coverage loss (27 → ~12)

| File | Tests to merge | Into |
|---|---|---|
| `BadgeSettingsFormTests.cs` | :32, :56, :66, :76 | 2 behavior tests: empty-state, visible/hidden partition affordances |
| `BookshelvesMobileTests.cs` | :120 | fold backdrop-present into `FilterButton_Click_OpensOverlay` |
| `ChapterListTests.cs` | :253 | fold no-anchor-links into `EmptyList_ShowsNoChaptersMessage` |
| `GroupCardTests.cs` | :32, :51, :59, :68 | fold RendersGroupName into a render test; 3 audience badges → one `[Theory]` |
| `PairingBuilderTests.cs` | :22, :34, :47 | one parameterized `<2`-char threshold test |
| `SearchMobileTests.cs` | :96 | fold panel-present into `FilterButton_Click_OpensOverlay` |
| `StoryDeckTests.cs` | :218 | fold PageSize=0 into single-page hidden test (div-by-zero edge) |
| `StoryPropertiesFormTests.cs` | :43, :52, :61, :71 | one "renders all fields" behavior test |
| `TagSelectorTests.cs` | :122 | merge with the remove-fires-callback test (same click) |
| `TreeSearchTabStripTests.cs` | :16, :29, :41, :51, :64 | two `[Theory]`s: selected-state, click-emit |
| `UserStoryInteractionFilterTests.cs` | :68 | merge into the add-toggle emit test |
| `VouchListTests.cs` | :49 | fold username-render into the one-per-row count test |

## 6. Judgment calls worth a glance (kept both halves — verify you agree)

These looked like inverse pairs but the negative half sits on a **computed expression**, not a bare
param, so both branches are genuine coverage and were KEPT:
- `ConversationListItemTests.cs:44` — `UnreadCount > 0` is a computed threshold, not a bare bool.
- `GroupCardTests.cs:87` (`MemberCount == 1` plural), `:105` (`IsNullOrWhiteSpace(Description)`).
- `InlineAlertTests.cs:23` (whitespace-filter boundary) · `SeriesCardTests.cs:59` (singular/plural ternary).
- `ResultsFilterPanelTests.cs:142` (RelevanceSort appears via computed VisibleSorts, not a bare param).
- `SeriesMembershipBoxTests.cs:57/79` and `StoryCard`/`StoryDesktop` author-null/cover-null branches —
  each negative branch renders a **distinct element** (disabled span, plaintext fallback), not mere absence.
- `CommentItemTests.cs:189/220` (edit/delete absent when not own) — kept per keep-bias: these guard a
  real "always-show edit/delete" authorization regression the compiler can't see.
- `StoryExternalLinksRowTests.cs:70` — semantic placement ordering in composition (row after chapters,
  before recommendations) is behavior, kept.

## 7. Not changing — 471 keeps

The keep set is the tier's real value: EventCallback arg-correctness, service-call id resolution,
conditional visibility, data-driven `@foreach`, computed values (word-count/ordinal/pluralization/
sprite-src/window-paging), authorization gates, `@key` state-bleed regressions, and lifecycle re-sync
guards. Files that are **100% keep** (exemplary, touch nothing): `AccountStatusBannerTests`,
`AddToCustomListMenuTests`, `CanalaveErrorBoundaryTests`, `CanalaveTypeaheadTests`, `ChapterListTests`,
`CommentItemTests`, `ConfirmDialogTests`, `CommunitySpotlightDisplayTests`, `DeepDiveTabTests`,
`DraftAutosaveTests`, `ExploreTabTests`, `ImportReviewPanelTests`, `ModSpotlightPageTests`,
`ProfilePageTests`, `RecommendationSectionTests`, `SeriesCreateEditPageTests`, `SeriesMembershipBoxTests`,
`SpotlightRedemptionPageTests`, `StoryLineageBoxTests`, `StoryViewStatsTests`, `ToastHostTests`,
`TreeSearchControlsTests`, `TreeSearchResultBadgeTests`, `UserStoryInteractionPanelTests`, `VouchButtonTests`.
