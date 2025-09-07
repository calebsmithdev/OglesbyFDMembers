// FeeSchedule.cs
using System.ComponentModel.DataAnnotations;

namespace OglesbyFDMembers.Domain.Entities;

/// <summary>
/// Annual fee schedule. Typically one row per Year.
/// Notes/Policy:
/// - Use a unique constraint on Year in DbContext.
/// - No cascade deletes; configure Restrict in DbContext.
/// </summary>
public class FeeSchedule
{
    public int Id { get; set; }

    [Required]
    public int Year { get; set; }

    /// <summary>
    /// Amount per property for the given year (currency, 2 decimal places).
    /// </summary>
    [Required]
    public decimal AmountPerProperty { get; set; }
}
