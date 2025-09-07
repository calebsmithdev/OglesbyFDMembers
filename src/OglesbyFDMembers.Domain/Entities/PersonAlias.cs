// PersonAlias.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace OglesbyFDMembers.Domain.Entities;

/// <summary>
/// Alternate names or spellings used to match imported records.
/// Notes/Policy:
/// - No cascade deletes; configure Restrict in DbContext.
/// </summary>
public class PersonAlias
{
    public int Id { get; set; }

    [Required]
    public int PersonId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Alias { get; set; } = string.Empty;

    [Required]
    public PersonAliasType Type { get; set; }

    public DateTime CreatedUtc { get; set; }

    // Navigation
    public Person? Person { get; set; }
}

public enum PersonAliasType
{
    FamilyMember = 0,
    VVEC = 1,
    Other = 2
}
