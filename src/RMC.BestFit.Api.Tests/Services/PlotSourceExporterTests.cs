using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Services
{
    [TestClass]
    public class PlotSourceExporterTests
    {
        [TestMethod]
        public void Export_RejectsNeverRunAndIncompleteState()
        {
            var resource = TestAnalyses.CreateUnivariateResource();
            Assert.ThrowsException<ResourceNotFoundException>(() => PlotSourceExporter.Export(resource));

            resource.State = AnalysisRunState.Succeeded;
            Assert.ThrowsException<ResourceNotFoundException>(() => PlotSourceExporter.Export(resource));
        }

        [TestMethod]
        public void Export_RejectsConcurrentRunWithoutWaiting()
        {
            var resource = TestAnalyses.CreateUnivariateResource();
            resource.RunLock.Wait();
            try
            {
                Assert.ThrowsException<ResourceConflictException>(() => PlotSourceExporter.Export(resource));
            }
            finally
            {
                resource.RunLock.Release();
            }
        }

        [TestMethod]
        public void Export_DetachesResultsDataAndOptInSamples()
        {
            var resource = TestAnalyses.CreateUnivariateResource(UnivariateDistributionType.Normal);
            var analysis = resource.Univariate!;
            var draws = Enumerable.Range(0, 100)
                .Select(index => new ParameterSet(new[] { 100d + index / 10d, 10d }, 0d)).ToList();
            analysis.BayesianAnalysis.SetCustomMCMCResults(new MCMCResults(draws[0], draws, 0.1),
                skipInformationCriteria: true);
            analysis.GetType().GetProperty("AnalysisResults")!.SetValue(analysis,
                new UncertaintyAnalysisResults
                {
                    ModeCurve = new[] { 100d },
                    MeanCurve = new[] { 105d },
                    ConfidenceIntervals = new[,] { { 90d, 120d } }
                });
            analysis.GetType().GetProperty("IsEstimated")!.SetValue(analysis, true);
            resource.State = AnalysisRunState.Succeeded;
            resource.LastRunUtc = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);

            var originalConfiguration = analysis.ToXElement().ToString();
            var originalParameters = analysis.UnivariateDistribution.Parameters.Select(p => p.Value).ToArray();

            var compact = PlotSourceExporter.Export(resource);
            Assert.AreEqual(originalConfiguration, analysis.ToXElement().ToString());
            CollectionAssert.AreEqual(originalParameters,
                analysis.UnivariateDistribution.Parameters.Select(p => p.Value).ToArray());
            Assert.AreEqual(1, compact.SchemaVersion);
            Assert.AreEqual(resource.Id, compact.AnalysisId);
            Assert.IsNull(compact.Samples);
            Assert.AreEqual("mcmc", compact.SampleOrigin);
            Assert.IsNotNull(compact.AnalysisXml);
            Assert.IsNotNull(compact.ModelXml);
            Assert.IsNotNull(compact.DataFrameXml);
            Assert.AreEqual(20, compact.Observations.Count);
            Assert.AreEqual(2000d, compact.Observations[0].Index);
            Assert.AreEqual(100d, compact.Observations[0].Value);
            Assert.AreEqual(100d, compact.Results.GetProperty("frequencyCurve").GetProperty("modeCurve")[0].GetDouble());
            var firstBin = compact.ParameterDiagnostics[0].Histogram[0];
            Assert.IsTrue(firstBin.UpperBound > firstBin.LowerBound);
            Assert.IsTrue(firstBin.Frequency >= 0d);
            var expectedPrior = analysis.UnivariateDistribution.Parameters[0].PriorDistribution.CreatePDFGraph();
            Assert.AreEqual(expectedPrior[0, 0], compact.ParameterDiagnostics[0].PriorDensity[0].X, 1e-12);
            Assert.AreEqual(expectedPrior[0, 1], compact.ParameterDiagnostics[0].PriorDensity[0].Y, 1e-12);
            var diagnosticJson = System.Text.Json.JsonSerializer.SerializeToElement(compact.ParameterDiagnostics[0]);
            Assert.IsTrue(diagnosticJson.TryGetProperty("DisplayName", out var displayName));
            Assert.AreEqual(analysis.UnivariateDistribution.Parameters[0].DisplayName, displayName.GetString());

            var full = PlotSourceExporter.Export(resource, includeSamples: true);
            Assert.AreEqual(100, full.Samples!.Output.Count);
            Assert.AreEqual(100d, full.Samples.Output[0].Values[0]);
            full.Samples.Output[0].Values[0] = -1d;
            Assert.AreEqual(100d, PlotSourceExporter.Export(resource, includeSamples: true).Samples!.Output[0].Values[0]);
            analysis.UnivariateDistribution.DataFrame.ExactSeries.Add(new ExactData(2099, 9999d));
            Assert.IsFalse(compact.DataFrameXml!.Contains("9999"));
            Assert.AreEqual(20, compact.Observations.Count);
        }

        [TestMethod]
        public void Export_BivariateIncludesBothMarginalObservationFrames()
        {
            var resource = TestAnalyses.CreateBivariateResource();
            var analysis = resource.Bivariate!;
            analysis.GetType().GetProperty("AnalysisResults")!.SetValue(analysis,
                new UncertaintyAnalysisResults { ModeCurve = new[] { 0.1d, 0.2d } });
            analysis.GetType().GetProperty("IsEstimated")!.SetValue(analysis, true);
            resource.State = AnalysisRunState.Succeeded;

            var source = PlotSourceExporter.Export(resource);
            StringAssert.Contains(source.MarginalXDataFrameXml!, "2000");
            StringAssert.Contains(source.MarginalYDataFrameXml!, "2000");
            StringAssert.Contains(source.MarginalXModelXml!, "UnivariateDistribution");
            StringAssert.Contains(source.MarginalYModelXml!, "UnivariateDistribution");
            Assert.IsNotNull(source.BivariatePlot);
            Assert.AreEqual(20, source.BivariatePlot.Observed.Count);
            Assert.AreEqual(100, source.BivariatePlot.XGrid.Count);
            Assert.AreEqual(100, source.BivariatePlot.YGrid.Count);
            Assert.AreEqual(100, source.BivariatePlot.LogPdf.Count);
            Assert.AreEqual(100, source.BivariatePlot.LogPdf[0].Count);
            Assert.AreEqual(100, source.BivariatePlot.JointExceedance.Count);
            // The app calls GenerateRandomValues with this saved seed and capped output length.
            var simulated = analysis.BivariateDistribution.GenerateRandomValues(
                Math.Min(10000, analysis.BayesianAnalysis.OutputLength), analysis.BayesianAnalysis.PRNGSeed);
            Assert.AreEqual(simulated[0, 0], source.BivariatePlot.Simulated[0].X, 1e-12);
            Assert.AreEqual(simulated[0, 1], source.BivariatePlot.Simulated[0].Y, 1e-12);
            var expectedFirstContour = analysis.BivariateDistribution.Copula.ANDJointExceedanceProbability(
                source.BivariatePlot.XCdf[0], source.BivariatePlot.YCdf[0]);
            Assert.AreEqual(expectedFirstContour, source.BivariatePlot.JointExceedance[0][0], 1e-12);
            var original = source.BivariatePlot.Observed[0].X;
            source.BivariatePlot.Observed[0].X = -999d;
            Assert.AreEqual(original, PlotSourceExporter.Export(resource).BivariatePlot!.Observed[0].X);
        }

        [TestMethod]
        public void Export_RejectsComponentRerunAfterParentRun()
        {
            var resource = TestAnalyses.CreateBivariateResource();
            resource.Bivariate!.GetType().GetProperty("AnalysisResults")!.SetValue(resource.Bivariate,
                new UncertaintyAnalysisResults { ModeCurve = new[] { 0.1d, 0.2d } });
            resource.Bivariate.GetType().GetProperty("IsEstimated")!.SetValue(resource.Bivariate, true);
            resource.State = AnalysisRunState.Succeeded;
            resource.LastRunUtc = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
            resource.MarginalXResource!.LastRunUtc = resource.LastRunUtc.Value.AddMinutes(1);

            Assert.ThrowsException<ResourceConflictException>(() => PlotSourceExporter.Export(resource));
        }

        [TestMethod]
        public void Export_RatingResidualsMatchCoreWithoutRunningAnalysis()
        {
            var resource = TestAnalyses.CreateRatingCurveResource();
            var analysis = resource.RatingCurve!;
            var count = analysis.StageBins;
            var intervals = new double[count, 3];
            var modes = new double[count];
            var means = new double[count];
            for (var i = 0; i < count; i++)
            {
                intervals[i, 0] = 2 + i;
                intervals[i, 1] = 10 + i;
                intervals[i, 2] = 12 + i;
                modes[i] = 11 + i;
                means[i] = 11.5 + i;
            }
            analysis.GetType().GetProperty("AnalysisResults")!.SetValue(analysis,
                new UncertaintyAnalysisResults { ModeCurve = modes, MeanCurve = means, ConfidenceIntervals = intervals });
            analysis.GetType().GetProperty("IsEstimated")!.SetValue(analysis, true);
            resource.State = AnalysisRunState.Succeeded;
            var source = PlotSourceExporter.Export(resource);
            var parameters = analysis.RatingCurve.Parameters.Select(p => p.Value).ToArray();
            CollectionAssert.AreEqual(analysis.RatingCurve.Residuals(parameters), source.ResidualPlot!.Residuals);
            CollectionAssert.AreEqual(analysis.RatingCurve.FittedValues(parameters), source.ResidualPlot.Fitted);
            Assert.AreEqual(analysis.RatingCurve.GetAlignedObservations().Count,
                source.ResidualPlot.AlignedObservations.Count);
        }

        [TestMethod]
        public void Export_TimeSeriesDatesAndResidualsMatchCoreWithoutRunningAnalysis()
        {
            var resource = TestAnalyses.CreateTimeSeriesAnalysisResource(TimeSeriesModelType.Ar);
            var analysis = resource.Ar!;
            var interval = new double[3, 3] { { 0, 9, 11 }, { 1, 10, 12 }, { 2, 11, 13 } };
            analysis.GetType().GetProperty("AnalysisResults")!.SetValue(analysis,
                new UncertaintyAnalysisResults { ModeCurve = new[] { 10d, 11d, 12d },
                    MeanCurve = new[] { 10d, 11d, 12d }, ConfidenceIntervals = interval });
            analysis.GetType().GetProperty("IsEstimated")!.SetValue(analysis, true);
            resource.State = AnalysisRunState.Succeeded;
            var source = PlotSourceExporter.Export(resource);
            var model = analysis.AutoRegressive;
            var parameters = model.Parameters.Select(p => p.Value).ToArray();
            CollectionAssert.AreEqual(model.Residuals(parameters), source.ResidualPlot!.Residuals);
            Assert.AreEqual(model.TimeSeries[0].Index, source.ResultDates[0]);
            Assert.AreEqual(Numerics.Data.TimeSeries.AddTimeInterval(model.TimeSeries[0].Index, model.TimeSeries.TimeInterval),
                source.ResultDates[1]);
        }

        [TestMethod]
        public void Export_CopiesSavedNonstationaryChronology()
        {
            var resource = TestAnalyses.CreateUnivariateResource(UnivariateDistributionType.Normal);
            var analysis = resource.Univariate!;
            analysis.UnivariateDistribution.IsNonstationary = true;
            var probabilityCount = analysis.ProbabilityOrdinates.Count;
            var frequencyIntervals = new double[probabilityCount, 2];
            for (var index = 0; index < probabilityCount; index++)
            {
                frequencyIntervals[index, 0] = 90d;
                frequencyIntervals[index, 1] = 110d;
            }
            analysis.GetType().GetProperty("AnalysisResults")!.SetValue(analysis,
                new UncertaintyAnalysisResults
                {
                    ModeCurve = Enumerable.Repeat(100d, probabilityCount).ToArray(),
                    MeanCurve = Enumerable.Repeat(101d, probabilityCount).ToArray(),
                    ConfidenceIntervals = frequencyIntervals
                });
            // The chronology result is independent of the frequency probability grid.
            var chronology = new UncertaintyAnalysisResults
            {
                ModeCurve = new[] { 100d, 101d },
                MeanCurve = new[] { 105d, 106d },
                ConfidenceIntervals = new[,] { { 90d, 120d }, { 91d, 121d } }
            };
            analysis.GetType().GetProperty("ChronologyAnalysisResults")!.SetValue(analysis, chronology);
            analysis.GetType().GetProperty("IsEstimated")!.SetValue(analysis, true);
            resource.State = AnalysisRunState.Succeeded;

            var snapshot = PlotSourceExporter.Export(resource);
            Assert.IsNotNull(snapshot.Chronology);
            CollectionAssert.AreEqual(new[] { 105d, 106d }, snapshot.Chronology.MeanCurve);
            CollectionAssert.AreEqual(new[] { 90d, 91d }, snapshot.Chronology.CiLower);
            snapshot.Chronology.MeanCurve[0] = -1d;
            Assert.AreEqual(105d, PlotSourceExporter.Export(resource).Chronology!.MeanCurve[0]);
        }

        [TestMethod]
        public void Export_CopiesCompositeComponentCurvesInConfiguredOrder()
        {
            var resource = TestAnalyses.CreateCompositeResource();
            foreach (var (component, position) in resource.ComponentResources!.Select((item, index) => (item, index)))
            {
                var analysis = component.Univariate!;
                var count = analysis.ProbabilityOrdinates.Count;
                analysis.GetType().GetProperty("AnalysisResults")!.SetValue(analysis,
                    new UncertaintyAnalysisResults
                    {
                        ModeCurve = Enumerable.Repeat(100d + position, count).ToArray(),
                        MeanCurve = Enumerable.Repeat(101d + position, count).ToArray(),
                        ConfidenceIntervals = new double[count, 2]
                    });
                analysis.GetType().GetProperty("IsEstimated")!.SetValue(analysis, true);
                component.State = AnalysisRunState.Succeeded;
            }
            var composite = resource.Composite!;
            var probabilityCount = composite.ProbabilityOrdinates.Count;
            composite.GetType().GetProperty("AnalysisResults")!.SetValue(composite,
                new UncertaintyAnalysisResults
                {
                    ModeCurve = Enumerable.Repeat(100d, probabilityCount).ToArray(),
                    MeanCurve = Enumerable.Repeat(101d, probabilityCount).ToArray(),
                    ConfidenceIntervals = new double[probabilityCount, 2]
                });
            composite.GetType().GetProperty("IsEstimated")!.SetValue(composite, true);
            resource.State = AnalysisRunState.Succeeded;

            var source = PlotSourceExporter.Export(resource);
            Assert.AreEqual(2, source.ComponentCurves.Count);
            Assert.AreEqual(resource.ComponentResources![0].Name, source.ComponentCurves[0].Name);
            Assert.AreEqual(100d, source.ComponentCurves[0].Values[0]);
            Assert.AreEqual(101d, source.ComponentCurves[1].Values[0]);
        }
    }
}
