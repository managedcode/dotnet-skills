# Common Analyzer Rule Categories and Severities

This reference covers the most frequently configured analyzer rules organized by category.

## Severity Levels

| Severity | Build Behavior | IDE Behavior |
|----------|----------------|--------------|
| `error` | Fails build | Red squiggle |
| `warning` | Produces warning | Yellow squiggle |
| `suggestion` | No build output | Gray dots |
| `silent` | No build output | No visual |
| `none` | Disabled | No visual |
| `default` | Use analyzer default | Use analyzer default |

## Bulk Severity Controls

Set baseline for entire categories:

```ini
[*.cs]
# All analyzers to warning by default
dotnet_analyzer_diagnostic.severity = warning

# All code quality analyzers
dotnet_analyzer_diagnostic.category-Design.severity = warning
dotnet_analyzer_diagnostic.category-Globalization.severity = warning
dotnet_analyzer_diagnostic.category-Interoperability.severity = warning
dotnet_analyzer_diagnostic.category-Maintainability.severity = warning
dotnet_analyzer_diagnostic.category-Naming.severity = warning
dotnet_analyzer_diagnostic.category-Performance.severity = warning
dotnet_analyzer_diagnostic.category-Reliability.severity = warning
dotnet_analyzer_diagnostic.category-Security.severity = error
dotnet_analyzer_diagnostic.category-Usage.severity = warning

# All code style analyzers
dotnet_analyzer_diagnostic.category-Style.severity = suggestion
```

## Code Quality Rules (CA)

### Design Rules (CA1xxx)

```ini
[*.cs]
# Do not declare static members on generic types
dotnet_diagnostic.CA1000.severity = warning

# Types that own disposable fields should be disposable
dotnet_diagnostic.CA1001.severity = warning

# Do not expose generic lists
dotnet_diagnostic.CA1002.severity = warning

# Use generic event handler instances
dotnet_diagnostic.CA1003.severity = warning

# Avoid excessive parameters on generic types
dotnet_diagnostic.CA1005.severity = warning

# Enums should have zero value
dotnet_diagnostic.CA1008.severity = warning

# Collections should implement generic interface
dotnet_diagnostic.CA1010.severity = warning

# Abstract types should not have public constructors
dotnet_diagnostic.CA1012.severity = warning

# Mark assemblies with CLSCompliant
dotnet_diagnostic.CA1014.severity = none

# Mark assemblies with AssemblyVersion
dotnet_diagnostic.CA1016.severity = warning

# Mark assemblies with ComVisible
dotnet_diagnostic.CA1017.severity = none

# Mark attributes with AttributeUsageAttribute
dotnet_diagnostic.CA1018.severity = warning

# Define accessors for attribute arguments
dotnet_diagnostic.CA1019.severity = warning

# Avoid out parameters
dotnet_diagnostic.CA1021.severity = suggestion

# Use properties where appropriate
dotnet_diagnostic.CA1024.severity = suggestion

# Mark enums with FlagsAttribute
dotnet_diagnostic.CA1027.severity = warning

# Enum storage should be Int32
dotnet_diagnostic.CA1028.severity = warning

# Use events where appropriate
dotnet_diagnostic.CA1030.severity = suggestion

# Do not catch general exception types
dotnet_diagnostic.CA1031.severity = warning

# Implement standard exception constructors
dotnet_diagnostic.CA1032.severity = warning

# Interface methods should be callable by child types
dotnet_diagnostic.CA1033.severity = warning

# Nested types should not be visible
dotnet_diagnostic.CA1034.severity = suggestion

# Override methods on comparable types
dotnet_diagnostic.CA1036.severity = warning

# Avoid empty interfaces
dotnet_diagnostic.CA1040.severity = warning

# Provide ObsoleteAttribute message
dotnet_diagnostic.CA1041.severity = warning

# Use integral or string argument for indexers
dotnet_diagnostic.CA1043.severity = warning

# Properties should not be write only
dotnet_diagnostic.CA1044.severity = warning

# Do not pass types by reference
dotnet_diagnostic.CA1045.severity = warning

# Do not overload operator equals on reference types
dotnet_diagnostic.CA1046.severity = warning

# Do not declare protected members in sealed types
dotnet_diagnostic.CA1047.severity = warning

# Do not declare virtual members in sealed types
dotnet_diagnostic.CA1048.severity = warning

# Types that own native resources should be disposable
dotnet_diagnostic.CA1049.severity = warning

# Declare types in namespaces
dotnet_diagnostic.CA1050.severity = warning

# Do not declare visible instance fields
dotnet_diagnostic.CA1051.severity = warning

# Static holder types should be sealed
dotnet_diagnostic.CA1052.severity = warning

# Static holder types should not have constructors
dotnet_diagnostic.CA1053.severity = warning

# URI parameters should not be strings
dotnet_diagnostic.CA1054.severity = suggestion

# URI return values should not be strings
dotnet_diagnostic.CA1055.severity = suggestion

# URI properties should not be strings
dotnet_diagnostic.CA1056.severity = suggestion

# Types should not extend certain base types
dotnet_diagnostic.CA1058.severity = warning

# Move pinvokes to native methods class
dotnet_diagnostic.CA1060.severity = suggestion

# Do not hide base class methods
dotnet_diagnostic.CA1061.severity = warning

# Validate arguments of public methods
dotnet_diagnostic.CA1062.severity = warning

# Implement IDisposable correctly
dotnet_diagnostic.CA1063.severity = warning

# Exceptions should be public
dotnet_diagnostic.CA1064.severity = warning

# Do not raise exceptions in unexpected locations
dotnet_diagnostic.CA1065.severity = warning

# Implement IEquatable when overriding Equals
dotnet_diagnostic.CA1066.severity = warning

# Override Equals when implementing IEquatable
dotnet_diagnostic.CA1067.severity = warning

# CancellationToken parameters must come last
dotnet_diagnostic.CA1068.severity = warning

# Enums should not have duplicate values
dotnet_diagnostic.CA1069.severity = warning

# Do not declare event fields as virtual
dotnet_diagnostic.CA1070.severity = warning
```

