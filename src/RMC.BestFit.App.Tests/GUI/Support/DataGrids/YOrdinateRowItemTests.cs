using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.ObjectModel;

namespace RMC.BestFit.App.Tests.GUI.Support.DataGrids
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.YOrdinateRowItem"/>.
    /// </summary>
    [TestClass]
    public class YOrdinateRowItemTests
    {
        /// <summary>
        /// Verifies the parameterless constructor does not throw and the row is usable.
        /// </summary>
        [TestMethod]
        public void Constructor_Parameterless_DoesNotThrow()
        {
            var row = new RMC_BestFit.YOrdinateRowItem();
            Assert.IsNotNull(row);
        }

        /// <summary>
        /// Verifies the parameterized constructor stores the value through the inherited <c>Value</c> getter.
        /// </summary>
        [TestMethod]
        public void Constructor_WithValue_ExposesValue()
        {
            var row = new RMC_BestFit.YOrdinateRowItem(new ObservableCollection<object>(), 2.5);
            Assert.AreEqual(2.5, row.Value, 1e-12);
        }

        /// <summary>
        /// Verifies the Y-axis row reports its display label as "Y Value" for the <c>Value</c>
        /// column.
        /// </summary>
        [TestMethod]
        public void PropertyDisplayName_Value_ReturnsYValue()
        {
            var row = new RMC_BestFit.YOrdinateRowItem();
            Assert.AreEqual("Y Value", row.PropertyDisplayName(nameof(row.Value)));
        }

        /// <summary>
        /// Verifies the display-name accessor returns <c>null</c> for any unknown property.
        /// </summary>
        [TestMethod]
        public void PropertyDisplayName_UnknownProperty_ReturnsNull()
        {
            var row = new RMC_BestFit.YOrdinateRowItem();
            Assert.IsNull(row.PropertyDisplayName("Anything"));
        }
    }
}
