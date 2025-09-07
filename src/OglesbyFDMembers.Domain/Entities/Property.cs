// Property.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OglesbyFDMembers.Domain.Entities;

/// <summary>
/// A property liable for annual assessments.
/// Notes/Policy:
/// - Liability is per property per year; Assessment is unique on (PropertyId, Year).
/// - Reports use the owner of record on Jan 1 (policy date configurable later).
/// - No cascade deletes; configure Restrict in DbContext.
/// </summary>
public class Property
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string AddressLine1 { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(64)]
    public string? State { get; set; }

    [MaxLength(32)]
    public string? Zip { get; set; }

    public bool Active { get; set; } = true;

    public DateTime CreatedUtc { get; set; }

    // Navigation
    public ICollection<Ownership> Ownerships { get; set; } = new List<Ownership>();
    public ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
}