### Globalization Rules (CA13xx)

```ini
[*.cs]
# Specify CultureInfo
dotnet_diagnostic.CA1304.severity = warning

# Specify IFormatProvider
dotnet_diagnostic.CA1305.severity = warning

# Specify StringComparison for clarity
dotnet_diagnostic.CA1307.severity = warning

# Normalize strings to uppercase
dotnet_diagnostic.CA1308.severity = warning

# Use ordinal StringComparison
dotnet_diagnostic.CA1309.severity = warning

# Specify StringComparison for correctness
dotnet_diagnostic.CA1310.severity = warning

# Specify a culture or use invariant
dotnet_diagnostic.CA1311.severity = warning
```

### Maintainability Rules (CA15xx)

```ini
[*.cs]
# Avoid excessive inheritance
dotnet_diagnostic.CA1501.severity = warning

# Avoid excessive complexity
dotnet_diagnostic.CA1502.severity = warning

# Avoid unmaintainable code
dotnet_diagnostic.CA1505.severity = warning

# Avoid excessive class coupling
dotnet_diagnostic.CA1506.severity = warning

# Use nameof in place of string
dotnet_diagnostic.CA1507.severity = warning

# Avoid dead conditional code
dotnet_diagnostic.CA1508.severity = warning

# Invalid entry in code metrics config
dotnet_diagnostic.CA1509.severity = warning

# Avoid redundant length argument
dotnet_diagnostic.CA1514.severity = suggestion
```

### Naming Rules (CA17xx)

```ini
[*.cs]
# Identifiers should not contain underscores
dotnet_diagnostic.CA1707.severity = warning

# Identifiers should differ by more than case
dotnet_diagnostic.CA1708.severity = warning

# Identifiers should have correct suffix
dotnet_diagnostic.CA1710.severity = suggestion

# Identifiers should not have incorrect suffix
dotnet_diagnostic.CA1711.severity = suggestion

# Do not prefix enum values with type name
dotnet_diagnostic.CA1712.severity = warning

# Events should not have before or after prefix
dotnet_diagnostic.CA1713.severity = warning

# Flags enums should have plural names
dotnet_diagnostic.CA1714.severity = warning

# Identifiers should have correct prefix
dotnet_diagnostic.CA1715.severity = warning

# Identifiers should not match keywords
dotnet_diagnostic.CA1716.severity = warning

# Only FlagsAttribute enums should have plural names
dotnet_diagnostic.CA1717.severity = warning

# Identifier contains type name
dotnet_diagnostic.CA1720.severity = suggestion

# Property names should not match get methods
dotnet_diagnostic.CA1721.severity = warning

# Type names should not match namespaces
dotnet_diagnostic.CA1724.severity = warning

# Parameter names should match base declaration
dotnet_diagnostic.CA1725.severity = warning

# Use PascalCase for named placeholders
dotnet_diagnostic.CA1727.severity = suggestion
```

### Performance Rules (CA18xx)

