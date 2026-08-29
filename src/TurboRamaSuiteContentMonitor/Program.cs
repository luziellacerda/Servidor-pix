using TurboRamaSuiteContentPublisher;

namespace TurboRamaSuiteContentMonitor;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--self-test") return MonitorSelfTest.Run();
        if (args.Length == 1 && args[0] == "--postgres-self-test")
            return await MonitorPostgresSelfTest.RunAsync();
        if (args.Length != 1 || args[0] is not ("run-once" or "candidate-once"))
        {
            Console.Error.WriteLine("SUITE CONTENT MONITOR: FAILED code=COMMAND_INVALID");
            return 2;
        }
        try
        {
            var options = MonitorOptions.FromEnvironment();
            using var shutdown = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                shutdown.Cancel();
            };
            using var coordinator = await MonitorCoordinator.CreateAsync(options, shutdown.Token);
            var result = args[0] == "run-once"
                ? await coordinator.RunOnceAsync(shutdown.Token)
                : await coordinator.RunCandidateOnceAsync(shutdown.Token);
            if (result.Outcome == MonitorRunOutcome.Blocked)
            {
                Console.Error.WriteLine($"SUITE CONTENT MONITOR: BLOCKED code={result.Code}");
                return 4;
            }
            Console.WriteLine($"SUITE CONTENT MONITOR: OK code={result.Code}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("SUITE CONTENT MONITOR: CANCELLED");
            return 130;
        }
        catch (PublisherFailure ex)
        {
            Console.Error.WriteLine($"SUITE CONTENT MONITOR: FAILED code={ResultCodes.Candidate(ex.Code)}");
            return 2;
        }
        catch (MonitorFailure ex)
        {
            Console.Error.WriteLine($"SUITE CONTENT MONITOR: FAILED code={ex.Code}");
            return 2;
        }
        catch (Exception ex)
        {
            // Exception messages from networking and databases are intentionally suppressed:
            // they can include private upstream material.
            Console.Error.WriteLine($"SUITE CONTENT MONITOR: FAILED code=UNEXPECTED_FAILURE type={ex.GetType().Name} stage={ex.TargetSite?.Name ?? "unknown"}");
            return 3;
        }
    }
}
