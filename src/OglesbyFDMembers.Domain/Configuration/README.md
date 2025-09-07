This folder is for EF Core configuration related to Domain models (if kept in Domain).

- EntityTypeConfiguration classes for fluent mappings (keys, indexes).
- Example: unique index on `Assessment(PropertyId, Year)`.
- Deletion behavior should be Restrict per policy (no cascade deletes).