```ini
[*.cs]
# Initialize reference type static fields inline
dotnet_diagnostic.CA1810.severity = warning

# Avoid uninstantiated internal classes
dotnet_diagnostic.CA1812.severity = warning

# Avoid unsealed attributes
dotnet_diagnostic.CA1813.severity = suggestion

# Prefer jagged arrays over multidimensional
dotnet_diagnostic.CA1814.severity = suggestion

# Override equals and operator equals on value types
dotnet_diagnostic.CA1815.severity = warning

# Dispose methods should call SuppressFinalize
dotnet_diagnostic.CA1816.severity = warning

# Properties should not return arrays
dotnet_diagnostic.CA1819.severity = warning

# Test for empty strings using string length
dotnet_diagnostic.CA1820.severity = warning

# Remove empty finalizers
dotnet_diagnostic.CA1821.severity = warning

# Mark members as static
dotnet_diagnostic.CA1822.severity = suggestion

# Avoid unused private fields
dotnet_diagnostic.CA1823.severity = warning

# Mark assemblies with NeutralResourcesLanguageAttribute
dotnet_diagnostic.CA1824.severity = warning

# Avoid zero-length array allocations
dotnet_diagnostic.CA1825.severity = warning

# Do not use Enumerable methods on indexable collections
dotnet_diagnostic.CA1826.severity = warning

# Do not use Count/LongCount when Any can be used
dotnet_diagnostic.CA1827.severity = warning

# Do not use CountAsync/LongCountAsync when AnyAsync can be used
dotnet_diagnostic.CA1828.severity = warning

# Use Length/Count property instead of Enumerable.Count
dotnet_diagnostic.CA1829.severity = warning

# Prefer strongly typed Append and Insert methods on StringBuilder
dotnet_diagnostic.CA1830.severity = warning

# Use AsSpan instead of Range-based indexers for string
dotnet_diagnostic.CA1831.severity = warning

# Use AsSpan or AsMemory instead of Range-based indexers
dotnet_diagnostic.CA1832.severity = warning
dotnet_diagnostic.CA1833.severity = warning

# Use StringBuilder.Append(char) for single-character strings
dotnet_diagnostic.CA1834.severity = warning

# Prefer IsEmpty over Count when available
dotnet_diagnostic.CA1836.severity = warning

# Use Environment.ProcessId instead of Process.GetCurrentProcess().Id
dotnet_diagnostic.CA1837.severity = warning

# Avoid StringBuilder.Append for concatenated strings
dotnet_diagnostic.CA1838.severity = warning

# Use Environment.ProcessPath instead of Process.GetCurrentProcess().MainModule.FileName
dotnet_diagnostic.CA1839.severity = warning

# Use Environment.CurrentManagedThreadId
dotnet_diagnostic.CA1840.severity = warning

# Prefer Dictionary Contains methods
dotnet_diagnostic.CA1841.severity = warning

# Do not use WhenAll with a single task
dotnet_diagnostic.CA1842.severity = warning

# Do not use WaitAll with a single task
dotnet_diagnostic.CA1843.severity = warning

# Provide memory-based overrides of async methods when subclassing Stream
dotnet_diagnostic.CA1844.severity = warning

# Use span-based String.Concat
dotnet_diagnostic.CA1845.severity = warning

# Prefer AsSpan over Substring
dotnet_diagnostic.CA1846.severity = warning

# Use string.Contains(char) instead of string.Contains(string)
dotnet_diagnostic.CA1847.severity = warning

# Use the LoggerMessage delegates
dotnet_diagnostic.CA1848.severity = suggestion

# Call async methods when in async method
dotnet_diagnostic.CA1849.severity = warning

# Prefer static HashData method over ComputeHash
dotnet_diagnostic.CA1850.severity = suggestion

# Possible multiple enumerations of IEnumerable collection
dotnet_diagnostic.CA1851.severity = warning

# Seal internal types
dotnet_diagnostic.CA1852.severity = suggestion

# Unnecessary call to Dictionary.ContainsKey(key)
dotnet_diagnostic.CA1853.severity = warning

# Prefer the IDictionary.TryGetValue(TKey, out TValue) method
dotnet_diagnostic.CA1854.severity = warning

# Use Span.Clear instead of Span.Fill(default)
dotnet_diagnostic.CA1855.severity = warning

# Use StartsWith instead of IndexOf
dotnet_diagnostic.CA1858.severity = warning

# Avoid using Enumerable.Any extension method
dotnet_diagnostic.CA1860.severity = suggestion

# Avoid constant arrays as arguments
dotnet_diagnostic.CA1861.severity = warning

# Use the StringComparison method overloads
dotnet_diagnostic.CA1862.severity = warning

# Use CompositeFormat
dotnet_diagnostic.CA1863.severity = suggestion

# Prefer the IDictionary.TryAdd(TKey, TValue) method
dotnet_diagnostic.CA1864.severity = warning

# Use char overload
dotnet_diagnostic.CA1865.severity = warning
dotnet_diagnostic.CA1866.severity = warning
dotnet_diagnostic.CA1867.severity = warning

# Unnecessary call to Contains for sets
dotnet_diagnostic.CA1868.severity = warning

# Cache and reuse JsonSerializerOptions instances
dotnet_diagnostic.CA1869.severity = warning

# Use a cached SearchValues instance
dotnet_diagnostic.CA1870.severity = suggestion
```

### Reliability Rules (CA20xx)

