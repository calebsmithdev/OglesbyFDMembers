// PaymentAllocation.cs
using System.ComponentModel.DataAnnotations;

namespace OglesbyFDMembers.Domain.Entities;

/// <summary>
/// Allocation of a payment amount to a specific assessment.
/// Notes/Policy:
/// - Use transactions for Payment + Allocations + Recalc.
/// - No cascade deletes; configure Restrict in DbContext.
/// </summary>
public class PaymentAllocation
{
    public int Id { get; set; }

    [Required]
    public int PaymentId { get; set; }

    [Required]
    public int AssessmentId { get; set; }

    /// <summary>
    /// Amount allocated (currency, 2 decimal places).
    /// </summary>
    [Required]
    public decimal Amount { get; set; }

    // Navigation
    public Payment? Payment { get; set; }
    public Assessment? Assessment { get; set; }
}
