using System.ComponentModel.DataAnnotations;

namespace LinkedInAutoReply.Models;

public class SyncState
{
    [Key]
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
