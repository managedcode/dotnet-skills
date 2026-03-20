# Developer Experience

Developer experience is the cost of doing normal engineering work in the repo.

## Essential Tasks

- build
- run
- debug
- test
- understand where to make a change

## Targets

- a new engineer can get to a first working result quickly
- the local inner loop is documented and reproducible
- common tasks use one obvious command path
- local setup does not depend on tribal knowledge

## Team Rules

- standardize the core commands before optimizing them
- reduce manual setup steps wherever possible
- document local dependencies and how to start them
- prefer one clear path for multi-service startup over per-service guesswork
- fix onboarding friction in the repo, not by telling people in chat

## Common Smells

- different engineers use different hidden startup sequences
- tests only work in CI
- local debugging requires remote-only dependencies
- onboarding docs are stale the week after they are written
