# ReportGenerator Output Formats

## Format Categories

### Human-Readable Formats

| Format | Description | Use Case |
|--------|-------------|----------|
| `Html` | Full interactive HTML report with file browser. | Local development, detailed review. |
| `HtmlSummary` | Single-page HTML summary. | CI artifacts, quick overview. |
| `HtmlChart` | HTML with coverage trend charts. | Historical tracking dashboards. |
| `HtmlInline` | HTML with embedded CSS/JS (no external files). | Email-friendly, standalone sharing. |
| `HtmlInline_AzurePipelines` | Inline HTML styled for Azure DevOps. | Azure Pipelines reports tab. |
| `HtmlInline_AzurePipelines_Dark` | Dark-themed Azure DevOps HTML. | Azure Pipelines with dark mode. |

### Markdown Formats

| Format | Description | Use Case |
|--------|-------------|----------|
| `MarkdownSummary` | Basic Markdown table summary. | Wiki pages, general Markdown consumers. |
| `MarkdownSummaryGithub` | GitHub-flavored Markdown summary. | GitHub Actions step summaries, PR comments. |
| `MarkdownDeltaSummary` | Markdown showing coverage delta. | PR reviews comparing coverage changes. |

### Machine-Readable Formats

| Format | Description | Use Case |
|--------|-------------|----------|
| `Cobertura` | Cobertura XML format. | CI tools, code quality gates, merging. |
| `OpenCover` | OpenCover XML format. | Tools expecting OpenCover input. |
| `Clover` | Atlassian Clover XML format. | Bamboo, Bitbucket integrations. |
| `Lcov` | lcov tracefile format. | Tools using lcov ecosystem. |
| `JsonSummary` | JSON summary of metrics. | Custom tooling, dashboards. |
| `SonarQube` | SonarQube generic coverage format. | SonarQube/SonarCloud integration. |
| `TeamCitySummary` | TeamCity service messages. | TeamCity build statistics. |

### Badge Formats

| Format | Description | Use Case |
|--------|-------------|----------|
| `Badges` | SVG coverage badges. | README files, shields.io style. |
| `PngChart` | PNG coverage trend chart. | Documentation, static hosting. |
| `SvgChart` | SVG coverage trend chart. | Scalable documentation images. |

### Text Formats

| Format | Description | Use Case |
|--------|-------------|----------|
| `TextSummary` | Plain text summary. | Console output, logs. |
| `TextDeltaSummary` | Plain text delta summary. | CI logs comparing coverage. |
| `CsvSummary` | CSV summary data. | Spreadsheet analysis. |
| `Latex` | LaTeX document format. | Academic papers, PDF generation. |
| `LatexSummary` | LaTeX summary table. | Academic paper appendices. |
| `Xml` | Custom XML format. | Custom XML consumers. |
| `XmlSummary` | XML summary data. | Lightweight XML parsing. |

## Common Format Combinations

### CI Pipeline (GitHub Actions)

```bash
-reporttypes:"HtmlSummary;Cobertura;MarkdownSummaryGithub;Badges"
```

Produces:
- `Summary.html` - Quick HTML overview for artifact download
- `Cobertura.xml` - Machine-readable for quality gates
- `SummaryGithub.md` - Append to `$GITHUB_STEP_SUMMARY`
- `badge_*.svg` - Coverage badges for README

### CI Pipeline (Azure DevOps)

```bash
-reporttypes:"HtmlInline_AzurePipelines;Cobertura"
```

### Local Development

```bash
-reporttypes:"Html"
```

Opens full interactive report with file-level detail.

### PR Review

```bash
-reporttypes:"MarkdownDeltaSummary;Cobertura"
```

Shows what changed and maintains machine-readable format.

### Merging Reports Only

```bash
-reporttypes:"Cobertura"
```

When the only goal is to merge multiple coverage files into one.

## Output File Names

Each format produces specific output files in the target directory:

| Format | Output File(s) |
|--------|----------------|
| `Html` | `index.html` + supporting files |
| `HtmlSummary` | `Summary.html` |
| `Cobertura` | `Cobertura.xml` |
| `MarkdownSummaryGithub` | `SummaryGithub.md` |
| `Badges` | `badge_linecoverage.svg`, `badge_branchcoverage.svg`, `badge_methodcoverage.svg` |
| `JsonSummary` | `Summary.json` |
| `TextSummary` | `Summary.txt` |
| `Lcov` | `lcov.info` |

## Badge Examples

Generated badges display coverage percentages:

- Line coverage: `badge_linecoverage.svg`
- Branch coverage: `badge_branchcoverage.svg`
- Method coverage: `badge_methodcoverage.svg`

Usage in README:

```markdown
`badge_linecoverage.svg`
```

Or link to raw file URL in GitHub:

```markdown
![Coverage](https://raw.githubusercontent.com/org/repo/main/artifacts/coverage/badge_linecoverage.svg)
```

## PRO-Only Formats

Some formats require a PRO license:

- `MHtml` - Single-file MHTML archive
- Risk hotspot analysis features in HTML reports

The core formats listed above are free under Apache 2.0.

## Sources

- [ReportGenerator Output Formats](https://github.com/danielpalme/ReportGenerator#output-formats)
- [ReportGenerator Wiki](https://github.com/danielpalme/ReportGenerator/wiki)
