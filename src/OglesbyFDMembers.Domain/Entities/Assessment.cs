// Assessment.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using OglesbyFDMembers.Domain.Enums;

namespace OglesbyFDMembers.Domain.Entities;

/// <summary>
/// Annual liability for a property.
/// Notes/Policy:
/// - Liability is per property per year; unique on (PropertyId, Year).
/// - No cascade deletes; configure Restrict in DbContext.
/// </summary>
public class Assessment
{
    public int Id { get; set; }

    [Required]
    public int PropertyId { get; set; }

    [Required]
    public int Year { get; set; }

    /// <summary>
    /// Total amount due for the year (currency, 2 decimal places).
    /// </summary>
    [Required]
    public decimal AmountDue { get; set; }

    /// <summary>
    /// Total amount paid toward this assessment (currency, 2 decimal places).
    /// </summary>
    [Required]
    public decimal AmountPaid { get; set; }

    [Required]
    public AssessmentStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }

    // Navigation
    public Property? Property { get; set; }
    public ICollection<PaymentAllocation> PaymentAllocations { get; set; } = new List<PaymentAllocation>();
}
