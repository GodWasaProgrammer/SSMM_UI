using System.Collections.Generic;

namespace SSMM_UI.Poster;

public class SocialPostResult
{
    public bool PostedAny { get; set; }
    public List<string> PostedTo { get; } = [];
    public List<string> SkippedReasons { get; } = [];

    public void AddReason(string reason)
    {
        if (!string.IsNullOrWhiteSpace(reason))
        {
            SkippedReasons.Add(reason);
        }
    }
}