```ini
[*.cs]
# Dispose objects before losing scope
dotnet_diagnostic.CA2000.severity = warning

# Do not directly await a Task without ConfigureAwait
dotnet_diagnostic.CA2007.severity = warning

# Do not create tasks without passing a TaskScheduler
dotnet_diagnostic.CA2008.severity = warning

# Do not call ToImmutableCollection on an ImmutableCollection value
dotnet_diagnostic.CA2009.severity = warning

# Always consume the value returned by methods marked with PreserveSigAttribute
dotnet_diagnostic.CA2010.severity = warning

# Do not assign property within its setter
dotnet_diagnostic.CA2011.severity = warning

# Use ValueTasks correctly
dotnet_diagnostic.CA2012.severity = warning

# Do not use ReferenceEquals with value types
dotnet_diagnostic.CA2013.severity = warning

# Do not use stackalloc in loops
dotnet_diagnostic.CA2014.severity = warning

# Do not define finalizers for types derived from MemoryManager<T>
dotnet_diagnostic.CA2015.severity = warning

# Forward CancellationToken to methods that take one
dotnet_diagnostic.CA2016.severity = warning

# Parameter count mismatch
dotnet_diagnostic.CA2017.severity = warning

# The count argument to Buffer.BlockCopy should specify the number of bytes
dotnet_diagnostic.CA2018.severity = warning

# ThreadStatic fields should not use inline initialization
dotnet_diagnostic.CA2019.severity = warning

# Prevent behavioral change
dotnet_diagnostic.CA2020.severity = warning

# Do not call problematic methods
dotnet_diagnostic.CA2021.severity = warning
```

### Security Rules (CA21xx, CA31xx, CA51xx-CA55xx)

```ini
[*.cs]
# Review SQL queries for security vulnerabilities
dotnet_diagnostic.CA2100.severity = warning

# Specify marshaling for P/Invoke string arguments
dotnet_diagnostic.CA2101.severity = warning

# Seal methods that satisfy private interfaces
dotnet_diagnostic.CA2119.severity = warning

# Do not catch corrupted state exceptions
dotnet_diagnostic.CA2153.severity = error

# Do not disable certificate validation
dotnet_diagnostic.CA5359.severity = error

# Do not call dangerous methods in deserialization
dotnet_diagnostic.CA5360.severity = error

# Do not disable SChannel use of strong crypto
dotnet_diagnostic.CA5361.severity = error

# Potential reference cycle in deserialized object graph
dotnet_diagnostic.CA5362.severity = warning

# Do not disable request validation
dotnet_diagnostic.CA5363.severity = error

# Do not use deprecated security protocols
dotnet_diagnostic.CA5364.severity = error

# Do not disable HTTP header checking
dotnet_diagnostic.CA5365.severity = error

# Use XmlReader for DataSet.ReadXml
dotnet_diagnostic.CA5366.severity = warning

# Do not serialize types with pointer fields
dotnet_diagnostic.CA5367.severity = warning

# Set ViewStateUserKey for classes derived from Page
dotnet_diagnostic.CA5368.severity = warning

# Use XmlReader for deserialize
dotnet_diagnostic.CA5369.severity = warning

# Use XmlReader for validating reader
dotnet_diagnostic.CA5370.severity = warning

# Use XmlReader for schema read
dotnet_diagnostic.CA5371.severity = warning

# Use XmlReader for XPathDocument
dotnet_diagnostic.CA5372.severity = warning

# Do not use obsolete key derivation function
dotnet_diagnostic.CA5373.severity = error

# Do not use XslTransform
dotnet_diagnostic.CA5374.severity = warning

# Do not use account shared access signature
dotnet_diagnostic.CA5375.severity = warning

# Use SharedAccessProtocol HttpsOnly
dotnet_diagnostic.CA5376.severity = warning

# Use container level access policy
dotnet_diagnostic.CA5377.severity = warning

# Do not disable ServicePointManagerSecurityProtocols
dotnet_diagnostic.CA5378.severity = error

# Ensure key derivation function algorithm is sufficiently strong
dotnet_diagnostic.CA5379.severity = error

# Do not add certificates to root store
dotnet_diagnostic.CA5380.severity = warning

# Ensure certificates are not added to root store
dotnet_diagnostic.CA5381.severity = warning

# Use secure cookies in ASP.NET Core
dotnet_diagnostic.CA5382.severity = warning

# Ensure use secure cookies in ASP.NET Core
dotnet_diagnostic.CA5383.severity = warning

# Do not use DSA
dotnet_diagnostic.CA5384.severity = error

# Use Rivest-Shamir-Adleman (RSA) algorithm with sufficient key size
dotnet_diagnostic.CA5385.severity = error

# Avoid hardcoding SecurityProtocolType value
dotnet_diagnostic.CA5386.severity = warning

# Do not use weak key derivation function with insufficient iteration count
dotnet_diagnostic.CA5387.severity = error

# Ensure sufficient iteration count when using weak key derivation function
dotnet_diagnostic.CA5388.severity = error

# Do not add archive item path to target filesystem path
dotnet_diagnostic.CA5389.severity = warning

# Do not hard-code encryption key
dotnet_diagnostic.CA5390.severity = error

# Use antiforgery tokens in ASP.NET Core MVC controllers
dotnet_diagnostic.CA5391.severity = warning

# Use DefaultDllImportSearchPaths attribute for P/Invokes
dotnet_diagnostic.CA5392.severity = warning

# Do not use unsafe DllImportSearchPath value
dotnet_diagnostic.CA5393.severity = warning

# Do not use insecure randomness
dotnet_diagnostic.CA5394.severity = warning

# Miss HttpVerb attribute for action methods
dotnet_diagnostic.CA5395.severity = warning

# Set HttpOnly to true for HttpCookie
dotnet_diagnostic.CA5396.severity = warning

# Do not use deprecated SslProtocols values
dotnet_diagnostic.CA5397.severity = error

# Avoid hardcoded SslProtocols values
dotnet_diagnostic.CA5398.severity = warning

# Definitely disable HttpClient certificate revocation list check
dotnet_diagnostic.CA5399.severity = warning

# Ensure HttpClient certificate revocation list check is not disabled
dotnet_diagnostic.CA5400.severity = warning

# Do not use CreateEncryptor with non-default IV
dotnet_diagnostic.CA5401.severity = warning

# Use CreateEncryptor with the default IV
dotnet_diagnostic.CA5402.severity = warning

# Do not hard-code certificate
dotnet_diagnostic.CA5403.severity = warning

# Do not disable token validation checks
dotnet_diagnostic.CA5404.severity = warning

# Do not always skip token validation in delegates
dotnet_diagnostic.CA5405.severity = warning
```

