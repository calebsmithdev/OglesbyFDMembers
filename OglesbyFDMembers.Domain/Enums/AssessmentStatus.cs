// AssessmentStatus.cs

namespace OglesbyFDMembers.Domain.Enums;

/// <summary>
/// Status of an assessment balance for a given year/property.
/// </summary>
public enum AssessmentStatus
{
    Unpaid = 0,
    Partial = 1,
    Paid = 2,
    Overpaid = 3
}
