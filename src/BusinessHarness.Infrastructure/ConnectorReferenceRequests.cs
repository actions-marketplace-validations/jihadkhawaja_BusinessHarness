using System.Text.Json.Nodes;
using AgentConnector;
using BusinessHarness.Core;

namespace BusinessHarness.Infrastructure;

/// <summary>Uses the pinned connector catalog without exposing its runtime, proxies or write actions.</summary>
public static class ConnectorReferenceRequests
{
    public static void ValidateSources(IEnumerable<ReferenceSourcePolicy> sources)
    {
        foreach (var source in sources.Where(s => s.Connector is not null))
        {
            using var request = Build(source.Connector!, "example");
        }
    }

    public static HttpRequestMessage Build(ConnectorReferencePolicy configuration, string id)
    {
        configuration.Validate();
        try
        {
            var catalog = new ProviderCatalog(new ConnectorOptions());
            var (provider, action) = catalog.GetAction(configuration.Action);
            // Confluence omits page content unless the read request selects a body representation.
            if (action.Id == "confluence.get_page")
                action = action with { Parameters = [.. action.Parameters, new("body_format", Required: true, WireName: "body-format")] };
            if (action.Method != HttpMethod.Get || action.AcceptsBody ||
                !action.Parameters.Any(p => p.Name == configuration.IdParameter && p.Kind == ParameterKind.String) ||
                (provider.RequiresBaseUri && configuration.BaseUri is null))
                throw Invalid();
            if (configuration.BaseUri is not null)
                provider = provider with { BaseUri = IntegrationEndpoint.Validate(configuration.BaseUri) };
            var input = new JsonObject();
            foreach (var (name, value) in configuration.Inputs)
                input[name] = JsonNode.Parse(value.GetRawText());
            input[configuration.IdParameter] = id;
            var request = ProviderRequestBuilder.Build(provider, action, input);
            try
            {
                _ = IntegrationEndpoint.Validate(request.RequestUri!.AbsoluteUri);
                return request;
            }
            catch { request.Dispose(); throw; }
        }
        catch (Exception exception) when (exception is ConnectorException or ArgumentException)
        {
            throw Invalid();
        }
    }

    public static HttpRequestMessage BuildJira(Uri site, string key, string fields)
    {
        var request = Build(new()
        {
            Action = "jira.get_issue",
            IdParameter = "issue_id",
            BaseUri = site.AbsoluteUri
        }, key);
        request.RequestUri = new Uri(request.RequestUri!, "?fields=" + Uri.EscapeDataString(fields));
        return request;
    }

    private static HarnessException Invalid() => new(ErrorCode.Configuration, "Invalid connector reference action or inputs.",
        "Use a catalog GET action with a string identifier parameter and all required inputs. Instance providers require a trusted baseUri. Writes, proxies and body inputs are unavailable. See docs/configuration.md.");
}