### Usage Rules (CA22xx)

```ini
[*.cs]
# Provide correct arguments to formatting methods
dotnet_diagnostic.CA2241.severity = warning

# Test for NaN correctly
dotnet_diagnostic.CA2242.severity = warning

# Attribute string literals should parse correctly
dotnet_diagnostic.CA2243.severity = warning

# Do not duplicate indexed element initializations
dotnet_diagnostic.CA2244.severity = warning

# Do not assign a property to itself
dotnet_diagnostic.CA2245.severity = warning

# Assigning symbol and its member in the same statement
dotnet_diagnostic.CA2246.severity = warning

# Argument passed to TaskCompletionSource constructor
dotnet_diagnostic.CA2247.severity = warning

# Provide correct enum argument to Enum.HasFlag
dotnet_diagnostic.CA2248.severity = warning

# Consider using String.Contains instead of String.IndexOf
dotnet_diagnostic.CA2249.severity = suggestion

# Use ThrowIfCancellationRequested
dotnet_diagnostic.CA2250.severity = suggestion

# Use String.Equals over String.Compare
dotnet_diagnostic.CA2251.severity = suggestion

# Named placeholders should not be numeric values
dotnet_diagnostic.CA2252.severity = warning

# Named placeholders should not be numeric values
dotnet_diagnostic.CA2253.severity = warning

# Template should be a static expression
dotnet_diagnostic.CA2254.severity = warning

# The ModuleInitializer attribute should not be used in libraries
dotnet_diagnostic.CA2255.severity = warning

# All members declared in parent interfaces must have an implementation
dotnet_diagnostic.CA2256.severity = warning

# Members defined on an interface with DynamicInterfaceCastableImplementationAttribute should be static
dotnet_diagnostic.CA2257.severity = warning

# Providing a DynamicInterfaceCastableImplementation interface in Visual Basic is unsupported
dotnet_diagnostic.CA2258.severity = warning

# Ensure ThreadStatic is only used with static fields
dotnet_diagnostic.CA2259.severity = warning

# Implement generic math interfaces correctly
dotnet_diagnostic.CA2260.severity = warning

# Do not use ConfigureAwaitOptions.SuppressThrowing with Task<TResult>
dotnet_diagnostic.CA2261.severity = warning
```

## StyleCop Analyzers (SA)

