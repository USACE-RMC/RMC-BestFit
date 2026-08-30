using Numerics;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Sampling;

namespace RMC.BestFit.Verification.Univariate.CompetingRiskTests;

/// <summary>
/// Provides verification-only diagnostics for the information content of competing-risk
/// recovery fixtures.
/// </summary>
public partial class CompetingRiskRecoveryTests
{
    /// <summary>The minimum predeclared theoretical cause share for every component.</summary>
    private const double MinimumTheoreticalCauseShare = 0.15d;

    /// <summary>The minimum predeclared hard winner count for every component.</summary>
    private const int MinimumHardWinnerCount = 100;

    /// <summary>The minimum composite-probability mass where a component owns the event.</summary>
    private const double MinimumDominanceMass = 0.10d;

    /// <summary>The lowest permitted dog-leg crossover probability.</summary>
    private const double MinimumCrossoverProbability = 0.10d;

    /// <summary>The highest permitted dog-leg crossover probability.</summary>
    private const double MaximumCrossoverProbability = 0.90d;

    /// <summary>The midpoint probability grid used for deterministic design integration.</summary>
    private const int DiagnosticProbabilityCount = 1000;

    /// <summary>
    /// Requires a fixture to contain balanced cause contributions and visible interior dog-leg
    /// behavior before an estimator may run. Additional extreme-tail dominance re-entry is
    /// retained as a diagnostic and does not invalidate the identified interior dog leg.
    /// </summary>
    /// <param name="fixture">The predeclared recovery fixture.</param>
    /// <param name="productionSample">The sample returned by the production generator.</param>
    private static void AssertIdentifiableDesign(
        RecoveryFixture fixture,
        IReadOnlyList<double> productionSample)
    {
        (double[] labeledSample, int[] hardWinnerCounts) = GenerateLabeledSample(fixture);
        Assert.AreEqual(fixture.SampleSize, productionSample.Count,
            $"{fixture.Label}: production sample size changed.");
        Assert.AreEqual(fixture.SampleSize, labeledSample.Length,
            $"{fixture.Label}: labeled sample size changed.");
        for (int observationIndex = 0; observationIndex < labeledSample.Length; observationIndex++)
        {
            Assert.AreEqual(
                productionSample[observationIndex],
                labeledSample[observationIndex],
                0d,
                $"{fixture.Label}: verification-only labels did not reproduce production " +
                $"observation {observationIndex + 1}.");
        }

        int componentCount = fixture.Parent.Distributions.Count;
        var theoreticalShares = new double[componentCount];
        var softEventCounts = new double[componentCount];
        var dominanceCounts = new int[componentCount];
        var dominantSequence = new int[DiagnosticProbabilityCount];

        for (int observationIndex = 0; observationIndex < productionSample.Count; observationIndex++)
        {
            double[] responsibilities = ComputeResponsibilities(fixture, productionSample[observationIndex]);
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                softEventCounts[componentIndex] += responsibilities[componentIndex];
        }

        for (int probabilityIndex = 0; probabilityIndex < DiagnosticProbabilityCount; probabilityIndex++)
        {
            double probability = (probabilityIndex + 0.5d) / DiagnosticProbabilityCount;
            double location = fixture.Parent.InverseCDF(probability);
            double[] responsibilities = ComputeResponsibilities(fixture, location);
            int dominantComponent = IndexOfMaximum(responsibilities);
            dominantSequence[probabilityIndex] = dominantComponent;

            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                theoreticalShares[componentIndex] +=
                    responsibilities[componentIndex] / DiagnosticProbabilityCount;
                if (responsibilities[componentIndex] >= 0.5d)
                    dominanceCounts[componentIndex]++;
            }
        }

        double[] crossovers = FindDominanceCrossovers(dominantSequence);
        double[] interiorCrossovers = crossovers
            .Where(probability =>
                probability >= MinimumCrossoverProbability &&
                probability <= MaximumCrossoverProbability)
            .ToArray();
        for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
        {
            double dominanceMass = dominanceCounts[componentIndex] /
                (double)DiagnosticProbabilityCount;
            Assert.IsTrue(
                theoreticalShares[componentIndex] >= MinimumTheoreticalCauseShare,
                $"{fixture.Label}: component {componentIndex + 1} theoretical cause share " +
                $"{theoreticalShares[componentIndex]:P2} is below " +
                $"{MinimumTheoreticalCauseShare:P0}.");
            Assert.IsTrue(
                hardWinnerCounts[componentIndex] >= MinimumHardWinnerCount,
                $"{fixture.Label}: component {componentIndex + 1} produced only " +
                $"{hardWinnerCounts[componentIndex]} hard wins; at least " +
                $"{MinimumHardWinnerCount} are required.");
            Assert.IsTrue(
                dominanceMass >= MinimumDominanceMass,
                $"{fixture.Label}: component {componentIndex + 1} owns only " +
                $"{dominanceMass:P2} of the composite probability scale; at least " +
                $"{MinimumDominanceMass:P0} is required.");
            Assert.IsTrue(
                softEventCounts[componentIndex] >= MinimumHardWinnerCount,
                $"{fixture.Label}: component {componentIndex + 1} has soft event count " +
                $"{softEventCounts[componentIndex]:F1}; at least {MinimumHardWinnerCount} is required.");
        }

