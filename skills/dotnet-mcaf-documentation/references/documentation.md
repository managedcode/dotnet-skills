# Documentation

Documentation should reduce guessing, not create a second project to maintain.

## Principles

- one durable fact has one canonical home
- entry pages route the reader to detail instead of copying it
- docs change with the code or policy they describe
- temporary notes do not belong in durable documentation

## What to Document

- architecture boundaries and entry points
- feature behaviour that needs stable verification
- architecture decisions and their trade-offs
- local setup, operational constraints, and recurring workflow rules

## What to Avoid

- duplicate tutorials that drift
- giant pages that mix overview, policy, and implementation detail
- templates copied into public pages without adaptation
- stale checklists that nobody updates

## Quality Check

- can a new engineer find the right doc quickly
- can an agent act without inventing missing behaviour
- is each important fact linked from the right entry page