```ini
[*.cs]
# Spacing rules
dotnet_diagnostic.SA1000.severity = warning
dotnet_diagnostic.SA1001.severity = warning
dotnet_diagnostic.SA1002.severity = warning
dotnet_diagnostic.SA1003.severity = warning
dotnet_diagnostic.SA1004.severity = warning
dotnet_diagnostic.SA1005.severity = warning
dotnet_diagnostic.SA1006.severity = warning
dotnet_diagnostic.SA1007.severity = warning
dotnet_diagnostic.SA1008.severity = warning
dotnet_diagnostic.SA1009.severity = warning
dotnet_diagnostic.SA1010.severity = warning
dotnet_diagnostic.SA1011.severity = warning
dotnet_diagnostic.SA1012.severity = warning
dotnet_diagnostic.SA1013.severity = warning

# Readability rules
dotnet_diagnostic.SA1100.severity = warning
dotnet_diagnostic.SA1101.severity = none
dotnet_diagnostic.SA1102.severity = warning
dotnet_diagnostic.SA1103.severity = warning
dotnet_diagnostic.SA1104.severity = warning
dotnet_diagnostic.SA1105.severity = warning
dotnet_diagnostic.SA1106.severity = warning
dotnet_diagnostic.SA1107.severity = warning
dotnet_diagnostic.SA1108.severity = warning
dotnet_diagnostic.SA1110.severity = warning
dotnet_diagnostic.SA1111.severity = warning
dotnet_diagnostic.SA1112.severity = warning
dotnet_diagnostic.SA1113.severity = warning
dotnet_diagnostic.SA1114.severity = warning
dotnet_diagnostic.SA1115.severity = warning
dotnet_diagnostic.SA1116.severity = warning
dotnet_diagnostic.SA1117.severity = warning
dotnet_diagnostic.SA1118.severity = warning
dotnet_diagnostic.SA1119.severity = warning
dotnet_diagnostic.SA1120.severity = warning
dotnet_diagnostic.SA1121.severity = warning
dotnet_diagnostic.SA1122.severity = warning
dotnet_diagnostic.SA1123.severity = warning
dotnet_diagnostic.SA1124.severity = warning
dotnet_diagnostic.SA1125.severity = warning
dotnet_diagnostic.SA1127.severity = warning
dotnet_diagnostic.SA1128.severity = warning
dotnet_diagnostic.SA1129.severity = warning
dotnet_diagnostic.SA1130.severity = warning
dotnet_diagnostic.SA1131.severity = warning
dotnet_diagnostic.SA1132.severity = warning
dotnet_diagnostic.SA1133.severity = warning
dotnet_diagnostic.SA1134.severity = warning
dotnet_diagnostic.SA1135.severity = warning
dotnet_diagnostic.SA1136.severity = warning
dotnet_diagnostic.SA1137.severity = warning
dotnet_diagnostic.SA1139.severity = warning

# Ordering rules
dotnet_diagnostic.SA1200.severity = none
dotnet_diagnostic.SA1201.severity = warning
dotnet_diagnostic.SA1202.severity = warning
dotnet_diagnostic.SA1203.severity = warning
dotnet_diagnostic.SA1204.severity = warning
dotnet_diagnostic.SA1205.severity = warning
dotnet_diagnostic.SA1206.severity = warning
dotnet_diagnostic.SA1207.severity = warning
dotnet_diagnostic.SA1208.severity = warning
dotnet_diagnostic.SA1209.severity = warning
dotnet_diagnostic.SA1210.severity = warning
dotnet_diagnostic.SA1211.severity = warning
dotnet_diagnostic.SA1212.severity = warning
dotnet_diagnostic.SA1213.severity = warning
dotnet_diagnostic.SA1214.severity = warning
dotnet_diagnostic.SA1216.severity = warning
dotnet_diagnostic.SA1217.severity = warning

# Naming rules
dotnet_diagnostic.SA1300.severity = warning
dotnet_diagnostic.SA1301.severity = none
dotnet_diagnostic.SA1302.severity = warning
dotnet_diagnostic.SA1303.severity = warning
dotnet_diagnostic.SA1304.severity = warning
dotnet_diagnostic.SA1305.severity = none
dotnet_diagnostic.SA1306.severity = warning
dotnet_diagnostic.SA1307.severity = warning
dotnet_diagnostic.SA1308.severity = warning
dotnet_diagnostic.SA1309.severity = none
dotnet_diagnostic.SA1310.severity = warning
dotnet_diagnostic.SA1311.severity = warning
dotnet_diagnostic.SA1312.severity = warning
dotnet_diagnostic.SA1313.severity = warning
dotnet_diagnostic.SA1314.severity = warning

# Maintainability rules
dotnet_diagnostic.SA1400.severity = warning
dotnet_diagnostic.SA1401.severity = warning
dotnet_diagnostic.SA1402.severity = warning
dotnet_diagnostic.SA1403.severity = warning
dotnet_diagnostic.SA1404.severity = warning
dotnet_diagnostic.SA1405.severity = warning
dotnet_diagnostic.SA1406.severity = warning
dotnet_diagnostic.SA1407.severity = warning
dotnet_diagnostic.SA1408.severity = warning
dotnet_diagnostic.SA1410.severity = warning
dotnet_diagnostic.SA1411.severity = warning
dotnet_diagnostic.SA1412.severity = none
dotnet_diagnostic.SA1413.severity = none

# Layout rules
dotnet_diagnostic.SA1500.severity = warning
dotnet_diagnostic.SA1501.severity = warning
dotnet_diagnostic.SA1502.severity = warning
dotnet_diagnostic.SA1503.severity = warning
dotnet_diagnostic.SA1504.severity = warning
dotnet_diagnostic.SA1505.severity = warning
dotnet_diagnostic.SA1506.severity = warning
dotnet_diagnostic.SA1507.severity = warning
dotnet_diagnostic.SA1508.severity = warning
dotnet_diagnostic.SA1509.severity = warning
dotnet_diagnostic.SA1510.severity = warning
dotnet_diagnostic.SA1511.severity = warning
dotnet_diagnostic.SA1512.severity = warning
dotnet_diagnostic.SA1513.severity = warning
dotnet_diagnostic.SA1514.severity = warning
dotnet_diagnostic.SA1515.severity = warning
dotnet_diagnostic.SA1516.severity = warning
dotnet_diagnostic.SA1517.severity = warning
dotnet_diagnostic.SA1518.severity = warning
dotnet_diagnostic.SA1519.severity = warning
dotnet_diagnostic.SA1520.severity = warning

# Documentation rules
dotnet_diagnostic.SA1600.severity = suggestion
dotnet_diagnostic.SA1601.severity = suggestion
dotnet_diagnostic.SA1602.severity = suggestion
dotnet_diagnostic.SA1604.severity = suggestion
dotnet_diagnostic.SA1605.severity = suggestion
dotnet_diagnostic.SA1606.severity = suggestion
dotnet_diagnostic.SA1607.severity = suggestion
dotnet_diagnostic.SA1608.severity = suggestion
dotnet_diagnostic.SA1609.severity = none
dotnet_diagnostic.SA1610.severity = suggestion
dotnet_diagnostic.SA1611.severity = suggestion
dotnet_diagnostic.SA1612.severity = suggestion
dotnet_diagnostic.SA1613.severity = suggestion
dotnet_diagnostic.SA1614.severity = suggestion
dotnet_diagnostic.SA1615.severity = suggestion
dotnet_diagnostic.SA1616.severity = suggestion
dotnet_diagnostic.SA1617.severity = suggestion
dotnet_diagnostic.SA1618.severity = suggestion
dotnet_diagnostic.SA1619.severity = suggestion
dotnet_diagnostic.SA1620.severity = suggestion
dotnet_diagnostic.SA1621.severity = suggestion
dotnet_diagnostic.SA1622.severity = suggestion
dotnet_diagnostic.SA1623.severity = suggestion
dotnet_diagnostic.SA1624.severity = suggestion
dotnet_diagnostic.SA1625.severity = suggestion
dotnet_diagnostic.SA1626.severity = suggestion
dotnet_diagnostic.SA1627.severity = suggestion
dotnet_diagnostic.SA1629.severity = suggestion
dotnet_diagnostic.SA1633.severity = none
dotnet_diagnostic.SA1634.severity = none
dotnet_diagnostic.SA1635.severity = none
dotnet_diagnostic.SA1636.severity = none
dotnet_diagnostic.SA1637.severity = none
dotnet_diagnostic.SA1638.severity = none
dotnet_diagnostic.SA1639.severity = none
dotnet_diagnostic.SA1640.severity = none
dotnet_diagnostic.SA1641.severity = none
dotnet_diagnostic.SA1642.severity = suggestion
dotnet_diagnostic.SA1643.severity = suggestion
dotnet_diagnostic.SA1648.severity = suggestion
dotnet_diagnostic.SA1649.severity = warning
dotnet_diagnostic.SA1651.severity = warning
```

