# Blazor Conventions

Target: **Blazor Web App on .NET 10**, global `InteractiveAuto`. Components and pages live in **SharedUI**
(a Razor Class Library referencing only Core). `App.razor` and Identity components live in the **server**
project.

## Render mode: Global InteractiveAuto

Set the render mode **once**, on `<Routes>` and `<HeadOutlet>` in `App.razor` — not per-component:

```razor
<Routes @rendermode="InteractiveAuto" />
...
<HeadOutlet @rendermode="InteractiveAuto" />
```

This creates a true SPA:
1. **First request** → server-side prerender (fast paint, SEO).
2. **Background** → WASM payload downloads and caches.
3. **Subsequent navigation** → client-side routing via WASM, no page reloads; layout state preserved.

> **Dev shortcut (spec-sanctioned):** during active development you may use `InteractiveServer` globally
> (faster debugging, no API controllers needed yet) and switch to `InteractiveAuto` when shipping WASM.
> This is a temporary state, not a per-component pattern.

**Rejected (do not reintroduce):** islands of interactivity (Static SSR default + per-component
`@rendermode InteractiveWasm`). It was rejected because each `<a>` click triggered a full reload, destroying
client state — bad for readers clicking "Next Chapter" repeatedly.

## The prerender → interactive transition

Under `InteractiveAuto`, `OnInitializedAsync` runs **twice** — once during server prerender, once when the
component becomes interactive (server circuit or WASM). Without state persistence this double-fetches data
and flickers the UI. Two ways to get the initial data into a component:

1. **Smart dispatcher / page parameter** — initial data passed in during static render (the pattern already
   established in this repo for desktop/mobile dispatch).
2. **Persisted component state** — see below.

## State persistence: `[PersistentState]` (.NET 10 — preferred)

.NET 10 replaces the manual `PersistentComponentState` + `RegisterOnPersisting` + `TryTakeFromJson` dance
with a **declarative attribute**. Prefer this for the prerender→interactive handoff:

```razor
@code {
    [PersistentState]
    public StoryListingDto[]? Stories { get; set; }

    protected override async Task OnInitializedAsync()
    {
        // Set only when not already restored from prerender state.
        Stories ??= await StoryService.GetListingsAsync();
    }
}
```

- Property must be **`public`** (framework uses reflection for trimming/source-gen).
- Serialized with `System.Text.Json` by default and embedded in the prerendered HTML.
- For multiple instances of the same component, pair with `@key` so state binds to the right instance.

**Security under InteractiveAuto:** persisted state is exposed to the browser (same as CSR/WASM). Under
`InteractiveServer` alone it's protected by Data Protection, but `InteractiveAuto` may resolve to WASM —
so **never persist sensitive/private data** this way.

### Attribute options

```csharp
// Allow re-reading fresh values during enhanced navigation (read-only, infrequently-changing data):
[PersistentState(AllowUpdates = true)]
public WeatherForecast[]? Forecasts { get; set; }

// Skip restoring the prerendered value (component always computes fresh on interactive load):
[PersistentState(RestoreBehavior = RestoreBehavior.SkipInitialValue)]
public string? NoPrerenderedData { get; set; }

// Skip restoring on reconnection (force fresh data after a dropped circuit reconnects):
[PersistentState(RestoreBehavior = RestoreBehavior.SkipLastSnapshot)]
public int CounterNotRestoredOnReconnect { get; set; }
```

By default persisted state is loaded only on a component's initial load, so later enhanced-navigation events
to the same page won't clobber in-progress state (e.g. an edited form). Opt into `AllowUpdates` only for
read-only cached data.

### Persisting service state across the handoff

For state held in a scoped DI service (rather than a component), annotate the service property and register
it with the render mode (it can't be inferred from the service type):

```csharp
public class CounterTracker
{
    [PersistentState]
    public int CurrentCount { get; set; }
}

// Program.cs
builder.Services.AddScoped<CounterTracker>();
builder.Services.AddRazorComponents()
    .RegisterPersistentService<CounterTracker>(RenderMode.InteractiveAuto);
```

Only **scoped** services are supported. Use `RenderMode.InteractiveAuto` so it persists for whichever mode
a component resolves to.

### When to drop to the manual service

The imperative `PersistentComponentState` service (`RegisterOnPersisting` / `PersistAsJson` /
`TryTakeFromJson`, plus `RegisterOnRestoring` in .NET 10) still works and is the escape hatch for complex
serialization. Use `[PersistentState]` by default; reach for the service only when the declarative model
can't express what you need.

## Component design

- **Inject interfaces, never `DbContext` or concrete services.** Components depend on `IStoryService`,
  `ISpriteService`, etc. — the same interface resolves to a server impl or an HTTP impl depending on where
  the component is running. See [service-patterns.md](service-patterns.md).
- **Components are render-mode agnostic.** A component in SharedUI must work under both server and WASM. No
  WASM-only NuGet in SharedUI; `@rendermode` directives are metadata only. Use `RendererInfo.IsInteractive`
  / `RendererInfo.Name` to branch on prerender vs interactive when genuinely needed.
- **Isolate interactivity.** Keep interactive logic in small focused components (the repo's RNG example is
  the model) and let surrounding pages stay static where possible — maximizes prerender/SEO surface.
- **Forms:** `EditForm` with DTO/ViewModel models (never EF Core entities) — anti-forgery comes from
  `EditForm`, and DTOs prevent over-posting.

## Identity components

Login, Register, and other Identity UI **must stay in the server project** — they use
`UserManager`/`SignInManager`/`HttpContext`, which don't exist in WASM. Do not move them to SharedUI.

## Rich text & sanitization

Chapter text / descriptions use the WYSIWYG editor (Blazored TextEditor / Quill.js). All user-submitted
HTML is sanitized **server-side** with `HtmlSanitizer` (allow-list) before saving — never trust client
sanitization, never persist raw user HTML.
