# Self-managed deployment

BusinessHarness supports on-premises and private cloud deployment. Organizations
manage review execution, runners, credentials, policy, report storage, and
optional memory. Local Git reviews do not require GitHub Actions or a GitHub App.

AI reviews use OpenAI or Claude API credentials and send
bounded, redacted review context directly to that provider. Self-managed
execution still requires approval for this external data flow. The current
providers use fixed public API endpoints; local models, custom model endpoints,
and air-gapped AI review are not supported. The offline `scan` makes no provider,
host, or reference calls and does not use memory.

## Deployment options

| Option | Execution environment | Operational model |
| --- | --- | --- |
| Direct CLI | Organization-managed workstation, server, or VM | An approved tool version runs manually or from an internal scheduler. Requires .NET 10 and Git. |
| Internal CI job | An isolated organization-managed runner or agent | The CLI reviews locally fetched commits using trusted pipeline definitions and policy, with credentials injected through the organization's secret store. |
| Internal container | Organization-managed container host or CI runner | Images built from the supplied Dockerfile can be distributed through an internal registry and pinned to reviewed digests. The image includes .NET and Git. |

These options use the existing CLI without a central BusinessHarness service.
Package/source acquisition and updates are separate from review execution.
Organizations can distribute approved packages and images internally, retaining
the included licenses and notices.
See [packaging and containers](integrations.md#local-packaging-and-containers).

## Start with the CLI

On an organization-managed machine with .NET 10 and Git, install a reviewed release.
Replace `YOUR_APPROVED_VERSION` with an available, approved package version:

```powershell
dotnet tool install --tool-path C:/tools/businessharness BusinessHarness --version YOUR_APPROVED_VERSION
C:/tools/businessharness/businessharness --help
```

Use a trusted clone of the repository to review, with both commits and their
common history already available. Start with an offline scan:

```powershell
C:/tools/businessharness/businessharness scan `
    --repo C:/repos/project --base origin/main --head HEAD `
    --output C:/reports/businessharness/scan
```

For AI review, provision `OPENAI_API_KEY` through an approved secret store into
the process environment. Select a model supporting native structured outputs:

```powershell
$env:HARNESS_PROVIDER = 'openai'
$env:HARNESS_MODEL = 'YOUR_STRUCTURED_OUTPUT_MODEL'
C:/tools/businessharness/businessharness validate --output C:/reports/businessharness/validation
C:/tools/businessharness/businessharness review `
    --repo C:/repos/project --base origin/main --head HEAD `
    --feedback none --memory-scope off --output C:/reports/businessharness/review
```

For Claude, set `HARNESS_PROVIDER=anthropic` and provision `ANTHROPIC_API_KEY`
instead. `validate` checks configuration and credential presence without making
API calls; it does not test remote authentication. Replace the example paths and
model, and use a dedicated output directory per job. Results are JSON, Markdown,
and SARIF reports with the documented [exit codes](errors.md).

In CI, run these commands from a protected pipeline definition with a trusted
tool installation. Supply explicit fetched target/source commit IDs when your
CI checkout creates a synthetic merge commit. Standard GitHub/GitLab PR metadata
can enable host API reads even with `--feedback none`; that option disables host
writes. Configure host credentials when those integrations are active.

## Network and data management

| Connection or data | Behavior |
| --- | --- |
| OpenAI review | HTTPS to `https://api.openai.com/v1/responses` with an organization-provided `OPENAI_API_KEY`. |
| Claude review | HTTPS to `https://api.anthropic.com/v1/messages` with an organization-provided `ANTHROPIC_API_KEY`. |
| Git host | The runner obtains Git objects. Optional metadata/feedback adapters contact the configured GitHub or GitLab API. |
| Tickets and specifications | Optional Jira Cloud or trusted HTTPS JSON API routes. Resolved content is included in provider context. |
| Reports | Written to the configured local output directory. The CI artifact publisher controls any subsequent upload, access, and retention. |
| Review memory | Disabled by default. If enabled, persistent storage must remain outside the reviewed checkout, under organization-managed permissions, backups, and retention. Recalled context can enter provider requests. |

Restrict runner egress to the selected provider and explicitly enabled
integrations. Internal GitHub Enterprise and self-managed GitLab hosts require
an explicit trusted `changeRequest.apiBase`; the native Jira adapter targets
Jira Cloud. Internal specifications or trackers can use configured JSON APIs.
Integration endpoints require HTTPS DNS names on the default port with valid
certificates. See [private host configuration](configuration.md#pullmerge-request-metadata-and-feedback)
and [reference routing](configuration.md#dynamic-reference-sources).

Installation, source builds, and updates need access to their package, source,
base-image, and OS-package sources unless artifacts have been provisioned
internally. Provision those before using the offline scan in a disconnected
environment. A self-hosted CI runner can still depend on an external CI control
plane; an internal CI control plane is needed when that component must also stay
on premises.

## Operating responsibilities

Use isolated, disposable review jobs with a trusted Git checkout. Never run
scripts, hooks, builds, or tests from the repository being reviewed in a
secret-bearing review job. Keep tool releases and policy outside contributor
control, and keep credentials in environment variables rather than command
arguments or policy files. For containers, mount reviewed code read-only and
reports separately; retain the non-root image default and the hardened
[container invocation](integrations.md#local-packaging-and-containers).

Host feedback defaults to `none`; enabling comments or approvals requires
organization-provided host credentials and the existing fresh-commit checks.
Provider credentials do not grant host write access. Redaction is heuristic,
and reports and memory can contain sensitive business context. Apply the
organization's provider data/retention requirements, artifact permissions, and
storage controls. See the [security model](../SECURITY.md),
[configuration](configuration.md), and [memory operations](memory.md).
