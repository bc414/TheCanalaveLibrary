using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class StoryImport
{
    public int ImportId { get; set; }

    public int StoryId { get; set; }

    public string SourcePlatform { get; set; } = null!;

    public string SourceUrl { get; set; } = null!;

    public byte VerificationStatus { get; set; }

    public DateTime DateImported { get; set; }

    public virtual Story Story { get; set; } = null!;
}
