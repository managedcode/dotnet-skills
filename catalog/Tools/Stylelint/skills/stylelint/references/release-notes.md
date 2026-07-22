# Stylelint 17.14.1 Notes

Use this note when a repo already has Stylelint wired and you need to refresh config or validation behavior after the `17.14.1` release. It also preserves the earlier 17.8 notes because those introduced new rule/config surfaces that still matter during upgrades.

## What changed that matters for repository maintenance

- `17.14.1` stops `quiet` from suppressing `report*` warning reports.
- `17.14.1` fixes the reported source range for unknown rules.
- `17.14.1` prevents `declaration-block-no-redundant-longhand-properties` from producing an invalid `background` shorthand when `background-size` is present.
- `17.14.1` fixes `rule-empty-line-before` false positives for shared-line comments with `except: ["after-single-line-comment"]`.
- `languageOptions.directionality` lets the config declare writing direction explicitly, which matters when a repo uses logical properties or bidirectional UI conventions.
- `property-layout-mappings` adds a new rule for property-to-layout relationships, so repos with strict layout policy may need to tune or adopt it during upgrade.
- `relative-selector-nesting-notation` gives repos a rule for nested selector notation, which can surface issues in Sass or nested CSS codebases.
- `selector-no-deprecated` adds a dedicated check for deprecated selector usage, which may reveal cleanup work in older style baselines.

## Practical repo guidance

1. Check whether the config still uses deprecated `*syntax` options.
2. Replace those options with explicit syntax handling via `customSyntax` or the repo's selected syntax package.
3. If the repo uses direction-sensitive styles, set `languageOptions.directionality` deliberately instead of relying on implied defaults.
4. Run the configured command once normally and once with `--quiet` when the repo relies on `report*` warnings, then verify both exit behavior and output.
5. Inspect `background` shorthand autofixes before accepting them, especially when longhands include `background-size`.
6. Re-run the lint baseline and remove stale ignores or waivers that only existed to compensate for older false positives or now-covered deprecated selectors.
7. If the repo introduces `property-layout-mappings` or `relative-selector-nesting-notation`, verify the new warnings map to real design-system policy rather than parser noise.

## Source

- [Stylelint 17.14.1 release notes](https://github.com/stylelint/stylelint/releases/tag/17.14.1)
- [Stylelint 17.8.0 release notes](https://github.com/stylelint/stylelint/releases/tag/17.8.0)
