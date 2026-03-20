# Source Control

Source-control policy exists to keep collaboration predictable and reviewable.

## Baseline Rules

- one protected main branch is the default source of truth
- changes land through pull requests or an equally reviewable flow
- branch naming is explicit and documented
- merge strategy is chosen intentionally, not ad hoc
- secrets in git are treated as incidents

## Team Decisions to Make Early

- default branch and release branch model
- merge strategy: squash, rebase, or merge commit
- PR expectations: size, review, and verification
- tag and versioning rules, if the repo publishes artifacts

## Hygiene

- keep commit messages understandable
- avoid long-lived branches unless the repo truly needs them
- prefer small PRs that can be reviewed coherently
- document the repo’s branch and merge rules in-repo

## Incident Rule

If a secret lands in git history, rotate it first and clean the history second.
