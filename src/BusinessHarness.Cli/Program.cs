using BusinessHarness.Cli;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
return await new CliApplication().RunAsync(args, Console.Out, Console.Error, cancellation.Token);
