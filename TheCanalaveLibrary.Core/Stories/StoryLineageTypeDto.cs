namespace TheCanalaveLibrary.Core;

/// <summary>
/// One row of the seeded <see cref="StoryLineageType"/> lookup (Inspired By / Prequel / Sequel /
/// Companion Piece) — feeds the type <c>&lt;select&gt;</c> on the lineage-request form. Lookup
/// table, not an enum (spec — content-only display, rename/add without deploy), so this DTO is
/// read from the database rather than enumerated from a C# enum.
/// </summary>
public record StoryLineageTypeDto(short TypeId, string TypeName);
