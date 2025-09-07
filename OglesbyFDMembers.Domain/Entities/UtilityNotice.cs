// UtilityNotice.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace OglesbyFDMembers.Domain.Entities;

/// <summary>
/// Imported utility payer notices used for matching and auto-allocation.
/// Notes/Policy:
/// - Auto-allocate when Amount == OwnedCount × Fee; else flag NeedsReview.
/// - No cascade deletes; configure Restrict in DbContext.
/// </summary>
public class UtilityNotice
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string PayerNameRaw { get; set; } = string.Empty;

    /// <summary>
    /// Payment amount from utility import (currency, 2 decimal places).
    /// </summary>
    [Required]
    public decimal Amount { get; set; }

    [Required]
    public DateTime ImportedUtc { get; set; }

    public int? MatchedPersonId { get; set; }

    public bool IsAllocated { get; set; }

    public bool NeedsReview { get; set; }

    // Navigation
    public Person? MatchedPerson { get; set; }
}
