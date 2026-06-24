using Xunit;

// The integration suite shares one Postgres container (PostgresFixture) and resets it between
// tests via Respawn — resets are serialized by design (one connection, FK-ordered deletes).
// Parallel execution would interleave resets and test bodies against the same DB, breaking
// isolation. This attribute makes serial execution deliberate and visible rather than accidental.
// See testing.md "Integration tests reset between every test (Respawn)."
[assembly: CollectionBehavior(DisableTestParallelization = true)]
