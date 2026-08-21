using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>
    /// Source-level regression tests for <c>TimeSeriesAnalysisControl</c> defaults that are
    /// difficult to exercise without constructing the full WPF control graph.
    /// </summary>
    [TestClass]
    public class TimeSeriesAnalysisControlSourceTests
    {
        /// <summary>
        /// Verifies the training-period annotation label is placed 30 percent along the line by default.
        /// </summary>
        [TestMethod]
        public void TrainingPeriodAnnotation_TextLinePosition_DefaultsToThirtyPercent()
        {
            string source = ReadAppSource(Path.Combine("GUI", "TimeSeriesAnalysis", "TimeSeriesAnalysisControl.xaml.cs"));

            StringAssert.Contains(source, "Text = \"End of Training Period\",");
            StringAssert.Contains(source, "TextLinePosition = 0.3,");
        }

        /// <summary>
        /// Verifies that the transform selector remains bound to the established ARIMAX property path.
        /// </summary>
        [TestMethod]
        public void TransformSelector_RetainsEstablishedBindingContract()
        {
            string xaml = ReadAppSource(Path.Combine("GUI", "TimeSeriesAnalysis", "TimeSeriesAnalysisPropertiesControl.xaml"));

            StringAssert.Contains(xaml, "ItemsSource=\"{Binding TransformTypeItems, UpdateSourceTrigger=PropertyChanged}\"");
            StringAssert.Contains(xaml, "SelectedValue=\"{Binding Element.ARIMAX.TransformType, UpdateSourceTrigger=PropertyChanged, Mode=TwoWay}\"");
            StringAssert.Contains(xaml, "DisplayMemberPath=\"DisplayName\" SelectedValuePath=\"Value\"");
        }

        /// <summary>
        /// Verifies that the App continues exposing the four established transform choices and enum members.
        /// </summary>
        [TestMethod]
        public void TransformSelector_RetainsEstablishedItems()
        {
            string source = ReadAppSource(Path.Combine("GUI", "TimeSeriesAnalysis", "TimeSeriesAnalysisPropertiesControl.xaml.cs"));

            StringAssert.Contains(source, "new TransformTypeItem(\"None\", RMC.BestFit.Models.Transform.None)");
            StringAssert.Contains(source, "new TransformTypeItem(\"Logarithmic\", RMC.BestFit.Models.Transform.Logarithmic)");
            StringAssert.Contains(source, "new TransformTypeItem(\"Box-Cox\", RMC.BestFit.Models.Transform.BoxCox)");
            StringAssert.Contains(source, "new TransformTypeItem(\"Yeo-Johnson\", RMC.BestFit.Models.Transform.YeoJohnson)");
        }

        /// <summary>
        /// Verifies the residual plot follows the transformed/differenced training series rather
        /// than indexing it with the longer raw training-window count.
        /// </summary>
        [TestMethod]
        public void ResidualPlot_UsesDateAlignedDifferencedCount()
        {
            string source = ReadAppSource(Path.Combine("GUI", "TimeSeriesAnalysis", "TimeSeriesAnalysisControl.xaml.cs"));

            StringAssert.Contains(source, "int residualCount = Math.Min(_residuals!.Length, Element.ARIMAX.TrainingTimeSeries.Count);");
            StringAssert.Contains(source, "for (int i = 0; i < residualCount; i++)");
            StringAssert.Contains(source, "Element.ARIMAX.TrainingTimeSeries[i].Index.ToOADate()");
        }

        /// <summary>
        /// Verifies credible intervals split at the final training index and share that boundary
        /// between training and prediction series.
        /// </summary>
        [TestMethod]
        public void PredictionPlot_SplitsAtFinalTrainingIndex()
        {
            string source = ReadAppSource(Path.Combine("GUI", "TimeSeriesAnalysis", "TimeSeriesAnalysisControl.xaml.cs"));

            StringAssert.Contains(source, "Element.ARIMAX.TrainingTimeSteps - 1");
            StringAssert.Contains(source, "var trainingCi = ciPoints.GetRange(0, splitIdx + 1);");
            StringAssert.Contains(source, "ciPoints.GetRange(splitIdx, ciPoints.Count - splitIdx)");
        }

        /// <summary>
        /// Reads an App source file using a byte-preserving single-byte decoding.
        /// </summary>
        /// <param name="relativePath">The source path relative to the App project root.</param>
        /// <param name="sourceFilePath">The compiler-provided source path for this test file.</param>
        /// <returns>The source file text.</returns>
        private static string ReadAppSource(string relativePath, [CallerFilePath] string sourceFilePath = "")
        {
            string path = Path.Combine(FindRepoRoot(sourceFilePath), "src", "RMC.BestFit.App", relativePath);
            return Encoding.Latin1.GetString(File.ReadAllBytes(path));
        }

        /// <summary>
        /// Finds the repository root by walking up from a known source file.
        /// </summary>
        /// <param name="sourceFilePath">A source file path inside the repository.</param>
        /// <returns>The absolute repository root path.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the repository root cannot be found.</exception>
        private static string FindRepoRoot(string sourceFilePath)
        {
            var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFilePath)!);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "RMC.BestFit.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not locate repository root.");
        }
    }
}
