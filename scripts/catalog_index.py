#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import subprocess
from functools import lru_cache
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CATALOG_ROOT = ROOT / "catalog"
CLI_PROJECT = ROOT / "cli" / "ManagedCode.DotnetSkills" / "ManagedCode.DotnetSkills.csproj"

LINK_KEYS = ("repository", "docs", "nuget")

CURATED_BUNDLES = [
    {
        "name": "dotnet-base",
        "title": ".NET base bundle",
        "description": "Install the focused .NET baseline: core platform guidance, project setup, modern C#, and Microsoft.Extensions composition patterns without pulling in diagnostics, migrations, or frontend tooling.",
        "kind": "stack",
        "stack": ".NET Foundations",
        "lane": "Foundations",
        "skills": [
            "dotnet",
            "dotnet-project-setup",
            "dotnet-modern-csharp",
            "dotnet-microsoft-extensions",
        ],
    },
    {
        "name": "dotnet-quality",
        "title": ".NET quality bundle",
        "description": "Install the core .NET quality toolchain: formatter, analyzers, complexity checks, CRAP analysis, editorconfig guidance, and CI quality checks. Frontend linters stay out of this bundle on purpose.",
        "kind": "stack",
        "stack": ".NET Quality",
        "lane": "Code Quality",
        "skills": [
            "dotnet-quality-ci",
            "dotnet-format",
            "dotnet-csharpier",
            "dotnet-roslynator",
            "dotnet-meziantou-analyzer",
            "dotnet-stylecop-analyzers",
            "dotnet-analyzer-config",
            "dotnet-code-analysis",
            "dotnet-complexity",
            "crap-score",
        ],
    },
    {
        "name": "frontend-quality",
        "title": "Frontend quality bundle",
        "description": "Install the frontend quality toolchain only: Biome, ESLint, Stylelint, HTMLHint, webhint, and SonarJS. No .NET analyzers or build diagnostics are mixed in.",
        "kind": "stack",
        "stack": "Frontend Quality",
        "lane": "Code Quality",
        "skills": [
            "dotnet-biome",
            "dotnet-eslint",
            "dotnet-stylelint",
            "dotnet-htmlhint",
            "dotnet-webhint",
            "dotnet-sonarjs",
        ],
    },
    {
        "name": "architecture-core",
        "title": "Architecture bundle",
        "description": "Install the focused architecture set: architecture guidance plus architecture-testing and visualization skills without mixing in general testing or migration flows.",
        "kind": "workflow",
        "stack": "Architecture",
        "lane": "Architecture",
        "skills": [
            "dotnet-architecture",
            "dotnet-netarchtest",
            "dotnet-archunitnet",
            "dotnet-graphify-dotnet",
        ],
    },
    {
        "name": "testing-base",
        "title": "Testing base bundle",
        "description": "Install the clean testing baseline: test command fundamentals, framework selection guidance, coverage, and report output. Framework migrations stay out of this bundle.",
        "kind": "workflow",
        "stack": "Testing",
        "lane": "Foundations",
        "skills": [
            "dotnet-test-frameworks",
            "run-tests",
            "coverage-analysis",
            "dotnet-coverlet",
            "dotnet-reportgenerator",
            "test-anti-patterns",
        ],
    },
    {
        "name": "testing-xunit",
        "title": "xUnit testing bundle",
        "description": "Install the testing baseline plus xUnit guidance. This stays focused on active xUnit usage and does not pull migration content.",
        "kind": "workflow",
        "stack": "Testing",
        "lane": "Frameworks",
        "skills": [
            "dotnet-test-frameworks",
            "run-tests",
            "coverage-analysis",
            "dotnet-coverlet",
            "dotnet-reportgenerator",
            "test-anti-patterns",
            "dotnet-xunit",
        ],
    },
    {
        "name": "testing-nunit",
        "title": "NUnit testing bundle",
        "description": "Install the testing baseline plus NUnit guidance. This stays focused on active NUnit usage and does not pull migration content.",
        "kind": "workflow",
        "stack": "Testing",
        "lane": "Frameworks",
        "skills": [
            "dotnet-test-frameworks",
            "run-tests",
            "coverage-analysis",
            "dotnet-coverlet",
            "dotnet-reportgenerator",
            "test-anti-patterns",
            "dotnet-nunit",
        ],
    },
    {
        "name": "testing-mstest",
        "title": "MSTest testing bundle",
        "description": "Install the testing baseline plus MSTest guidance and authoring patterns. Migration skills remain separate so the default MSTest path stays clean.",
        "kind": "workflow",
        "stack": "Testing",
        "lane": "Frameworks",
        "skills": [
            "dotnet-test-frameworks",
            "run-tests",
            "coverage-analysis",
            "dotnet-coverlet",
            "dotnet-reportgenerator",
            "test-anti-patterns",
            "dotnet-mstest",
            "writing-mstest-tests",
        ],
    },
    {
        "name": "testing-tunit",
        "title": "TUnit testing bundle",
        "description": "Install the testing baseline plus TUnit guidance for teams that want the newer .NET-native test framework option without unrelated migration content.",
        "kind": "workflow",
        "stack": "Testing",
        "lane": "Frameworks",
        "skills": [
            "dotnet-test-frameworks",
            "run-tests",
            "coverage-analysis",
            "dotnet-coverlet",
            "dotnet-reportgenerator",
            "test-anti-patterns",
            "dotnet-tunit",
        ],
    },
    {
        "name": "testing-migrations",
        "title": "Testing migrations bundle",
        "description": "Install only the testing migration path: MSTest, xUnit, and VSTest-to-MTP migration guidance. This is intentionally separate from the default testing bundles.",
        "kind": "workflow",
        "stack": "Upgrades & Migration",
        "lane": "Testing migrations",
        "skills": [
            "migrate-vstest-to-mtp",
            "migrate-xunit-to-xunit-v3",
            "migrate-mstest-v1v2-to-v3",
            "migrate-mstest-v3-to-v4",
            "mtp-hot-reload",
        ],
    },
    {
        "name": "runtime-upgrades",
        "title": "Runtime upgrades bundle",
        "description": "Install the runtime-upgrade path for platform migrations such as nullable references, AOT compatibility, and targeted .NET version transitions. This stays separate from default `.NET` bundles.",
        "kind": "workflow",
        "stack": "Upgrades & Migration",
        "lane": "Runtime upgrades",
        "skills": [
            "dotnet-aot-compat",
            "migrate-dotnet8-to-dotnet9",
            "migrate-dotnet9-to-dotnet10",
            "migrate-dotnet10-to-dotnet11",
            "migrate-nullable-references",
            "thread-abort-migration",
        ],
    },
    {
        "name": "mcaf",
        "title": "MCAF bundle",
        "description": "Install the locally mirrored MCAF governance skills in one command, including adoption, delivery workflow, developer experience, documentation, feature specs, review planning, NFRs, source-control policy, UI/UX, and ML/AI delivery guidance.",
        "kind": "curated",
        "stack": "Governance & Delivery",
        "lane": "Governance",
        "skills": [
            "dotnet-mcaf",
            "dotnet-mcaf-agile-delivery",
            "dotnet-mcaf-devex",
            "dotnet-mcaf-documentation",
            "dotnet-mcaf-feature-spec",
            "dotnet-mcaf-human-review-planning",
            "dotnet-mcaf-ml-ai-delivery",
            "dotnet-mcaf-nfr",
            "dotnet-mcaf-source-control",
            "dotnet-mcaf-ui-ux",
        ],
    },
    {
        "name": "orleans",
        "title": "Orleans bundle",
        "description": "Install the focused Orleans stack in one command: Orleans core guidance plus the adjacent ManagedCode Orleans integrations. Cross-surface hosting and generic web delivery guidance stay separate.",
        "kind": "curated",
        "stack": "Distributed",
        "lane": "Frameworks",
        "skills": [
            "dotnet-orleans",
            "dotnet-managedcode-orleans-graph",
            "dotnet-managedcode-orleans-signalr",
        ],
    },
]

