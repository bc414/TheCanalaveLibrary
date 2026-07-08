# Horizontal Scaling — Going from One Web Node to N≥2

Everything that changes — or doesn't — when this app moves from a single web node to more than
one. This is a **deployment-topology concern, not an L1–L4 contract change**: per `grid_axes.md`
§"The Two Boundaries," nothing here alters an interface, DTO, or component contract. Dormant until
an actual N≥2 decision is made on measured need — never a day-one dependency (same framing as
`SKILL.md` axiom 7).

There are two genuinely separate concerns that are easy to conflate. This project only needs one
of them.

## 1. Connection/session affinity ("sticky sessions") — needed, load-balancer config

A Blazor Server circuit's entire component tree lives in **one server process's memory**. There is
no framework mechanism to migrate or reach a circuit from a different node — whichever node
accepts the initial SignalR handshake *is* that circuit for its lifetime. This is true regardless
of whether the app defines any SignalR Hubs of its own; it's inherent to how `InteractiveServer`
rendering works.

**Where this still applies even after the Global Flip:** the site's end state is global
`InteractiveAuto` (`SKILL.md` axiom 8) — but Auto mode still creates a real Server circuit during
its WASM-bootstrap window for any visitor whose browser doesn't already have the WASM bundle
cached (every first-time visitor, forever, and anyone whose cache was cleared). While that circuit
is alive, it has the same single-node constraint as full `InteractiveServer` does today.

**Concretely, at N≥2 without affinity:** `ReconnectModal` (built in WU-ErrorHandling) exists to
gracefully resume a dropped circuit after a network blip. Without sticky sessions, a reconnect
attempt has only a `1/N` chance of landing back on the node that holds the circuit — it will
usually fail and force the hard-reload fallback the feature exists to avoid. Session affinity is
what makes that feature actually work once there's more than one node.

**Action, whenever N≥2 happens:** configure the load balancer for connection affinity (cookie-based
affinity or IP-hash — either is standard, this is routing config, not application code). Not yet
done because there's nothing to configure against at N=1.

## 2. SignalR backplane — not needed

A backplane (Redis backplane, Azure SignalR Service, etc.) solves one specific problem: app code on
node A calls `IHubContext.Clients.X.SendAsync(...)`, but the target client is connected to node B —
the backplane routes that broadcast across nodes. It is only relevant when the app defines its own
SignalR **Hub** with cross-client push.

This app has **zero app-defined Hubs**, and that's permanent, not provisional: private messaging's
SignalR-push idea (`MessagesHub`) — the only Hub ever planned anywhere in this project — was ruled
out entirely on 2026-07-07. Messaging is deliberately request/response, permanently: Discord
already serves the real-time-chat need, and this site's messaging is for substantive, infrequent,
async/long-form conversation, not ambient chat. See `cross-cutting.md` §"Private Messaging
Architecture" for the full rationale. With no Hub, there is nothing to broadcast across nodes, and
therefore nothing to backplane. If some future feature ever proposes an app-defined Hub, revisit
this — but nothing today calls for one.

## 3. The Valkey signal-buffer body-swap (moved here from layer2-services.md/SKILL.md)

Unrelated to SignalR — this is about the **in-process signal buffers** (`layer2-services.md`
§"Signal Buffering": reading-progress and view-count), which are a different N≥2 concern entirely.

**Why a shared store is needed at N≥2:** each buffer is a per-process `ConcurrentDictionary`. At
N=1 that's strictly better than a network hop — no dependency, no latency. At N≥2, two nodes have
two independent buffers; if the same signal's writes land on different nodes across requests (no
affinity requirement here — these are fire-and-forget pings, not circuits), each node's periodic
flush only sees its own slice, so max-progress/sum-of-views merging silently under-counts instead
of coalescing correctly across the whole fleet.

**The swap, when it happens:** each buffer's store swaps from the in-process dictionary to a shared
RESP-compatible store — **Valkey** (the open-licensed fork; DigitalOcean-managed target for
production — not Redis, which relicensed off open source in 2024) — behind the **same interface**:
`buffer.Record(...)` and the flusher's scoped-DI structure don't change; a hash/counter structure
replaces the dictionary, and the same `BackgroundService` worker drains via RESP reads instead of
`Drain()`. Body swap only — no interface, caller, UI, or schema change (`grid_axes.md`'s
vertical-boundary property).

**Provisioning today:** the Aspire dev orchestration already provisions a `cache` container for
this future day (currently a plain Redis image, since nothing consumes it at N=1 and the specific
RESP-store choice only matters once something does) — see `run-server/SKILL.md` "Aspire path."
Nothing reads or writes it yet.

## 4. Data Protection keyring — already solved, no action needed

Keys are persisted to Postgres (`PersistKeysToDbContext<ApplicationDbContext>`, see `security.md`
§"Data Protection Keyring") rather than to local disk. Since Postgres is already the single shared
source of truth across every node, this works identically at N=1 or N≥2 with zero additional work
— noted here only so the full N≥2 picture is in one place instead of scattered.

## Summary: the whole N≥2 story in one table

| Concern | Needed? | What it takes |
|---|---|---|
| Load-balancer session affinity | **Yes** | LB config (cookie affinity or IP-hash) — not yet done, nothing to configure against at N=1 |
| SignalR backplane | **No** | No app-defined Hub exists or is planned (messaging's Hub permanently dropped 2026-07-07) |
| Signal-buffer store swap | **Yes, when N≥2 happens** | Body swap to Valkey behind the unchanged buffer interface — already designed, not yet built |
| Data Protection keyring | **No further action** | Already Postgres-backed; works today, unchanged at N≥2 |
