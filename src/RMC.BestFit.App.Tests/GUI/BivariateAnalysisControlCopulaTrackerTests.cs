using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Models;
using RMC_BestFit;
using System;
using System.Collections.Generic;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>
    /// Tests the bivariate copula plot helpers that feed observed-point tracker text.
    /// </summary>
    /// <remarks>
    /// The helpers are static so the tests can verify tracker data without constructing
    /// the full WPF control and application resource graph.
    /// </remarks>
    [TestClass]
    public class BivariateAnalysisControlCopulaTrackerTests
    {
        /// <summary>
        /// Verifies raw-value observed copula points include only sorted shared indexes.
        /// </summary>
        [TestMethod]
        public void CreateObservedCopulaPlotPoints_RawMode_UsesSharedIndexesAndRawValues()
        {
            var xData = new List<ExactData>
            {
                new ExactData(3, 30.0),
                new ExactData(1, 10.0),
                new ExactData(2, 20.0)
            };
            var yData = new List<ExactData>
            {
                new ExactData(2, 200.0),
                new ExactData(4, 400.0),
                new ExactData(1, 100.0)
            };

            var points = BivariateAnalysisControl.CreateObservedCopulaPlotPoints(xData, yData, useProbabilityAxis: false);

            Assert.AreEqual(2, points.Count);
            Assert.AreEqual(1, points[0].Index);
            Assert.AreEqual(10.0, points[0].X, 1e-12);
            Assert.AreEqual(100.0, points[0].Y, 1e-12);
            Assert.AreEqual(2, points[1].Index);
            Assert.AreEqual(20.0, points[1].X, 1e-12);
            Assert.AreEqual(200.0, points[1].Y, 1e-12);
        }

        /// <summary>
        /// Verifies probability-axis observed copula points use plotting-position complements.
        /// </summary>
        [TestMethod]
        public void CreateObservedCopulaPlotPoints_ProbabilityMode_UsesPlottingPositionComplements()
        {
            var xData = new List<ExactData>
            {
                new ExactData(10, 100.0, plottingPosition: 0.25),
                new ExactData(20, 200.0, plottingPosition: 0.40)
            };
            var yData = new List<ExactData>
            {
                new ExactData(10, 1000.0, plottingPosition: 0.10),
                new ExactData(20, 2000.0, plottingPosition: 0.70)
            };

            var points = BivariateAnalysisControl.CreateObservedCopulaPlotPoints(xData, yData, useProbabilityAxis: true);

            Assert.AreEqual(2, points.Count);
            Assert.AreEqual(10, points[0].Index);
            Assert.AreEqual(0.75, points[0].X, 1e-12);
            Assert.AreEqual(0.90, points[0].Y, 1e-12);
            Assert.AreEqual(20, points[1].Index);
            Assert.AreEqual(0.60, points[1].X, 1e-12);
            Assert.AreEqual(0.30, points[1].Y, 1e-12);
        }

        /// <summary>
        /// Verifies low outliers are excluded before observed copula points are paired.
        /// </summary>
        [TestMethod]
        public void CreateObservedCopulaPlotPoints_LowOutliers_AreExcludedBeforePairing()
        {
            var xData = new List<ExactData>
            {
                new ExactData(1, 10.0, isLowOutlier: true),
                new ExactData(2, 20.0),
                new ExactData(3, 30.0)
            };
            var yData = new List<ExactData>
            {
                new ExactData(1, 100.0),
                new ExactData(2, 200.0, isLowOutlier: true),
                new ExactData(3, 300.0)
            };

            var points = BivariateAnalysisControl.CreateObservedCopulaPlotPoints(xData, yData, useProbabilityAxis: false);

            Assert.AreEqual(1, points.Count);
            Assert.AreEqual(3, points[0].Index);
            Assert.AreEqual(30.0, points[0].X, 1e-12);
            Assert.AreEqual(300.0, points[0].Y, 1e-12);
        }

        /// <summary>
        /// Verifies observed exact-data trackers expose the OxyPlot item-property index token.
        /// </summary>
        [TestMethod]
        public void CreateObservedCopulaTrackerFormat_IncludesIndexPlaceholder()
        {
            string rawTracker = BivariateAnalysisControl.CreateObservedCopulaTrackerFormat(useProbabilityAxis: false);
            string probabilityTracker = BivariateAnalysisControl.CreateObservedCopulaTrackerFormat(useProbabilityAxis: true);

            StringAssert.Contains(rawTracker, "Index: {Index}");
            StringAssert.Contains(probabilityTracker, "Index: {Index}");
            StringAssert.Contains(rawTracker, "{1}: {2:");
            StringAssert.Contains(probabilityTracker, "{1}: {2:0.000000}");
        }

        /// <summary>
        /// Verifies simulated scatter and contour tracker formats do not include input indexes.
        /// </summary>
        [TestMethod]
        public void CopulaModelTrackerFormats_DoNotIncludeIndexPlaceholder()
        {
            string simulatedRaw = BivariateAnalysisControl.CreateCopulaScatterTrackerFormat(useProbabilityAxis: false);
            string simulatedProbability = BivariateAnalysisControl.CreateCopulaScatterTrackerFormat(useProbabilityAxis: true);
            string contourRaw = BivariateAnalysisControl.CreateContourTrackerFormat(useProbabilityAxis: false);
            string contourProbability = BivariateAnalysisControl.CreateContourTrackerFormat(useProbabilityAxis: true);

            Assert.IsFalse(simulatedRaw.Contains("{Index}", StringComparison.Ordinal));
            Assert.IsFalse(simulatedProbability.Contains("{Index}", StringComparison.Ordinal));
            Assert.IsFalse(contourRaw.Contains("{Index}", StringComparison.Ordinal));
            Assert.IsFalse(contourProbability.Contains("{Index}", StringComparison.Ordinal));
            StringAssert.Contains(contourRaw, "{5}: {6:0.000000}");
            StringAssert.Contains(contourProbability, "{5}: {6:0.000000}");
        }
    }
}
