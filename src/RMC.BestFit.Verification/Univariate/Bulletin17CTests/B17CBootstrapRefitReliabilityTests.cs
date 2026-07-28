using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets.UnivariateData;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;

namespace RMC.BestFit.Verification.Univariate.Bulletin17CTests;

/// <summary>
/// Long-running reliability verification for Bulletin 17C bootstrap GMM refits.
/// </summary>
/// <remarks>
/// Exercises worked Examples 1 through 7 with both ordinary and bias-corrected pivotal
/// bootstrap uncertainty. Seeded cells request 1,000 parameter sets except for highly censored
/// Example 7, which requests 500. Every cell requires that each output be obtained without
/// exhausting the bounded retry policy and substituting the parent fit.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class B17CBootstrapRefitReliabilityTests
{
    private const int StandardBootstrapReplicates = 1_000;
    private const int HighlyCensoredBootstrapReplicates = 500;

    /// <summary>
    /// Gets or sets the MSTest context used to report bootstrap diagnostics and captured trace output.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies 1,000 ordinary-bootstrap refits for Bulletin 17C Example 1.
    /// </summary>
    /// <returns>A task representing the seeded reliability cell.</returns>
    [TestCategory("LongRunning")]
    [TestMethod]
    public Task Example1_OrdinaryBootstrap_ThousandRefits()
    {
        return VerifyWorkedExampleCell(1, UncertaintyMethod.Bootstrap, StandardBootstrapReplicates);
    }

    /// <summary>
    /// Verifies 1,000 pivotal-bootstrap refits for Bulletin 17C Example 1.
    /// </summary>
    /// <returns>A task representing the seeded reliability cell.</returns>
    [TestCategory("LongRunning")]
    [TestMethod]
    public Task Example1_PivotalBootstrap_ThousandRefits()
    {
        return VerifyWorkedExampleCell(1, UncertaintyMethod.BiasCorrectedBootstrap, StandardBootstrapReplicates);
    }

    /// <summary>
    /// Verifies 1,000 ordinary-bootstrap refits for Bulletin 17C Example 2.
    /// </summary>
    /// <returns>A task representing the seeded reliability cell.</returns>
    [TestCategory("LongRunning")]
    [TestMethod]
    public Task Example2_OrdinaryBootstrap_ThousandRefits()
    {
        return VerifyWorkedExampleCell(2, UncertaintyMethod.Bootstrap, StandardBootstrapReplicates);
    }

    /// <summary>
    /// Verifies 1,000 pivotal-bootstrap refits for Bulletin 17C Example 2.
    /// </summary>
    /// <returns>A task representing the seeded reliability cell.</returns>
    [TestCategory("LongRunning")]
    [TestMethod]
    public Task Example2_PivotalBootstrap_ThousandRefits()
    {
        return VerifyWorkedExampleCell(2, UncertaintyMethod.BiasCorrectedBootstrap, StandardBootstrapReplicates);
    }

    /// <summary>
    /// Verifies 1,000 ordinary-bootstrap refits for Bulletin 17C Example 3.
    /// </summary>
    /// <returns>A task representing the seeded reliability cell.</returns>
    [TestCategory("LongRunning")]
    [TestMethod]
    public Task Example3_OrdinaryBootstrap_ThousandRefits()
    {
        return VerifyWorkedExampleCell(3, UncertaintyMethod.Bootstrap, StandardBootstrapReplicates);
    }

    /// <summary>
    /// Verifies 1,000 pivotal-bootstrap refits for Bulletin 17C Example 3.
    /// </summary>
    /// <returns>A task representing the seeded reliability cell.</returns>
    [TestCategory("LongRunning")]
    [TestMethod]
    public Task Example3_PivotalBootstrap_ThousandRefits()
    {
        return VerifyWorkedExampleCell(3, UncertaintyMethod.BiasCorrectedBootstrap, StandardBootstrapReplicates);
    }

    /// <summary>
    /// Verifies 1,000 ordinary-bootstrap refits for Bulletin 17C Example 4.
    /// </summary>
    /// <returns>A task representing the seeded reliability cell.</returns>
    [TestCategory("LongRunning")]
    [TestMethod]
    public Task Example4_OrdinaryBootstrap_ThousandRefits()
    {
        return VerifyWorkedExampleCell(4, UncertaintyMethod.Bootstrap, StandardBootstrapReplicates);
    }

    /// <summary>
    /// Verifies 1,000 pivotal-bootstrap refits for Bulletin 17C Example 4.
    /// </summary>
    /// <returns>A task representing the seeded reliability cell.</returns>
    [TestCategory("LongRunning")]
    [TestMethod]
    public Task Example4_PivotalBootstrap_ThousandRefits()
    {
        return VerifyWorkedExampleCell(4, UncertaintyMethod.BiasCorrectedBootstrap, StandardBootstrapReplicates);
    }

    /// <summary>
    /// Verifies 1,000 ordinary-bootstrap refits for Bulletin 17C Example 5.
    /// </summary>
    /// <returns>A task representing the seeded reliability cell.</returns>
    [TestCategory("LongRunning")]
    [TestMethod]
    public Task Example5_OrdinaryBootstrap_ThousandRefits()
    {
        return VerifyWorkedExampleCell(5, UncertaintyMethod.Bootstrap, StandardBootstrapReplicates);
    }

    /// <summary>
    /// Verifies 1,000 pivotal-bootstrap refits for Bulletin 17C Example 5.
    /// </summary>
    /// <returns>A task representing the seeded reliability cell.</returns>
    [TestCategory("LongRunning")]
    [TestMethod]
    public Task Example5_PivotalBootstrap_ThousandRefits()
    {
        return VerifyWorkedExampleCell(5, UncertaintyMethod.BiasCorrectedBootstrap, StandardBootstrapReplicates);
    }

    /// <summary>
    /// Verifies 1,000 ordinary-bootstrap refits for Bulletin 17C Example 6.
    /// </summary>
    /// <returns>A task representing the seeded reliability cell.</returns>
    [TestCategory("LongRunning")]
    [TestMethod]
    public Task Example6_OrdinaryBootstrap_ThousandRefits()
    {
        return VerifyWorkedExampleCell(6, UncertaintyMethod.Bootstrap, StandardBootstrapReplicates);
    }

    /// <summary>
    /// Verifies 1,000 pivotal-bootstrap refits for Bulletin 17C Example 6.
    /// </summary>
    /// <returns>A task representing the seeded reliability cell.</returns>
    [TestCategory("LongRunning")]
    [TestMethod]
    public Task Example6_PivotalBootstrap_ThousandRefits()
    {
        return VerifyWorkedExampleCell(6, UncertaintyMethod.BiasCorrectedBootstrap, StandardBootstrapReplicates);
    }

    /// <summary>
    /// Verifies 500 ordinary-bootstrap refits for the highly censored Bulletin 17C Example 7.
    /// </summary>
    /// <returns>A task representing the seeded reliability cell.</returns>
    [TestCategory("LongRunning")]
    [TestMethod]
    public Task Example7_OrdinaryBootstrap_FiveHundredRefits()
    {
        return VerifyWorkedExampleCell(7, UncertaintyMethod.Bootstrap, HighlyCensoredBootstrapReplicates);
    }

    /// <summary>
    /// Verifies 500 pivotal-bootstrap refits for the highly censored Bulletin 17C Example 7.
    /// </summary>
    /// <returns>A task representing the seeded reliability cell.</returns>
    [TestCategory("LongRunning")]
    [TestMethod]
    public Task Example7_PivotalBootstrap_FiveHundredRefits()
    {
        return VerifyWorkedExampleCell(7, UncertaintyMethod.BiasCorrectedBootstrap, HighlyCensoredBootstrapReplicates);
    }

    /// <summary>
    /// Executes and verifies one seeded worked-example and uncertainty-method cell.
    /// </summary>
    /// <param name="exampleNumber">Bulletin 17C worked-example number in the range 1 through 7.</param>
    /// <param name="bootstrapReplicates">Number of finite parameter sets required from the cell.</param>
    /// <param name="uncertaintyMethod">Bootstrap method to verify.</param>
    /// <returns>A task representing the asynchronous analysis and assertions.</returns>
    /// <exception cref="AssertFailedException">
    /// Thrown when an exception, parent substitution, output-count error, or nonfinite result is observed.
    /// </exception>
    /// <remarks>
    /// The exception and trace listeners are process-wide, so cells execute serially within the
    /// enclosing <see cref="DoNotParallelizeAttribute"/> test class.
    /// </remarks>
    private async Task VerifyWorkedExampleCell(
        int exampleNumber,
        UncertaintyMethod uncertaintyMethod,
        int bootstrapReplicates)
    {
        var (dataFrame, _) = GetExample(exampleNumber);
        var model = new Bulletin17CDistribution(
            dataFrame, UnivariateDistributionType.LogPearsonTypeIII);
        var analysis = new Bulletin17CAnalysis(model)
        {
            UncertaintyMethod = uncertaintyMethod
        };
        analysis.BayesianAnalysis.PointEstimator =
            BayesianAnalysis.PointEstimateType.PosteriorMode;
        analysis.BayesianAnalysis.OutputLength = bootstrapReplicates;
        analysis.BayesianAnalysis.PRNGSeed =
            817_000 + (100 * exampleNumber) + (int)uncertaintyMethod;

        var firstChanceExceptions = new ConcurrentQueue<string>();
        EventHandler<FirstChanceExceptionEventArgs> firstChanceHandler = (_, eventArgs) =>
        {
            Exception exception = eventArgs.Exception;
            string? assemblyName = exception.TargetSite?.DeclaringType?.Assembly.GetName().Name;
            if (assemblyName == "Numerics" || assemblyName == "RMC.BestFit")
            {
                firstChanceExceptions.Enqueue(
                    $"[{assemblyName}] {exception.GetType().FullName}: {exception.Message}" +
                    $"{Environment.NewLine}{exception.StackTrace}");
            }
        };
        var traceBuffer = new StringBuilder();
        using var traceWriter = new StringWriter(traceBuffer, CultureInfo.InvariantCulture);
        using var traceListener = new TextWriterTraceListener(TextWriter.Synchronized(traceWriter));
        AppDomain.CurrentDomain.FirstChanceException += firstChanceHandler;
        Trace.Listeners.Add(traceListener);
        try
        {
            await analysis.RunAsync();
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= firstChanceHandler;
            traceListener.Flush();
            Trace.Listeners.Remove(traceListener);
        }

        if (!string.IsNullOrWhiteSpace(traceBuffer.ToString()))
        {
            TestContext.WriteLine(
                $"Example {exampleNumber} {uncertaintyMethod} Debug/Trace output:" +
                $"{Environment.NewLine}{traceBuffer}");
        }
        if (!firstChanceExceptions.IsEmpty)
        {
            TestContext.WriteLine(
                $"Example {exampleNumber} {uncertaintyMethod} first-chance exceptions:" +
                $"{Environment.NewLine}{string.Join(Environment.NewLine, firstChanceExceptions)}");
        }
        string firstChanceSummary = string.Join(
            Environment.NewLine + Environment.NewLine,
            firstChanceExceptions);
        Assert.IsTrue(firstChanceExceptions.IsEmpty,
            $"Example {exampleNumber} {uncertaintyMethod} threw exception(s) from Numerics or " +
            $"RMC.BestFit during bootstrap refitting.{Environment.NewLine}{firstChanceSummary}");

        Assert.IsNotNull(analysis.BootstrapResults,
            $"Example {exampleNumber} {uncertaintyMethod} did not publish bootstrap diagnostics." +
            $"{Environment.NewLine}{traceBuffer}");
        BootstrapDiagnostics diagnostics = analysis.BootstrapResults!;
        string diagnosticSummary =
            $"attempted={diagnostics.AttemptedReplicates:N0}, " +
            $"outer retries={diagnostics.TotalRetries:N0}, " +
            $"Mahalanobis rejections={diagnostics.MahalanobisRejections:N0}, " +
            $"optimizer fallbacks={diagnostics.OptimizerFallbacks:N0}, " +
            $"function evaluations={diagnostics.TotalFunctionEvaluations:N0}, " +
            $"GMM statuses=success:{diagnostics.StatusSuccessCount:N0}/" +
            $"max-iterations:{diagnostics.StatusMaximumIterationsCount:N0}/" +
            $"max-evaluations:{diagnostics.StatusMaximumFunctionEvaluationsCount:N0}/" +
            $"failure:{diagnostics.StatusFailureCount:N0}/none:{diagnostics.StatusNoneCount:N0}, " +
            $"phase 1={diagnostics.Phase1Time.TotalSeconds:F3}s";

        TestContext.WriteLine(
            $"Example {exampleNumber} {uncertaintyMethod}: {diagnosticSummary}.");

        Assert.AreEqual(bootstrapReplicates, diagnostics.AttemptedReplicates,
            $"Example {exampleNumber} {uncertaintyMethod} retried a realization; {diagnosticSummary}.");
        Assert.AreEqual(0, diagnostics.TotalRetries,
            $"Example {exampleNumber} {uncertaintyMethod} required an outer retry; {diagnosticSummary}.");
        Assert.AreEqual(0, diagnostics.MahalanobisRejections,
            $"Example {exampleNumber} {uncertaintyMethod} unexpectedly used the removed Mahalanobis guard; {diagnosticSummary}.");
        Assert.AreEqual(0, diagnostics.StatusFailureCount,
            $"Example {exampleNumber} {uncertaintyMethod} recorded a failed GMM candidate; {diagnosticSummary}.");
        Assert.AreEqual(0, diagnostics.StatusNoneCount,
            $"Example {exampleNumber} {uncertaintyMethod} recorded an uninitialized GMM candidate; {diagnosticSummary}.");
        Assert.AreEqual(0, diagnostics.FailedReplicates,
            $"Example {exampleNumber} {uncertaintyMethod} used a parent-fit fallback; {diagnosticSummary}.");
        Assert.AreEqual(bootstrapReplicates, diagnostics.RetainedReplicates,
            $"Example {exampleNumber} {uncertaintyMethod} did not retain every requested draw; {diagnosticSummary}.");
        Assert.IsNotNull(analysis.BayesianAnalysis.Results,
            $"Example {exampleNumber} {uncertaintyMethod} did not publish uncertainty results; {diagnosticSummary}.");

        var output = analysis.BayesianAnalysis.Results!.Output;
        Assert.AreEqual(bootstrapReplicates, output.Count,
            $"Example {exampleNumber} {uncertaintyMethod} returned the wrong output length; {diagnosticSummary}.");
        Assert.IsTrue(output.All(parameterSet =>
                parameterSet.Values.Length == model.NumberOfParameters &&
                parameterSet.Values.All(double.IsFinite)),
            $"Example {exampleNumber} {uncertaintyMethod} returned non-finite parameters; {diagnosticSummary}.");
    }

    /// <summary>
    /// Returns one of the seven Bulletin 17C worked-example fixtures.
    /// </summary>
    /// <param name="exampleNumber">Worked-example number in the range 1 through 7.</param>
    /// <returns>The example data frame and its stored reference parameters.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="exampleNumber"/> is outside the supported range.
    /// </exception>
    private static (RMC.BestFit.Models.DataFrame DataFrame, double[] Parameters) GetExample(
        int exampleNumber)
    {
        return exampleNumber switch
        {
            1 => Bulletin17CData.GetExample1(),
            2 => Bulletin17CData.GetExample2(),
            3 => Bulletin17CData.GetExample3(),
            4 => Bulletin17CData.GetExample4(),
            5 => Bulletin17CData.GetExample5(),
            6 => Bulletin17CData.GetExample6(),
            7 => Bulletin17CData.GetExample7(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(exampleNumber), exampleNumber, "Example number must be between 1 and 7.")
        };
    }
}
