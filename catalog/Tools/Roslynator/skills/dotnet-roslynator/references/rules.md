# Roslynator Rules Reference

This document covers commonly used Roslynator analyzer rules, organized by category.

## Rule Naming Convention

Roslynator rules follow these prefixes:

- `RCS0xxx` - Formatting analyzers (from `Roslynator.Formatting.Analyzers`)
- `RCS1xxx` - Code analyzers (from `Roslynator.Analyzers`)
- `RCS9xxx` - Code analysis analyzers

## Commonly Used Code Analyzers (RCS1xxx)

### Simplification and Redundancy

| Rule | Description | Default |
|------|-------------|---------|
| RCS1001 | Add braces to if-else chain | Info |
| RCS1003 | Add braces to if-else when multi-line | Info |
| RCS1015 | Use nameof operator | Warning |
| RCS1021 | Convert lambda expression body to expression-body | Info |
| RCS1032 | Remove redundant parentheses | Info |
| RCS1033 | Remove redundant boolean literal | Info |
| RCS1036 | Remove unnecessary blank line | Info |
| RCS1037 | Remove trailing white-space | Hidden |
| RCS1038 | Remove empty statement | Info |
| RCS1039 | Remove argument list from attribute | Info |
| RCS1040 | Remove empty else clause | Info |
| RCS1049 | Simplify boolean comparison | Info |
| RCS1058 | Use compound assignment | Info |
| RCS1068 | Simplify logical negation | Info |
| RCS1069 | Remove unnecessary case label | Hidden |
| RCS1073 | Convert if to return statement | Info |
| RCS1077 | Optimize LINQ method call | Info |
| RCS1084 | Use coalesce expression instead of conditional expression | Info |
| RCS1085 | Use auto-implemented property | Info |
| RCS1089 | Use postfix unary operator instead of assignment | Info |
| RCS1097 | Remove redundant ToString call | Info |
| RCS1104 | Simplify conditional expression | Info |
| RCS1118 | Mark local variable as const | Info |
| RCS1123 | Add parentheses when necessary | Info |
| RCS1128 | Use coalesce expression | Info |
| RCS1129 | Remove redundant field initialization | Hidden |
| RCS1132 | Remove redundant overriding member | Info |
| RCS1133 | Remove redundant Dispose/Close call | Hidden |
| RCS1138 | Add summary to documentation comment | Warning |
| RCS1139 | Add summary element to documentation comment | Warning |
| RCS1140 | Add exception to documentation comment | Info |
| RCS1141 | Add 'param' element to documentation comment | Info |
| RCS1142 | Add 'typeparam' element to documentation comment | Info |
| RCS1143 | Simplify coalesce expression | Info |
| RCS1146 | Use conditional access | Info |
| RCS1151 | Remove redundant cast | Hidden |
| RCS1154 | Sort enum members | Info |
| RCS1155 | Use StringComparison when comparing strings | Warning |
| RCS1156 | Use string.Length instead of comparison with empty string | Info |
| RCS1161 | Enum should declare explicit values | Hidden |
| RCS1163 | Unused parameter | Info |
| RCS1168 | Parameter name differs from base name | Hidden |
| RCS1169 | Make field read-only | Info |
| RCS1170 | Use read-only auto-implemented property | Info |
| RCS1171 | Simplify lazy initialization | Info |
| RCS1173 | Use coalesce expression instead of if | Info |
| RCS1175 | Unused 'this' parameter | Info |
| RCS1177 | Use 'var' instead of explicit type in foreach | Hidden |
| RCS1179 | Unnecessary assignment | Info |
| RCS1180 | Inline lazy initialization | Info |
| RCS1181 | Convert comment to documentation comment | Hidden |
| RCS1182 | Remove redundant base interface | Hidden |
| RCS1187 | Use constant instead of field | Info |
| RCS1188 | Remove redundant auto-property initialization | Hidden |
| RCS1189 | Add or remove region name | Hidden |
| RCS1190 | Join string expressions | Info |
| RCS1191 | Declare enum value as combination of names | Info |
| RCS1192 | Unnecessary usage of verbatim string literal | Info |
| RCS1193 | Overriding member should not change 'params' modifier | Warning |
| RCS1194 | Implement exception constructors | Warning |
| RCS1195 | Use ^ operator | Info |
| RCS1196 | Call extension method as instance method | Info |
| RCS1197 | Optimize StringBuilder.Append/AppendLine call | Info |
| RCS1198 | Avoid unnecessary boxing of value type | Hidden |
| RCS1199 | Unnecessary null check | Info |
| RCS1200 | Call 'Enumerable.ThenBy' instead of 'Enumerable.OrderBy' | Info |
| RCS1201 | Use method chaining | Hidden |
| RCS1202 | Avoid NullReferenceException | Info |
| RCS1203 | Use AttributeUsageAttribute | Warning |
| RCS1205 | Order named arguments according to the order of parameters | Info |
| RCS1206 | Use conditional access instead of conditional expression | Info |
| RCS1207 | Convert anonymous function to method group | Info |
| RCS1208 | Reduce if nesting | Hidden |
| RCS1209 | Order type parameter constraints | Info |
| RCS1210 | Return completed task instead of returning null | Warning |
| RCS1211 | Remove unnecessary else | Hidden |
| RCS1212 | Remove redundant assignment | Info |
| RCS1213 | Remove unused member declaration | Info |
| RCS1214 | Unnecessary interpolated string | Info |
| RCS1215 | Expression is always equal to true/false | Warning |
| RCS1216 | Unnecessary unsafe context | Info |
| RCS1217 | Convert interpolated string to concatenation | Hidden |
| RCS1218 | Simplify code branching | Hidden |
| RCS1220 | Use pattern matching instead of combination of 'is' and cast | Info |
| RCS1221 | Use pattern matching instead of combination of 'as' and null check | Info |
| RCS1222 | Merge preprocessor directives | Info |
| RCS1223 | Mark publicly visible type with DebuggerDisplay attribute | Hidden |
| RCS1224 | Make method an extension method | Info |
| RCS1225 | Make class sealed | Info |
| RCS1226 | Add paragraph to documentation comment | Info |
| RCS1227 | Validate arguments correctly | Info |
| RCS1228 | Unused element in documentation comment | Info |
| RCS1229 | Use async/await when necessary | Info |
| RCS1230 | Unnecessary explicit use of enumerator | Info |
| RCS1231 | Make parameter ref read-only | Hidden |
| RCS1232 | Order elements in documentation comment | Info |
| RCS1233 | Use short-circuiting operator | Info |
| RCS1234 | Duplicate enum value | Info |
| RCS1235 | Optimize method call | Info |
| RCS1236 | Use exception filter | Info |
| RCS1238 | Avoid nested ?: operators | Info |
| RCS1239 | Use 'for' statement instead of 'while' statement | Info |
| RCS1240 | Operator is unnecessary | Info |
| RCS1241 | Implement non-generic counterpart | Info |
| RCS1242 | Do not pass non-read-only struct by read-only reference | Warning |
| RCS1243 | Duplicate word in comment | Info |
| RCS1244 | Simplify 'default' expression | Hidden |
| RCS1246 | Use element access | Info |
| RCS1247 | Fix documentation comment tag | Info |
| RCS1248 | Normalize null check | Info |
| RCS1249 | Unnecessary null-forgiving operator | Info |
| RCS1250 | Use implicit/explicit object creation | Hidden |
| RCS1251 | Remove unnecessary braces from record declaration | Info |
| RCS1252 | Normalize usage of infinite loop | Hidden |
| RCS1253 | Format documentation comment summary | Hidden |
| RCS1254 | Normalize format of enum flag value | Info |
| RCS1255 | Simplify argument null check | Info |
| RCS1256 | Invalid argument null check | Info |
| RCS1257 | Use enum field explicitly | Info |
| RCS1258 | Unnecessary enum flag | Info |
| RCS1259 | Remove empty syntax | Info |
| RCS1260 | Add/remove trailing comma | Hidden |
| RCS1261 | Resource can be disposed asynchronously | Info |
| RCS1262 | Unnecessary raw string literal | Info |
| RCS1263 | Invalid reference in a documentation comment | Warning |
| RCS1264 | Use 'var' or explicit type | Hidden |
| RCS1265 | Remove redundant catch block | Info |
| RCS1266 | Use raw string literal | Info |
| RCS1267 | Use string interpolation instead of 'string.Concat' | Info |
| RCS1268 | Simplify numeric comparison | Info |