## Roslynator Analyzers (RCS)

```ini
[*.cs]
# Add braces
dotnet_diagnostic.RCS1001.severity = suggestion

# Remove braces
dotnet_diagnostic.RCS1002.severity = none

# Add braces to if-else
dotnet_diagnostic.RCS1003.severity = suggestion

# Remove braces from if-else
dotnet_diagnostic.RCS1004.severity = none

# Simplify nested using statement
dotnet_diagnostic.RCS1005.severity = suggestion

# Merge else clause with nested if
dotnet_diagnostic.RCS1006.severity = suggestion

# Add braces
dotnet_diagnostic.RCS1007.severity = none

# Use predefined type
dotnet_diagnostic.RCS1013.severity = suggestion

# Use explicitly typed array
dotnet_diagnostic.RCS1014.severity = none

# Use nameof operator
dotnet_diagnostic.RCS1015.severity = warning

# Convert block body to expression-body
dotnet_diagnostic.RCS1016.severity = none

# Convert expression-body to block body
dotnet_diagnostic.RCS1017.severity = none

# Add accessibility modifiers
dotnet_diagnostic.RCS1018.severity = warning

# Order modifiers
dotnet_diagnostic.RCS1019.severity = suggestion

# Simplify Nullable<T> to T?
dotnet_diagnostic.RCS1020.severity = suggestion

# Convert lambda expression body to expression-body
dotnet_diagnostic.RCS1021.severity = suggestion

# Remove unnecessary parentheses
dotnet_diagnostic.RCS1032.severity = suggestion

# Remove redundant boolean literal
dotnet_diagnostic.RCS1033.severity = warning

# Remove redundant sealed modifier
dotnet_diagnostic.RCS1034.severity = suggestion

# Remove unnecessary blank line
dotnet_diagnostic.RCS1036.severity = suggestion

# Remove trailing white-space
dotnet_diagnostic.RCS1037.severity = suggestion

# Remove empty statement
dotnet_diagnostic.RCS1038.severity = warning

# Remove argument list from attribute
dotnet_diagnostic.RCS1039.severity = suggestion

# Use var instead of explicit type (when type is obvious)
dotnet_diagnostic.RCS1040.severity = none

# Remove empty initializer
dotnet_diagnostic.RCS1041.severity = warning

# Remove enum default underlying type
dotnet_diagnostic.RCS1042.severity = suggestion

# Remove partial modifier from type with a single part
dotnet_diagnostic.RCS1043.severity = suggestion

# Asynchronous method name should end with Async
dotnet_diagnostic.RCS1046.severity = none

# Non-asynchronous method name should not end with Async
dotnet_diagnostic.RCS1047.severity = warning

# Use lambda expression instead of anonymous method
dotnet_diagnostic.RCS1048.severity = suggestion

# Simplify boolean comparison
dotnet_diagnostic.RCS1049.severity = suggestion

# Include/omit parentheses when creating new object
dotnet_diagnostic.RCS1050.severity = none

# Add/remove parentheses from condition in conditional operator
dotnet_diagnostic.RCS1051.severity = none

# Declare each attribute separately
dotnet_diagnostic.RCS1052.severity = none

# Replace foreach with for
dotnet_diagnostic.RCS1054.severity = none

# Avoid semicolon at the end of declaration
dotnet_diagnostic.RCS1055.severity = none

# Avoid usage of using alias directive
dotnet_diagnostic.RCS1056.severity = none

# Add empty line between declarations
dotnet_diagnostic.RCS1057.severity = suggestion

# Use compound assignment
dotnet_diagnostic.RCS1058.severity = suggestion

# Avoid locking on publicly accessible instance
dotnet_diagnostic.RCS1059.severity = warning

# Declare each type in separate file
dotnet_diagnostic.RCS1060.severity = none

# Merge if statement with nested if statement
dotnet_diagnostic.RCS1061.severity = suggestion

# Avoid interpolated string with no interpolation
dotnet_diagnostic.RCS1062.severity = suggestion

# Avoid usage of do statement to create an infinite loop
dotnet_diagnostic.RCS1063.severity = suggestion

# Avoid usage of for statement to create an infinite loop
dotnet_diagnostic.RCS1064.severity = suggestion

# Avoid usage of while statement to create an infinite loop
dotnet_diagnostic.RCS1065.severity = suggestion

# Remove empty finally clause
dotnet_diagnostic.RCS1066.severity = warning

# Remove argument list from object creation expression
dotnet_diagnostic.RCS1067.severity = none

# Simplify logical negation
dotnet_diagnostic.RCS1068.severity = suggestion

# Remove unnecessary case label
dotnet_diagnostic.RCS1069.severity = suggestion

# Remove redundant default switch section
dotnet_diagnostic.RCS1070.severity = suggestion

# Remove redundant base constructor call
dotnet_diagnostic.RCS1071.severity = suggestion

# Convert if to return statement
dotnet_diagnostic.RCS1073.severity = none

# Remove redundant constructor
dotnet_diagnostic.RCS1074.severity = suggestion

# Avoid empty catch clause that catches System.Exception
dotnet_diagnostic.RCS1075.severity = warning

# Optimize LINQ method call
dotnet_diagnostic.RCS1077.severity = suggestion

# Use "" or string.Empty
dotnet_diagnostic.RCS1078.severity = none

# Throwing of new NotImplementedException
dotnet_diagnostic.RCS1079.severity = suggestion

# Use Count/Length property instead of Any method
dotnet_diagnostic.RCS1080.severity = suggestion

# Split variable declaration
dotnet_diagnostic.RCS1081.severity = none

# Use coalesce expression instead of conditional expression
dotnet_diagnostic.RCS1084.severity = suggestion

# Use auto-implemented property
dotnet_diagnostic.RCS1085.severity = suggestion

# Use --/++ operator instead of assignment
dotnet_diagnostic.RCS1089.severity = suggestion

# Add call to ConfigureAwait
dotnet_diagnostic.RCS1090.severity = none

# Remove empty region
dotnet_diagnostic.RCS1091.severity = suggestion

# Add using directive
dotnet_diagnostic.RCS1094.severity = none

# Use C# 6.0 dictionary initializer
dotnet_diagnostic.RCS1095.severity = suggestion

# Use HasFlag method or bitwise operator
dotnet_diagnostic.RCS1096.severity = suggestion

# Remove redundant ToString call
dotnet_diagnostic.RCS1097.severity = suggestion

# Constant values should be placed on right side of comparisons
dotnet_diagnostic.RCS1098.severity = suggestion

# Default label should be last label in switch section
dotnet_diagnostic.RCS1099.severity = suggestion
```

