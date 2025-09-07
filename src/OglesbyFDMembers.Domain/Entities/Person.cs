// Person.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OglesbyFDMembers.Domain.Entities;

/// <summary>
/// Represents a person in the system.
/// Notes/Policy:
/// - Reports use owner of record on Jan 1 (policy date configurable later).
/// - No cascade deletes; configure Restrict in DbContext.
/// - Addresses: one primary, multiple historical; IsValidForMail=false on returns.
/// </summary>
public class Person
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(32)]
    public string? Phone { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool Active { get; set; } = true;

    public DateTime CreatedUtc { get; set; }

    // Navigation
    public ICollection<PersonAddress> Addresses { get; set; } = new List<PersonAddress>();
    public ICollection<PersonAlias> Aliases { get; set; } = new List<PersonAlias>();
    public ICollection<Ownership> Ownerships { get; set; } = new List<Ownership>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    // Utility notices matched to this person (nullable FK on UtilityNotice)
    public ICollection<UtilityNotice> MatchedUtilityNotices { get; set; } = new List<UtilityNotice>();
}