### Async and Threading

| Rule | Description | Default |
|------|-------------|---------|
| RCS1046 | Asynchronous method name should end with 'Async' | Hidden |
| RCS1047 | Non-asynchronous method name should not end with 'Async' | Info |
| RCS1090 | Add call to 'ConfigureAwait' | Hidden |
| RCS1174 | Remove redundant async/await | Info |
| RCS1229 | Use async/await when necessary | Info |

### Design and Architecture

| Rule | Description | Default |
|------|-------------|---------|
| RCS1016 | Convert block body to expression-body | Hidden |
| RCS1017 | Convert expression-body to block body | Hidden |
| RCS1018 | Add accessibility modifiers | Info |
| RCS1019 | Order modifiers | Hidden |
| RCS1045 | Rename private field to camel case with underscore | Hidden |
| RCS1050 | Include/omit parentheses when creating new object | Hidden |
| RCS1051 | Add/remove parentheses from condition in conditional operator | Hidden |
| RCS1061 | Merge if with nested if | Hidden |
| RCS1080 | Use 'Count' or 'Length' property instead of 'Any' method | Info |
| RCS1090 | Add call to 'ConfigureAwait' | Hidden |
| RCS1091 | Remove empty region | Hidden |
| RCS1094 | Declare using directive on top level | Hidden |
| RCS1096 | Convert 'HasFlag' call to bitwise operation | Info |
| RCS1102 | Make class static | Info |
| RCS1110 | Declare type inside namespace | Warning |
| RCS1126 | Add braces to if-else | Hidden |
| RCS1134 | Remove redundant statement | Info |
| RCS1135 | Declare enum member with zero value | Info |
| RCS1136 | Merge switch sections with equivalent content | Hidden |
| RCS1145 | Remove redundant 'as' operator | Hidden |
| RCS1158 | Static member in generic type should use a type parameter | Info |
| RCS1159 | Use EventHandler<T> | Info |
| RCS1160 | Abstract type should not have public constructors | Info |
| RCS1162 | Avoid chain of assignments | Hidden |
| RCS1164 | Unused type parameter | Info |
| RCS1166 | Value type object is never equal to null | Info |
| RCS1225 | Make class sealed | Info |

