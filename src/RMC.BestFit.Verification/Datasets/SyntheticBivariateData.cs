using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using RMC.BestFit.Models;
using System.Diagnostics;

namespace RMC.BestFit.Verification.Datasets
{
    /// <summary>
    /// Synthetic paired bivariate datasets for copula parameter-recovery verification.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    ///     <b>Purpose:</b>
    ///     Each method simulates <c>n</c> paired (X, Y) observations from a known parent
    ///     copula with known marginal distributions, and returns both the raw DataFrames
    ///     (ready to drop into a <see cref="BivariateDistribution"/>) and the true parent
    ///     parameters. Tests use the truth to assert MCMC / MLE recovery within tolerance.
    /// </para>
    /// <para>
    ///     <b>Parent setup (mirrors <c>examples/5-bivariate-distribution-analysis/1-bivariate-distributions/bivariate-distribution-examples.bestfit</c>):</b>
    ///     X ~ Normal(μ=<see cref="MuX"/>=100, σ=<see cref="SigmaX"/>=15),
    ///     Y ~ Normal(μ=<see cref="MuY"/>=80, σ=<see cref="SigmaY"/>=25),
    ///     sample size n=<see cref="DefaultSampleSize"/>=100.
    /// </para>
    /// <para>
    ///     <b>Parent copula parameters (defaults):</b>
    ///     Normal ρ=0.8, Joe θ=3.0, Gumbel θ=2.0, Frank θ=8.0, Clayton θ=1.5, AMH θ=0.8,
    ///     Student's t ρ=0.8, ν=4 (strong symmetric tail dependence; λ_U = λ_L ≈ 0.49).
    /// </para>
    /// <para>
    ///     Seeds are offset per copula to keep datasets independent while staying reproducible.
    /// </para>
    /// </remarks>
    public static class SyntheticBivariateData
    {
        /// <summary>Mean of the marginal X (Normal).</summary>
        public const double MuX = 100.0;

        /// <summary>Standard deviation of the marginal X (Normal).</summary>
        public const double SigmaX = 15.0;

        /// <summary>Mean of the marginal Y (Normal).</summary>
        public const double MuY = 80.0;

        /// <summary>Standard deviation of the marginal Y (Normal).</summary>
        public const double SigmaY = 25.0;

        /// <summary>Default paired sample size; matches the example project.</summary>
        public const int DefaultSampleSize = 100;

        /// <summary>Shared base seed; per-copula methods add offsets for independence.</summary>
        public const int BaseSeed = 12345;

        /// <summary>
        /// A paired bivariate sample ready for parameter-recovery testing.
        /// </summary>
        /// <param name="DataFrameX">Marginal X observations in an <see cref="ExactSeries"/> with matched indices.</param>
        /// <param name="DataFrameY">Marginal Y observations in an <see cref="ExactSeries"/> with matched indices.</param>
        /// <param name="TrueCopulaParameters">The parent copula parameters: one entry (Theta) for 1-parameter copulas, two entries (rho, df) for Student's t.</param>
        /// <param name="TrueMarginalXParameters">The parent X marginal parameters (μ, σ) for Normal marginals.</param>
        /// <param name="TrueMarginalYParameters">The parent Y marginal parameters (μ, σ) for Normal marginals.</param>
        /// <param name="CopulaType">The parent copula type.</param>
        public readonly record struct BivariateSample(
            DataFrame DataFrameX,
            DataFrame DataFrameY,
            double[] TrueCopulaParameters,
            double[] TrueMarginalXParameters,
            double[] TrueMarginalYParameters,
            CopulaType CopulaType);

        #region Generators

        /// <summary>
        /// Generates a paired sample from a Normal (Gaussian) copula with the shared Normal marginals.
        /// </summary>
        /// <param name="rho">True correlation parameter. Default = 0.8.</param>
        /// <param name="n">Sample size. Default = <see cref="DefaultSampleSize"/>.</param>
        /// <param name="seed">PRNG seed. Default = <see cref="BaseSeed"/> + 700.</param>
        /// <returns>A <see cref="BivariateSample"/> with the paired data and parent truth.</returns>
        public static BivariateSample GenerateNormalCopulaData(
            double rho = 0.8, int n = DefaultSampleSize, int seed = BaseSeed + 700)
        {
            return Build(new NormalCopula(rho), new[] { rho }, CopulaType.Normal, n, seed);
        }

        /// <summary>
        /// Generates a paired sample from a Joe copula with the shared Normal marginals.
        /// </summary>
        /// <param name="theta">True dependency parameter (θ ≥ 1). Default = 3.0.</param>
        /// <param name="n">Sample size. Default = <see cref="DefaultSampleSize"/>.</param>
        /// <param name="seed">PRNG seed. Default = <see cref="BaseSeed"/> + 701.</param>
        /// <returns>A <see cref="BivariateSample"/> with the paired data and parent truth.</returns>
        public static BivariateSample GenerateJoeCopulaData(
            double theta = 3.0, int n = DefaultSampleSize, int seed = BaseSeed + 701)
        {
            return Build(new JoeCopula(theta), new[] { theta }, CopulaType.Joe, n, seed);
        }

        /// <summary>
        /// Generates a paired sample from a Gumbel copula with the shared Normal marginals.
        /// </summary>
        /// <param name="theta">True dependency parameter (θ ≥ 1). Default = 2.0.</param>
        /// <param name="n">Sample size. Default = <see cref="DefaultSampleSize"/>.</param>
        /// <param name="seed">PRNG seed. Default = <see cref="BaseSeed"/> + 702.</param>
        /// <returns>A <see cref="BivariateSample"/> with the paired data and parent truth.</returns>
        public static BivariateSample GenerateGumbelCopulaData(
            double theta = 2.0, int n = DefaultSampleSize, int seed = BaseSeed + 702)
        {
            return Build(new GumbelCopula(theta), new[] { theta }, CopulaType.Gumbel, n, seed);
        }

