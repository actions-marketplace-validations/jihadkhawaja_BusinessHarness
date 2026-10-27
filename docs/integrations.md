# CI integration and distribution

The CLI contract is identical across vendors. A completed review returns 0/1; integration errors return 2–10. Always retain the report directory even on failure. No organization/repository IDs, hosted image names, model IDs or tokens have been invented for you; replace the marked placeholders before deploying.

## Publish once

From a trusted release checkout:

```powershell
git submodule update --init --recursive
dotnet restore BusinessHarness.slnx --locked-mode
dotnet build BusinessHarness.slnx -c Release --no-restore
dotnet test --solution BusinessHarness.slnx -c Release --no-build
dotnet pack src/BusinessHarness.Cli -c Release --no-build -o artifacts/packages
docker build --pull -t YOUR_REGISTRY/businessharness:0.1.0 .
```

Publish the `.nupkg` to your approved NuGet feed or push the image to your registry using a separate trusted release process. Pin consumers to an exact package version, action commit, or image digest. Publishing credentials do not belong in review jobs. The GitHub action fetches its trusted action revision and initializes the pinned submodule outside the reviewed checkout before building; the caller does not have to install .NET manually. Container consumers need no .NET installation.

Example Linux container invocation after building the local image:

```bash
mkdir -p /tmp/businessharness-results
docker run --rm --read-only --cap-drop=ALL --security-opt=no-new-privileges \
  --user "$(id -u):$(id -g)" \
  --mount "type=bind,source=$PWD,target=/repo,readonly" \
  --mount "type=bind,source=/tmp/businessharness-results,target=/reports" \
  -e OPENAI_API_KEY -e HARNESS_MODEL \
  businessharness:0.1.0 review --repo /repo --base origin/main --head HEAD --output /reports
```

The report mount must be writable by the selected UID. The source mount must include `.git` and all required objects. A linked worktree may reference Git metadata outside its folder; include that metadata at matching paths or use a normal CI clone. Git is bundled in the image. For restricted egress, allow the selected provider API and, only when configured, your Jira Cloud tenant, approved reference API endpoints and GitHub/GitLab host APIs. Building the image also needs Microsoft container registries, nuget.org and OS package repositories.

## Platform specifics

- **GitHub:** copy [github.yml](../examples/pipelines/github.yml), replace the action ref, configure `HARNESS_MODEL`, and store `OPENAI_API_KEY` as an environment secret under an `ai-review` environment with required reviewers. `fetch-depth: 0` supplies the source, target and merge base. The example restricts forks but still requires trusted workflow definitions and environment approvals. Change provider/key for Claude. Configure Jira variables or trusted `referenceSources` for automatic discovery. The action supplies its GitHub token for metadata access. Set `feedback: comment` or `feedback: auto` and `pull-requests: write` to publish feedback. A separate eligible bot token can be passed through `github-token`. The composite uses Bash and requires a runner with Bash and Git (GitHub-hosted Ubuntu is recommended).
- **GitLab:** copy/include [gitlab.yml](../examples/pipelines/gitlab.yml) in a trusted pipeline, define `HARNESS_IMAGE` as an image digest, and configure protected/masked provider variables and model. The image entrypoint is cleared so GitLab can execute its shell script. Configure `GITLAB_TOKEN` for MR metadata access and an eligible bot with `api` scope if enabling `--feedback comment` or `--feedback auto`. Standard detached MR pipelines are supported by the shown refs. Merged-result/merge-train policies should pass their intended source commit explicitly. Protected secrets may be unavailable unless both branches and the user meet GitLab's protected-resource rules; the runner reports this as configuration failure rather than silently skipping review.
- **Azure DevOps:** use [azure-devops.yml](../examples/pipelines/azure-devops.yml) as a branch build-validation pipeline for Azure Repos. The example assumes the normal PR merge-commit checkout, using parent 1 as target and parent 2 as source. If your checkout strategy differs, pass explicit fetched commit IDs instead. Define the image, model and secret variable. Protect the YAML/template and secret access through your organization policies.
- **Bitbucket:** copy [bitbucket.yml](../examples/pipelines/bitbucket.yml), replace the image digest and configure model plus secured provider variable. Bitbucket's PR preparation can merge the target into the working tree; the example deliberately passes `BITBUCKET_COMMIT` as source and the destination commit as base, rather than using that synthetic checkout HEAD. Fetch the destination commit outside the runner first. Keep fork pipelines isolated from secrets.

Reports are always written locally; optional `comment` or `auto` feedback publishes only after validation. See [configuration](configuration.md) for dynamic sources, private hosts and feedback semantics. Azure DevOps and Bitbucket retain local reports and exit-code gates; native metadata/feedback adapters currently cover GitHub and GitLab. SARIF can be uploaded separately with the hosting platform’s approved publisher and required permissions. Do not execute model output as workflow commands.

## Persistent memory and follow-up events

Enable trusted `memory` settings and provide a persistent writable directory separate from the reviewed checkout and reports. Ephemeral runners and containers need an explicitly provisioned memory volume; a temporary workspace does not provide long-term storage. See [memory setup](memory.md), the [memory policy example](../examples/harness.memory.policy.json), and the [GitHub comment workflow](../examples/pipelines/github-follow-up.yml). Authenticated Jira/GitLab webhook receivers can invoke `follow-up --event PATH`, followed by a normal review, or `review --event PATH` for immediate re-review. Repository and actor routing remain controlled by trusted policy. No webhook endpoint is registered automatically.
