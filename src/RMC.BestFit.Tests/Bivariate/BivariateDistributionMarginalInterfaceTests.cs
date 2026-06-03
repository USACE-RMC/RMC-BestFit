using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Bivariate;

/// <summary>
/// Guards the <see cref="IUnivariateModel"/> marginal contract: every model type that
/// is supposed to serve as a bivariate marginal (Univariate, Bulletin17C, PointProcess,
/// Mixture) implements the interface, and the <see cref="BivariateDistribution"/>
/// constructor accepts each of them via the interface.
/// </summary>
[TestClass]
public class BivariateDistributionMarginalInterfaceTests
{
    #region Helpers

    /// <summary>
    /// Creates a pre-fit <see cref="UnivariateDistribution"/> with Normal parameters seeded
    /// to <c>(μ, σ)</c> so <c>Validate()</c> passes without an MLE call.
    /// </summary>
    private static UnivariateDistribution BuildNormalMarginal(double mu, double sigma, double[] data)
    {
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(data);
        df.CalculatePlottingPositions();
        var dist = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        dist.SetParameterValues(new[] { mu, sigma });
        for (int i = 0; i < data.Length; i++)
            ((ExactData)df.ExactSeries[i]).Index = i;
        return dist;
    }

    private static readonly double[] SampleX =
        { 98.1, 102.7, 115.3, 88.4, 104.9, 92.0, 110.5, 99.2, 107.6, 101.3,
          95.8, 108.1, 103.4, 97.6, 112.9, 89.5, 106.2, 100.0, 93.7, 109.4 };

    private static readonly double[] SampleY =
        { 75.2, 82.1, 93.6, 68.7, 84.3, 72.5, 90.4, 78.9, 88.5, 81.0,
          74.1, 87.6, 82.8, 76.3, 91.5, 69.2, 86.0, 80.4, 73.5, 89.1 };

    #endregion

    #region Interface Implementations

    /// <summary>Each target model class implements <see cref="IUnivariateModel"/>.</summary>
    [TestMethod]
    public void TargetModelClasses_ImplementIUnivariateModel()
    {
        Assert.IsTrue(typeof(IUnivariateModel).IsAssignableFrom(typeof(UnivariateDistribution)));
        Assert.IsTrue(typeof(IUnivariateModel).IsAssignableFrom(typeof(Bulletin17CDistribution)));
        Assert.IsTrue(typeof(IUnivariateModel).IsAssignableFrom(typeof(PointProcessModel)));
        Assert.IsTrue(typeof(IUnivariateModel).IsAssignableFrom(typeof(MixtureModel)));
    }

    /// <summary>
    /// <see cref="MixtureModel"/> exposes its fitted distribution as <c>Mixture</c> (type-specific)
    /// but the interface accessor must route back to the same underlying object.
    /// </summary>
    [TestMethod]
    public void MixtureModel_ExplicitInterfaceDistribution_ReturnsMixtureProperty()
    {
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(SampleX);

        var components = new List<UnivariateDistributionBase> { new Normal(95, 10), new Normal(110, 10) };
        var model = new MixtureModel(df, components);

        var mix = new Mixture(new[] { 0.5, 0.5 }, components.ToArray());
        model.Mixture = mix;

        Assert.AreSame(mix, ((IUnivariateModel)model).Distribution);
    }

    /// <summary>
    /// <see cref="PointProcessModel"/> exposes its fitted distribution as <c>Distribution</c>
    /// typed <c>CompetingRisks?</c>. The interface accessor must return the same object typed
    /// as <see cref="UnivariateDistributionBase"/> (which <c>CompetingRisks</c> derives from).
    /// </summary>
    [TestMethod]
    public void PointProcessModel_ExplicitInterfaceDistribution_ReturnsSameObject()
    {
        var model = new PointProcessModel();
        var cr = new CompetingRisks(new UnivariateDistributionBase[] { new Normal(95, 10), new Normal(110, 10) });
        model.Distribution = cr;

        Assert.AreSame(cr, ((IUnivariateModel)model).Distribution);
    }

    /// <summary>
    /// Models without a trend concept report <c>IsNonstationary == false</c> — prevents
    /// the bivariate validator from falsely rejecting them as nonstationary.
    /// </summary>
    [TestMethod]
    public void NonTrendModels_ReportStationary()
    {
        var df = new DataFrame();
        df.ExactSeries = new ExactSeries(SampleX);

        var b17c = new Bulletin17CDistribution();
        var pot  = new PointProcessModel();
        var mix  = new MixtureModel();

        Assert.IsFalse(((IUnivariateModel)b17c).IsNonstationary);
        Assert.IsFalse(((IUnivariateModel)pot).IsNonstationary);
        Assert.IsFalse(((IUnivariateModel)mix).IsNonstationary);
    }

    #endregion

    #region BivariateDistribution Accepts Any IUnivariateModel

    /// <summary>
    /// The <see cref="BivariateDistribution"/> constructor accepts any
    /// <see cref="IUnivariateModel"/> implementation, not just <see cref="UnivariateDistribution"/>.
    /// </summary>
    [TestMethod]
    public void Construct_WithMixedMarginalTypes_DoesNotThrow()
    {
        IUnivariateModel normalX = BuildNormalMarginal(101.0, 7.5, SampleX);

        // Mixture-backed marginal with a fitted Mixture distribution.
        var dfY = new DataFrame();
        dfY.ExactSeries = new ExactSeries(SampleY);
        dfY.CalculatePlottingPositions();
        var components = new UnivariateDistributionBase[] { new Normal(77, 5), new Normal(88, 5) };
        IUnivariateModel mixY = new MixtureModel(dfY, new List<UnivariateDistributionBase>(components))
        {
            Mixture = new Mixture(new[] { 0.5, 0.5 }, components)
        };
        for (int i = 0; i < SampleY.Length; i++)
            ((ExactData)dfY.ExactSeries[i]).Index = i;

        // Construction with the heterogeneous marginal pair must succeed.
        var bivariate = new BivariateDistribution(normalX, mixY, CopulaType.Normal);

        Assert.IsNotNull(bivariate);
        Assert.AreSame(normalX, bivariate.MarginalX);
        Assert.AreSame(mixY,    bivariate.MarginalY);
    }

    /// <summary>
    /// Swapping the X marginal from a <see cref="UnivariateDistribution"/> to a
    /// <see cref="MixtureModel"/> via the property setter rewires the reactive
    /// PropertyChanged subscription through <see cref="INotifyPropertyChanged"/>
    /// without throwing.
    /// </summary>
    [TestMethod]
    public void SwapMarginal_AcrossInterfaceImplementations_Succeeds()
    {
        IUnivariateModel origX = BuildNormalMarginal(101.0, 7.5, SampleX);
        IUnivariateModel origY = BuildNormalMarginal(80.8, 7.5, SampleY);

        var bivariate = new BivariateDistribution(origX, origY, CopulaType.Normal);

        // Replace X with a Mixture-based marginal
        var dfX2 = new DataFrame();
        dfX2.ExactSeries = new ExactSeries(SampleX);
        dfX2.CalculatePlottingPositions();
        var components = new UnivariateDistributionBase[] { new Normal(95, 10), new Normal(110, 10) };
        IUnivariateModel newX = new MixtureModel(dfX2, new List<UnivariateDistributionBase>(components))
        {
            Mixture = new Mixture(new[] { 0.5, 0.5 }, components)
        };

        bivariate.MarginalX = newX;

        Assert.AreSame(newX, bivariate.MarginalX);
    }

    #endregion
}
