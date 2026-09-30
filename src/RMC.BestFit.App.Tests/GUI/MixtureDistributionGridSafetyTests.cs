using GenericControls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Distributions;
using RMC.BestFit.App.Tests.GUI.Support;
using RMC.BestFit.UI;
using RMC_BestFit;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace RMC.BestFit.App.Tests.GUI
{
    /// <summary>
    /// Exercises the actual mixture properties grid, realized cells, and deferred row edits.
    /// </summary>
    /// <remarks>
    /// No estimation runs. Tests use default unestimated analyses and the production resource graph.
    /// Application resources and WPF controls require serialized STA execution.
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public class MixtureDistributionGridSafetyTests
    {
        /// <summary>
        /// Releases resources created on this test's STA before another test uses its own STA.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            WpfTestHost.ReleaseResources();
        }

        /// <summary>
        /// Keeps the column-header sort menu off: the grid rows are identity references that cannot be compared,
        /// and a sorted view would no longer follow the component order.
        /// </summary>
        [STATestMethod]
        public void DistributionGrid_DoesNotOfferHeaderSorting()
        {
            using var fixture = new GridFixture();
            Assert.IsFalse(fixture.Grid.ShowSortContextMenu,
                "Sorting compares MixtureDistributionRow items, which throws and terminates the application.");
        }

        /// <summary>
        /// Deletes the second equal-valued default row and renders the result without corrupting WPF's generator.
        /// </summary>
        [STATestMethod]
        public void DeleteSecondDefaultRow_PreservesFirstRowAndRendersThroughUndoRedo()
        {
            using var fixture = new GridFixture();
            object firstRow = fixture.Grid.Items[0];
            fixture.SelectRows(1);
            fixture.Grid.DeleteRows();
            fixture.Render();

            fixture.AssertCounts(1);
            Assert.AreSame(firstRow, fixture.Grid.Items[0], "Deleting the second row must retain the first row identity.");
            Assert.IsTrue(BindingOperations.IsDataBound(fixture.Grid, ItemsControl.ItemsSourceProperty));
            Assert.AreEqual(1, fixture.Element.UndoManager.UndoStack.Count);

            fixture.Element.UndoManager.Undo();
            fixture.Render();
            fixture.AssertCounts(2);
            Assert.IsFalse(fixture.Element.UndoManager.CanUndo, "One row deletion is one undo action.");
            fixture.Element.UndoManager.Redo();
            fixture.Render();
            fixture.AssertCounts(1);
        }

        /// <summary>
        /// Verifies equal distribution values remain distinct selectable and editable rows.
        /// </summary>
        [STATestMethod]
        public void SecondDuplicateRow_SelectsAndEditsOnlySecondComponent()
        {
            using var fixture = new GridFixture();
            object firstRow = fixture.Grid.Items[0];
            object secondRow = fixture.Grid.Items[1];
            fixture.SelectRows(1);
            CollectionAssert.AreEqual(new[] { 1 }, fixture.Grid.GetRowsWithSelectedCells().ToArray());

            var combo = FindDescendant<ComboBox>(fixture.Grid.GetCell(1, 0));
            Assert.IsNotNull(combo);
            combo.SelectedValue = UnivariateDistributionType.Gumbel;
            fixture.Render();

            CollectionAssert.AreEqual(new[] { UnivariateDistributionType.Normal, UnivariateDistributionType.Gumbel },
                fixture.Element.Distributions.ToArray());
            Assert.AreSame(firstRow, fixture.Grid.Items[0]);
            Assert.AreSame(secondRow, fixture.Grid.Items[1], "Changing a distribution type retains its row identity.");
            fixture.Element.UndoManager.Undo();
            fixture.Render();
            Assert.AreEqual(UnivariateDistributionType.Normal, fixture.Element.Distributions[1]);
            fixture.Element.UndoManager.Redo();
            fixture.Render();
            Assert.AreEqual(UnivariateDistributionType.Gumbel, fixture.Element.Distributions[1]);
        }

        /// <summary>
        /// Verifies toolbar operations retain the existing one-to-three component bounds.
        /// </summary>
        [STATestMethod]
        public void AddAndDeleteRows_EnforceExistingComponentBounds()
        {
            using var fixture = new GridFixture();
            fixture.Grid.AddRows(20);
            fixture.Render();
            fixture.AssertCounts(3);
            fixture.SelectRows(0, 1, 2);
            fixture.Grid.DeleteRows();
            fixture.Render();
            fixture.AssertCounts(3);
            fixture.SelectRows(1, 2);
            fixture.Grid.DeleteRows();
            fixture.Render();
            fixture.AssertCounts(1);
            fixture.SelectRows(0);
            fixture.Grid.DeleteRows();
            fixture.Render();
            fixture.AssertCounts(1);
        }

        /// <summary>
        /// Verifies a queued delete cannot operate on a different analysis after an element switch.
        /// </summary>
        [STATestMethod]
        public void DeferredDelete_AfterElementSwitch_LeavesBothAnalysesUnchanged()
        {
            using var fixture = new GridFixture();
            var replacement = new MixtureAnalysis("Replacement", fixture.Collection);
            fixture.SelectRows(1);
            fixture.Grid.DeleteRows();
            fixture.Control.Element = replacement;
            fixture.Render();

            Assert.AreEqual(2, fixture.Element.Distributions.Count);
            Assert.AreEqual(2, replacement.Distributions.Count);
            Assert.AreEqual(2, fixture.Grid.Items.Count);
        }

        /// <summary>
        /// Verifies replacing the public collection invalidates a delete already queued for its previous rows.
        /// </summary>
        [STATestMethod]
        public void DeferredDelete_AfterCollectionReplacement_LeavesBothCollectionsUnchanged()
        {
            using var fixture = new GridFixture();
            var previous = fixture.Element.Distributions;
            var replacement = new ObservableCollection<UnivariateDistributionType>
            {
                UnivariateDistributionType.Gumbel, UnivariateDistributionType.Normal
            };
            fixture.SelectRows(1);
            fixture.Grid.DeleteRows();
            fixture.Element.Distributions = replacement;
            fixture.Render();

            Assert.AreEqual(2, previous.Count);
            CollectionAssert.AreEqual(new[] { UnivariateDistributionType.Gumbel, UnivariateDistributionType.Normal },
                replacement.ToArray());
            fixture.AssertCounts(2);
        }

        /// <summary>
        /// Verifies a queued deletion follows its captured row identity after intervening collection edits.
        /// </summary>
        [STATestMethod]
        public void DeferredDelete_AfterEarlierRowRemoval_DoesNotDeleteTheRemainingRow()
        {
            using var fixture = new GridFixture();
            fixture.Grid.AddRows(1);
            fixture.Render();
            object retainedRow = fixture.Grid.Items[2];
            fixture.SelectRows(1);
            fixture.Grid.DeleteRows();
            fixture.Element.Distributions.RemoveAt(0);
            fixture.Render();

            fixture.AssertCounts(1);
            Assert.AreSame(retainedRow, fixture.Grid.Items[0]);
        }

        /// <summary>
        /// Verifies replacing or resetting the public enum collection updates the persistent grid source.
        /// </summary>
        [STATestMethod]
        public void CollectionReplacementAndReset_RefreshRowsAndDetachPreviousCollection()
        {
            using var fixture = new GridFixture();
            var previous = fixture.Element.Distributions;
            var replacement = new ObservableCollection<UnivariateDistributionType>
            {
                UnivariateDistributionType.Gumbel, UnivariateDistributionType.Normal
            };
            fixture.Element.Distributions = replacement;
            fixture.Render();
            object source = fixture.Grid.ItemsSource;
            previous.Add(UnivariateDistributionType.Normal);
            fixture.Render();
            fixture.AssertCounts(2);

            replacement.Clear();
            replacement.Add(UnivariateDistributionType.Normal);
            fixture.Render();
            fixture.AssertCounts(1);
            Assert.AreSame(source, fixture.Grid.ItemsSource);
            Assert.IsTrue(BindingOperations.IsDataBound(fixture.Grid, ItemsControl.ItemsSourceProperty));
        }

        /// <summary>
        /// Finds the first realized descendant of the requested WPF type.
        /// </summary>
        /// <typeparam name="T">The descendant type.</typeparam>
        /// <param name="root">The visual subtree root.</param>
        /// <returns>The matching descendant, or null if none is realized.</returns>
        private static T FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) return null;
            if (root is T match) return match;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var found = FindDescendant<T>(VisualTreeHelper.GetChild(root, i));
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Owns an unestimated mixture and its actual, realized properties grid for one test.
        /// </summary>
        private sealed class GridFixture : IDisposable
        {
            /// <summary>
            /// Creates the production control without adding an element to persistent project storage.
            /// </summary>
            internal GridFixture()
            {
                WpfTestHost.EnsureResources();
                Collection = new UnivariateAnalysisCollection(BestFitProject.GetInstance());
                Element = new MixtureAnalysis("GridProbe", Collection);
                Control = new MixtureAnalysisPropertiesControl { Element = Element };
                Grid = (CopyPasteDataGrid)Control.FindName("DistributionDataGrid");
                Render();
                Element.UndoManager.Clear();
            }

            /// <summary>Gets the transient parent collection.</summary>
            internal UnivariateAnalysisCollection Collection { get; }

            /// <summary>Gets the unestimated analysis.</summary>
            internal MixtureAnalysis Element { get; }

            /// <summary>Gets the production properties control.</summary>
            internal MixtureAnalysisPropertiesControl Control { get; }

            /// <summary>Gets the actual toolbar-compatible grid.</summary>
            internal CopyPasteDataGrid Grid { get; }

            /// <summary>
            /// Realizes cells and executes deferred row edits and layout callbacks.
            /// </summary>
            internal void Render()
            {
                Control.Measure(new Size(850, 1100));
                Control.Arrange(new Rect(0, 0, 850, 1100));
                Control.UpdateLayout();
                WpfTestHost.DrainDispatcher();
                Control.UpdateLayout();
            }

            /// <summary>
            /// Selects actual realized cells so framework row lookup participates in the test.
            /// </summary>
            /// <param name="indices">The physical row indices to select.</param>
            internal void SelectRows(params int[] indices)
            {
                Grid.SelectedCells.Clear();
                foreach (int index in indices)
                    Grid.SelectedCells.Add(new DataGridCellInfo(Grid.GetCell(index, 0)));
            }

            /// <summary>
            /// Checks that the grid, public collection, and model agree about the component count.
            /// </summary>
            /// <param name="expected">The expected number of components.</param>
            internal void AssertCounts(int expected)
            {
                Assert.AreEqual(expected, Grid.Items.Count);
                Assert.AreEqual(expected, Element.Distributions.Count);
                Assert.AreEqual(expected, Element.MixtureDistribution.Mixture.Distributions.Length);
            }

            /// <summary>
            /// Detaches element-scoped subscriptions and drains binding work before the test exits.
            /// </summary>
            public void Dispose()
            {
                Control.Element = null;
                WpfTestHost.DrainDispatcher();
            }
        }
    }
}
