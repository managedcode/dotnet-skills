from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import generate_pages


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
