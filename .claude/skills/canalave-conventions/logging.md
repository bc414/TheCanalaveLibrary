# Logging & Telemetry — All Layers

Conventions for the three OpenTelemetry signals — **logs** (`ILogger<T>`), **traces**
(`ActivitySource`/`Activity`), **metrics** (`Meter`/instruments) — established by
WU-Observability (2026-07-06) and battle-tested on the image-storage pilot. Every later
work-unit adds *instances* of the patterns here, never new patterns; if a new pattern is
genuinely needed, this file gets the section first (Doc-Touch moment 2).

The plumbing lives in `ServiceDefaults/Extensions.cs` (subscriptions, OTLP export gated on
`OTEL_EXPORTER_OTLP_ENDPOINT` — set only by the Aspire dashboard in dev; Grafana LGTM on the
droplet at Phase 7, decision row 7 Resolved 2026-07-06). App code only *emits*; it never
references the OpenTelemetry SDK.

## What you get for free (do not re-instrument)

Subscription-only — the framework instruments itself:

| Signal source | What it emits |
|---|---|
| `Npgsql` (via `Npgsql.OpenTelemetry`, version tracks transitive Npgsql) | A span per SQL command (statement text + duration), parented under the ambient circuit/request span; connection-pool metrics |
| `Microsoft.AspNetCore.Components` (+`.Lifecycle`, `.Server.Circuits`) | Circuit lifecycle/navigation/event-handler spans; circuit active/connected/duration, render-batch metrics (.NET 10 built-ins) |
| ASP.NET Core / `HttpClient` | Request spans (health-check-filtered), outbound HTTP spans — **including the AWS SDK's S3 calls** |
| Runtime | GC/thread-pool/exception-count metrics |

Consequence: **a service method is already observable** as "the queries it runs inside the
circuit event that invoked it." Do not add `Service.Method` wrapper spans.

**Custom spans go only where auto-instrumentation is blind:** external I/O that needs a
domain-level name (blob storage, email), background work with no ambient parent (workers,
marts), or a multi-step operation whose internal phase boundaries matter.

## Custom instrumentation: `CanalaveTelemetry` (Core/Diagnostics/)

One nested class per instrumented component, each owning an `ActivitySource` + `Meter` named
`TheCanalaveLibrary.{Component}`. ServiceDefaults subscribes to the wildcard
`"TheCanalaveLibrary.*"` (string literal, both directions cross-commented — deliberately no
ServiceDefaults→Core project reference), so a new component lights up with no registration
change. Existing: `ImageStorage`. Reserved next: `ViewCount` (WU-Redis), `Email` (WU-Email),
`Marts` (WU-Marts).

- Producers are **`static readonly` process singletons** — `ActivitySource`/`Meter` are
  thread-safe, stateless funnels (the mirror image of `DbContext`, which is scoped because it
  is stateful). Never inject, never scope, never `IMeterFactory` in app code.
- The bare app name is NOT a source name — it would collide with `service.name` /
  `ApplicationName`, which already carries app identity on every signal.
- **Emission idiom is null-safe** — `StartActivity` returns `null` with no listener/sampler
  (unit tests, unsubscribed source):
  ```csharp
  using Activity? activity = CanalaveTelemetry.ImageStorage.Source.StartActivity("ImageStorage.Save");
  activity?.SetTag("canalave.image.kind", kind.ToString());
  ```
- On failure: record on the span, rethrow, **don't also log** (see "no double-log" below):
  ```csharp
  catch (Exception ex)
  {
      activity?.AddException(ex);
      activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
      throw;
  }
  ```
- Metric instruments are `static readonly` fields beside their Meter; multi-instrument
  emissions get a helper so the tag shape can't drift between callers (see
  `CanalaveTelemetry.ImageStorage.RecordUpload` — both storage impls call it).

### Naming

