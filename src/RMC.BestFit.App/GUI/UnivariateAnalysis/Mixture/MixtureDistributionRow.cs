using Numerics.Distributions;
using System.ComponentModel;

namespace RMC_BestFit
{
    /// <summary>
    /// Gives each mixture grid row a distinct identity while the public model retains enum values.
    /// </summary>
    internal sealed class MixtureDistributionRow : INotifyPropertyChanged
    {
        /// <summary>Initializes a row for one component distribution.</summary>
        /// <param name="distributionType">The component's distribution type.</param>
        internal MixtureDistributionRow(UnivariateDistributionType distributionType)
        {
            DistributionType = distributionType;
        }

        /// <summary>Gets the component type displayed by the row's binding.</summary>
        public UnivariateDistributionType DistributionType { get; private set; }

        /// <summary>Occurs when the component type changes without replacing the row.</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Updates the displayed type from the public enum collection.</summary>
        /// <param name="distributionType">The current component type.</param>
        internal void UpdateType(UnivariateDistributionType distributionType)
        {
            if (DistributionType == distributionType) return;
            DistributionType = distributionType;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DistributionType)));
        }
    }
}
