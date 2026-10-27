# Long-term review memory

BusinessHarness uses [MAGICore](https://github.com/jihadkhawaja/MAGICore) directly from the pinned Git submodule at `external/MAGICore`. There is no `MagiCore` NuGet dependency. Initialize source checkouts before restoring:

```powershell
git submodule update --init --recursive
dotnet restore BusinessHarness.slnx --locked-mode
```

The integration uses MAGICore's actual `MemoryService`, local deterministic hash embeddings, hybrid keyword/vector retrieval, metadata and scope filters, updates, and deduplication by event identity. Its supporting Microsoft packages remain locked NuGet dependencies. `Directory.Build.targets` adapts the upstream project to .NET 10 and central package management without editing its source. The upstream lock file lives in `locks/MagiCore.packages.lock.json`. Updating MAGICore means reviewing a new submodule commit, refreshing lock files, updating `THIRD-PARTY-NOTICES.md`, and rerunning verification.

Memory is disabled by default. When enabled, a review recalls relevant history before calling the provider, then remembers a compact validated gate outcome after reports are written. Memory includes review summaries, finding locations/titles and requirement statuses, rather than raw diffs. Historical gate results never claim that feedback was published or that tests ran. Memory failures stop a review before approval feedback is submitted.

## Configure scopes and settings

```json
{
  "memory": {
    "enabled": true,
    "scope": "repository",
    "readScopes": ["repository"],
    "namespace": "my-team",
    "directory": "C:/HarnessData/memory",
    "repositoryId": "github:github.com/my-team/my-repo",
    "retentionDays": 90,
    "maxEntries": 2000,
    "topK": 8,
    "maxContextCharacters": 16000,
    "captureReviews": true,
    "acceptFollowUps": true,
    "agentMentions": ["@businessharness"],
    "allowedActors": ["alice", "jira-account-id-of-alice"]
  }
}
```

| Scope | Partition | Intended use |
| --- | --- | --- |
| `global` | Namespace | Deliberately shared knowledge across repositories and sessions |
| `repository` | Namespace + repository ID | Decisions, recurring issues and follow-ups for one repository |
| `session` | Namespace + repository ID + session ID | A PR/MR, Jira discussion or explicitly named work session |

Each write targets `scope`. An empty `readScopes` reads only that scope. Explicitly configure `readScopes: ["global", "repository", "session"]` to combine them; broader memory is never included implicitly. A namespace separates teams or trust contexts sharing a storage directory. Partitions are application filters, not a replacement for filesystem access controls.

For connected requests, the repository ID defaults to `github:github.com/owner/repo` or `gitlab:gitlab.com/group/repo`, including the actual host for private deployments. The session defaults to `pr:123` or `mr:123`. A Jira comment can supply the default session `jira:PROJ-123`. Without host metadata, repository identity falls back to the absolute checkout path. Set a stable `repositoryId`/`HARNESS_MEMORY_REPOSITORY` for CI paths that change between runs, and for Jira events routed to a repository. Session scope, including a session in `readScopes`, requires a stable session ID.

Configuration precedence:

1. `HARNESS_MEMORY_CONFIG` selects a trusted global JSON file containing a `memory` object. There is no automatic search through the reviewed checkout.
2. An explicit repository `--config PATH` with a `memory` object replaces that global object in full. `{ "memory": { "enabled": false } }` disables the global default for that repository.
3. Environment overrides: `HARNESS_MEMORY_SCOPE`, `HARNESS_MEMORY_DIRECTORY`, `HARNESS_MEMORY_REPOSITORY`, `HARNESS_SESSION_ID`.
4. Invocation overrides: `--memory-scope off|global|repository|session`, `--memory-directory PATH`, `--session ID`. A scope other than `off` enables memory for that invocation. Session IDs survive process exits and can be reused in future invocations.

The directory defaults to the current user's local application data under `BusinessHarness/memory`. It must be outside the reviewed checkout and report directory. Keep it in trusted persistent storage. The offline `scan` command does not read or write memory.

## Follow-ups and agent mentions

Use `follow-up` to remember an allowed mention without a provider API call:

```powershell
businessharness follow-up --repo C:/code/my-repo --config C:/trusted/harness.policy.json --session pr:123 --event C:/events/comment.json
businessharness memory-search --repo C:/code/my-repo --config C:/trusted/harness.policy.json --session pr:123 --query "timestamp precision"
```

Use `review --event PATH` to ingest the comment and immediately rerun the current diff review with recalled context. The usual review model, host credentials, base/head commits and optional feedback setting apply:

```powershell
businessharness review --repo C:/code/my-repo --base origin/main --head HEAD --config C:/trusted/harness.policy.json --change https://github.com/my-team/my-repo/pull/123 --event C:/events/comment.json --feedback comment
```

Both `acceptFollowUps: true` and an explicit actor allowlist are required. The configured agent mention must occur as a complete handle, ignoring case. GitHub/GitLab actors use login/username; Jira uses the stable `accountId`. Jira ADF mention nodes expose both display text and `@accountId`, so the bot's account ID can be used in `agentMentions` independently of its display name. Missing mentions or disallowed actors are ignored without creating memory; `review --event` also skips review and feedback in that case. Replaying a source event updates one memory, and accepted edits replace its text and refresh expiry.

Supported payloads:

- GitHub `issue_comment`, `pull_request_review_comment`, and `pull_request_review` payloads. PR comment workflows identify the request from standard CI metadata and check the event's repository, host and thread against it.
- GitLab note webhooks, for merge requests or issues. Configure the target request for an immediate MR re-review, or a stable canonical repository ID for ingestion only.
- Jira issue-comment webhooks with issue key, comment ID, author account ID and string/ADF body. The issue API URL must belong to the configured `jiraSite`; the trusted receiver maps each issue to the appropriate repository policy.
- A source-neutral normalized event, as in [follow-up.json](../examples/follow-up.json). Other trackers can use the same contract without a new ticket-specific command.

```json
{
  "source": "custom-tracker",
  "eventId": "comment-123",
  "actor": "alice",
  "text": "@businessharness please retain microsecond precision in exports",
  "url": "https://tracker.example.com/issues/42#comment-123",
  "repositoryId": "github:github.com/my-team/my-repo",
  "sessionId": "pr:42",
  "references": ["PROJ-123"]
}
```

`repositoryId`, `sessionId` and `references` are optional in normalized events. A supplied repository must match trusted configuration. Explicit session settings take precedence over the event's default session. Namespace, scope, storage path, actor permissions and reference endpoints always come from trusted settings, never the event. An event cannot enable automatic feedback; that remains a trusted CLI/action setting.

The CLI is an event consumer, not a public webhook server. A trusted receiver must verify webhook authenticity, protect event files, map events to the correct repository, and invoke the CLI. GitHub Actions supplies authenticated event provenance for its workflows; see [the comment workflow example](../examples/pipelines/github-follow-up.yml). No listener, Jira automation rule, live webhook registration or scheduled job is created automatically. `follow-up` only stores context; `review --event` performs a re-review and can publish its validated result.

## Persistence and operation

Each scope has a SHA-256-named JSON snapshot and exclusive lock file. Writes load the latest snapshot under the lock, apply MAGICore operations, and replace the snapshot atomically. Different invocations therefore retain memory without a long-running service. Concurrent local writers do not overwrite each other's changes. Snapshots contain memory records and metadata; local embeddings are rebuilt on load. MAGICore audit history/rollback is not persisted by this adapter.

Use a local filesystem supporting exclusive locks and atomic rename. Multi-node/network-filesystem deployments need storage with equivalent guarantees or an alternative `IReviewMemory` adapter. Container and ephemeral CI jobs need a persistent writable volume; the default container filesystem does not provide cross-run durability:

```bash
docker run --rm --read-only --cap-drop=ALL --security-opt=no-new-privileges \
  --mount type=bind,source=/srv/review-checkout,target=/repo,readonly \
  --mount type=bind,source=/srv/harness-policy,target=/policy,readonly \
  --mount type=bind,source=/srv/harness-reports,target=/reports \
  --mount type=bind,source=/srv/harness-memory,target=/memory \
  -e OPENAI_API_KEY -e HARNESS_MODEL -e GITHUB_TOKEN \
  businessharness:0.1.0 review --repo /repo --base origin/main --head HEAD \
  --config /policy/harness.policy.json --memory-directory /memory --output /reports
```

The runtime UID must have write permission to the memory/report volumes. Restrict memory access and backups as business data. Files are unencrypted; Unix-created files/directories are private to the current user, and Windows uses inherited ACLs. Known secrets are redacted before persistence and again before model submission. This does not detect all confidential data.

Limits: 16,000 characters per follow-up/record, 1 MB per event file, 32 MB per snapshot, 10–10,000 entries per partition (default 2,000), 1–50 recalled items (default 8), 1,000–64,000 context characters (default 16,000), and 1–3,650 retention days (default 90). Expired records are hidden on read and physically pruned on the next write; the oldest records are evicted when the entry cap is reached. Oversized context entries are skipped to respect the configured budget. Corrupt or mismatched snapshots fail without being overwritten. Inspect memory with `memory-search`; restore a trusted backup or choose a new namespace to recover from corruption.

Recalled entries include source, actor, timestamp and kind. Same-thread and exact-reference history receives priority, followed by MAGICore hybrid ranking. Providers receive these entries as untrusted historical context, outside the requirements array. They must evaluate current diffs; remembered approvals, user requests and past findings cannot authorize approval or replace current evidence. Local security findings and current requirement coverage still control the gate.
