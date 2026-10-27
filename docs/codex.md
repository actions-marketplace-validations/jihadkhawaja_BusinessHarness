# Working with Codex

[AGENTS.md](../AGENTS.md) describes the architecture, security boundaries, coding
conventions and validation commands for contributors. The project-scoped custom
agents are standalone TOML definitions in [.codex/agents](../.codex/agents), using
the format in the [official OpenAI documentation](https://learn.chatgpt.com/docs/agent-configuration/subagents).

Open this repository in a Codex client that supports project-scoped custom agents.
Start a new session after adding or changing definitions if the existing session
does not discover them. These files omit model/effort overrides so agents inherit
the session's choices. They also inherit parent permissions; the review-only rules
below are behavioral instructions, not an operating-system sandbox. Choose the
parent session's permissions appropriate to the work before delegation.

| Agent | Purpose | Writes |
| --- | --- | --- |
| [`security_reviewer`](../.codex/agents/security_reviewer.toml) | Trace security-sensitive changes and report concrete defects | No source edits or check execution |
| [`validation_runner`](../.codex/agents/validation_runner.toml) | Run relevant verification and offline smoke checks | Ignored build/package output and isolated fixtures only |
| [`supply_chain_reviewer`](../.codex/agents/supply_chain_reviewer.toml) | Inspect package/SDK/submodule pins, CI trust and container restrictions | No source edits or check execution |

Example requests:

```text
Review my uncommitted changes with security_reviewer. Report actionable defects
with file/line evidence and missing regression cases; do not edit source.
```

```text
Have validation_runner validate my changes with the repository's verification
and offline smoke scripts. Report commands, exit codes, test totals and anything
that could not run. Give it exclusive access to build outputs while checks run.
```

```text
Review this branch against origin/main with security_reviewer and
supply_chain_reviewer, then run validation_runner after edits are complete.
Keep review findings separate from execution results.
```

Supply an existing base ref when reviewing committed changes; reviewers should not
guess the comparison. Uncommitted reviews include staged, unstaged and relevant
untracked files. Static review can run beside validation, but only one process or
agent should own restore/build/test/format/pack or container work in a shared
checkout. The parent agent implements fixes, evaluates findings and reruns affected
checks; review agents do not recursively delegate.

For documentation and agent-only changes, checking TOML, links, paths and command
accuracy is normally sufficient. For runtime changes, use `scripts/Verify.ps1` and
`scripts/Smoke-Test.ps1`; for container changes add the Docker checks in
[AGENTS.md](../AGENTS.md). Do not disable package audit or regenerate locks to work
around a failing check. Record network/tooling failures and skipped checks explicitly.

These contributor agents are separate from BusinessHarness's runtime review
providers. Adding them does not launch Codex from the CLI, grant provider tools,
run checks automatically in CI, or publish feedback. Existing
[CI](../.github/workflows/ci.yml) runs the deterministic checks. No live model calls,
host comments, approvals or publishing are needed to validate contributor changes.
