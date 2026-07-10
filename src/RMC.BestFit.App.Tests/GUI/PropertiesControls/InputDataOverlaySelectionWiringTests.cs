using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace RMC.BestFit.App.Tests.GUI.PropertiesControls
{
    /// <summary>
    /// Source-level regression tests for optional InputData overlay wiring in properties controls.
    /// </summary>
    /// <remarks>
    /// Full WPF properties-control construction depends on the application resource graph. These
    /// tests pin the key control wiring directly: the ComboBox must bind through
    /// <c>InputDataSelectionItem.Value</c>, the option list must include <c>&lt;None&gt;</c>, and
    /// null must be treated as a valid no-overlay selection.
    /// </remarks>
    [TestClass]
    public class InputDataOverlaySelectionWiringTests
    {
        /// <summary>
        /// Verifies the Composite and CFA input-data ComboBoxes bind through selection items.
        /// </summary>
        [TestMethod]
        public void InputDataComboBoxes_BindThroughSelectionItemValue()
        {
            foreach (string relativePath in InputDataOverlayXamlPaths())
            {
                string markup = ExtractInputDataComboBoxMarkup(ReadRepoFile(relativePath));

                StringAssert.Contains(markup, "DisplayMemberPath=\"DisplayName\"");
                StringAssert.Contains(markup, "SelectedValuePath=\"Value\"");
                StringAssert.Contains(markup, "SelectedValue=\"{Binding Element.InputData");
            }
        }

        /// <summary>
        /// Verifies the Composite and CFA controls use selection-item collections.
        /// </summary>
        [TestMethod]
        public void InputDataLists_UseSelectionItemCollections()
        {
            foreach (string relativePath in InputDataOverlayCodePaths())
            {
                string source = ReadRepoFile(relativePath);

                StringAssert.Contains(source, "ObservableCollection<InputDataSelectionItem> InputDataList");
            }
        }

        /// <summary>
        /// Verifies the no-overlay item is inserted before real project input data.
        /// </summary>
        [TestMethod]
        public void LoadInputData_AddsNoneBeforeProjectInputData()
        {
            foreach (string relativePath in InputDataOverlayCodePaths())
            {
                string loadInputData = ExtractMethodSource(ReadRepoFile(relativePath), "LoadInputData");
                int noneIndex = loadInputData.IndexOf("InputDataList.Add(InputDataSelectionItem.CreateNone())", StringComparison.Ordinal);
                int realItemsIndex = loadInputData.IndexOf("foreach (IElement element in collection)", StringComparison.Ordinal);

                Assert.IsTrue(noneIndex >= 0, relativePath + " should add the <None> item.");
                Assert.IsTrue(realItemsIndex >= 0, relativePath + " should enumerate project input data.");
                Assert.IsTrue(noneIndex < realItemsIndex, relativePath + " should add <None> before real input data.");
            }
        }

        /// <summary>
        /// Verifies null input data is treated as valid in both properties controls.
        /// </summary>
        [TestMethod]
        public void InputDataValidation_AllowsNullOverlaySelection()
        {
            foreach (string relativePath in InputDataOverlayCodePaths())
            {
                string source = ReadRepoFile(relativePath);

                StringAssert.Contains(source, "Element.InputData != null && Element.InputData.Name == null");
            }
        }

        /// <summary>
        /// Gets the XAML files whose optional input-data ComboBoxes are in scope.
        /// </summary>
        /// <returns>The repository-relative XAML paths.</returns>
        private static string[] InputDataOverlayXamlPaths()
        {
            return new[]
            {
                Path.Combine("src", "RMC.BestFit.App", "GUI", "UnivariateAnalysis", "Composite", "CompositeAnalysisPropertiesControl.xaml"),
                Path.Combine("src", "RMC.BestFit.App", "GUI", "BivariateAnalysis", "CoincidentFrequency", "CoincidentFrequencyPropertiesControl.xaml")
            };
        }

        /// <summary>
        /// Gets the code-behind files whose optional input-data lists are in scope.
        /// </summary>
        /// <returns>The repository-relative code-behind paths.</returns>
        private static string[] InputDataOverlayCodePaths()
        {
            return new[]
            {
                Path.Combine("src", "RMC.BestFit.App", "GUI", "UnivariateAnalysis", "Composite", "CompositeAnalysisPropertiesControl.xaml.cs"),
                Path.Combine("src", "RMC.BestFit.App", "GUI", "BivariateAnalysis", "CoincidentFrequency", "CoincidentFrequencyPropertiesControl.xaml.cs")
            };
        }

        /// <summary>
        /// Reads a repository-relative file.
        /// </summary>
        /// <param name="relativePath">The repository-relative path to read.</param>
        /// <param name="sourceFilePath">The compiler-provided path used to locate the repository root.</param>
        /// <returns>The file contents.</returns>
        private static string ReadRepoFile(string relativePath, [CallerFilePath] string sourceFilePath = "")
        {
            return File.ReadAllText(Path.Combine(FindRepoRoot(sourceFilePath), relativePath));
        }

        /// <summary>
        /// Extracts the InputDataComboBox control markup from a properties-control XAML file.
        /// </summary>
        /// <param name="xaml">The XAML source text.</param>
        /// <returns>The markup block for the InputDataComboBox property control.</returns>
        private static string ExtractInputDataComboBoxMarkup(string xaml)
        {
            int start = xaml.IndexOf("x:Name=\"InputDataComboBox\"", StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, "InputDataComboBox should exist.");

            int end = xaml.IndexOf("</cntrls:ContentPropertyControl>", start, StringComparison.Ordinal);
            Assert.IsTrue(end > start, "InputDataComboBox property-control block should close.");

            return xaml.Substring(start, end - start);
        }

        /// <summary>
        /// Extracts a private method body from a C# source file using brace counting.
        /// </summary>
        /// <param name="source">The C# source text.</param>
        /// <param name="methodName">The method name to extract.</param>
        /// <returns>The method source, including its signature and braces.</returns>
        private static string ExtractMethodSource(string source, string methodName)
        {
            int start = source.IndexOf("private void " + methodName + "()", StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, methodName + " should exist.");

            int openingBrace = source.IndexOf('{', start);
            Assert.IsTrue(openingBrace > start, methodName + " should have an opening brace.");

            int depth = 0;
            for (int i = openingBrace; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(start, i - start + 1);
                    }
                }
            }

            Assert.Fail(methodName + " should have a closing brace.");
            return string.Empty;
        }

        /// <summary>
        /// Finds the repository root from the test output directory.
        /// </summary>
        /// <returns>The absolute repository root.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when the source tree cannot be found.</exception>
        private static string FindRepoRoot(string sourceFilePath)
        {
            string[] searchRoots = new[]
            {
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory(),
                Path.GetDirectoryName(sourceFilePath)
            };
            foreach (string searchRoot in searchRoots)
            {
                if (string.IsNullOrEmpty(searchRoot)) continue;
                DirectoryInfo directory = new DirectoryInfo(searchRoot);
                while (directory != null)
                {
                    string candidate = Path.Combine(directory.FullName, "src", "RMC.BestFit.App");
                    if (Directory.Exists(candidate))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }

            throw new DirectoryNotFoundException("Could not locate repository root from the test output directory.");
        }
    }
}
