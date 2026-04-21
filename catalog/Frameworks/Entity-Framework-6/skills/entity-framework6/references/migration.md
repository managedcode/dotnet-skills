# EF6 to EF Core Migration Guide

## Migration Decision Framework

### When to Stay on EF6

- Application is stable and not actively evolving
- Heavy use of EDMX designer models without code-first equivalents
- Complex stored procedure mappings that drive business logic
- Dependency on ObjectContext or ObjectStateManager APIs
- No plans to move the host application to modern .NET

### When to Consider EF Core

- Targeting .NET 6+ or planning a runtime migration
- Need for cross-platform deployment (Linux, containers)
- Desire for improved performance characteristics
- Need for features only available in EF Core (e.g., compiled queries, split queries, interceptors)
- Starting a new module or service that will coexist with EF6 code

### When to Skip EF Core Entirely

- Moving to a different data access strategy (Dapper, raw ADO.NET, document stores)
- Decomposing the monolith into microservices with dedicated data strategies
- The data layer is being replaced by an external API or service

## Migration Approaches

### Parallel Coexistence

Run EF6 and EF Core side by side in the same solution:

1. Add EF Core packages alongside existing EF6 references
2. Create a new DbContext for EF Core targeting the same database
3. Migrate entity configurations incrementally
4. Gradually shift new features and queries to EF Core
5. Retire EF6 DbContext once all code paths are migrated

Benefits:
- No big-bang cutover
- Validate behavior slice by slice
- Rollback is straightforward

Risks:
- Two DbContexts means two change trackers; avoid crossing them in the same unit of work
- Schema migrations need coordination

### Module-by-Module Migration

Migrate entire bounded contexts or modules at once:

1. Identify module boundaries in the existing codebase
2. Extract the module's data access into a dedicated project
3. Port that project to EF Core and modern .NET
4. Integrate via API or shared database until full cutover

Benefits:
- Clean separation reduces cross-cutting risks
- Easier to test in isolation

Risks:
- Requires clear module boundaries
- May need temporary integration shims

### Big-Bang Rewrite

Replace the entire data layer in a single release:

1. Map all entities, configurations, and queries
2. Port all migrations or regenerate schema
3. Run extensive regression testing
4. Deploy as a single release

Benefits:
- No ongoing dual maintenance

Risks:
- High risk of regressions
- Long development cycle without production feedback
- Rollback is difficult

## Feature Mapping

### Entity Configuration

| EF6                             | EF Core                                      |
|---------------------------------|----------------------------------------------|
| `EntityTypeConfiguration<T>`    | `IEntityTypeConfiguration<T>` or Fluent API  |
| `modelBuilder.Configurations.Add()` | `modelBuilder.ApplyConfigurationsFromAssembly()` |
| EDMX designer                   | No equivalent; use code-first                |
| Complex Types                   | Owned Types                                  |

### Lazy Loading

| EF6                                  | EF Core                                      |
|--------------------------------------|----------------------------------------------|
| Enabled by default with virtual props | Opt-in via `UseLazyLoadingProxies()` or `ILazyLoader` |

### Change Tracking

| EF6                        | EF Core                                      |
|----------------------------|----------------------------------------------|
| Snapshot by default        | Snapshot by default; change-tracking proxies optional |
| `ObjectStateManager`       | `ChangeTracker`                              |

### Stored Procedures

| EF6                                         | EF Core                                      |
|---------------------------------------------|----------------------------------------------|
| `MapToStoredProcedures()` for CUD           | Sproc mapping for CUD introduced in EF Core 7 |
| Function imports in EDMX                    | `FromSql` or raw SQL                         |

### Migrations

| EF6                           | EF Core                                      |
|-------------------------------|----------------------------------------------|
| `Add-Migration`, `Update-Database` | `Add-Migration`, `Update-Database` (similar commands) |
| `__MigrationHistory` table    | `__EFMigrationsHistory` table                |

## Common Pitfalls

### Behavioral Differences

- **Query translation**: EF Core has different LINQ translation behavior; some queries that worked in EF6 may throw or produce different SQL
- **Cascade delete defaults**: EF Core defaults to cascade delete for required relationships; EF6 does not
- **Shadow properties**: EF Core tracks FK values as shadow properties by default; EF6 requires explicit FK properties
- **Global query filters**: EF Core supports them; EF6 does not

### Missing Features in EF Core

Some EF6 features have no direct equivalent:

- EDMX designer and visual model-first workflows
- ObjectContext API (only DbContext is supported)
- Automatic migrations (removed; use explicit migrations)
- Entity SQL (ESQL) query language

### Provider Differences

- Verify your database provider has EF Core support
- Provider feature parity varies; some advanced database features may differ
- Test against the real database, not just in-memory

## Testing Strategy

1. **Characterization tests**: Before migration, write tests that capture current EF6 behavior
2. **SQL diff comparisons**: Compare generated SQL between EF6 and EF Core for critical queries
3. **Integration tests**: Run against a real database with realistic data volumes
4. **Performance baselines**: Measure query performance before and after migration

## References

- [Microsoft Docs: Porting from EF6 to EF Core](https://learn.microsoft.com/en-us/ef/efcore-and-ef6/porting/)
- [Microsoft Docs: EF6 Overview](https://learn.microsoft.com/en-us/ef/ef6/)
- [Microsoft Docs: EF Core Overview](https://learn.microsoft.com/en-us/ef/core/)
