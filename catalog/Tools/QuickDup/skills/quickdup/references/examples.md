# QuickDup Usage Examples

## Scenario 1: Initial Discovery in a Large Solution

**Goal:** Find the worst duplication hotspots across an entire .NET solution.

```bash
# Step 1: Broad scan with standard exclusions
quickdup -path . -ext .cs -exclude "bin/*,obj/*,*.g.cs,*.generated.cs,*.Designer.cs" -top 20

# Step 2: Review the top 5 patterns in detail
quickdup -path . -ext .cs -exclude "bin/*,obj/*,*.g.cs,*.generated.cs,*.Designer.cs" -select 0..4

# Step 3: Export findings for team review
quickdup -path . -ext .cs -exclude "bin/*,obj/*,*.g.cs,*.generated.cs,*.Designer.cs" -output html -o duplicates-report.html
```

## Scenario 2: Domain Layer Audit

**Goal:** Focus on duplication within the Domain or Core project only.

```bash
# Scan the domain layer specifically
quickdup -path src/MyApp.Domain -ext .cs -top 10

# Compare with infrastructure layer
quickdup -path src/MyApp.Infrastructure -ext .cs -top 10
```

## Scenario 3: Pre-Refactoring Assessment

**Goal:** Identify extraction candidates before a planned refactoring sprint.

```bash
# Generate JSON for tooling integration
quickdup -path src -ext .cs -exclude "bin/*,obj/*,Migrations/*" -output json -o .quickdup/baseline.json

# Focus on larger clones that justify extraction
quickdup -path src -ext .cs -exclude "bin/*,obj/*,Migrations/*" -min-lines 15 -min-tokens 150 -top 10
```

## Scenario 4: CI Pipeline Integration

**Goal:** Fail the build when significant new duplication is introduced.

```yaml
# GitHub Actions example
- name: Check for duplicate code
  run: |
    quickdup -path src -ext .cs -exclude "bin/*,obj/*,*.g.cs" -min-lines 10 -output json -o duplicates.json
    # Fail if duplicates exceed threshold (custom script)
    python scripts/check-duplicates.py duplicates.json --max-groups 5
```

```bash
# Local CI simulation
quickdup -path src -ext .cs -exclude "bin/*,obj/*" -min-lines 10
if [ $? -eq 1 ]; then
  echo "Warning: Duplicates detected"
fi
```

## Scenario 5: Incremental Cleanup Workflow

**Goal:** Clean up duplication incrementally over multiple sprints.

```bash
# Sprint 1: Establish baseline and suppress known acceptable duplicates
quickdup -path . -ext .cs -exclude "bin/*,obj/*" -output json -o .quickdup/results.json
# Review and create .quickdup/ignore.json for acceptable patterns

# Sprint 2: Check for new duplicates only
quickdup -path . -ext .cs -exclude "bin/*,obj/*" -top 10
# Refactor the top 3 patterns

# Sprint 3: Re-scan and verify reduction
quickdup -path . -ext .cs -exclude "bin/*,obj/*" -top 10
```

## Scenario 6: Razor and C# Combined Scan

**Goal:** Detect duplication across both C# code and Razor views.

```bash
# Scan both file types
quickdup -path src -ext ".cs,.razor" -exclude "bin/*,obj/*,wwwroot/*"

# Focus on Razor views only
quickdup -path src -ext .razor -exclude "bin/*,obj/*"
```

## Scenario 7: Test Code Audit

**Goal:** Find duplicate test setup or assertion patterns.

```bash
# Scan test projects specifically
quickdup -path tests -ext .cs -exclude "bin/*,obj/*" -top 15

# Look for larger duplicates that suggest missing test helpers
quickdup -path tests -ext .cs -min-lines 20 -top 10
```

## Scenario 8: Migration-Heavy EF Core Project

**Goal:** Scan while excluding EF Core migration noise.

```bash
# Standard exclusion for EF Core projects
quickdup -path src -ext .cs -exclude "bin/*,obj/*,Migrations/*,**/Migrations/*,*.Designer.cs"

# If using multiple DbContexts with separate migration folders
quickdup -path src -ext .cs -exclude "bin/*,obj/*,**/Migrations/*,*.Designer.cs,*.Snapshot.cs"
```

## Scenario 9: Monorepo with Multiple Solutions

**Goal:** Scan specific bounded contexts in a large monorepo.

```bash
# Scan only the Orders bounded context
quickdup -path src/Orders -ext .cs -exclude "bin/*,obj/*" -top 10

# Compare duplication across contexts
quickdup -path src/Orders -ext .cs -exclude "bin/*,obj/*" -output json -o orders-dups.json
quickdup -path src/Inventory -ext .cs -exclude "bin/*,obj/*" -output json -o inventory-dups.json
```

## Scenario 10: Quick Health Check

**Goal:** Fast sanity check before a code review or PR merge.

```bash
# Fast check with strict thresholds
quickdup -path src -ext .cs -exclude "bin/*,obj/*,*.g.cs" -min-lines 15 -top 5

# If clean, proceed; if not, investigate before merging
```

## Common Refactoring Patterns After Detection

### Pattern: Repeated Null Checks

**Before:** Multiple files with identical null-checking logic.

**After:** Extract to a guard helper or use a validation library.

### Pattern: Similar LINQ Queries

**Before:** Duplicate filtering and projection logic across services.

**After:** Extract to a shared query specification or repository method.

### Pattern: Copy-Paste DTOs

**Before:** Identical DTO definitions in multiple projects.

**After:** Move to a shared contracts project or use record inheritance.

### Pattern: Repeated Configuration Builders

**Before:** Similar builder patterns across integration tests.

**After:** Extract to a test fixture base class or builder helper.
