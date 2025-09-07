// PersonAddress.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace OglesbyFDMembers.Domain.Entities;

/// <summary>
/// Represents a mailing or historical address for a person.
/// Notes/Policy:
/// - One primary address per person; multiple historical addresses allowed.
/// - On returned mail, set IsValidForMail=false.
/// - No cascade deletes; configure Restrict in DbContext.
/// </summary>
public class PersonAddress
{
    public int Id { get; set; }

    [Required]
    public int PersonId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Line1 { get; set; } = string.Empty;

    /// <summary>
    /// Optional addressee line to print above the address (e.g., "Wife & Husband Lastname").
    /// </summary>
    [MaxLength(200)]
    public string? AddresseeName { get; set; }

    [MaxLength(200)]
    public string? Line2 { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(64)]
    public string? State { get; set; }

    [MaxLength(32)]
    public string? PostalCode { get; set; }

    public bool IsPrimary { get; set; }

    public bool IsValidForMail { get; set; } = true;

    public DateTime CreatedUtc { get; set; }

    // Navigation
    public Person? Person { get; set; }
}