STACK_ORDER = [
    ".NET Foundations",
    ".NET Quality",
    "MSBuild",
    "NuGet & Publishing",
    "Templates & Scaffolding",
    "Diagnostics & Metrics",
    "Web",
    "Aspire",
    "Azure Functions",
    "Background Workers",
    "Mobile & Device",
    "XR & Spatial",
    "Desktop & UI",
    "Frontend Quality",
    "Testing",
    "Testing Research",
    "Architecture",
    "Governance & Delivery",
    "Data",
    "AI & Agents",
    "Distributed",
    "Legacy",
    "Upgrades & Migration",
]

LANE_ORDER = [
    "Foundations",
    "Code Quality",
    "Frameworks",
    "Libraries",
    "Interop",
    "Quality",
    "Mutation",
    "Experimental",
    "Review",
    "Governance",
    "Delivery Workflow",
    "Project & Templates",
    "Package Management",
    "Package Publishing",
    "Automation",
    "Build Pipelines",
    "Crash Analysis",
    "Performance",
    "Observability",
    "Static Analysis",
    "Runtime upgrades",
    "Testing migrations",
    "Legacy frameworks",
    "Migration",
    "Architecture",
    "Analysis",
    "Tooling",
]

FRONTEND_QUALITY_PACKAGES = {"Biome", "ESLint", "Stylelint", "HTMLHint", "webhint", "SonarJS"}
DOTNET_QUALITY_PACKAGES = {
    "Analyzer-Config",
    "Chous",
    "Code-Analysis",
    "CSharpier",
    "Format",
    "Meziantou-Analyzer",
    "Metalint",
    "Quality-CI",
    "ReSharper-CLT",
    "Roslynator",
    "StyleCop-Analyzers",
}
MSBUILD_PACKAGES = {"Official-DotNet-MSBuild"}
DIAGNOSTICS_PACKAGES = {"Asynkron-Profiler", "cloc", "CodeQL", "Complexity", "Official-DotNet-Diagnostics", "Profiling", "QuickDup"}
ARCHITECTURE_PACKAGES = {"ArchUnitNET", "Architecture", "Graphify", "NetArchTest"}
GOVERNANCE_PACKAGES = {"Code-Review", "MCAF"}
TESTING_FRAMEWORK_PACKAGES = {"MSTest", "NUnit", "TUnit", "xUnit"}
TESTING_QUALITY_PACKAGES = {"Coverlet", "ReportGenerator", "Stryker"}
LEGACY_PACKAGES = {"Entity-Framework-6", "Legacy-ASP.NET", "Official-DotNet-Upgrade", "WCF", "Workflow-Foundation"}
DISTRIBUTED_PACKAGES = {"ManagedCode-Orleans-Graph", "ManagedCode-Orleans-SignalR", "Orleans"}
MOBILE_DEVICE_PACKAGES = {"MAUI", "Official-DotNet-MAUI"}


