using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

/// <summary>Replays the supplied Example #1 without diagnostic instrumentation or UI work.</summary>
public static class BenchmarkProgram
{
    /// <summary>Runs one warm-up and three measured fits using the frozen model inputs.</summary>
    /// <param name="args">An optional directory containing the three model XML snapshots.</param>
    /// <returns>A task completing after the measurements have been written as JSON lines.</returns>
    /// <remarks>Uses seed 12345 and 1,000 realizations; every run creates a fresh analysis.</remarks>
    public static async Task Main(string[] args)
    {
        string root = args.Length == 0 ? AppContext.BaseDirectory : args[0];
        for (int run = 0; run < 4; run++)
        {
            var frame = new DataFrame(XElement.Load(Path.Combine(root, "DataFrame.xml")));
            var model = new Bulletin17CDistribution(frame, XElement.Load(Path.Combine(root, "Bulletin17CDistribution.xml")));
            var analysis = new Bulletin17CAnalysis(model, XElement.Load(Path.Combine(root, "AnalysisXml.xml")));
            analysis.BayesianAnalysis.OutputLength = 1000;
            analysis.BayesianAnalysis.PRNGSeed = 12345;
            var watch = Stopwatch.StartNew();
            await analysis.RunAsync();
            watch.Stop();
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                run,
                warmup = run == 0,
                milliseconds = watch.Elapsed.TotalMilliseconds,
                estimated = analysis.IsEstimated,
                diagnostics = analysis.BootstrapResults?.ToXElement().ToString()
            }));
        }
    }
}
