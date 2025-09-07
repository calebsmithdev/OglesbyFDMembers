// Ownership.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace OglesbyFDMembers.Domain.Entities;

/// <summary>
/// Ownership relationship between a person and a property over a date range.
/// Notes/Policy:
/// - Reports use owner of record on Jan 1 (policy date configurable later).
/// - No cascade deletes; configure Restrict in DbContext.
/// </summary>
public class Ownership
{
    public int Id { get; set; }

    [Required]
    public int PersonId { get; set; }

    [Required]
    public int PropertyId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    // Navigation
    public Person? Person { get; set; }
    public Property? Property { get; set; }
}
