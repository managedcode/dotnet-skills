from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = REPO_ROOT / "scripts" / "waza_skill_quality.py"
SPEC = importlib.util.spec_from_file_location("waza_skill_quality", MODULE_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Failed to load waza_skill_quality module from {MODULE_PATH}")
WAZA_QUALITY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(WAZA_QUALITY)


class WazaSkillQualityTests(unittest.TestCase):
    def test_vendir_managed_package_prefixes_are_imported(self) -> None:
        self.assertTrue(
            WAZA_QUALITY.is_imported_skill_path(
                "catalog/Frameworks/ThreeJS-WebGPU-TSL/skills/webgpu-threejs-tsl/SKILL.md"
            )
        )
        self.assertTrue(
            WAZA_QUALITY.is_imported_skill_path(
                "catalog/Platform/Official-DotNet/skills/setup-local-sdk/SKILL.md"
            )
        )

    def test_repo_owned_package_is_not_imported(self) -> None:
        self.assertFalse(
            WAZA_QUALITY.is_imported_skill_path(
                "catalog/Frameworks/ASPNet-Core/skills/aspnet-core/SKILL.md"
            )
        )


if __name__ == "__main__":
    unittest.main()
