from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import generate_pages


class UpstreamRepositoryRenderingTests(unittest.TestCase):
    def test_repositories_in_nested_references_are_exposed_without_rewriting_markdown(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            skill_dir = Path(directory)
            entrypoint = "# Skill\n[Tool](https://github.com/owner/tool/tree/main/docs)\n"
            (skill_dir / "SKILL.md").write_text(entrypoint, encoding="utf-8")
            references = skill_dir / "references" / "nested"
            references.mkdir(parents=True)
            reference = """# Sources
https://github.com/OWNER/TOOL/issues/1
[Compiler](https://raw.githubusercontent.com/dotnet/roslyn/main/README.md)
[Other](https://gitlab.com/group/subgroup/project/-/blob/main/README.md)
[Mirror](https://codeberg.org/owner/mirror.git)
[Profile](https://github.com/owner)
[Topic](https://github.com/topics/dotnet)
[Docs](https://learn.microsoft.com/dotnet/)
[Catalog](https://github.com/managedcode/dotnet-skills/tree/main/catalog)
"""
            reference_path = references / "sources.md"
            reference_path.write_text(reference, encoding="utf-8")

            repositories = generate_pages.referenced_repositories(skill_dir)

            self.assertEqual(entrypoint, (skill_dir / "SKILL.md").read_text(encoding="utf-8"))
            self.assertEqual(reference, reference_path.read_text(encoding="utf-8"))
        self.assertEqual([
            "https://github.com/owner/tool",
            "https://github.com/dotnet/roslyn",
            "https://gitlab.com/group/subgroup/project",
            "https://codeberg.org/owner/mirror",
        ], repositories)

    def test_manifest_links_remain_primary_and_reference_only_links_render(self) -> None:
        skill = {
            "links": {
                "repository": "https://github.com/Owner/Tool/",
                "docs": "https://example.com/docs",
                "nuget": "https://www.nuget.org/packages/Tool",
            },
            "referenced_repositories": ["https://github.com/owner/tool", "https://github.com/dotnet/roslyn"],
        }
        links = generate_pages.build_upstream_links(skill)
        self.assertEqual("Repository", links[0][0])
        self.assertEqual(4, len(links))
        rendered = generate_pages.render_upstream_sidebar(skill)
        for _, url in links:
            self.assertIn(f'href="{url}"', rendered)
        reference_only = generate_pages.render_upstream_sidebar({
            "referenced_repositories": ["https://github.com/dotnet/roslyn"],
        })
        self.assertIn('href="https://github.com/dotnet/roslyn"', reference_only)

    def test_roslynk_and_nuget_manager_have_original_project_links(self) -> None:
        skills = generate_pages.collect_skills()
        selected = [skill for skill in skills if skill["name"] in {"roslynk", "nuget-package-manager"}]
        documents = generate_pages.load_skill_documents(selected, "https://example.com/")
        expected = {
            "roslynk": "https://github.com/mrpmorris/Roslynk",
            "nuget-package-manager": "https://github.com/managedcode/NuGetPackageManager",
        }
        self.assertEqual(set(expected), {skill["name"] for skill in documents})
        for skill, payload in zip(documents, generate_pages.build_skill_payload(documents, "../")):
            with self.subTest(skill=skill["name"]):
                repository = expected[skill["name"]]
                self.assertIn(f'href="{repository}"', generate_pages.render_upstream_sidebar(skill))
                self.assertIn(f'href="{repository}"', generate_pages.render_skill_card(skill, "../"))
                self.assertIn({"label": "Repository", "url": repository}, payload["upstreamLinks"])


class PackageSignalRenderingTests(unittest.TestCase):
    def test_delimited_prefix_is_consistent_across_public_surfaces(self) -> None:
        skill = {
            "name": "avalonia",
            "short_name": "avalonia",
            "title": "Avalonia",
            "description": "Build Avalonia apps.",
            "detail_url": "skills/avalonia/",
            "lastmod": "2026-10-10",
            "path": "catalog/Frameworks/Avalonia/skills/avalonia",
            "stack": "Desktop & UI",
            "packages": ["Avalonia"],
            "package_prefix": "Avalonia.",
        }
        sidebar = generate_pages.render_nuget_sidebar(skill)
        cards = generate_pages.render_nuget_signal_list(skill)
        index = generate_pages.build_nuget_package_index([skill])
        structured = generate_pages.build_skill_json_ld("https://example.com/", skill, [])
        for rendered in [sidebar, cards, str(index), str(structured)]:
            with self.subTest(surface=rendered[:50]):
                self.assertIn("Avalonia.*", rendered)
                self.assertNotIn("Avalonia..*", rendered)
        self.assertEqual("Avalonia.", skill["package_prefix"])


if __name__ == "__main__":
    unittest.main()