## Formatting Analyzers (RCS0xxx)

These rules control code formatting and are part of `Roslynator.Formatting.Analyzers`:

| Rule | Description | Default |
|------|-------------|---------|
| RCS0001 | Add blank line after embedded statement | None |
| RCS0002 | Add blank line after #region | None |
| RCS0003 | Add blank line after using directive list | None |
| RCS0004 | Add blank line before #endregion | None |
| RCS0005 | Add blank line before using directive list | None |
| RCS0006 | Add blank line between declaration and documentation comment | None |
| RCS0007 | Add blank line between accessors | None |
| RCS0008 | Add blank line between closing brace and next statement | None |
| RCS0009 | Add blank line between declaration and documentation comment | None |
| RCS0010 | Add blank line between declarations | None |
| RCS0011 | Add/remove blank line between single-line accessors | None |
| RCS0012 | Add blank line between single-line declarations | None |
| RCS0013 | Add blank line between single-line declarations of different kind | None |
| RCS0014 | Add blank line between switch sections | None |
| RCS0015 | Add/remove blank line between using directives | None |
| RCS0016 | Put attribute list on its own line | None |
| RCS0020 | Format accessor's braces on a single line or multiple lines | None |
| RCS0021 | Format block's braces on a single line or multiple lines | None |
| RCS0022 | Format type declaration's braces | None |
| RCS0023 | Format type declaration's braces | None |
| RCS0024 | Add newline after switch label | None |
| RCS0025 | Put full accessor on its own line | None |
| RCS0027 | Place new line after/before binary operator | None |
| RCS0028 | Place new line after/before '?:' operator | None |
| RCS0029 | Put constructor initializer on its own line | None |
| RCS0030 | Put embedded statement on its own line | None |
| RCS0031 | Put enum member on its own line | None |
| RCS0032 | Place new line after/before arrow token | None |
| RCS0033 | Put statement on its own line | None |
| RCS0034 | Put type parameter constraint on its own line | None |
| RCS0036 | Remove blank line between single-line declarations of same kind | None |
| RCS0038 | Remove blank line between using directives with same root namespace | None |
| RCS0039 | Remove newline before base list | None |
| RCS0041 | Remove newline between 'if' keyword and 'else' keyword | None |
| RCS0042 | Put auto-accessors on a single line | None |
| RCS0043 | Format accessor's braces on a single line when expression is on single line | None |
| RCS0044 | Use carriage return + linefeed as newline | None |
| RCS0045 | Use linefeed as newline | None |
| RCS0046 | Use spaces instead of tab | None |
| RCS0048 | Put initializer on a single line | None |
| RCS0049 | Add blank line after top comment | None |
| RCS0050 | Add blank line before top declaration | None |
| RCS0051 | Add/remove newline between closing brace and 'while' keyword | None |
| RCS0052 | Place new line after/before equals token | None |
| RCS0053 | Fix formatting of a list | None |
| RCS0054 | Fix formatting of a call chain | None |
| RCS0055 | Fix formatting of a binary expression chain | None |
| RCS0056 | Line is too long | None |
| RCS0057 | Normalize whitespace at the beginning of a file | None |
| RCS0058 | Normalize whitespace at the end of a file | None |
| RCS0059 | Place new line after/before null-conditional operator | None |
| RCS0060 | Add/remove line after file scoped namespace declaration | None |
| RCS0061 | Add/remove blank line between switch sections | None |

