using Microsoft.VisualStudio.TestTools.UnitTesting;
using RMC.BestFit.Models.TrendFunctions.Support;

namespace RMC.BestFit.App.Tests.GUI.Support.DataGrids
{
    /// <summary>
    /// Unit tests for <see cref="RMC_BestFit.TrendModelRowItem"/>.
    /// </summary>
    [TestClass]
    public class TrendModelRowItemTests
    {
        /// <summary>
        /// Verifies the constructor stores the display name through the <c>Name</c> getter.
        /// </summary>
        [TestMethod]
        public void Constructor_StoresName()
        {
            var item = new RMC_BestFit.TrendModelRowItem("Linear", TrendModelType.Linear);
            Assert.AreEqual("Linear", item.Name);
        }

        /// <summary>
        /// Verifies the constructor stores the trend model type through the <c>Value</c> getter.
        /// </summary>
        [TestMethod]
        public void Constructor_StoresValue()
        {
            var item = new RMC_BestFit.TrendModelRowItem("Quadratic", TrendModelType.Quadratic);
            Assert.AreEqual(TrendModelType.Quadratic, item.Value);
        }

        /// <summary>
        /// Verifies setting the <c>Name</c> property fires <c>PropertyChanged</c> with the
        /// correct property name.
        /// </summary>
        [TestMethod]
        public void Name_SetValue_FiresPropertyChanged()
        {
            var item = new RMC_BestFit.TrendModelRowItem("A", TrendModelType.Constant);
            int notifications = 0;
            item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(item.Name)) notifications++; };
            item.Name = "B";
            Assert.AreEqual(1, notifications);
        }

        /// <summary>
        /// Verifies setting the <c>Value</c> property fires <c>PropertyChanged</c> with the
        /// correct property name.
        /// </summary>
        [TestMethod]
        public void Value_SetValue_FiresPropertyChanged()
        {
            var item = new RMC_BestFit.TrendModelRowItem("A", TrendModelType.Constant);
            int notifications = 0;
            item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(item.Value)) notifications++; };
            item.Value = TrendModelType.Linear;
            Assert.AreEqual(1, notifications);
        }

        /// <summary>
        /// Verifies the <c>Name</c> setter updates the stored value.
        /// </summary>
        [TestMethod]
        public void Name_SetValue_UpdatesValue()
        {
            var item = new RMC_BestFit.TrendModelRowItem("A", TrendModelType.Constant);
            item.Name = "Updated";
            Assert.AreEqual("Updated", item.Name);
        }

        /// <summary>
        /// Verifies the <c>Value</c> setter updates the stored value.
        /// </summary>
        [TestMethod]
        public void Value_SetValue_UpdatesValue()
        {
            var item = new RMC_BestFit.TrendModelRowItem("A", TrendModelType.Constant);
            item.Value = TrendModelType.Quadratic;
            Assert.AreEqual(TrendModelType.Quadratic, item.Value);
        }
    }
}
