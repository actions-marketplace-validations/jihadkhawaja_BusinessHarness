# Verification

Verified locally on 2026-10-28 using .NET SDK 10.0.403, Windows, and Docker Desktop with Linux containers.

| Check | Result |
| --- | --- |
| Locked NuGet restore | Passed |
| Release build with warnings as errors | Passed; zero warnings/errors |
| Automated tests | 139 passed, zero skipped |
| Formatting/analyzer verification | Passed |
| NuGet direct/transitive vulnerability query | No known vulnerable packages reported |
| NuGet direct/transitive outdated query | No outdated stable packages reported |
| Tool packaging and isolated tool installation | Passed |
| YAML parsing | All action/pipeline/dependency YAML files parsed |
| Container build | Passed using .NET runtime 10.0.14 |
| Container default user | Non-root UID 1654 |
| Offline CLI smoke checks | Clean=0, finding=1, missing-ref=3; stale report removed |
| Offline container smoke checks | Same results with no network, read-only root/source and dropped capabilities |
| SARIF output | Validated against the official OASIS SARIF 2.1.0 JSON schema |

Reproduce with `scripts/Verify.ps1` and `scripts/Smoke-Test.ps1`. Build the Docker image before invoking the smoke script with `-ContainerImage businessharness:0.1.0`.

HTTP tests use fake transport responses to validate request contracts, schema handling, authentication/rate-limit errors, refusals, size limits, dynamic source routing, deduplication, PR/MR metadata, commit-bound feedback and withholding feedback after metadata changes. They do not authenticate to live OpenAI, Anthropic, Jira, GitHub or GitLab accounts and do not assess real model review quality. Hosted CI jobs have not been dispatched to the four vendors; the examples require the consumer's actual image/action reference, model, credentials, permissions and trust policies. No package, image or action has been published externally.

The dynamic reference/feedback extension passed `scripts/Verify.ps1` (locked restore, Release build, 107 tests, formatting/analyzers, dependency vulnerability audit and tool packaging) and `scripts/Smoke-Test.ps1`. Updated action/pipeline YAML and example policy JSON parsed successfully. Container build, container smoke checks, isolated tool installation and SARIF schema validation listed above are results from the prior baseline; they were not rerun for this extension. No live comments or approvals were posted.

## MAGICore memory extension

Verified locally on 2026-10-28 with the pinned source submodule `ae8ba6bb1a8213066e7655581c5326fce93424c3` and no modifications inside that checkout:

- Locked restore, Release build, all 139 tests, formatting/analyzers, NuGet vulnerability audit and tool packaging passed through `scripts/Verify.ps1`.
- Real MAGICore tests cover durable reopen, scope/namespace isolation, layered reads, event replay/edits, retention, concurrent writers, corrupted/misplaced snapshots, actor/mention gates, native GitHub/GitLab/Jira payloads and review integration.
- `scripts/Smoke-Test.ps1` passed natively and against the rebuilt Linux image `businessharness:memory` with networking disabled and read-only root/source mounts.
- An isolated installation of the newly packaged tool persisted and recalled a follow-up across invocations. Separate offline containers read that same snapshot, persisted another follow-up and recalled both records on a fresh invocation with a read-only root filesystem.
- Package inspection confirmed bundled `MagiCore.dll`, its Apache license at `licenses/MAGICore/LICENSE`, and no MagiCore package dependency in the tool manifest. MAGICore is a project reference to the actual Git submodule.
- Updated JSON policy/examples, documentation JSON blocks and all action/pipeline YAML parsed successfully.

No live PR/Jira webhook endpoint, model invocation, comment or approval was dispatched for this extension. GitHub-hosted action bootstrap and deployment-specific webhook authentication remain deployment checks. Earlier SARIF-schema and outdated-package checks above were not repeated for the memory extension.