def unquote(value: str) -> str:
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in {"'", '"'}:
        return value[1:-1]
    return value


def slugify(value: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")


def singularize_type_dir(type_dir_name: str) -> str:
    if type_dir_name.endswith("ies"):
        return f"{type_dir_name[:-3]}y"
    if type_dir_name.endswith("s"):
        return type_dir_name[:-1]
    return type_dir_name


def get_type_directories() -> list[str]:
    if not CATALOG_ROOT.is_dir():
        return []

    return sorted(
        [
            path.name
            for path in CATALOG_ROOT.iterdir()
            if path.is_dir() and not path.name.startswith(".")
        ],
        key=lambda item: (item.casefold(), item),
    )


def resolve_category_order(skills: list[dict[str, object]]) -> list[str]:
    categories = {
        str(skill["category"]).strip()
        for skill in skills
        if str(skill.get("category", "")).strip()
    }
    return sorted(categories, key=lambda item: (item.casefold(), item))


def resolve_stack_order(skills: list[dict[str, object]]) -> list[str]:
    stacks = {
        str(skill["stack"]).strip()
        for skill in skills
        if str(skill.get("stack", "")).strip()
    }
    return sorted(stacks, key=lambda item: (get_stack_rank(item), item.casefold(), item))


def classify_skill(skill_type: str, package: str, category: str, name: str) -> tuple[str, str]:
    if is_governance_skill(package, name):
        return "Governance & Delivery", resolve_governance_lane(package, name)

    if is_migration_skill(package, name):
        return "Upgrades & Migration", resolve_migration_lane(package, name)

    if is_legacy_skill(package, category):
        return "Legacy", resolve_legacy_lane(skill_type, package, category)

    if is_architecture_skill(package, category):
        return "Architecture", resolve_architecture_lane(skill_type, package)

    if is_testing_research_skill(package, category, name):
        return "Testing Research", resolve_testing_research_lane(package, name)

    if is_testing_skill(skill_type, category):
        return "Testing", resolve_testing_lane(package, name)

    if is_data_skill(package, category):
        return "Data", resolve_entity_lane(skill_type)

    if is_xr_spatial_skill(package):
        return "XR & Spatial", resolve_xr_spatial_lane(skill_type, package)

    if is_mobile_device_skill(package, name):
        return "Mobile & Device", resolve_mobile_device_lane(skill_type, package, name)

    if is_ai_skill(package, category):
        return "AI & Agents", resolve_ai_lane(skill_type, package)

    if is_aspire_skill(package):
        return "Aspire", resolve_framework_lane(skill_type)

    if is_azure_functions_skill(package):
        return "Azure Functions", resolve_framework_lane(skill_type)

    if is_background_worker_skill(package):
        return "Background Workers", resolve_framework_lane(skill_type)

    if is_distributed_skill(package, category):
        return "Distributed", resolve_entity_lane(skill_type)

    if is_frontend_quality_skill(package):
        return "Frontend Quality", "Code Quality"

    if is_web_skill(skill_type, package, category):
        return "Web", resolve_web_lane(skill_type, package)

    if is_desktop_skill(skill_type, package, category):
        return "Desktop & UI", resolve_desktop_lane(skill_type, package)

    if is_dotnet_quality_skill(package, category):
        return ".NET Quality", "Code Quality"

    if is_msbuild_skill(package):
        return "MSBuild", "Build Pipelines"

    if is_nuget_publishing_skill(package, name):
        return "NuGet & Publishing", resolve_nuget_publishing_lane(package, name)

    if is_template_skill(package):
        return "Templates & Scaffolding", "Project & Templates"

    if is_diagnostics_skill(package, category, name):
        return "Diagnostics & Metrics", resolve_diagnostics_lane(package, category, name)

    return ".NET Foundations", resolve_dotnet_lane(skill_type, package, category, name)


def is_governance_skill(package: str, name: str) -> bool:
    return package in GOVERNANCE_PACKAGES or name == "dotnet-code-review"


def is_migration_skill(package: str, name: str) -> bool:
    lowered = name.lower()
    return (
        package == "Official-DotNet-Upgrade"
        or name == "dotnet-aot-compat"
        or "migrate-" in lowered
        or "migration" in lowered
        or name == "mtp-hot-reload"
    )


def is_legacy_skill(package: str, category: str) -> bool:
    return package in LEGACY_PACKAGES or category.lower() == "legacy"


def is_architecture_skill(package: str, category: str) -> bool:
    return package in ARCHITECTURE_PACKAGES or category.lower() == "architecture"


def is_testing_skill(skill_type: str, category: str) -> bool:
    return skill_type.lower() == "testing" or category.lower() == "testing"


def is_testing_research_skill(package: str, category: str, name: str) -> bool:
    return (
        name == "code-testing-agent"
        or package == "Stryker"
        or (package == "Official-DotNet-Experimental" and category.lower() == "testing")
    )


def is_data_skill(package: str, category: str) -> bool:
    return category.lower() == "data" or package in {"Official-DotNet-Data", "Sep"}


def is_ai_skill(package: str, category: str) -> bool:
    return category.lower() == "ai" or package in {"MCP", "Microsoft-Extensions-AI", "Official-DotNet-AI", "Semantic-Kernel"}


def is_mobile_device_skill(package: str, name: str) -> bool:
    return package in MOBILE_DEVICE_PACKAGES


def is_xr_spatial_skill(package: str) -> bool:
    return package == "Mixed-Reality"


def is_aspire_skill(package: str) -> bool:
    return package == "Aspire"


def is_azure_functions_skill(package: str) -> bool:
    return package == "Azure-Functions"


def is_background_worker_skill(package: str) -> bool:
    return package == "Worker-Services"


def is_distributed_skill(package: str, category: str) -> bool:
    return package in DISTRIBUTED_PACKAGES or category.lower() == "distributed"


def is_frontend_quality_skill(package: str) -> bool:
    return package in FRONTEND_QUALITY_PACKAGES


def is_web_skill(skill_type: str, package: str, category: str) -> bool:
    return (
        category.lower() == "web"
        or (
            skill_type.lower() == "framework"
            and any(token in package.lower() for token in ("asp", "blazor", "web", "signalr", "grpc"))
        )
    )


def is_desktop_skill(skill_type: str, package: str, category: str) -> bool:
    return (
        category.lower() in {"cross-platform ui", "desktop"}
        or package in {"MVVM-Toolkit", "LibVLC"}
        or (
            skill_type.lower() == "framework"
            and any(token in package.lower() for token in ("maui", "uno", "win"))
        )
    )


def is_dotnet_quality_skill(package: str, category: str) -> bool:
    if package == "Modern-CSharp":
        return False
    return package in DOTNET_QUALITY_PACKAGES or category.lower() == "code quality"


def is_msbuild_skill(package: str) -> bool:
    return package in MSBUILD_PACKAGES


def is_nuget_publishing_skill(package: str, name: str) -> bool:
    return package == "Official-DotNet-NuGet" or name == "nuget-trusted-publishing"


def is_template_skill(package: str) -> bool:
    return package == "Official-DotNet-Template-Engine"


def is_diagnostics_skill(package: str, category: str, name: str) -> bool:
    return package in DIAGNOSTICS_PACKAGES or category.lower() == "metrics" or name == "exp-simd-vectorization"


def resolve_dotnet_lane(skill_type: str, package: str, category: str, name: str) -> str:
    if name == "csharp-scripts":
        return "Tooling"
    if name == "dotnet-pinvoke":
        return "Interop"
    lane = resolve_entity_lane(skill_type)
    return "Libraries" if lane == "Libraries" else "Foundations"


def resolve_web_lane(skill_type: str, package: str) -> str:
    if skill_type.lower() == "tool":
        return "Foundations"
    return resolve_entity_lane(skill_type)


def resolve_framework_lane(skill_type: str) -> str:
    if skill_type.lower() == "tool":
        return "Foundations"
    return resolve_entity_lane(skill_type)


def resolve_mobile_device_lane(skill_type: str, package: str, name: str) -> str:
    lowered = name.lower()
    if "doctor" in lowered:
        return "Tooling"
    return resolve_entity_lane(skill_type)


def resolve_xr_spatial_lane(skill_type: str, package: str) -> str:
    if package == "Mixed-Reality":
        return "Frameworks"
    return resolve_entity_lane(skill_type)


def resolve_desktop_lane(skill_type: str, package: str) -> str:
    if package in {"MVVM-Toolkit", "LibVLC"}:
        return "Libraries"
    return resolve_entity_lane(skill_type)


def resolve_testing_lane(package: str, name: str) -> str:
    if package in TESTING_FRAMEWORK_PACKAGES or name in {"writing-mstest-tests", "dotnet-mstest", "dotnet-nunit", "dotnet-tunit", "dotnet-xunit"}:
        return "Frameworks"
    if package in TESTING_QUALITY_PACKAGES or name in {"coverage-analysis", "crap-score", "test-anti-patterns"}:
        return "Quality"
    return "Foundations"


def resolve_testing_research_lane(package: str, name: str) -> str:
    if name == "code-testing-agent":
        return "Automation"
    if package == "Stryker":
        return "Mutation"
    return "Experimental"


def resolve_migration_lane(package: str, name: str) -> str:
    lowered = name.lower()
    if any(token in lowered for token in ("mstest", "xunit", "vstest", "mtp")):
        return "Testing migrations"
    if package == "Official-DotNet-Upgrade" or name == "dotnet-aot-compat":
        return "Runtime upgrades"
    return "Migration"


def resolve_legacy_lane(skill_type: str, package: str, category: str) -> str:
    if category.lower() == "legacy" or package in LEGACY_PACKAGES:
        return "Legacy frameworks"
    return resolve_entity_lane(skill_type)


def resolve_architecture_lane(skill_type: str, package: str) -> str:
    if skill_type.lower() == "tool":
        return "Analysis"
    lane = resolve_entity_lane(skill_type)
    return "Architecture" if lane in {"Frameworks", "Foundations"} else lane


def resolve_governance_lane(package: str, name: str) -> str:
    lowered = name.lower()
    if name == "dotnet-code-review":
        return "Review"
    if any(token in lowered for token in ("delivery", "devex", "ui-ux", "ml-ai")):
        return "Delivery Workflow"
    return "Governance"


def resolve_ai_lane(skill_type: str, package: str) -> str:
    if skill_type.lower() == "tool" or package == "Official-DotNet-AI":
        return "Tooling"
    return resolve_entity_lane(skill_type)


def resolve_nuget_publishing_lane(package: str, name: str) -> str:
    if package == "Official-DotNet-NuGet":
        return "Package Management"
    if name == "nuget-trusted-publishing":
        return "Package Publishing"
    return "Package Management"


def resolve_diagnostics_lane(package: str, category: str, name: str) -> str:
    if package == "CodeQL":
        return "Static Analysis"
    lowered = name.lower()
    if "tombstone" in lowered or "dump" in lowered or "crash" in lowered:
        return "Crash Analysis"
    if name == "exp-simd-vectorization":
        return "Performance"
    if package in {"cloc", "Complexity", "QuickDup"}:
        return "Observability"
    if category.lower() == "metrics" or package in {"Asynkron-Profiler", "Official-DotNet-Diagnostics", "Profiling"}:
        return "Performance"
    return "Static Analysis"


def resolve_entity_lane(skill_type: str) -> str:
    if skill_type == "Library":
        return "Libraries"
    if skill_type == "Framework":
        return "Frameworks"
    return "Foundations"


def get_stack_rank(stack: str) -> int:
    try:
        return STACK_ORDER.index(stack)
    except ValueError:
        return len(STACK_ORDER)


def get_lane_rank(lane: str) -> int:
    try:
        return LANE_ORDER.index(lane)
    except ValueError:
        return len(LANE_ORDER)


def resolve_mixed_bundle_stack(skills: list[dict[str, object]]) -> str:
    counts: dict[str, int] = {}
    for skill in skills:
        stack = str(skill.get("stack", "")).strip()
        if not stack:
            continue
        counts[stack] = counts.get(stack, 0) + 1

    if not counts:
        return ""

    return sorted(counts.items(), key=lambda item: (-item[1], get_stack_rank(item[0]), item[0].casefold(), item[0]))[0][0]


def build_catalog_definitions(skills: list[dict[str, object]] | None = None) -> dict[str, list[str]]:
    resolved_skills = skills if skills is not None else collect_skills()
    return {
        "categories": resolve_category_order(resolved_skills),
        "typeDirectories": get_type_directories(),
    }


def parse_simple_yaml_mapping(path: Path, raw_frontmatter: str) -> dict[str, object]:
    data: dict[str, object] = {}
    lines = raw_frontmatter.splitlines()
    index = 0

    while index < len(lines):
        line = lines[index]
        stripped = line.strip()
        if not stripped:
            index += 1
            continue

        if ":" not in line:
            raise ValueError(f"{path} has malformed frontmatter line: {line}")

        key, raw_value = line.split(":", 1)
        key = key.strip()
        value = raw_value.strip()

        if re.fullmatch(r"[>|][+-]?", value):
            index += 1
            block_lines: list[str] = []
            while index < len(lines):
                candidate = lines[index]
                if candidate.startswith("  ") or candidate.startswith("\t"):
                    block_lines.append(candidate.lstrip())
                    index += 1
                    continue
                if not candidate.strip():
                    block_lines.append("")
                    index += 1
                    continue
                break
            data[key] = " ".join(part for part in (segment.strip() for segment in block_lines) if part)
            continue

        if not value:
            index += 1
            items: list[str] = []
            while index < len(lines):
                candidate = lines[index]
                if candidate.startswith("  - "):
                    items.append(candidate[4:].strip())
                    index += 1
                    continue
                if not candidate.strip():
                    index += 1
                    continue
                break
            data[key] = items
            continue

        index += 1
        continuation_lines: list[str] = []
        while index < len(lines):
            candidate = lines[index]
            if candidate.startswith("  ") or candidate.startswith("\t"):
                continuation_lines.append(candidate.strip())
                index += 1
                continue
            if not candidate.strip():
                index += 1
                continue
            break

        scalar_value = unquote(value)
        if continuation_lines:
            scalar_value = " ".join(part for part in [scalar_value, *continuation_lines] if part)
        data[key] = scalar_value

    return data


def parse_frontmatter(path: Path) -> tuple[dict[str, str], str]:
    text = path.read_text(encoding="utf-8")
    if not text.startswith("---\n"):
        raise ValueError(f"{path} is missing YAML frontmatter")

    match = re.match(r"^---\n(.*?)\n---\n(.*)$", text, flags=re.DOTALL)
    if not match:
        raise ValueError(f"{path} has invalid frontmatter")

    raw_frontmatter, body = match.groups()
    parsed = parse_simple_yaml_mapping(path, raw_frontmatter)
    data: dict[str, str] = {}
    for key, value in parsed.items():
        if isinstance(value, list):
            raise ValueError(f"{path} field {key!r} must be a scalar value")
        data[key] = str(value)
    return data, body


def parse_frontmatter_with_lists(path: Path) -> tuple[dict[str, str | list[str]], str]:
    text = path.read_text(encoding="utf-8")
    if not text.startswith("---\n"):
        raise ValueError(f"{path} is missing YAML frontmatter")

    match = re.match(r"^---\n(.*?)\n---\n(.*)$", text, flags=re.DOTALL)
    if not match:
        raise ValueError(f"{path} has invalid frontmatter")

    raw_frontmatter, body = match.groups()
    parsed = parse_simple_yaml_mapping(path, raw_frontmatter)
    data: dict[str, str | list[str]] = {}
    for key, value in parsed.items():
        if isinstance(value, list):
            data[key] = [str(item) for item in value]
        else:
            data[key] = str(value)
    return data, body


def parse_title(body: str, path: Path, fallback_title: str) -> str:
    for line in body.splitlines():
        stripped = line.strip()
        if stripped.startswith("#"):
            return re.sub(r"^#+\s*", "", stripped).strip() or fallback_title
    return fallback_title


def load_package_manifest(package_dir: Path) -> tuple[Path, dict]:
    manifest_path = package_dir / "manifest.json"
    if not manifest_path.exists():
        raise ValueError(f"{package_dir} is missing manifest.json")

    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise ValueError(f"{manifest_path} contains invalid JSON: {exc}") from exc

    if not isinstance(manifest, dict):
        raise ValueError(f"{manifest_path} must contain a JSON object")

    return manifest_path, manifest


def load_optional_entity_manifest(entity_dir: Path) -> tuple[Path, dict] | tuple[None, dict]:
    manifest_path = entity_dir / "manifest.json"
    if not manifest_path.exists():
        return None, {}

    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise ValueError(f"{manifest_path} contains invalid JSON: {exc}") from exc

    if not isinstance(manifest, dict):
        raise ValueError(f"{manifest_path} must contain a JSON object")

    return manifest_path, manifest


def _read_skill_manifest_metadata(manifest_path: Path | None, entity_manifest: dict, expected_manifest_path: Path) -> dict[str, object]:
    if not manifest_path or not entity_manifest:
        raise ValueError(f"{expected_manifest_path} is required and must define `version` and `category`")

    allowed = {"version", "category", "compatibility", "packages", "package_prefix"}
    unknown = sorted(set(entity_manifest) - allowed)
    if unknown:
        raise ValueError(f"{manifest_path} has unsupported keys: {', '.join(unknown)}")

    result: dict[str, object] = {}

    version = entity_manifest.get("version")
    if not isinstance(version, str) or not version.strip():
        raise ValueError(f"{manifest_path} field `version` must be a non-empty string")
    result["version"] = version.strip()

    category = entity_manifest.get("category")
    if not isinstance(category, str) or not category.strip():
        raise ValueError(f"{manifest_path} field `category` must be a non-empty string")
    result["category"] = category.strip()

    if "compatibility" in entity_manifest:
        compatibility = entity_manifest["compatibility"]
        if not isinstance(compatibility, str) or not compatibility.strip():
            raise ValueError(f"{manifest_path} field `compatibility` must be a non-empty string")
        result["compatibility"] = compatibility.strip()

    if "packages" in entity_manifest:
        packages = entity_manifest["packages"]
        if not isinstance(packages, list) or any(not isinstance(item, str) or not item.strip() for item in packages):
            raise ValueError(f"{manifest_path} field `packages` must be a list of non-empty strings")
        result["packages"] = [item.strip() for item in packages]

    if "package_prefix" in entity_manifest:
        package_prefix = entity_manifest["package_prefix"]
        if not isinstance(package_prefix, str) or not package_prefix.strip():
            raise ValueError(f"{manifest_path} field `package_prefix` must be a non-empty string")
        result["package_prefix"] = package_prefix.strip()

    return result


def _read_agent_manifest_metadata(manifest_path: Path | None, entity_manifest: dict) -> dict[str, object]:
    if not manifest_path or not entity_manifest:
        return {}

    allowed = {"packages", "package_prefix"}
    unknown = sorted(set(entity_manifest) - allowed)
    if unknown:
        raise ValueError(f"{manifest_path} has unsupported keys: {', '.join(unknown)}")

    result: dict[str, object] = {}

    if "packages" in entity_manifest:
        packages = entity_manifest["packages"]
        if not isinstance(packages, list) or any(not isinstance(item, str) or not item.strip() for item in packages):
            raise ValueError(f"{manifest_path} field `packages` must be a list of non-empty strings")
        result["packages"] = [item.strip() for item in packages]

    if "package_prefix" in entity_manifest:
        package_prefix = entity_manifest["package_prefix"]
        if not isinstance(package_prefix, str) or not package_prefix.strip():
            raise ValueError(f"{manifest_path} field `package_prefix` must be a non-empty string")
        result["package_prefix"] = package_prefix.strip()

    return result


def ensure_no_legacy_package_skill_map(manifest_path: Path, package_manifest: dict) -> None:
    if "skills" in package_manifest:
        raise ValueError(
            f"{manifest_path} must not define a top-level `skills` map; move skill-specific metadata into "
            "the nearest `skills/<skill>/manifest.json` file"
        )


def _read_package_links(manifest_path: Path, package_manifest: dict) -> dict[str, str]:
    raw_links = package_manifest.get("links", {})
    if raw_links in (None, {}):
        return {}
    if not isinstance(raw_links, dict):
        raise ValueError(f"{manifest_path} field `links` must be an object")

    unknown = sorted(set(raw_links) - set(LINK_KEYS))
    if unknown:
        raise ValueError(f"{manifest_path} field `links` has unsupported keys: {', '.join(unknown)}")

    links: dict[str, str] = {}
    for key in LINK_KEYS:
        value = raw_links.get(key)
        if value in (None, ""):
            continue
        if not isinstance(value, str) or not value.strip():
            raise ValueError(f"{manifest_path} field `links.{key}` must be a non-empty string")
        links[key] = value.strip()

    return links


@lru_cache(maxsize=1)
def load_token_counts() -> dict[str, int]:
    result = subprocess.run(
        [
            "dotnet",
            "run",
            "-v",
            "q",
            "--project",
            str(CLI_PROJECT),
            "--",
            "catalog",
            "tokens",
            "--catalog-root",
            str(ROOT),
        ],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=True,
    )
    raw_output = result.stdout.strip()
    json_payload = raw_output.splitlines()[-1] if raw_output else ""
    payload = json.loads(json_payload)
    return {
        str(item.get("path") or item.get("Path")): int(item.get("tokenCount", item.get("TokenCount", 0)))
        for item in payload.get("skills", [])
    }


def collect_skills(include_token_counts: bool = False) -> list[dict[str, object]]:
    skills: list[dict[str, object]] = []
    token_counts = load_token_counts() if include_token_counts else {}

    for type_dir_name in get_type_directories():
        type_dir = CATALOG_ROOT / type_dir_name
        if not type_dir.is_dir():
            continue

        skill_type = singularize_type_dir(type_dir_name)

        for package_dir in sorted(p for p in type_dir.iterdir() if p.is_dir()):
            manifest_path, package_manifest = load_package_manifest(package_dir)
            ensure_no_legacy_package_skill_map(manifest_path, package_manifest)
            package_links = _read_package_links(manifest_path, package_manifest)
            skills_subdir = package_dir / "skills"
            if not skills_subdir.is_dir():
                continue

            package_name = package_dir.name

            for skill_dir in sorted(p for p in skills_subdir.iterdir() if p.is_dir()):
                skill_path = skill_dir / "SKILL.md"
                if not skill_path.exists():
                    continue
                skill_manifest_path, skill_manifest = load_optional_entity_manifest(skill_dir)

                metadata, body = parse_frontmatter(skill_path)
                title = parse_title(body, skill_path, metadata["name"])

                required = ["name", "description"]
                missing = [key for key in required if key not in metadata or not metadata[key].strip()]
                if missing:
                    raise ValueError(f"{skill_path} is missing required frontmatter keys: {', '.join(missing)}")

                disallowed = [key for key in ("version", "category", "packages", "package_prefix") if key in metadata]
                if disallowed:
                    raise ValueError(
                        f"{skill_path} must not declare {', '.join(disallowed)} in frontmatter; move them to {skill_dir / 'manifest.json'}"
                    )

                skill_name = metadata["name"]
                skill_manifest_metadata = _read_skill_manifest_metadata(skill_manifest_path, skill_manifest, skill_dir / "manifest.json")
                compatibility = metadata.get("compatibility", "").strip() or str(skill_manifest_metadata.get("compatibility", "")).strip()
                if not compatibility:
                    raise ValueError(
                        f"{skill_path} must define `compatibility` in frontmatter or in {skill_dir / 'manifest.json'}"
                    )
                stack, lane = classify_skill(skill_type, package_name, str(skill_manifest_metadata["category"]), skill_name)

                skill_entry: dict[str, object] = {
                    "name": skill_name,
                    "title": title,
                    "version": skill_manifest_metadata["version"],
                    "category": skill_manifest_metadata["category"],
                    "type": skill_type,
                    "package": package_name,
                    "stack": stack,
                    "lane": lane,
                    "description": metadata["description"],
                    "compatibility": compatibility,
                    "path": f"catalog/{type_dir_name}/{package_name}/skills/{skill_dir.name}/",
                }
                if include_token_counts:
                    skill_entry["tokenCount"] = token_counts.get(skill_entry["path"], 0)

                if package_links:
                    skill_entry["links"] = package_links
                for key in ("packages", "package_prefix"):
                    if key in skill_manifest_metadata:
                        skill_entry[key] = skill_manifest_metadata[key]
                skills.append(skill_entry)

    ensure_unique_entries(skills, "skill")
    return skills


def collect_agents() -> list[dict[str, object]]:
    agents: list[dict[str, object]] = []

    for type_dir_name in get_type_directories():
        type_dir = CATALOG_ROOT / type_dir_name
        if not type_dir.is_dir():
            continue

        for package_dir in sorted(p for p in type_dir.iterdir() if p.is_dir()):
            manifest_path, package_manifest = load_package_manifest(package_dir)
            ensure_no_legacy_package_skill_map(manifest_path, package_manifest)
            package_links = _read_package_links(manifest_path, package_manifest)
            agents_dir = package_dir / "agents"
            if not agents_dir.is_dir():
                continue

            for agent_dir in sorted(p for p in agents_dir.iterdir() if p.is_dir()):
                agent_path = agent_dir / "AGENT.md"
                if not agent_path.exists():
                    continue
                agent_manifest_path, agent_manifest = load_optional_entity_manifest(agent_dir)

                metadata, body = parse_frontmatter_with_lists(agent_path)
                title = parse_title(body, agent_path, str(metadata["name"]))

                required = ["name", "description"]
                missing = [key for key in required if key not in metadata or not str(metadata[key]).strip()]
                if missing:
                    raise ValueError(f"{agent_path} is missing required frontmatter keys: {', '.join(missing)}")

                agent_entry: dict[str, object] = {
                    "name": str(metadata["name"]),
                    "title": title,
                    "description": str(metadata["description"]),
                    "skills": metadata.get("skills", []),
                    "tools": metadata.get("tools", ""),
                    "model": metadata.get("model", "inherit"),
                    "package": package_dir.name,
                    "type": type_dir_name,
                    "path": f"catalog/{type_dir_name}/{package_dir.name}/agents/{agent_dir.name}/",
                }
                if package_links:
                    agent_entry["links"] = package_links
                agent_entry.update(_read_agent_manifest_metadata(agent_manifest_path, agent_manifest))
                agents.append(agent_entry)

    ensure_unique_entries(agents, "agent")
    return agents


def ensure_unique_entries(entries: list[dict[str, object]], kind: str) -> None:
    seen: dict[str, str] = {}
    duplicates: list[str] = []

    for entry in entries:
        name = str(entry["name"])
        path = str(entry["path"])
        previous = seen.get(name)
        if previous is None:
            seen[name] = path
            continue
        duplicates.append(f"{name}: {previous}, {path}")

    if duplicates:
        raise ValueError(f"Duplicate {kind} ids were found in catalog metadata: {'; '.join(duplicates)}")


def build_bundles(skills: list[dict[str, object]]) -> list[dict[str, object]]:
    skills_by_name = {str(skill["name"]): skill for skill in skills}
    bundles: list[dict[str, object]] = []

    for bundle in CURATED_BUNDLES:
        missing = [skill_name for skill_name in bundle["skills"] if skill_name not in skills_by_name]
        if missing:
            continue

        bundles.append(
            {
                "name": bundle["name"],
                "title": bundle["title"],
                "description": bundle["description"],
                "kind": bundle["kind"],
                "stack": bundle.get("stack", ""),
                "lane": bundle.get("lane", ""),
                "sourceCategory": "",
                "skills": bundle["skills"],
            }
        )

    return bundles


def build_skill_manifest(skills: list[dict[str, object]], bundles: list[dict[str, object]]) -> dict[str, object]:
    return {"skills": skills, "bundles": bundles}


def build_agent_manifest(agents: list[dict[str, object]]) -> dict[str, object]:
    return {"agents": agents}
