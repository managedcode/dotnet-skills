# Stylelint 17.8.0 Notes

Use this note when a repo already has Stylelint wired and you need to refresh config or validation behavior after the `17.8.0` release.

## What changed that matters for repository maintenance

- `languageOptions.directionality` lets the config declare writing direction explicitly, which matters when a repo uses logical properties or bidirectional UI conventions.
- `property-layout-mappings` adds a new rule for property-to-layout relationships, so repos with strict layout policy may need to tune or adopt it during upgrade.
- `relative-selector-nesting-notation` gives repos a rule for nested selector notation, which can surface issues in Sass or nested CSS codebases.
- `selector-no-deprecated` adds a dedicated check for deprecated selector usage, which may reveal cleanup work in older style baselines.

## Practical repo guidance

1. Check whether the config still uses deprecated `*syntax` options.
2. Replace those options with explicit syntax handling via `customSyntax` or the repo's selected syntax package.
3. If the repo uses direction-sensitive styles, set `languageOptions.directionality` deliberately instead of relying on implied defaults.
4. Re-run the lint baseline and remove stale ignores or waivers that only existed to compensate for older false positives or now-covered deprecated selectors.
5. If the repo introduces `property-layout-mappings` or `relative-selector-nesting-notation`, verify the new warnings map to real design-system policy rather than parser noise.

## Source

- [Stylelint 17.8.0 release notes](https://github.com/stylelint/stylelint/releases/tag/17.8.0)
