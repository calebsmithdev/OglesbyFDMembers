// Payment.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using OglesbyFDMembers.Domain.Enums;

namespace OglesbyFDMembers.Domain.Entities;

/// <summary>
/// A payment made by a person, optionally a donation.
/// Notes/Policy:
/// - Allocations determine how amounts apply to assessments.
/// - Use transactions for Payment + Allocations + Recalc.
/// - No cascade deletes; configure Restrict in DbContext.
/// </summary>
public class Payment
{
    public int Id { get; set; }

    [Required]
    public int PersonId { get; set; }

    [Required]
    public PaymentType PaymentType { get; set; }

    /// <summary>
    /// Payment amount (currency, 2 decimal places).
    /// </summary>
    [Required]
    public decimal Amount { get; set; }

    [Required]
    public DateTime PaidUtc { get; set; }

    [MaxLength(50)]
    public string? CheckNumber { get; set; }

    public bool IsDonation { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    public Person? Person { get; set; }
    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}
