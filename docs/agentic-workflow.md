# How this repo is built: ArggonManager

This repo is built by AI agents working under
[ArggonManager](https://github.com/Arggon/ArggonManager), a methodology and CLI
I (Gonzalo) designed for shipping software with agents without losing control
of quality. It replaces the OpenSpec-based workflow I used before.

## Who does what

| Role | Responsibility |
|---|---|
| **Product owner (Gonzalo)** | Sets priority, writes or approves specs, accepts stories, waives risks, merges and releases. These stay human-only. |
| **Delivery lead** (agent) | Sequences the work, dispatches tasks, checks merges. |
| **Practice & standards** (agent) | Reviews each PR against the project bar (playbooks, ADRs, tests). |
| **Maker** (agent) | Implements a claimed task on its own branch. |
| **Verifier** (agent) | Runs the gates (build, tests, Testcontainers, benchmarks) and reports observed vs expected, without giving the verdict. |

## The loop for one feature

1. **Spec** in `ArggonManager/docs/specs/`: invariants and acceptance criteria.
2. **Plan** in `ArggonManager/docs/plans/`: tasks with dependencies.
3. **Tasks** as Markdown items under `ArggonManager/<initiative>/…`, each with a live `## Acceptance` checklist.
4. **Branch per task**, test-first commits (red, then green), one small PR per task that references the item id.
5. **Review verdict** recorded on the item with evidence for each acceptance box.
6. **Merge by the product owner**, then the done gate: no unticked acceptance criterion.

Hard-to-reverse choices get an ADR first (`ArggonManager/docs/adr/`), and every
technology gets a pinned playbook (`ArggonManager/docs/playbooks/`).
`arggon validate` runs in CI next to the .NET build.

## What was written by agents

Most code, tests and docs in this repo were written by agents. I designed the
method, wrote or approved the specs and ADRs, review every PR and decide what
gets merged. The PR history shows the order things happened in.
