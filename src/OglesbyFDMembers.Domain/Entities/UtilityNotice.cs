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

    /// <summary>
    /// Snapshot of the person's full name that was chosen during import
    /// (auto or manual). Kept for audit even if the person's name changes later.
    /// </summary>
    [MaxLength(200)]
    public string? OriginalFullName { get; set; }

    /// <summary>
    /// When a payment is created from this notice, links to that Payment row
    /// for auditability and idempotency.
    /// </summary>
    public int? PaymentId { get; set; }

    public bool IsAllocated { get; set; }

    public bool NeedsReview { get; set; }

    // Navigation
    public Person? MatchedPerson { get; set; }
    public Payment? Payment { get; set; }
}