        /// <summary>
        /// Generates a paired sample from a Frank copula with the shared Normal marginals.
        /// </summary>
        /// <param name="theta">True dependency parameter (θ ≠ 0). Default = 8.0.</param>
        /// <param name="n">Sample size. Default = <see cref="DefaultSampleSize"/>.</param>
        /// <param name="seed">PRNG seed. Default = <see cref="BaseSeed"/> + 703.</param>
        /// <returns>A <see cref="BivariateSample"/> with the paired data and parent truth.</returns>
        public static BivariateSample GenerateFrankCopulaData(
            double theta = 8.0, int n = DefaultSampleSize, int seed = BaseSeed + 703)
        {
            return Build(new FrankCopula(theta), new[] { theta }, CopulaType.Frank, n, seed);
        }

        /// <summary>
        /// Generates a paired sample from a Clayton copula with the shared Normal marginals.
        /// </summary>
        /// <param name="theta">True dependency parameter (θ > 0 for positive dependence). Default = 1.5.</param>
        /// <param name="n">Sample size. Default = <see cref="DefaultSampleSize"/>.</param>
        /// <param name="seed">PRNG seed. Default = <see cref="BaseSeed"/> + 704.</param>
        /// <returns>A <see cref="BivariateSample"/> with the paired data and parent truth.</returns>
        public static BivariateSample GenerateClaytonCopulaData(
            double theta = 1.5, int n = DefaultSampleSize, int seed = BaseSeed + 704)
        {
            return Build(new ClaytonCopula(theta), new[] { theta }, CopulaType.Clayton, n, seed);
        }

        /// <summary>
        /// Generates a paired sample from an Ali-Mikhail-Haq copula with the shared Normal marginals.
        /// </summary>
        /// <param name="theta">True dependency parameter (θ ∈ [-1, 1]). Default = 0.8.</param>
        /// <param name="n">Sample size. Default = <see cref="DefaultSampleSize"/>.</param>
        /// <param name="seed">PRNG seed. Default = <see cref="BaseSeed"/> + 705.</param>
        /// <returns>A <see cref="BivariateSample"/> with the paired data and parent truth.</returns>
        public static BivariateSample GenerateAMHCopulaData(
            double theta = 0.8, int n = DefaultSampleSize, int seed = BaseSeed + 705)
        {
            return Build(new AMHCopula(theta), new[] { theta }, CopulaType.AliMikhailHaq, n, seed);
        }

        /// <summary>
        /// Generates a paired sample from a Student's t copula with strong symmetric tail dependence
        /// and the shared Normal marginals.
        /// </summary>
        /// <param name="rho">True correlation parameter (ρ ∈ [-1, 1]). Default = 0.8.</param>
        /// <param name="degreesOfFreedom">True degrees-of-freedom parameter (ν > 2).
        /// Default = 4 — low enough to give visible tail dependence (λ_U = λ_L ≈ 0.49)
        /// without becoming degenerate.</param>
        /// <param name="n">Sample size. Default = <see cref="DefaultSampleSize"/>.</param>
        /// <param name="seed">PRNG seed. Default = <see cref="BaseSeed"/> + 706.</param>
        /// <returns>A <see cref="BivariateSample"/> with the paired data and parent truth (rho, df).</returns>
        public static BivariateSample GenerateStudentTCopulaData(
            double rho = 0.8, int degreesOfFreedom = 4,
            int n = DefaultSampleSize, int seed = BaseSeed + 706)
        {
            return Build(
                new StudentTCopula(rho, degreesOfFreedom),
                new[] { rho, (double)degreesOfFreedom },
                CopulaType.StudentT, n, seed);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Shared body: attaches Normal(μ, σ) marginals to the copula, simulates <c>n</c>
        /// paired draws with the given seed, packages the X and Y columns into DataFrames
        /// with matched indices, and returns a <see cref="BivariateSample"/>.
        /// </summary>
        private static BivariateSample Build(
            BivariateCopula copula,
            double[] trueCopulaParameters,
            CopulaType copulaType,
            int n,
            int seed)
        {
            var distX = new Normal(MuX, SigmaX);
            var distY = new Normal(MuY, SigmaY);

            copula.MarginalDistributionX = distX;
            copula.MarginalDistributionY = distY;

            double[,] pairs = copula.GenerateRandomValues(n, seed);

            var xValues = new double[n];
            var yValues = new double[n];
            for (int i = 0; i < n; i++)
            {
                xValues[i] = pairs[i, 0];
                yValues[i] = pairs[i, 1];
                Debug.WriteLine(xValues[i].ToString() + "," + yValues[i].ToString());
            }

            var dfX = new DataFrame();
            dfX.ExactSeries = new ExactSeries(xValues);
            dfX.CalculatePlottingPositions();

            var dfY = new DataFrame();
            dfY.ExactSeries = new ExactSeries(yValues);
            dfY.CalculatePlottingPositions();

            // Match indices across the two series so BivariateDistribution.SetSampleData
            // can pair them position-wise (the same convention the example project uses).
            for (int i = 0; i < n; i++)
            {
                ((ExactData)dfX.ExactSeries[i]).Index = i;
                ((ExactData)dfY.ExactSeries[i]).Index = i;
            }

            return new BivariateSample(
                DataFrameX: dfX,
                DataFrameY: dfY,
                TrueCopulaParameters: trueCopulaParameters,
                TrueMarginalXParameters: new[] { MuX, SigmaX },
                TrueMarginalYParameters: new[] { MuY, SigmaY },
                CopulaType: copulaType);
        }

        #endregion
    }
}
