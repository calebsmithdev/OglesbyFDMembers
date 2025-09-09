namespace OglesbyFDMembers.Domain.Entities;

/// <summary>
/// Generic key/value application setting stored in the database.
/// </summary>
public class AppSetting
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

