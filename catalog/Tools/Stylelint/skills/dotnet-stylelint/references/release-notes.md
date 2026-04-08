# Stylelint 17.5.0 Notes

Use this note when a repo already has Stylelint wired and you need to refresh config or validation behavior after the `17.5.0` release.

## What changed that matters for repository maintenance

- `declaration-property-value-no-unknown` deprecates its `*syntax` rule options.
- `media-feature-name-value-no-unknown` adds `ignoreMediaFeatureNameValues`.
- The Node.js API now correctly ignores `node_modules` again when `codeFilename` is used.
- Selector and declaration fixes reduce false positives and false negatives in:
  - `no-descending-specificity`
  - `no-duplicate-selectors`
  - `declaration-property-value-keyword-no-deprecated`
  - `declaration-property-value-no-unknown`
  - `no-invalid-position-declaration`
  - `property-no-unknown`

## Practical repo guidance

1. Check whether the config still uses deprecated `*syntax` options.
2. Replace those options with explicit syntax handling via `customSyntax` or the repo's selected syntax package.
3. If the repo uses `media-feature-name-value-no-unknown` and intentionally permits platform-specific values, document them via `ignoreMediaFeatureNameValues`.
4. Re-run the lint baseline and remove stale ignores or waivers that only existed to compensate for older false positives.
5. If the repo drives Stylelint through the Node.js API, drop any workaround ignore logic that was only there because `codeFilename` used to bypass the default `node_modules` ignore behavior.

## Source

- [Stylelint 17.5.0 release notes](https://github.com/stylelint/stylelint/releases/tag/17.5.0)