## Compiler Warnings (CS)

```ini
[*.cs]
# Missing XML comment
dotnet_diagnostic.CS1591.severity = suggestion

# Possible null reference
dotnet_diagnostic.CS8600.severity = warning
dotnet_diagnostic.CS8601.severity = warning
dotnet_diagnostic.CS8602.severity = warning
dotnet_diagnostic.CS8603.severity = warning
dotnet_diagnostic.CS8604.severity = warning

# Nullable reference types
dotnet_diagnostic.CS8618.severity = warning
dotnet_diagnostic.CS8619.severity = warning
dotnet_diagnostic.CS8620.severity = warning
dotnet_diagnostic.CS8625.severity = warning

# Async method lacks await
dotnet_diagnostic.CS1998.severity = warning

# Obsolete warnings
dotnet_diagnostic.CS0612.severity = warning
dotnet_diagnostic.CS0618.severity = warning

# Unused variable
dotnet_diagnostic.CS0168.severity = suggestion

# Unreachable code
dotnet_diagnostic.CS0162.severity = warning

# Hiding inherited member
dotnet_diagnostic.CS0108.severity = warning
```

## Sources

- [Code analysis rule reference](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/)
- [Code style rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/)
- [StyleCop Analyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers)
- [Roslynator Analyzers](https://github.com/dotnet/roslynator)
