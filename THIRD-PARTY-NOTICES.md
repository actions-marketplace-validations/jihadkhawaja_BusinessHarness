# Third-party notices

BusinessHarness builds MAGICore from the Git submodule at `external/MAGICore`, pinned to commit `ae8ba6bb1a8213066e7655581c5326fce93424c3` (v1.0.0).

MAGICore is licensed under Apache License 2.0. Its source and copyright/license notices are preserved in the submodule. The packaged tool and container include its license at `licenses/MAGICore/LICENSE`. BusinessHarness does not consume the MagiCore NuGet package. MAGICore's supporting Microsoft abstractions and tensor library remain ordinary, version-pinned NuGet dependencies.

Source: https://github.com/jihadkhawaja/MAGICore

BusinessHarness builds AgentConnector from the Git submodule at `external/AgentConnector`, pinned to commit `3db84cfbcbff26375bac04f43915136716d3d758`.

AgentConnector is licensed under Apache License 2.0. Its source and copyright/license notices are preserved in the submodule. The packaged tool and container include its license at `licenses/AgentConnector/LICENSE`. BusinessHarness uses the provider catalog and request builder, without hosting connector endpoints or enabling its credential store, OAuth runtime, proxies or write actions. Its private Sonar analyzer is centrally pinned; the library uses the .NET and ASP.NET Core shared frameworks.

Source: https://github.com/jihadkhawaja/AgentConnector
