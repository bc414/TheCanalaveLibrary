namespace TheCanalaveLibrary.Core;

/// <summary>
/// Layer-5 paging envelope for JSON transport (layer5-wasm.md §"Paged-Result Ruling"). Several
/// read-service methods return <c>Task&lt;(T[] Items, int TotalCount)&gt;</c> — value tuples don't
/// round-trip named fields over <c>System.Text.Json</c> — so the endpoint/client hop translates to
/// this record at the HTTP boundary only. This is a paging envelope, not a service-contract
/// change: interface signatures stay tuples; only <c>{Feature}Endpoints.cs</c>/
/// <c>Client{Feature}ReadService</c> construct/deconstruct it.
/// </summary>
public sealed record PagedResult<T>(T[] Items, int TotalCount);
