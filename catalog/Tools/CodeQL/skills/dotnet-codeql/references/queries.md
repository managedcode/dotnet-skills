# Common CodeQL Queries for .NET

## Built-in Query Suites

CodeQL ships with pre-built query suites for C#/.NET security analysis.

### Default Security Suites

```yaml
# In your CodeQL workflow
queries:
  - uses: security-extended
  - uses: security-and-quality
```

Available suites for `csharp`:

| Suite | Purpose |
|-------|---------|
| `csharp-code-scanning.qls` | Default code scanning queries |
| `csharp-security-extended.qls` | Extended security queries |
| `csharp-security-and-quality.qls` | Security plus code quality |
| `csharp-security-experimental.qls` | Experimental security queries |

## Common Security Queries

### SQL Injection

Query ID: `cs/sql-injection`

Detects unsanitized user input flowing into SQL queries.

```csharp
// Vulnerable pattern detected:
string query = "SELECT * FROM Users WHERE Name = '" + userInput + "'";
cmd.CommandText = query;

// Safe pattern:
cmd.CommandText = "SELECT * FROM Users WHERE Name = @name";
cmd.Parameters.AddWithValue("@name", userInput);
```

### Path Injection

Query ID: `cs/path-injection`

Detects file path manipulation from user input.

```csharp
// Vulnerable pattern detected:
string path = Path.Combine(basePath, userInput);
File.ReadAllText(path);

// Safe pattern:
string safePath = Path.GetFullPath(Path.Combine(basePath, userInput));
if (!safePath.StartsWith(Path.GetFullPath(basePath)))
    throw new SecurityException("Path traversal detected");
```

### Cross-Site Scripting (XSS)

Query ID: `cs/web/xss`

Detects unencoded user input in web responses.

```csharp
// Vulnerable pattern detected:
Response.Write(userInput);

// Safe pattern:
Response.Write(HttpUtility.HtmlEncode(userInput));
```

### Insecure Deserialization

Query ID: `cs/unsafe-deserialization-untrusted-input`

Detects dangerous deserialization of untrusted data.

```csharp
// Vulnerable pattern detected:
BinaryFormatter formatter = new BinaryFormatter();
object obj = formatter.Deserialize(untrustedStream);

// Safe pattern:
// Use System.Text.Json or explicitly typed serializers
var obj = JsonSerializer.Deserialize<MyType>(jsonString);
```

### Hardcoded Credentials

Query ID: `cs/hardcoded-credentials`

Detects passwords and secrets in source code.

```csharp
// Vulnerable pattern detected:
string connectionString = "Server=db;Password=secret123;";

// Safe pattern:
string connectionString = configuration.GetConnectionString("Default");
```

### LDAP Injection

Query ID: `cs/ldap-injection`

Detects unsanitized input in LDAP queries.

```csharp
// Vulnerable pattern detected:
string filter = "(uid=" + userInput + ")";
searcher.Filter = filter;

// Safe pattern:
string safeInput = userInput.Replace("\\", "\\5c").Replace("*", "\\2a");
string filter = "(uid=" + safeInput + ")";
```

### Command Injection

Query ID: `cs/command-line-injection`

Detects OS command injection vulnerabilities.

```csharp
// Vulnerable pattern detected:
Process.Start("cmd.exe", "/c " + userInput);

// Safe pattern:
var psi = new ProcessStartInfo("myapp.exe");
psi.ArgumentList.Add(userInput);  // Properly escaped
Process.Start(psi);
```

### XML External Entity (XXE)

Query ID: `cs/xml/insecure-dtd-handling`

Detects insecure XML parsing configurations.

```csharp
// Vulnerable pattern detected:
XmlReaderSettings settings = new XmlReaderSettings();
settings.DtdProcessing = DtdProcessing.Parse;

// Safe pattern:
XmlReaderSettings settings = new XmlReaderSettings();
settings.DtdProcessing = DtdProcessing.Prohibit;
settings.XmlResolver = null;
```

## Running Custom Queries

### CLI Query Execution

```bash
# Run a specific query
codeql query run path/to/query.ql --database=my-csharp-db

# Run a query suite
codeql database analyze my-csharp-db csharp-security-extended.qls \
  --format=sarif-latest \
  --output=results.sarif
```

### Query Pack Installation

```bash
# Download standard query packs
codeql pack download codeql/csharp-queries

# List available queries
codeql resolve queries codeql/csharp-queries
```

## Custom Query Example

Create a custom query to find specific patterns:

```ql
/**
 * @name Find Console.WriteLine calls
 * @description Finds all Console.WriteLine method calls
 * @kind problem
 * @problem.severity recommendation
 * @id custom/find-console-writeline
 */

import csharp

from MethodCall mc
where mc.getTarget().hasQualifiedName("System.Console", "WriteLine")
select mc, "Console.WriteLine call found"
```

Save as `custom-queries/find-console.ql` and run:

```bash
codeql query run custom-queries/find-console.ql --database=my-csharp-db
```

## Filtering Results

### Severity Levels

- `error` - Critical security issues
- `warning` - Potential security concerns
- `recommendation` - Code quality improvements
- `note` - Informational findings

### Excluding False Positives

Create a `.github/codeql/codeql-config.yml`:

```yaml
name: "Custom CodeQL Config"

queries:
  - uses: security-extended

paths-ignore:
  - "**/Tests/**"
  - "**/test/**"
  - "**/*.Designer.cs"
  - "**/Migrations/**"

query-filters:
  - exclude:
      id: cs/hardcoded-credentials
      tags contain: test
```

## Sources

- [CodeQL for C# documentation](https://codeql.github.com/docs/codeql-language-guides/codeql-for-csharp/)
- [C# CodeQL queries on GitHub](https://github.com/github/codeql/tree/main/csharp/ql/src)
- [CodeQL query help](https://codeql.github.com/codeql-query-help/csharp/)