## High-Impact Rules for Code Quality

These rules have the highest impact on code quality and maintainability:

### Must-Enable (Recommended as Warning or Error)

1. **RCS1155** - Use StringComparison when comparing strings
2. **RCS1210** - Return completed task instead of returning null
3. **RCS1194** - Implement exception constructors
4. **RCS1203** - Use AttributeUsageAttribute
5. **RCS1110** - Declare type inside namespace
6. **RCS1215** - Expression is always equal to true/false
7. **RCS1263** - Invalid reference in a documentation comment
8. **RCS1242** - Do not pass non-read-only struct by read-only reference
9. **RCS1193** - Overriding member should not change 'params' modifier

### Often Elevated to Warning

1. **RCS1163** - Unused parameter
2. **RCS1213** - Remove unused member declaration
3. **RCS1169** - Make field read-only
4. **RCS1102** - Make class static
5. **RCS1225** - Make class sealed
6. **RCS1077** - Optimize LINQ method call
7. **RCS1080** - Use 'Count' or 'Length' property instead of 'Any' method

### Performance-Related Rules

1. **RCS1077** - Optimize LINQ method call
2. **RCS1080** - Use 'Count' or 'Length' property instead of 'Any' method
3. **RCS1096** - Convert 'HasFlag' call to bitwise operation
4. **RCS1197** - Optimize StringBuilder.Append/AppendLine call
5. **RCS1198** - Avoid unnecessary boxing of value type
6. **RCS1235** - Optimize method call

## Refactorings

Roslynator also provides refactorings (not analyzers) that can be invoked manually. These are not rule-based but are useful code transformations available through the IDE.

Common refactorings include:

- Add braces
- Extract method
- Inline method
- Introduce local variable
- Merge if statements
- Replace method group with lambda
- Split variable declaration
- Wrap in region
- And many more

Refactorings can be enabled/disabled in `.editorconfig` using `roslynator_refactoring.<id>.enabled` settings.

## Further Reading

- [Roslynator Analyzers Documentation](https://josefpihrt.github.io/docs/roslynator/analyzers)
- [Roslynator GitHub Repository](https://github.com/dotnet/roslynator)
- [Roslynator Configuration](https://josefpihrt.github.io/docs/roslynator/configuration)