| Thing | Rule | Examples |
|---|---|---|
| Source/Meter | `TheCanalaveLibrary.{Component}` PascalCase | `TheCanalaveLibrary.ImageStorage` |
| Span | `{Component}.{Operation}` PascalCase; IDs in tags, **never** in names (cardinality) | `ImageStorage.Save`, `ViewCountDrain.Cycle` |
| Metric | `canalave.` prefix, lowercase dotted, UCUM unit | `canalave.image.uploads` (`{upload}`), `canalave.image.upload.size` (`By`) |
| Tag | `canalave.{noun}.{property}`, matching log property names modulo prefix/case so traces↔logs correlate; low-cardinality values only | `canalave.user.id`, `canalave.image.kind` |

Reserved metric names (WU-Redis): `canalave.viewcount.queue.depth` (observable gauge),
`canalave.viewcount.drain.batch.size` (histogram), `canalave.viewcount.drain.duration`
(histogram, `s`). Instrument choice encodes meaning: `Counter` = only goes up,
`Histogram` = distribution, `ObservableGauge` = sampled point-in-time level.

## Structured log templates

- **Named PascalCase placeholders always** — `"Saved {ImageKind} image {ImageKey}"` — never
  string interpolation into the message (kills queryability, and the analyzer can't check it).
- Placeholder names match trace tag names modulo the `canalave.` prefix (`{UserId}` ↔
  `canalave.user.id`) — that's what lets a Loki/dashboard query pivot between signals.
- IDs the line is *about* go in the template; ambient correlation (circuit, user) comes from
  the scope — don't repeat scope values in templates.
- Plain `ILogger<T>` calls (constructor-injected) are the default. `[LoggerMessage]`
  source-generated methods are the convention for hot paths — **mandated starting with
  WU-Redis's drain worker** (per-cycle code), not retrofitted onto existing sites.
- Services gain loggers when they gain something worth logging — no wholesale injection.

## Level semantics (this domain)

| Level | Meaning here | Examples |
|---|---|---|
| `Trace` | Never committed | — |
| `Debug` | Dev-loop diagnostics, deleted or demoted before the WU lands | — |
| `Information` | A domain event changed state | image saved, story published, drain batch flushed |
| `Warning` | Degraded but continuing — including **every best-effort swallow** | replaced-blob delete failed (orphaned), notification fan-out failed post-commit, delete no-op on a foreign path |
| `Error` | An operation the caller asked for failed and wasn't swallowed by design | worker cycle failed and will retry, unhandled service exception logged by WU-ErrorHandling's contract *(section stub — extended there)* |
| `Critical` | The process is unhealthy | startup seed/migration failure in prod |

Settled 2026-07-06 (consistency sweep): best-effort post-commit fan-out failures are
**Warning with the triggering entity IDs**, not Error — the primary action succeeded and the
loss is tolerated by design. (Following's two legacy `LogError` sites were normalized.)

**What NOT to log:** email addresses, message/chapter/comment bodies, tokens/secrets, full
claims — IDs only. Pre-launch stance: numeric user IDs as log properties are fine; revisit
retention/PII at Phase 7 (pairs with decision row 10).

## No silent catches

Log-and-continue, never swallow-without-trace. Every `catch` either rethrows, translates
(typed exception → user-facing error/HTTP result), or **logs at Warning+ with structured IDs**.

A silent catch is legal only when annotated at the catch site:
```csharp
// sanctioned-silent: <why silence is by-design> (see logging.md §"No Silent Catches")
```
Registry of sanctioned sites (keep current — this list is the audit trail):
- `Server/Identity/ServerActiveUserContext.cs` `ResolvePrincipal` — anonymous fallback for
  scopes with no HttpContext and no circuit state (DataSeeder); static helper inside
  auth-middleware timing, predates any logger; anonymous IS the correct outcome, not a failure.

Sweep verification: `grep -E 'catch\s*(\(\s*Exception\s*\))?\s*\{?\s*$' **/*.cs` across
Server/Core/SharedUI/Client must return only sanctioned sites.

## Context scopes: correlation at the dispatch boundary

Blazor Server's execution path is the circuit, not HTTP — after the initial request, event
handlers never pass through middleware. Scope enrichment therefore lives at the **dispatch
boundary**, once, not per-callsite:

- **Circuit dispatches** — `TelemetryCircuitHandler` (Server/Telemetry/), a scoped
  `CircuitHandler` whose `CreateInboundActivityHandler` wraps every inbound activity:
  `BeginScope` with `CircuitId`/`UserId` (lazily resolved from the circuit-scoped
  `IActiveUserContext`, cached after first non-anonymous read) + `canalave.user.id` tag on
  `Activity.Current`. ServiceDefaults sets `IncludeScopes = true`, so scope values land on
  every exported log record.
- **HTTP requests** (minimal-API endpoints) — the `EnrichWithHttpResponse` hook in
  ServiceDefaults tags `canalave.user.id` from `HttpContext.User`. Response hook, not request:
  the request span starts before auth middleware has populated the principal.
- **Workers** (recipe — first instance lands in WU-Redis) — no user; `BeginScope` per cycle
  with a cycle/batch ID inside the `{Worker}.Cycle` span.

Rejected (do not reintroduce): OTel log-record enrichment processor (singleton — cannot see
circuit-scoped identity); per-callsite `BeginScope` in service methods (unenforceable
boilerplate; the boundary owns ambient context).

## Recipes per surface type

**External call** (worked example: `S3ImageStorageService` / `LocalImageStorageService`):
custom span named `{Component}.{Operation}` + provider/kind tags; success = `Information` log
with key + size and the completed-work metrics; failure = exception on span, rethrow, no log
(no double-log: the exception is logged once, wherever it finally surfaces, and the trace
carries the span). Best-effort *callers* of the throwing operation log the Warning (they own
the "loss" context — see the replaced-blob delete sites in `ServerStoryWriteService` /
`ServerUserSettingsService`).

**Background worker** (WU-Redis lands the first instance): `{Worker}.Cycle` span per drain
cycle (no ambient parent — the span IS the root); `[LoggerMessage]` methods; per-cycle scope;
queue-depth observable gauge + batch-size and duration histograms under the reserved
`canalave.viewcount.*` names; cycle failure = `Error` log + error span, then continue/retry —
a worker never dies silently.

**Hub method** (stub — WU-SignalR extends): hub-adjacent tracing exists via the Components
sources; add `AddMeter("Microsoft.AspNetCore.Http.Connections")` when `MessagesHub` lands, and
write the per-method conventions here then.

**Unhandled exceptions** (stub — WU-ErrorHandling extends): what gets logged on circuit crash /
service exception / failed form post is that WU's server-side contract; it extends this file's
level table rather than inventing its own.

## Testing telemetry (pattern reference: `Tests.Unit/ImageStorageTelemetryTests.cs`)

- **Logs:** `FakeLogger<T>` (`Microsoft.Extensions.Diagnostics.Testing`, Unit csproj) — assert
  `Level` + `StructuredState` property names, not message prose.
- **Spans:** `ActivityListener` filtered to the component's source name, capture on
  `ActivityStopped`, assert name/tags/`Status`.
- **Metrics:** `MetricCollector<T>` over the static instruments — assert value + tag shape.
- **Seam smoke:** `Tests.Integration/NpgsqlTracingSmokeTests.cs` pins the `"Npgsql"` source
  name against silent breakage in an Npgsql upgrade.
- Placement rule unchanged (testing.md): the write services' one-line catch-log sites are
  DbContext-bound → not Unit-constructible; forcing throwing fakes through the host is
  disproportionate machinery for one-line declarative catches — convention + review carries
  them, the pilot's tests carry the emission patterns.

## How to read it (dev)

Run the Aspire path (`run-server/SKILL.md`) → dashboard. **Traces**: filter Resource = `web`;
a story-page load shows the circuit event span parenting Npgsql query spans (SQL text on the
span); an image upload shows `ImageStorage.Save` with the S3 HTTP child. **Structured logs**:
each record carries `CircuitId`/`UserId` scope values; click a record's trace link to jump to
its trace. **Metrics**: `canalave.*` under meter `TheCanalaveLibrary.ImageStorage`;
`aspnetcore.components.circuit.*` for circuit health; `Npgsql` meter for pool pressure.
Server-only path: no OTLP endpoint → signals still emit (listeners just absent) and console
logging behaves as always.
