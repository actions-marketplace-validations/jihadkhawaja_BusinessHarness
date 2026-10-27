# Research and design decisions

Verified 2026-10-28 against official vendor documentation and the live NuGet registry. Recheck version pins before future releases.

| Topic | Finding and decision | Primary source |
| --- | --- | --- |
| .NET | .NET 10 SDK 10.0.403 includes runtime 10.0.14 and C# 14. Use this security servicing release and avoid .NET 11 previews for this .NET 10 project. | [.NET 10 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) |
| Dependencies | NuGet audit can examine transitive dependencies; locked restore enforces a reviewed graph. Runtime code uses the BCL HTTP/JSON/process APIs, avoiding unnecessary provider SDK dependencies. | [NuGet audit](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages), [lock files](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies) |
| OpenAI | Responses API supports JSON-schema output via text.format. Handle refusals and incomplete generations explicitly and validate results locally. Use a direct tool-free API call for portable read-only review. | [OpenAI structured outputs](https://developers.openai.com/api/docs/guides/structured-outputs?api-mode=responses) |
| Claude | Messages supports output_config.format with type json_schema. Use the native schema feature rather than parsing prose or markdown code fences. | [Claude structured outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs) |
| Jira | Cloud REST API v3 represents description/rich text with Atlassian Document Format. Read summary, description and an explicitly configured acceptance criteria field. Basic API-token authentication is supported for bot/ad-hoc integrations. | [Jira Cloud REST API v3](https://developer.atlassian.com/cloud/jira/platform/rest/v3/) |
| GitHub | Privileged pull_request_target/workflow_run flows need careful trust separation. Ship a normal pull_request example, pin dependencies and keep fork secrets withheld. | [GitHub secure use](https://docs.github.com/en/actions/reference/security/secure-use) |
| GitLab | MR pipelines and protected resource access have distinct rules; fork pipelines must not gain protected secrets. The example checks project identity and requires a manual job. | [GitLab MR pipelines](https://docs.gitlab.com/ci/pipelines/merge_request_pipelines/) |
| Azure DevOps | Fork code and YAML are untrusted; keep fork secrets disabled and use isolated hosted agents. Use a native container invocation and build-validation branch policy for Azure Repos. | [Azure pipeline security](https://learn.microsoft.com/en-us/azure/devops/pipelines/security/misc?view=azure-devops) |
| Bitbucket | Secured variables mask logs but remain available to executing pipeline code. Keep secret-bearing definitions trusted and use a pinned image. | [Bitbucket variables/secrets](https://support.atlassian.com/bitbucket-cloud/docs/variables-and-secrets/) |
| Test tooling | Current xUnit v3 4.x uses Microsoft Testing Platform v2. .NET 10 selects MTP in global.json. Avoid adding legacy VSTest packages to this new project. | [xUnit MTP](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform), [dotnet test](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test) |

The choice of direct API adapters rather than interactive Codex/Claude Code agents is an architectural decision: this project needs bounded inputs, no code execution, a consistent result contract and portability across CI vendors. Existing vendor-specific agent actions remain alternatives if an organization needs sandboxed agent execution. This implementation is a review orchestrator, not an autonomous code editor.

Package versions were resolved from [NuGet's version index](https://api.nuget.org/v3-flatcontainer/xunit.v3/index.json): xunit.v3 4.0.2. The test dependency graph also centrally pins available stable transitive updates; lock files record the tested result. Re-run tests and the outdated/vulnerability checks whenever these pins change. Runtime projects have no external NuGet dependencies.

## Dynamic context and feedback adapters

The reference resolver uses trusted regex-to-endpoint routes, so GET JSON sources can be configured without adding a provider-specific ticket command. GitHub/GitLab adapters fetch current PR/MR metadata and use the validated gate for optional feedback.

- [GitHub pull request reviews](https://docs.github.com/en/rest/pulls/reviews): create reviews with `commit_id` and `COMMENT`, `APPROVE` or `REQUEST_CHANGES`.
- [GitLab merge request approvals](https://docs.gitlab.com/api/merge_request_approvals/): approve with `sha`; unapprove removes the current caller’s approval.
- [GitLab merge request notes](https://docs.gitlab.com/api/notes/): publish the escaped review as an MR note.
- [GitLab merge requests](https://docs.gitlab.com/api/merge_requests/): read source SHA, diff base, title, description and draft/open status.

These contracts are covered by fake-transport tests. No live approval or comment was posted during development.