        Assert.AreEqual(
            componentCount - 1,
            interiorCrossovers.Length,
            $"{fixture.Label}: expected {componentCount - 1} ordered interior dog-leg " +
            $"crossovers but found {interiorCrossovers.Length} inside " +
            $"[{MinimumCrossoverProbability:F2}, {MaximumCrossoverProbability:F2}]; all " +
            $"crossovers are [{string.Join(", ", crossovers.Select(value => value.ToString("F3")))}].");

        fixture.Diagnostics = new DesignDiagnostics(
            theoreticalShares,
            hardWinnerCounts,
            softEventCounts,
            dominanceCounts.Select(count => count / (double)DiagnosticProbabilityCount).ToArray(),
            crossovers);
    }

    /// <summary>
    /// Reproduces the production random-number call order while retaining the latent winning
    /// component label for each composite observation.
    /// </summary>
    /// <param name="fixture">The recovery fixture to generate.</param>
    /// <returns>The composite sample and component hard winner counts.</returns>
    private static (double[] Sample, int[] HardWinnerCounts) GenerateLabeledSample(
        RecoveryFixture fixture)
    {
        int componentCount = fixture.Parent.Distributions.Count;
        var sample = new double[fixture.SampleSize];
        var hardWinnerCounts = new int[componentCount];

        if (fixture.Parent.Dependency == Probability.DependencyType.Independent)
        {
            var random = new MersenneTwister(FixtureSeed);
            for (int observationIndex = 0; observationIndex < fixture.SampleSize; observationIndex++)
            {
                var latentValues = new double[componentCount];
                for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                {
                    latentValues[componentIndex] = fixture.Parent.Distributions[componentIndex]
                        .InverseCDF(random.NextDouble());
                }

                RecordCompositeObservation(fixture, latentValues, sample, hardWinnerCounts, observationIndex);
            }
        }
        else
        {
            Assert.AreEqual(
                Probability.DependencyType.CorrelationMatrix,
                fixture.Parent.Dependency,
                $"{fixture.Label}: labeled generation supports independent or fixed-correlation fixtures only.");
            var multivariateNormal = new MultivariateNormal(
                new double[componentCount],
                fixture.Parent.CorrelationMatrix);
            double[,] latentNormals = multivariateNormal.GenerateRandomValues(fixture.SampleSize, FixtureSeed);
            for (int observationIndex = 0; observationIndex < fixture.SampleSize; observationIndex++)
            {
                var latentValues = new double[componentCount];
                for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                {
                    double probability = Normal.StandardCDF(latentNormals[observationIndex, componentIndex]);
                    latentValues[componentIndex] = fixture.Parent.Distributions[componentIndex]
                        .InverseCDF(probability);
                }

                RecordCompositeObservation(fixture, latentValues, sample, hardWinnerCounts, observationIndex);
            }
        }

        return (sample, hardWinnerCounts);
    }

    /// <summary>
    /// Records one generated minimum or maximum and increments the corresponding hard winner.
    /// </summary>
    /// <param name="fixture">The recovery fixture.</param>
    /// <param name="latentValues">The generated component values.</param>
    /// <param name="sample">The composite sample under construction.</param>
    /// <param name="hardWinnerCounts">The component hard winner counts.</param>
    /// <param name="observationIndex">The observation being recorded.</param>
    private static void RecordCompositeObservation(
        RecoveryFixture fixture,
        IReadOnlyList<double> latentValues,
        double[] sample,
        int[] hardWinnerCounts,
        int observationIndex)
    {
        int winnerIndex = 0;
        for (int componentIndex = 1; componentIndex < latentValues.Count; componentIndex++)
        {
            bool replacesWinner = fixture.Parent.MinimumOfRandomVariables
                ? latentValues[componentIndex] < latentValues[winnerIndex]
                : latentValues[componentIndex] > latentValues[winnerIndex];
            if (replacesWinner)
                winnerIndex = componentIndex;
        }

        sample[observationIndex] = latentValues[winnerIndex];
        hardWinnerCounts[winnerIndex]++;
    }

    /// <summary>
    /// Computes the conditional cause probabilities for one observed composite value.
    /// </summary>
    /// <param name="fixture">The recovery fixture.</param>
    /// <param name="location">The observed composite value.</param>
    /// <returns>The normalized component cause probabilities.</returns>
    private static double[] ComputeResponsibilities(RecoveryFixture fixture, double location)
    {
        int componentCount = fixture.Parent.Distributions.Count;
        var contributions = new double[componentCount];
        if (fixture.Parent.Dependency == Probability.DependencyType.Independent)
        {
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                double contribution = fixture.Parent.Distributions[componentIndex].PDF(location);
                for (int otherIndex = 0; otherIndex < componentCount; otherIndex++)
                {
                    if (otherIndex == componentIndex)
                        continue;
                    contribution *= fixture.Parent.MinimumOfRandomVariables
                        ? fixture.Parent.Distributions[otherIndex].CCDF(location)
                        : fixture.Parent.Distributions[otherIndex].CDF(location);
                }
                contributions[componentIndex] = contribution;
            }
        }
        else
        {
            Assert.AreEqual(2, componentCount,
                $"{fixture.Label}: fixed-correlation responsibilities require two components.");
            Assert.IsTrue(fixture.Parent.MinimumOfRandomVariables,
                $"{fixture.Label}: only the approved fixed-correlation minimum is supported.");
            double correlation = fixture.Parent.CorrelationMatrix[0, 1];
            double conditionalScale = Math.Sqrt(1d - correlation * correlation);
            double[] marginalProbabilities = fixture.Parent.Distributions
                .Select(distribution => Tools.Clamp(distribution.CDF(location), 1E-14, 1d - 1E-14))
                .ToArray();
            double[] latentNormals = marginalProbabilities.Select(Normal.StandardZ).ToArray();
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                int otherIndex = 1 - componentIndex;
                double standardizedConditionalThreshold =
                    (latentNormals[otherIndex] - correlation * latentNormals[componentIndex]) /
                    conditionalScale;
                double conditionalSurvival = 1d - Normal.StandardCDF(standardizedConditionalThreshold);
                contributions[componentIndex] =
                    fixture.Parent.Distributions[componentIndex].PDF(location) * conditionalSurvival;
            }
        }

        double total = contributions.Sum();
        Assert.IsTrue(Tools.IsFinite(total) && total > 0d,
            $"{fixture.Label}: cause contributions are invalid at x={location:G17}.");
        for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            contributions[componentIndex] /= total;
        return contributions;
    }

    /// <summary>
    /// Returns the index of the largest responsibility, resolving an exact tie by component order.
    /// </summary>
    /// <param name="values">The responsibility vector.</param>
    /// <returns>The largest-value index.</returns>
    private static int IndexOfMaximum(IReadOnlyList<double> values)
    {
        int maximumIndex = 0;
        for (int index = 1; index < values.Count; index++)
        {
            if (values[index] > values[maximumIndex])
                maximumIndex = index;
        }
        return maximumIndex;
    }

    /// <summary>
    /// Finds the composite probabilities where the dominant component changes.
    /// </summary>
    /// <param name="dominantSequence">Dominant component indices on the midpoint grid.</param>
    /// <returns>The ordered crossover probabilities.</returns>
    private static double[] FindDominanceCrossovers(IReadOnlyList<int> dominantSequence)
    {
        var crossovers = new List<double>();
        int previous = dominantSequence[0];
        for (int probabilityIndex = 1; probabilityIndex < dominantSequence.Count; probabilityIndex++)
        {
            if (dominantSequence[probabilityIndex] == previous)
                continue;
            crossovers.Add(probabilityIndex / (double)DiagnosticProbabilityCount);
            previous = dominantSequence[probabilityIndex];
        }
        return crossovers.ToArray();
    }

    /// <summary>
    /// Stores the measured identification evidence for one fixed recovery realization.
    /// </summary>
    /// <param name="TheoreticalShares">Integrated cause shares on the parent probability scale.</param>
    /// <param name="HardWinnerCounts">Latent hard winner counts from the fixed realization.</param>
    /// <param name="SoftEventCounts">Observed conditional cause counts from the fixed realization.</param>
    /// <param name="DominanceMasses">Probability masses where each cause responsibility is at least one half.</param>
    /// <param name="Crossovers">Dominant-cause crossover probabilities.</param>
    private sealed record DesignDiagnostics(
        double[] TheoreticalShares,
        int[] HardWinnerCounts,
        double[] SoftEventCounts,
        double[] DominanceMasses,
        double[] Crossovers);
}
