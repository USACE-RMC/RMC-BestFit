using Microsoft.VisualStudio.TestTools.UnitTesting;
using Numerics.Data;
using RMC.BestFit.UI;
using System;
using System.ComponentModel;

namespace RMC.BestFit.App.Tests.GUI.Support.ComboBoxItems
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.TimeSeriesAlternativeItem"/>.
    /// </summary>
    [TestClass]
    public class TimeSeriesAlternativeItemTests
    {
        /// <summary>
        /// Helper that constructs a minimal <see cref="TimeSeriesElement"/> with a stable name.
        /// </summary>
        /// <param name="name">The element name.</param>
        /// <returns>A new <see cref="TimeSeriesElement"/> instance.</returns>
        private static TimeSeriesElement MakeTimeSeriesElement(string name = "TS-Test")
        {
            return new TimeSeriesElement(name);
        }

        /// <summary>
        /// Verifies the constructor stores the supplied <see cref="TimeSeriesElement"/> as the
        /// <c>Alternative</c> property.
        /// </summary>
        [STATestMethod]
        public void Constructor_StoresAlternative()
        {
            var ts = MakeTimeSeriesElement();
            var item = new RMC_BestFit.TimeSeriesAlternativeItem(ts);
            Assert.AreSame(ts, item.Alternative);
        }

        /// <summary>
        /// Verifies the constructor creates the <c>TimeSeriesLine</c> with the expected name.
        /// </summary>
        [STATestMethod]
        public void Constructor_CreatesTimeSeriesLine_WithName()
        {
            var item = new RMC_BestFit.TimeSeriesAlternativeItem(MakeTimeSeriesElement());
            Assert.IsNotNull(item.TimeSeriesLine);
            Assert.AreEqual("TimeSeriesLine", item.TimeSeriesLine.Name);
        }

        /// <summary>
        /// Verifies the constructor sets the line's <c>Title</c> from the alternative's name.
        /// </summary>
        [STATestMethod]
        public void Constructor_TimeSeriesLineTitle_MatchesAlternativeName()
        {
            var ts = MakeTimeSeriesElement("MySeries");
            var item = new RMC_BestFit.TimeSeriesAlternativeItem(ts);
            Assert.AreEqual("MySeries", item.TimeSeriesLine.Title);
        }

        /// <summary>
        /// Verifies the initial <c>IsChecked</c> state is <c>false</c>.
        /// </summary>
        [STATestMethod]
        public void IsChecked_InitialValue_IsFalse()
        {
            var item = new RMC_BestFit.TimeSeriesAlternativeItem(MakeTimeSeriesElement());
            Assert.IsFalse(item.IsChecked);
        }

        /// <summary>
        /// Verifies setting <c>IsChecked</c> to a new value updates the property.
        /// </summary>
        [STATestMethod]
        public void IsChecked_SetToTrue_UpdatesValue()
        {
            var item = new RMC_BestFit.TimeSeriesAlternativeItem(MakeTimeSeriesElement());
            item.IsChecked = true;
            Assert.IsTrue(item.IsChecked);
        }

        /// <summary>
        /// Verifies setting <c>IsChecked</c> fires <c>PropertyChanged</c> with the correct property name.
        /// </summary>
        [STATestMethod]
        public void IsChecked_SetNewValue_FiresPropertyChanged()
        {
            var item = new RMC_BestFit.TimeSeriesAlternativeItem(MakeTimeSeriesElement());
            int notifications = 0;
            item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(item.IsChecked)) notifications++; };
            item.IsChecked = true;
            Assert.AreEqual(1, notifications);
        }

        /// <summary>
        /// Verifies setting <c>IsChecked</c> to its current value does NOT fire <c>PropertyChanged</c>.
        /// </summary>
        [STATestMethod]
        public void IsChecked_SetSameValue_DoesNotFirePropertyChanged()
        {
            var item = new RMC_BestFit.TimeSeriesAlternativeItem(MakeTimeSeriesElement());
            int notifications = 0;
            item.PropertyChanged += (_, _) => notifications++;
            item.IsChecked = false;
            Assert.AreEqual(0, notifications);
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.TimeSeriesAlternativeItem.HasData"/> returns <c>true</c>
        /// when the wrapped time series contains at least one ordinate. (The
        /// <see cref="TimeSeriesElement"/> constructor seeds at least one ordinate, so this
        /// branch is the practical case.)
        /// </summary>
        [STATestMethod]
        public void HasData_WithOrdinates_ReturnsTrue()
        {
            var ts = MakeTimeSeriesElement();
            ts.TimeSeries.Add(new SeriesOrdinate<DateTime, double>(new DateTime(2020, 1, 1), 1.0));
            var item = new RMC_BestFit.TimeSeriesAlternativeItem(ts);
            Assert.IsTrue(item.HasData);
        }

        /// <summary>
        /// Verifies <see cref="RMC_BestFit.TimeSeriesAlternativeItem.Detach"/> removes the internal
        /// handler so subsequent property changes on the alternative don't propagate.
        /// </summary>
        [STATestMethod]
        public void Detach_AfterCall_PreventsFurtherPropagation()
        {
            var ts = MakeTimeSeriesElement();
            var item = new RMC_BestFit.TimeSeriesAlternativeItem(ts);
            int count = 0;
            item.PropertyChanged += (_, _) => count++;
            item.Detach();

            // Trigger Name change after detach — should NOT be propagated by the item.
            ts.Name = "Renamed";
            Assert.AreEqual(0, count);
        }
    }
}
