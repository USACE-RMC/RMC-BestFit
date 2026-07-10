using Numerics.Distributions.Copulas;
using RMC.BestFit.Models;
using RMC.BestFit.Estimation;

namespace RMC.BestFit.Verification.Bivariate;

/// <summary>
/// Unit tests for the <see cref="BivariateDistribution"/> model class and <see cref="MaximumLikelihood"/> estimation.
/// Validates copula-based bivariate distributions against the R 'copula' package.
/// </summary>
/// <remarks>
/// <para>
/// Copulas provide a flexible framework for modeling the dependence structure between random variables
/// independently from their marginal distributions. This allows analysts to combine any marginal distributions
/// with various dependence structures, offering greater flexibility than traditional multivariate distributions.
/// </para>
/// <para>
/// Tests validate two estimation approaches:
/// - Maximum Pseudo Likelihood (MPL): Uses empirical CDFs (plotting positions) of marginals
/// - Inference From Margins (IFM): Two-stage estimation fitting marginals first, then copula parameters
/// </para>
/// <para>
///     <b> Authors: </b>
///     <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
///     </list>
/// </para>
/// </remarks>
[TestClass]
public class BivariateDistributionMLETests
{

    /// <summary>
    /// Tests Ali-Mikhail-Haq copula fitting using Maximum Pseudo Likelihood (MPL) method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates <see cref="BivariateDistribution"/> with <see cref="CopulaEstimationMethod.PseudoLikelihood"/>
    /// against R 'copula' package results. The pseudo-likelihood method estimates copula parameters using
    /// empirical CDFs (plotting positions) rather than parametric marginal distributions, making it
    /// semi-parametric and robust to marginal distribution misspecification.
    /// </para>
    /// <para>
    /// The Ali-Mikhail-Haq (AMH) copula is an asymmetric Archimedean copula with parameter range [-1, 1).
    /// It exhibits relatively weak dependence and is limited in the strength of tail dependence it can model.
    /// The copula approaches independence as θ → 0.
    /// </para>
    /// <para>
    /// MPL advantages: Distribution-free treatment of marginals, computational efficiency, robustness.
    /// MPL limitations: Less efficient than full MLE when marginals are correctly specified.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_AMH_MPL()
    {
        // Set up data
        var dataX = new double[] { 82.6207861517446, 122.99162719591, 85.5464857510852, 88.271076232573, 75.5957634925482, 115.666296571075, 88.1535968625871, 89.0486583921505, 95.6520223040253, 93.8704340350363, 94.8983409930905, 98.190034452137, 119.062097667798, 101.396326234446, 102.044102658515, 122.09278272123, 87.5930294017727, 89.467574539934, 68.759249687252, 97.3170891237622, 115.23726463545, 88.7249972292411, 104.808913056224, 81.4376985296622, 88.5496448465636, 85.9566777828142, 124.159708173032, 82.3989677011281, 81.0754908215474, 96.9227210098483, 81.9598285215311, 106.023194937944, 98.9429238885091, 99.6105440441767, 76.8967482790596, 93.8733402407522, 97.1593258552729, 83.8476766091024, 83.3372066079669, 84.2378120351813, 130.294157330837, 92.0183206123361, 91.0344208437507, 62.6399930777225, 100.724150023017, 105.381851336153, 106.052861517738, 105.695937045651, 98.7180560328115, 103.659278801543, 135.355695309546, 112.126374548073, 75.8894228126445, 113.303033282488, 108.572361018471, 84.4697433912218, 102.158309989318, 104.169634603778, 126.258771553477, 92.0530458784438, 121.851579456857, 112.009972090868, 82.566074288035, 100.197743002857, 109.653015549203, 103.193458190759, 120.818259910061, 109.584519216444, 102.469182012814, 107.385973965207, 97.2671206491186, 112.417149113877, 97.8701780074462, 86.9632866191297, 129.722705545036, 118.480752744013, 109.568435955988, 122.488504266404, 112.534825234329, 110.178551830369, 85.8393034892458, 128.148787670707, 88.8620889850668, 100.212554271105, 111.449083807286, 97.9922242053636, 96.0208140774823, 118.595431696388, 96.5101956950509, 89.0070522979225, 111.690497497928, 98.2256095188502, 109.751617939794, 113.262405920361, 89.1969936327915, 126.836051984505, 94.7671769565446, 101.621377036869, 85.9837125336272, 80.6830712615443 };
        var dataY = new double[] { 23.3689941895676, 139.075605092248, 125.391972129225, 68.0466559628945, 21.3849501824245, 72.623739980515, 118.431110203811, 55.6433779771001, 61.7394973031962, 117.820425024964, 51.4915091268943, 90.6318678923286, 90.2225515717848, 82.9583115041602, 58.8208994489536, 89.3934020879786, 84.2687389199291, 52.8123343271364, 133.922245003475, 65.7460273014489, 94.0306797439919, 92.7622201239545, 67.4956202291755, 52.1748570092584, 76.289463467535, 30.6442121096422, 80.367744128015, 84.589322268581, 89.4931756387178, 99.8444599387594, 147.360635147879, 26.7990606303491, 75.9359921708942, 132.224006705134, 43.5713891672347, 69.4331898107351, 113.826594948864, 44.856181127981, 59.6818830690231, 34.8777185962398, 124.522970067359, 55.0531003117469, 112.641824278124, 53.3587463398865, 82.3818569319419, 131.306495042214, 92.5500215682676, 96.1266469742884, 126.22260773411, 105.118018987749, 98.5541508572697, 81.6528854407349, 77.1433525995117, 76.8338526759968, 89.723133164602, 55.8177007161922, 94.0035174340863, 164.923630526842, 133.423371650793, 89.0043059347403, 89.7587258229626, 88.4640664624267, 104.247452450998, 67.9322685363551, 83.4200659472935, 73.8431913350094, 17.7019731704449, 114.710483451006, 101.065426716036, 55.4556570293316, 67.8514034006699, 127.73989553804, 125.34525003517, 57.0971025319617, 85.1768621826704, 101.705832867004, 85.3021580666208, 89.4450158150175, 75.5941517929671, 124.424804607655, 83.3449371155293, 96.7642165625312, 57.9643876742987, 126.024606562933, 66.5425894700683, 125.964000028259, 45.1213432721552, 86.2256615507381, 101.231675185045, 27.2201147115773, 95.015298927854, 63.4674026223287, 86.224525433146, 116.818569536018, 63.082998559889, 73.7218123340297, 92.935473959462, 84.2370209154411, 49.9905902403315, 61.0021219814154 };

        // Create data frame
        var dfX = new DataFrame();
        var dfY = new DataFrame();
        // Add exact data
        dfX.ExactSeries = new ExactSeries(dataX);
        dfY.ExactSeries = new ExactSeries(dataY);
        // Calculate plotting positions for pseudo likelihood
        dfX.CalculatePlottingPositions();
        dfY.CalculatePlottingPositions();

        // Fit each marginal univariate distribution
        // Fit marginal-X
        var distX = new UnivariateDistribution(dfX, Numerics.Distributions.UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(distX);
        mle.Estimate();
        distX.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Fit marginal-Y
        var distY = new UnivariateDistribution(dfY, Numerics.Distributions.UnivariateDistributionType.Normal);
        mle = new MaximumLikelihood(distY);
        mle.Estimate();
        distY.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Note: fitting of the marginals is not necessary for the pseudo likelihood method. 
        // But the method uses univariate distribution marginals for input. 
        // The data frame of the marginal univariate plotting positions are used to define the 
        // pseudo likelihood values.

        // Create bivariate distribution
        var bivariateDist = new BivariateDistribution(distX, distY, CopulaType.AliMikhailHaq) { 
            CopulaEstimationMethod = CopulaEstimationMethod.PseudoLikelihood 
        };

        // Estimate using MLE
        mle = new MaximumLikelihood(bivariateDist, OptimizationMethod.Brent);
        mle.Estimate();
        bivariateDist.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Bivariate distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        Assert.AreEqual(0.8321504, bivariateDist.Copula.Theta, 1E-3, "Copula dependency parameter is incorrect.");
    }


    /// <summary>
    /// Tests Ali-Mikhail-Haq copula fitting using Inference From Margins (IFM) method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates <see cref="BivariateDistribution"/> with <see cref="CopulaEstimationMethod.InferenceFromMargins"/>
    /// against R 'copula' package results. The IFM method uses a two-stage approach: first estimating marginal
    /// distribution parameters independently, then estimating copula parameters conditional on the fitted marginals.
    /// This method provides full maximum likelihood estimates when marginal distributions are correctly specified.
    /// </para>
    /// <para>
    /// The Ali-Mikhail-Haq (AMH) copula is an asymmetric Archimedean copula with parameter range [-1, 1).
    /// It exhibits relatively weak dependence and is limited in the strength of tail dependence it can model.
    /// The copula approaches independence as θ → 0.
    /// </para>
    /// <para>
    /// IFM advantages: Statistically efficient under correct specification, theoretically sound, separate
    /// marginal and dependence modeling. IFM limitations: Sensitive to marginal misspecification, computationally
    /// more intensive than MPL, requires parametric marginal distributions.
    /// </para>
    /// <para>
    /// The IFM estimate (θ = 0.8393) is slightly higher than the MPL estimate (θ = 0.8322), reflecting
    /// the efficiency gain from using parametric marginals rather than empirical CDFs.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_AMH_IFM()
    {
        // Set up data
        var dataX = new double[] { 82.6207861517446, 122.99162719591, 85.5464857510852, 88.271076232573, 75.5957634925482, 115.666296571075, 88.1535968625871, 89.0486583921505, 95.6520223040253, 93.8704340350363, 94.8983409930905, 98.190034452137, 119.062097667798, 101.396326234446, 102.044102658515, 122.09278272123, 87.5930294017727, 89.467574539934, 68.759249687252, 97.3170891237622, 115.23726463545, 88.7249972292411, 104.808913056224, 81.4376985296622, 88.5496448465636, 85.9566777828142, 124.159708173032, 82.3989677011281, 81.0754908215474, 96.9227210098483, 81.9598285215311, 106.023194937944, 98.9429238885091, 99.6105440441767, 76.8967482790596, 93.8733402407522, 97.1593258552729, 83.8476766091024, 83.3372066079669, 84.2378120351813, 130.294157330837, 92.0183206123361, 91.0344208437507, 62.6399930777225, 100.724150023017, 105.381851336153, 106.052861517738, 105.695937045651, 98.7180560328115, 103.659278801543, 135.355695309546, 112.126374548073, 75.8894228126445, 113.303033282488, 108.572361018471, 84.4697433912218, 102.158309989318, 104.169634603778, 126.258771553477, 92.0530458784438, 121.851579456857, 112.009972090868, 82.566074288035, 100.197743002857, 109.653015549203, 103.193458190759, 120.818259910061, 109.584519216444, 102.469182012814, 107.385973965207, 97.2671206491186, 112.417149113877, 97.8701780074462, 86.9632866191297, 129.722705545036, 118.480752744013, 109.568435955988, 122.488504266404, 112.534825234329, 110.178551830369, 85.8393034892458, 128.148787670707, 88.8620889850668, 100.212554271105, 111.449083807286, 97.9922242053636, 96.0208140774823, 118.595431696388, 96.5101956950509, 89.0070522979225, 111.690497497928, 98.2256095188502, 109.751617939794, 113.262405920361, 89.1969936327915, 126.836051984505, 94.7671769565446, 101.621377036869, 85.9837125336272, 80.6830712615443 };
        var dataY = new double[] { 23.3689941895676, 139.075605092248, 125.391972129225, 68.0466559628945, 21.3849501824245, 72.623739980515, 118.431110203811, 55.6433779771001, 61.7394973031962, 117.820425024964, 51.4915091268943, 90.6318678923286, 90.2225515717848, 82.9583115041602, 58.8208994489536, 89.3934020879786, 84.2687389199291, 52.8123343271364, 133.922245003475, 65.7460273014489, 94.0306797439919, 92.7622201239545, 67.4956202291755, 52.1748570092584, 76.289463467535, 30.6442121096422, 80.367744128015, 84.589322268581, 89.4931756387178, 99.8444599387594, 147.360635147879, 26.7990606303491, 75.9359921708942, 132.224006705134, 43.5713891672347, 69.4331898107351, 113.826594948864, 44.856181127981, 59.6818830690231, 34.8777185962398, 124.522970067359, 55.0531003117469, 112.641824278124, 53.3587463398865, 82.3818569319419, 131.306495042214, 92.5500215682676, 96.1266469742884, 126.22260773411, 105.118018987749, 98.5541508572697, 81.6528854407349, 77.1433525995117, 76.8338526759968, 89.723133164602, 55.8177007161922, 94.0035174340863, 164.923630526842, 133.423371650793, 89.0043059347403, 89.7587258229626, 88.4640664624267, 104.247452450998, 67.9322685363551, 83.4200659472935, 73.8431913350094, 17.7019731704449, 114.710483451006, 101.065426716036, 55.4556570293316, 67.8514034006699, 127.73989553804, 125.34525003517, 57.0971025319617, 85.1768621826704, 101.705832867004, 85.3021580666208, 89.4450158150175, 75.5941517929671, 124.424804607655, 83.3449371155293, 96.7642165625312, 57.9643876742987, 126.024606562933, 66.5425894700683, 125.964000028259, 45.1213432721552, 86.2256615507381, 101.231675185045, 27.2201147115773, 95.015298927854, 63.4674026223287, 86.224525433146, 116.818569536018, 63.082998559889, 73.7218123340297, 92.935473959462, 84.2370209154411, 49.9905902403315, 61.0021219814154 };

        // Create data frame
        var dfX = new DataFrame();
        var dfY = new DataFrame();
        // Add exact data
        dfX.ExactSeries = new ExactSeries(dataX);
        dfY.ExactSeries = new ExactSeries(dataY);
        // Calculate plotting positions for pseudo likelihood
        dfX.CalculatePlottingPositions();
        dfY.CalculatePlottingPositions();

        // Fit each marginal univariate distribution
        // Fit marginal-X
        var distX = new UnivariateDistribution(dfX, Numerics.Distributions.UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(distX);
        mle.Estimate();
        distX.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Fit marginal-Y
        var distY = new UnivariateDistribution(dfY, Numerics.Distributions.UnivariateDistributionType.Normal);
        mle = new MaximumLikelihood(distY);
        mle.Estimate();
        distY.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Create bivariate distribution
        var bivariateDist = new BivariateDistribution(distX, distY, CopulaType.AliMikhailHaq)
        {
            CopulaEstimationMethod = CopulaEstimationMethod.InferenceFromMargins
        };

        // Estimate using MLE
        mle = new MaximumLikelihood(bivariateDist, OptimizationMethod.Brent);
        mle.Estimate();
        bivariateDist.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Bivariate distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        Assert.AreEqual(0.8392506, bivariateDist.Copula.Theta, 1E-3, "Copula dependency parameter is incorrect.");
    }

    /// <summary>
    /// Tests Clayton copula fitting using Maximum Pseudo Likelihood (MPL) method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates <see cref="BivariateDistribution"/> with <see cref="CopulaEstimationMethod.PseudoLikelihood"/>
    /// against R 'copula' package results. The pseudo-likelihood method estimates copula parameters using
    /// empirical CDFs (plotting positions) rather than parametric marginal distributions, making it
    /// semi-parametric and robust to marginal distribution misspecification.
    /// </para>
    /// <para>
    /// </para>
    /// <para>
    /// MPL advantages: Distribution-free treatment of marginals, computational efficiency, robustness.
    /// MPL limitations: Less efficient than full MLE when marginals are correctly specified.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Clayton_MPL()
    {
        // Set up data
        var dataX = new double[] { 135.852695757514, 104.082298038859, 108.737560538974, 99.2685553595344, 134.734517358047, 90.96671487189, 77.2933084429689, 115.40695335195, 108.98103448672, 79.030140378764, 119.344309239779, 114.057958622873, 121.376980352823, 102.545934287125, 116.171688724822, 90.2737860301091, 118.384838925781, 78.5650043184329, 122.502611362073, 119.21456701156, 122.901928011128, 79.7224942508733, 103.26308186608, 134.856147348969, 87.2443704188882, 107.484484922021, 102.369642440458, 106.295354390286, 103.333259725228, 114.944097101105, 101.947395165995, 99.3368989207062, 107.600964955177, 63.8796678112047, 122.606004825966, 96.2971006035691, 101.928179675011, 98.6811421254304, 136.414461816426, 94.4488702685262, 118.178109208696, 99.3447054496099, 73.6895736287131, 111.543756772209, 94.1765631568512, 108.378057968653, 82.0682187268786, 93.2316741233556, 92.1848572845123, 101.62266736115, 90.9160104057016, 97.6045250944967, 123.668316008262, 91.5790391025517, 118.507497005719, 116.028361864995, 106.431274472801, 88.1845431623773, 100.593879884171, 106.477111540899, 125.501606137489, 120.558072482172, 93.9426575106378, 61.491148690679, 81.7991169923448, 70.0617929783816, 124.844586043561, 97.6329727079425, 98.4538343555991, 78.7796746466831, 93.4697095163469, 94.4204666650014, 121.959329690142, 86.5743545885648, 119.317279401324, 78.9585491320436, 115.814312639539, 102.440872193275, 124.220180616412, 88.2386416291415, 111.637544083286, 74.24445197777, 139.719855097958, 111.842726007709, 106.593685525292, 95.7079419062174, 100.768445085655, 89.9934014730302, 89.7346792611329, 90.5836289523697, 95.6118805500478, 98.0110615835651, 104.685614771651, 105.840599667288, 110.492236632241, 115.285805520348, 112.406028227278, 97.3674746819745, 77.7088711413582, 102.843691800664 };
        var dataY = new double[] { 106.247204356978, 119.634538634978, 72.5075040420413, 96.4624345242022, 93.7887427218662, 88.0710430691384, 54.3965822738992, 133.939640413503, 147.313192863588, 39.596862389244, 113.685219500925, 65.661791172481, 125.494512719209, 122.353356065013, 86.4850623431384, 56.2427898884044, 112.239490666668, 48.2002798009377, 112.836791609701, 70.306705761088, 59.4614291231979, 44.3647320315126, 91.7637292129578, 95.6179940736635, 60.4815479415048, 58.3786398648642, 94.1724393628596, 87.9631903747507, 91.5086027340312, 114.720801315278, 89.8113482015865, 82.2111665540629, 74.6453503599532, 43.9664396683432, 74.8202107513964, 98.3796818314241, 105.279101457561, 97.8452040931319, 99.7804654911693, 75.7974672384221, 139.913874523229, 81.687017386353, 34.7670214588405, 68.5923527448465, 93.0469463410792, 90.1397446225404, 51.1775088910909, 55.8146471548922, 77.1273200963725, 66.6745681621479, 95.5181914247239, 61.5430213312507, 76.4727428009896, 64.5965344713655, 75.1297368849306, 117.072661743052, 79.6250066166689, 37.2231394673498, 71.1212261596989, 76.6759488420322, 107.428580054692, 110.536313019896, 87.680895839141, 30.0011658085059, 48.6775096662048, 21.1838108219624, 95.5909448097503, 83.8207210333862, 116.670732017431, 40.4019864082416, 91.469151821309, 70.7404242103512, 45.6819094938675, 86.6712212708917, 121.431171230518, 38.4195748844222, 143.699998594758, 64.2498614632223, 75.9822870091017, 59.6123708381618, 113.933833528108, 42.0682009456935, 73.2020173163658, 51.1044236832251, 98.8788896427372, 46.7142903461546, 80.8155805864023, 87.4214895184672, 62.741943393305, 80.361655652615, 70.7251173704377, 89.8903637671805, 74.0531075745106, 72.4690834186899, 107.394378394182, 89.3201273021353, 54.0735001346141, 66.3099470947684, 55.5401474570749, 63.1916628514916 };

        // Create data frame
        var dfX = new DataFrame();
        var dfY = new DataFrame();
        // Add exact data
        dfX.ExactSeries = new ExactSeries(dataX);
        dfY.ExactSeries = new ExactSeries(dataY);
        // Calculate plotting positions for pseudo likelihood
        dfX.CalculatePlottingPositions();
        dfY.CalculatePlottingPositions();

        // Fit each marginal univariate distribution
        // Fit marginal-X
        var distX = new UnivariateDistribution(dfX, Numerics.Distributions.UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(distX);
        mle.Estimate();
        distX.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Fit marginal-Y
        var distY = new UnivariateDistribution(dfY, Numerics.Distributions.UnivariateDistributionType.Normal);
        mle = new MaximumLikelihood(distY);
        mle.Estimate();
        distY.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Note: fitting of the marginals is not necessary for the pseudo likelihood method. 
        // But the method uses univariate distribution marginals for input. 
        // The data frame of the marginal univariate plotting positions are used to define the 
        // pseudo likelihood values.

        // Create bivariate distribution
        var bivariateDist = new BivariateDistribution(distX, distY, CopulaType.Clayton)
        {
            CopulaEstimationMethod = CopulaEstimationMethod.PseudoLikelihood
        };

        // Estimate using MLE
        mle = new MaximumLikelihood(bivariateDist, OptimizationMethod.Brent);
        mle.Estimate();
        bivariateDist.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Bivariate distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        Assert.AreEqual(1.534016, bivariateDist.Copula.Theta, 1E-3, "Copula dependency parameter is incorrect.");
    }


    /// <summary>
    /// Tests Clayton copula fitting using Inference From Margins (IFM) method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates <see cref="BivariateDistribution"/> with <see cref="CopulaEstimationMethod.InferenceFromMargins"/>
    /// against R 'copula' package results. The IFM method uses a two-stage approach: first estimating marginal
    /// distribution parameters independently, then estimating copula parameters conditional on the fitted marginals.
    /// This method provides full maximum likelihood estimates when marginal distributions are correctly specified.
    /// </para>
    /// <para>
    /// </para>
    /// <para>
    /// IFM advantages: Statistically efficient under correct specification, theoretically sound, separate
    /// marginal and dependence modeling. IFM limitations: Sensitive to marginal misspecification, computationally
    /// more intensive than MPL, requires parametric marginal distributions.
    /// </para>
    /// <para>
    /// The IFM estimate (θ = 0.8393) is slightly higher than the MPL estimate (θ = 0.8322), reflecting
    /// the efficiency gain from using parametric marginals rather than empirical CDFs.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Clayton_IFM()
    {
        // Set up data
        var dataX = new double[] { 135.852695757514, 104.082298038859, 108.737560538974, 99.2685553595344, 134.734517358047, 90.96671487189, 77.2933084429689, 115.40695335195, 108.98103448672, 79.030140378764, 119.344309239779, 114.057958622873, 121.376980352823, 102.545934287125, 116.171688724822, 90.2737860301091, 118.384838925781, 78.5650043184329, 122.502611362073, 119.21456701156, 122.901928011128, 79.7224942508733, 103.26308186608, 134.856147348969, 87.2443704188882, 107.484484922021, 102.369642440458, 106.295354390286, 103.333259725228, 114.944097101105, 101.947395165995, 99.3368989207062, 107.600964955177, 63.8796678112047, 122.606004825966, 96.2971006035691, 101.928179675011, 98.6811421254304, 136.414461816426, 94.4488702685262, 118.178109208696, 99.3447054496099, 73.6895736287131, 111.543756772209, 94.1765631568512, 108.378057968653, 82.0682187268786, 93.2316741233556, 92.1848572845123, 101.62266736115, 90.9160104057016, 97.6045250944967, 123.668316008262, 91.5790391025517, 118.507497005719, 116.028361864995, 106.431274472801, 88.1845431623773, 100.593879884171, 106.477111540899, 125.501606137489, 120.558072482172, 93.9426575106378, 61.491148690679, 81.7991169923448, 70.0617929783816, 124.844586043561, 97.6329727079425, 98.4538343555991, 78.7796746466831, 93.4697095163469, 94.4204666650014, 121.959329690142, 86.5743545885648, 119.317279401324, 78.9585491320436, 115.814312639539, 102.440872193275, 124.220180616412, 88.2386416291415, 111.637544083286, 74.24445197777, 139.719855097958, 111.842726007709, 106.593685525292, 95.7079419062174, 100.768445085655, 89.9934014730302, 89.7346792611329, 90.5836289523697, 95.6118805500478, 98.0110615835651, 104.685614771651, 105.840599667288, 110.492236632241, 115.285805520348, 112.406028227278, 97.3674746819745, 77.7088711413582, 102.843691800664 };
        var dataY = new double[] { 106.247204356978, 119.634538634978, 72.5075040420413, 96.4624345242022, 93.7887427218662, 88.0710430691384, 54.3965822738992, 133.939640413503, 147.313192863588, 39.596862389244, 113.685219500925, 65.661791172481, 125.494512719209, 122.353356065013, 86.4850623431384, 56.2427898884044, 112.239490666668, 48.2002798009377, 112.836791609701, 70.306705761088, 59.4614291231979, 44.3647320315126, 91.7637292129578, 95.6179940736635, 60.4815479415048, 58.3786398648642, 94.1724393628596, 87.9631903747507, 91.5086027340312, 114.720801315278, 89.8113482015865, 82.2111665540629, 74.6453503599532, 43.9664396683432, 74.8202107513964, 98.3796818314241, 105.279101457561, 97.8452040931319, 99.7804654911693, 75.7974672384221, 139.913874523229, 81.687017386353, 34.7670214588405, 68.5923527448465, 93.0469463410792, 90.1397446225404, 51.1775088910909, 55.8146471548922, 77.1273200963725, 66.6745681621479, 95.5181914247239, 61.5430213312507, 76.4727428009896, 64.5965344713655, 75.1297368849306, 117.072661743052, 79.6250066166689, 37.2231394673498, 71.1212261596989, 76.6759488420322, 107.428580054692, 110.536313019896, 87.680895839141, 30.0011658085059, 48.6775096662048, 21.1838108219624, 95.5909448097503, 83.8207210333862, 116.670732017431, 40.4019864082416, 91.469151821309, 70.7404242103512, 45.6819094938675, 86.6712212708917, 121.431171230518, 38.4195748844222, 143.699998594758, 64.2498614632223, 75.9822870091017, 59.6123708381618, 113.933833528108, 42.0682009456935, 73.2020173163658, 51.1044236832251, 98.8788896427372, 46.7142903461546, 80.8155805864023, 87.4214895184672, 62.741943393305, 80.361655652615, 70.7251173704377, 89.8903637671805, 74.0531075745106, 72.4690834186899, 107.394378394182, 89.3201273021353, 54.0735001346141, 66.3099470947684, 55.5401474570749, 63.1916628514916 };

        // Create data frame
        var dfX = new DataFrame();
        var dfY = new DataFrame();
        // Add exact data
        dfX.ExactSeries = new ExactSeries(dataX);
        dfY.ExactSeries = new ExactSeries(dataY);
        // Calculate plotting positions for pseudo likelihood
        dfX.CalculatePlottingPositions();
        dfY.CalculatePlottingPositions();

        // Fit each marginal univariate distribution
        // Fit marginal-X
        var distX = new UnivariateDistribution(dfX, Numerics.Distributions.UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(distX);
        mle.Estimate();
        distX.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Fit marginal-Y
        var distY = new UnivariateDistribution(dfY, Numerics.Distributions.UnivariateDistributionType.Normal);
        mle = new MaximumLikelihood(distY);
        mle.Estimate();
        distY.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Create bivariate distribution
        var bivariateDist = new BivariateDistribution(distX, distY, CopulaType.Clayton)
        {
            CopulaEstimationMethod = CopulaEstimationMethod.InferenceFromMargins
        };

        // Estimate using MLE
        mle = new MaximumLikelihood(bivariateDist, OptimizationMethod.Brent);
        mle.Estimate();
        bivariateDist.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Bivariate distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        Assert.AreEqual(1.485167, bivariateDist.Copula.Theta, 1E-3, "Copula dependency parameter is incorrect.");
    }

    /// <summary>
    /// Tests Frank copula fitting using Maximum Pseudo Likelihood (MPL) method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates <see cref="BivariateDistribution"/> with <see cref="CopulaEstimationMethod.PseudoLikelihood"/>
    /// against R 'copula' package results. The pseudo-likelihood method estimates copula parameters using
    /// empirical CDFs (plotting positions) rather than parametric marginal distributions, making it
    /// semi-parametric and robust to marginal distribution misspecification.
    /// </para>
    /// <para>
    /// </para>
    /// <para>
    /// MPL advantages: Distribution-free treatment of marginals, computational efficiency, robustness.
    /// MPL limitations: Less efficient than full MLE when marginals are correctly specified.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Frank_MPL()
    {
        // Set up data
        var dataX = new double[] { 89.4465116086853, 91.7257825797634, 102.113218235982, 80.9258507082031, 83.4871920273991, 115.695292225839, 82.9627093424983, 88.6038265767081, 128.772275696669, 102.984538236351, 121.578135793465, 108.431406395934, 80.0293061353146, 111.431178502881, 79.2589288833566, 118.158376357189, 80.7620921129063, 108.376156927961, 95.8476352445177, 114.311862750292, 94.7747731262201, 118.875199050171, 93.2349488601981, 98.3959955105056, 108.649741186885, 122.182781815476, 77.0562107320218, 107.540344227161, 117.370406779471, 112.182445319761, 93.0289332852279, 90.529599567024, 120.201762251141, 114.278074578765, 104.568760050718, 99.7636591326382, 110.046851177132, 94.1493960120251, 104.412586917358, 98.7607116866476, 98.7463881469725, 105.1085034505, 121.269695545648, 113.731813064378, 107.800720302073, 106.36957073507, 91.6744161473404, 116.374681447127, 81.77133664558, 85.6156796128899, 107.491138655012, 102.732572344842, 121.586784481517, 116.993480194217, 124.032257210449, 99.1570143649983, 71.7128277313518, 54.6418351602958, 120.014404396034, 96.6470795565503, 107.428703111402, 99.3256773796286, 96.3098320017686, 115.238337503403, 72.0162782248935, 80.5473603316434, 85.8099029975343, 134.325558667819, 102.547186178411, 109.9543113498, 112.218655560109, 103.637934442487, 105.599530792916, 104.374711756055, 117.597527250648, 108.696922523792, 95.7635882353413, 113.556079933849, 91.7625855813032, 101.738578831165, 63.8120024793359, 63.7998925020047, 100.584112662855, 103.281311868372, 87.666605980906, 116.010424048388, 120.789094918399, 115.413149279569, 93.3814849565503, 91.1936357063422, 95.8835908172229, 104.01051720411, 118.117029822129, 101.092380522772, 105.948133484193, 101.511409332803, 91.6808235778427, 112.274907117709, 113.335593237504, 109.199879710188 };
        var dataY = new double[] { 90.2725830142295, 50.9675107814588, 72.8091017436628, 37.834394607052, 66.0298908678443, 131.543666094728, 23.4646646758453, 38.387445258362, 90.0045371339763, 64.0836922901644, 113.713441516048, 86.2362874241136, 68.9809017958243, 83.2228976474751, 40.2100658509047, 126.376008778581, 79.489380523177, 105.171916388245, 92.2811081494569, 111.661707493053, 76.4955819232933, 64.4896701009032, 59.6898595730439, 95.893807000091, 84.416144203673, 119.239043275835, 41.0210587766723, 80.3403252114675, 98.987503709067, 117.127667708957, 75.0679115951468, 47.6762994744396, 98.631369967578, 145.07181315581, 73.404950592094, 77.2596264433046, 85.5789153296777, 58.5136745231103, 90.0353584106097, 88.7890539339681, 82.6531147843648, 85.5315028451663, 107.486065585877, 115.188926317826, 88.5805804644887, 94.0736037456068, 70.9824270336269, 104.904710824485, 56.6492033519975, 49.5998637369, 87.9079770735456, 85.6153352921363, 109.84039662402, 109.70332488243, 109.013245187645, 95.3425794566713, 37.0734004156272, 68.2576366613219, 83.0475074812988, 76.0326431319754, 99.6616980295915, 73.6251299875544, 58.5130416264008, 125.024141331457, 20.7835530562455, 55.6776014119347, 40.421171413744, 109.884427624024, 93.0410150569105, 98.1134492367469, 103.110412789979, 70.3696410672021, 87.9378660295918, 88.828217701758, 145.785916448632, 124.799405281758, 61.6454234189007, 74.1550632448535, 53.7379563429746, 103.133087738044, 64.6916624574593, 21.1435390667273, 77.6876521796421, 75.6302369353579, 71.5323052064531, 160.885271053561, 95.1756368878511, 119.852069907319, 68.0321254425055, 74.3625711976756, 79.8206997832619, 84.3152823622181, 113.17252314479, 54.5297187685205, 82.1517568341444, 87.0940228919095, 69.4555843222865, 81.7696161911127, 73.1591225199505, 93.8908358293613 };

        // Create data frame
        var dfX = new DataFrame();
        var dfY = new DataFrame();
        // Add exact data
        dfX.ExactSeries = new ExactSeries(dataX);
        dfY.ExactSeries = new ExactSeries(dataY);
        // Calculate plotting positions for pseudo likelihood
        dfX.CalculatePlottingPositions();
        dfY.CalculatePlottingPositions();

        // Fit each marginal univariate distribution
        // Fit marginal-X
        var distX = new UnivariateDistribution(dfX, Numerics.Distributions.UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(distX);
        mle.Estimate();
        distX.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Fit marginal-Y
        var distY = new UnivariateDistribution(dfY, Numerics.Distributions.UnivariateDistributionType.Normal);
        mle = new MaximumLikelihood(distY);
        mle.Estimate();
        distY.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Note: fitting of the marginals is not necessary for the pseudo likelihood method. 
        // But the method uses univariate distribution marginals for input. 
        // The data frame of the marginal univariate plotting positions are used to define the 
        // pseudo likelihood values.

        // Create bivariate distribution
        var bivariateDist = new BivariateDistribution(distX, distY, CopulaType.Frank)
        {
            CopulaEstimationMethod = CopulaEstimationMethod.PseudoLikelihood
        };

        // Estimate using MLE
        mle = new MaximumLikelihood(bivariateDist, OptimizationMethod.Brent);
        mle.Estimate();
        bivariateDist.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Bivariate distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        Assert.AreEqual(7.718761, bivariateDist.Copula.Theta, 1E-3, "Copula dependency parameter is incorrect.");
    }


    /// <summary>
    /// Tests Frank copula fitting using Inference From Margins (IFM) method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates <see cref="BivariateDistribution"/> with <see cref="CopulaEstimationMethod.InferenceFromMargins"/>
    /// against R 'copula' package results. The IFM method uses a two-stage approach: first estimating marginal
    /// distribution parameters independently, then estimating copula parameters conditional on the fitted marginals.
    /// This method provides full maximum likelihood estimates when marginal distributions are correctly specified.
    /// </para>
    /// <para>
    /// </para>
    /// <para>
    /// IFM advantages: Statistically efficient under correct specification, theoretically sound, separate
    /// marginal and dependence modeling. IFM limitations: Sensitive to marginal misspecification, computationally
    /// more intensive than MPL, requires parametric marginal distributions.
    /// </para>
    /// <para>
    /// The IFM estimate (θ = 0.8393) is slightly higher than the MPL estimate (θ = 0.8322), reflecting
    /// the efficiency gain from using parametric marginals rather than empirical CDFs.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Frank_IFM()
    {
        // Set up data
        var dataX = new double[] { 89.4465116086853, 91.7257825797634, 102.113218235982, 80.9258507082031, 83.4871920273991, 115.695292225839, 82.9627093424983, 88.6038265767081, 128.772275696669, 102.984538236351, 121.578135793465, 108.431406395934, 80.0293061353146, 111.431178502881, 79.2589288833566, 118.158376357189, 80.7620921129063, 108.376156927961, 95.8476352445177, 114.311862750292, 94.7747731262201, 118.875199050171, 93.2349488601981, 98.3959955105056, 108.649741186885, 122.182781815476, 77.0562107320218, 107.540344227161, 117.370406779471, 112.182445319761, 93.0289332852279, 90.529599567024, 120.201762251141, 114.278074578765, 104.568760050718, 99.7636591326382, 110.046851177132, 94.1493960120251, 104.412586917358, 98.7607116866476, 98.7463881469725, 105.1085034505, 121.269695545648, 113.731813064378, 107.800720302073, 106.36957073507, 91.6744161473404, 116.374681447127, 81.77133664558, 85.6156796128899, 107.491138655012, 102.732572344842, 121.586784481517, 116.993480194217, 124.032257210449, 99.1570143649983, 71.7128277313518, 54.6418351602958, 120.014404396034, 96.6470795565503, 107.428703111402, 99.3256773796286, 96.3098320017686, 115.238337503403, 72.0162782248935, 80.5473603316434, 85.8099029975343, 134.325558667819, 102.547186178411, 109.9543113498, 112.218655560109, 103.637934442487, 105.599530792916, 104.374711756055, 117.597527250648, 108.696922523792, 95.7635882353413, 113.556079933849, 91.7625855813032, 101.738578831165, 63.8120024793359, 63.7998925020047, 100.584112662855, 103.281311868372, 87.666605980906, 116.010424048388, 120.789094918399, 115.413149279569, 93.3814849565503, 91.1936357063422, 95.8835908172229, 104.01051720411, 118.117029822129, 101.092380522772, 105.948133484193, 101.511409332803, 91.6808235778427, 112.274907117709, 113.335593237504, 109.199879710188 };
        var dataY = new double[] { 90.2725830142295, 50.9675107814588, 72.8091017436628, 37.834394607052, 66.0298908678443, 131.543666094728, 23.4646646758453, 38.387445258362, 90.0045371339763, 64.0836922901644, 113.713441516048, 86.2362874241136, 68.9809017958243, 83.2228976474751, 40.2100658509047, 126.376008778581, 79.489380523177, 105.171916388245, 92.2811081494569, 111.661707493053, 76.4955819232933, 64.4896701009032, 59.6898595730439, 95.893807000091, 84.416144203673, 119.239043275835, 41.0210587766723, 80.3403252114675, 98.987503709067, 117.127667708957, 75.0679115951468, 47.6762994744396, 98.631369967578, 145.07181315581, 73.404950592094, 77.2596264433046, 85.5789153296777, 58.5136745231103, 90.0353584106097, 88.7890539339681, 82.6531147843648, 85.5315028451663, 107.486065585877, 115.188926317826, 88.5805804644887, 94.0736037456068, 70.9824270336269, 104.904710824485, 56.6492033519975, 49.5998637369, 87.9079770735456, 85.6153352921363, 109.84039662402, 109.70332488243, 109.013245187645, 95.3425794566713, 37.0734004156272, 68.2576366613219, 83.0475074812988, 76.0326431319754, 99.6616980295915, 73.6251299875544, 58.5130416264008, 125.024141331457, 20.7835530562455, 55.6776014119347, 40.421171413744, 109.884427624024, 93.0410150569105, 98.1134492367469, 103.110412789979, 70.3696410672021, 87.9378660295918, 88.828217701758, 145.785916448632, 124.799405281758, 61.6454234189007, 74.1550632448535, 53.7379563429746, 103.133087738044, 64.6916624574593, 21.1435390667273, 77.6876521796421, 75.6302369353579, 71.5323052064531, 160.885271053561, 95.1756368878511, 119.852069907319, 68.0321254425055, 74.3625711976756, 79.8206997832619, 84.3152823622181, 113.17252314479, 54.5297187685205, 82.1517568341444, 87.0940228919095, 69.4555843222865, 81.7696161911127, 73.1591225199505, 93.8908358293613 };

        // Create data frame
        var dfX = new DataFrame();
        var dfY = new DataFrame();
        // Add exact data
        dfX.ExactSeries = new ExactSeries(dataX);
        dfY.ExactSeries = new ExactSeries(dataY);
        // Calculate plotting positions for pseudo likelihood
        dfX.CalculatePlottingPositions();
        dfY.CalculatePlottingPositions();

        // Fit each marginal univariate distribution
        // Fit marginal-X
        var distX = new UnivariateDistribution(dfX, Numerics.Distributions.UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(distX);
        mle.Estimate();
        distX.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Fit marginal-Y
        var distY = new UnivariateDistribution(dfY, Numerics.Distributions.UnivariateDistributionType.Normal);
        mle = new MaximumLikelihood(distY);
        mle.Estimate();
        distY.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Create bivariate distribution
        var bivariateDist = new BivariateDistribution(distX, distY, CopulaType.Frank)
        {
            CopulaEstimationMethod = CopulaEstimationMethod.InferenceFromMargins
        };

        // Estimate using MLE
        mle = new MaximumLikelihood(bivariateDist, OptimizationMethod.Brent);
        mle.Estimate();
        bivariateDist.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Bivariate distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        Assert.AreEqual(8.130259, bivariateDist.Copula.Theta, 1E-3, "Copula dependency parameter is incorrect.");
    }

    /// <summary>
    /// Tests Gumbel copula fitting using Maximum Pseudo Likelihood (MPL) method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates <see cref="BivariateDistribution"/> with <see cref="CopulaEstimationMethod.PseudoLikelihood"/>
    /// against R 'copula' package results. The pseudo-likelihood method estimates copula parameters using
    /// empirical CDFs (plotting positions) rather than parametric marginal distributions, making it
    /// semi-parametric and robust to marginal distribution misspecification.
    /// </para>
    /// <para>
    /// </para>
    /// <para>
    /// MPL advantages: Distribution-free treatment of marginals, computational efficiency, robustness.
    /// MPL limitations: Less efficient than full MLE when marginals are correctly specified.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Gumbel_MPL()
    {
        // Set up data
        var dataX = new double[] { 112.472889925526, 108.197032461095, 113.64666688938, 113.965212991957, 117.333562148345, 100.559345907851, 102.180053944561, 110.97067894608, 104.077049999534, 102.473973961948, 107.113945888296, 106.082540016705, 87.0978719961949, 75.1150155332284, 112.802882878613, 138.120743213339, 79.1789109173273, 106.623115297302, 110.92063481489, 109.543820203885, 73.138959971343, 98.3431545632918, 102.952511132798, 93.0465814076382, 111.688341287489, 79.2581834425453, 103.249260938183, 85.8316965436919, 84.6132137548954, 110.962644400059, 84.0087454428379, 107.223748328677, 121.506255972552, 123.73645847787, 79.0481183395772, 146.264725342603, 95.3276983471542, 107.603210240998, 99.9974503587899, 90.8379276433018, 104.67679932997, 73.4569986162982, 113.260163491206, 97.0605626966906, 89.0374440090811, 133.729158410007, 78.9190177023254, 108.345190856042, 108.953076977455, 97.0704392084539, 86.2765535028879, 102.219521216094, 63.2511232136125, 76.7529568677982, 74.1684311723453, 84.3685879177063, 94.7059136752836, 92.5916995697557, 107.923148783474, 105.261642868879, 84.7155767681601, 105.132628194701, 102.500956437438, 88.5763924520429, 95.9492723947193, 131.375734529198, 123.260221112319, 73.5681907570129, 93.1887735334748, 106.007277431115, 100.356675515534, 91.4930966213857, 122.640033750305, 107.143857399285, 91.0914154936143, 69.1482456631603, 122.434236175527, 111.906044824347, 111.632367993799, 84.6813208809395, 99.4489764877588, 106.884486176956, 95.3274691248045, 107.839259276592, 90.3967465603013, 76.5485833533036, 98.4847265047609, 96.2342050639354, 91.963420241494, 101.351308748786, 95.4725609428008, 145.010119874994, 88.3665385050844, 109.704283131981, 81.2884314113531, 109.836099370176, 102.386450396465, 93.0092098477109, 83.7158456419459, 89.7486244037052 };
        var dataY = new double[] { 101.222248773938, 81.7313758478523, 123.411631899952, 93.2413907814405, 102.50494017697, 96.5306123268502, 18.1227322903384, 74.307009995493, 82.2705528425937, 89.6693353083137, 87.2256413770307, 81.1744664866471, 81.2165207417765, 81.4927985171313, 100.651709224046, 130.599135003894, 45.4442163124226, 66.6919540534553, 83.5881809451671, 88.4770416926437, 75.1039197272015, 52.1553833221993, 49.528146406319, 66.3900605051234, 97.2816608378485, 26.5831021290638, 88.5107383079526, 78.4085272967254, 70.7569274155408, 109.703373315445, 87.4165170640218, 85.6182893046897, 110.738685438064, 119.768351328997, 21.6161419679033, 154.357497576718, 53.8795395966417, 75.943071862762, 105.35244272571, 86.5868773757059, 76.4752235253919, 48.5832730671902, 84.6605006576596, 77.1067893953142, 46.6141185543174, 126.161273608663, 67.6305173666572, 94.9907772124782, 112.133285294602, 72.9506266499651, 37.128394091496, 63.4492833651505, 59.0759448691672, 56.0401720089048, 74.4223335518519, 43.9584062661215, 64.1266855983013, 103.095921282224, 107.649317301385, 72.9193080139833, 91.8847445373411, 75.1454442126996, 76.9540119546376, 64.9699411254905, 55.4012215444807, 118.83302426895, 106.302264714345, 48.1415318738189, 62.2914940564682, 84.5812336961348, 80.3688979491351, 47.5308700972038, 107.759633266225, 95.6723525891828, 27.2716316191787, 99.1054148896251, 82.2568018516306, 96.0432032275742, 96.3923733319187, 62.287551336599, 47.541023973346, 95.9802695805108, 80.2482799857865, 88.743034379118, 51.9583360652995, 73.0816610190799, 33.729130931116, 55.4771104609277, 93.3789037599054, 90.0234978870232, 62.3635712815712, 146.062306846747, 54.4992333698561, 89.1013264737916, 80.1329566564616, 123.235472480686, 73.4015267834993, 49.6295804684941, 74.6326460427444, 64.7088795224836 };

        // Create data frame
        var dfX = new DataFrame();
        var dfY = new DataFrame();
        // Add exact data
        dfX.ExactSeries = new ExactSeries(dataX);
        dfY.ExactSeries = new ExactSeries(dataY);
        // Calculate plotting positions for pseudo likelihood
        dfX.CalculatePlottingPositions();
        dfY.CalculatePlottingPositions();

        // Fit each marginal univariate distribution
        // Fit marginal-X
        var distX = new UnivariateDistribution(dfX, Numerics.Distributions.UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(distX);
        mle.Estimate();
        distX.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Fit marginal-Y
        var distY = new UnivariateDistribution(dfY, Numerics.Distributions.UnivariateDistributionType.Normal);
        mle = new MaximumLikelihood(distY);
        mle.Estimate();
        distY.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Note: fitting of the marginals is not necessary for the pseudo likelihood method. 
        // But the method uses univariate distribution marginals for input. 
        // The data frame of the marginal univariate plotting positions are used to define the 
        // pseudo likelihood values.

        // Create bivariate distribution
        var bivariateDist = new BivariateDistribution(distX, distY, CopulaType.Gumbel)
        {
            CopulaEstimationMethod = CopulaEstimationMethod.PseudoLikelihood
        };

        // Estimate using MLE
        mle = new MaximumLikelihood(bivariateDist, OptimizationMethod.Brent);
        mle.Estimate();
        bivariateDist.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Bivariate distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        Assert.AreEqual(2.097953, bivariateDist.Copula.Theta, 1E-3, "Copula dependency parameter is incorrect.");
    }


    /// <summary>
    /// Tests Gumbel copula fitting using Inference From Margins (IFM) method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates <see cref="BivariateDistribution"/> with <see cref="CopulaEstimationMethod.InferenceFromMargins"/>
    /// against R 'copula' package results. The IFM method uses a two-stage approach: first estimating marginal
    /// distribution parameters independently, then estimating copula parameters conditional on the fitted marginals.
    /// This method provides full maximum likelihood estimates when marginal distributions are correctly specified.
    /// </para>
    /// <para>
    /// </para>
    /// <para>
    /// IFM advantages: Statistically efficient under correct specification, theoretically sound, separate
    /// marginal and dependence modeling. IFM limitations: Sensitive to marginal misspecification, computationally
    /// more intensive than MPL, requires parametric marginal distributions.
    /// </para>
    /// <para>
    /// The IFM estimate (θ = 0.8393) is slightly higher than the MPL estimate (θ = 0.8322), reflecting
    /// the efficiency gain from using parametric marginals rather than empirical CDFs.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Gumbel_IFM()
    {
        // Set up data
        var dataX = new double[] { 112.472889925526, 108.197032461095, 113.64666688938, 113.965212991957, 117.333562148345, 100.559345907851, 102.180053944561, 110.97067894608, 104.077049999534, 102.473973961948, 107.113945888296, 106.082540016705, 87.0978719961949, 75.1150155332284, 112.802882878613, 138.120743213339, 79.1789109173273, 106.623115297302, 110.92063481489, 109.543820203885, 73.138959971343, 98.3431545632918, 102.952511132798, 93.0465814076382, 111.688341287489, 79.2581834425453, 103.249260938183, 85.8316965436919, 84.6132137548954, 110.962644400059, 84.0087454428379, 107.223748328677, 121.506255972552, 123.73645847787, 79.0481183395772, 146.264725342603, 95.3276983471542, 107.603210240998, 99.9974503587899, 90.8379276433018, 104.67679932997, 73.4569986162982, 113.260163491206, 97.0605626966906, 89.0374440090811, 133.729158410007, 78.9190177023254, 108.345190856042, 108.953076977455, 97.0704392084539, 86.2765535028879, 102.219521216094, 63.2511232136125, 76.7529568677982, 74.1684311723453, 84.3685879177063, 94.7059136752836, 92.5916995697557, 107.923148783474, 105.261642868879, 84.7155767681601, 105.132628194701, 102.500956437438, 88.5763924520429, 95.9492723947193, 131.375734529198, 123.260221112319, 73.5681907570129, 93.1887735334748, 106.007277431115, 100.356675515534, 91.4930966213857, 122.640033750305, 107.143857399285, 91.0914154936143, 69.1482456631603, 122.434236175527, 111.906044824347, 111.632367993799, 84.6813208809395, 99.4489764877588, 106.884486176956, 95.3274691248045, 107.839259276592, 90.3967465603013, 76.5485833533036, 98.4847265047609, 96.2342050639354, 91.963420241494, 101.351308748786, 95.4725609428008, 145.010119874994, 88.3665385050844, 109.704283131981, 81.2884314113531, 109.836099370176, 102.386450396465, 93.0092098477109, 83.7158456419459, 89.7486244037052 };
        var dataY = new double[] { 101.222248773938, 81.7313758478523, 123.411631899952, 93.2413907814405, 102.50494017697, 96.5306123268502, 18.1227322903384, 74.307009995493, 82.2705528425937, 89.6693353083137, 87.2256413770307, 81.1744664866471, 81.2165207417765, 81.4927985171313, 100.651709224046, 130.599135003894, 45.4442163124226, 66.6919540534553, 83.5881809451671, 88.4770416926437, 75.1039197272015, 52.1553833221993, 49.528146406319, 66.3900605051234, 97.2816608378485, 26.5831021290638, 88.5107383079526, 78.4085272967254, 70.7569274155408, 109.703373315445, 87.4165170640218, 85.6182893046897, 110.738685438064, 119.768351328997, 21.6161419679033, 154.357497576718, 53.8795395966417, 75.943071862762, 105.35244272571, 86.5868773757059, 76.4752235253919, 48.5832730671902, 84.6605006576596, 77.1067893953142, 46.6141185543174, 126.161273608663, 67.6305173666572, 94.9907772124782, 112.133285294602, 72.9506266499651, 37.128394091496, 63.4492833651505, 59.0759448691672, 56.0401720089048, 74.4223335518519, 43.9584062661215, 64.1266855983013, 103.095921282224, 107.649317301385, 72.9193080139833, 91.8847445373411, 75.1454442126996, 76.9540119546376, 64.9699411254905, 55.4012215444807, 118.83302426895, 106.302264714345, 48.1415318738189, 62.2914940564682, 84.5812336961348, 80.3688979491351, 47.5308700972038, 107.759633266225, 95.6723525891828, 27.2716316191787, 99.1054148896251, 82.2568018516306, 96.0432032275742, 96.3923733319187, 62.287551336599, 47.541023973346, 95.9802695805108, 80.2482799857865, 88.743034379118, 51.9583360652995, 73.0816610190799, 33.729130931116, 55.4771104609277, 93.3789037599054, 90.0234978870232, 62.3635712815712, 146.062306846747, 54.4992333698561, 89.1013264737916, 80.1329566564616, 123.235472480686, 73.4015267834993, 49.6295804684941, 74.6326460427444, 64.7088795224836 };

        // Create data frame
        var dfX = new DataFrame();
        var dfY = new DataFrame();
        // Add exact data
        dfX.ExactSeries = new ExactSeries(dataX);
        dfY.ExactSeries = new ExactSeries(dataY);
        // Calculate plotting positions for pseudo likelihood
        dfX.CalculatePlottingPositions();
        dfY.CalculatePlottingPositions();

        // Fit each marginal univariate distribution
        // Fit marginal-X
        var distX = new UnivariateDistribution(dfX, Numerics.Distributions.UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(distX);
        mle.Estimate();
        distX.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Fit marginal-Y
        var distY = new UnivariateDistribution(dfY, Numerics.Distributions.UnivariateDistributionType.Normal);
        mle = new MaximumLikelihood(distY);
        mle.Estimate();
        distY.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Create bivariate distribution
        var bivariateDist = new BivariateDistribution(distX, distY, CopulaType.Gumbel)
        {
            CopulaEstimationMethod = CopulaEstimationMethod.InferenceFromMargins
        };

        // Estimate using MLE
        mle = new MaximumLikelihood(bivariateDist, OptimizationMethod.Brent);
        mle.Estimate();
        bivariateDist.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Bivariate distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        Assert.AreEqual(2.031034, bivariateDist.Copula.Theta, 1E-3, "Copula dependency parameter is incorrect.");
    }

    /// <summary>
    /// Tests Joe copula fitting using Maximum Pseudo Likelihood (MPL) method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates <see cref="BivariateDistribution"/> with <see cref="CopulaEstimationMethod.PseudoLikelihood"/>
    /// against R 'copula' package results. The pseudo-likelihood method estimates copula parameters using
    /// empirical CDFs (plotting positions) rather than parametric marginal distributions, making it
    /// semi-parametric and robust to marginal distribution misspecification.
    /// </para>
    /// <para>
    /// </para>
    /// <para>
    /// MPL advantages: Distribution-free treatment of marginals, computational efficiency, robustness.
    /// MPL limitations: Less efficient than full MLE when marginals are correctly specified.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Joe_MPL()
    {
        // Set up data
        var dataX = new double[] { 105.080527801019, 115.026257454374, 117.358129524541, 84.3469768155338, 84.5362454182564, 115.832013303087, 103.698610148398, 88.8411298970013, 94.7422294382998, 109.79929825752, 94.0976045955494, 112.417576846617, 110.180042813334, 105.093201216588, 136.275040650675, 91.0470309608862, 84.2474349276179, 100.535501865457, 80.9058461473664, 119.213220466183, 100.572669469592, 103.131258346566, 100.810317630538, 114.061624071483, 83.5975402855976, 89.0115333472513, 66.3268165070727, 84.7329927826247, 69.7680817872407, 115.650963119626, 92.5472957212446, 106.114531288404, 108.032885546296, 110.38969090409, 99.6051228521885, 71.2293045424982, 93.7583750323159, 88.56905726009, 101.688577044379, 88.1152932378495, 102.179951148463, 147.661448558484, 130.057283273608, 109.854007734265, 81.2705409811028, 117.66598298105, 88.8769701106725, 102.802427495432, 93.4008884560194, 90.2277365987588, 103.036719364918, 77.5184782680141, 91.5324658931925, 101.835989095901, 101.800043749889, 109.764650578377, 79.7834884356497, 80.6891470677998, 116.183753310048, 97.7251444184232, 101.93962603208, 91.479747920405, 106.616497717471, 88.9732889869284, 100.347155748138, 64.2776497298067, 97.8630230693096, 115.039994756502, 100.160509753111, 99.0744120656348, 111.633096916686, 99.3446937251762, 113.010250612283, 91.3689738704099, 93.0857066194397, 112.761306730353, 111.792394529326, 51.8176166632693, 107.422232145344, 102.659904000871, 82.1117724410236, 99.3163646335942, 113.555145795822, 116.501871333875, 95.4376498231323, 118.137910178593, 101.824522891831, 98.675229136211, 87.3054670597499, 107.50132629382, 88.6217672049816, 108.587609616577, 117.070978807494, 100.829377608782, 85.9716443690607, 87.1615186321506, 101.929033409522, 129.887046688029, 106.165881910737, 110.123691041869 };
        var dataY = new double[] { 65.3627311420053, 109.338093441946, 110.187130564663, 72.6572491437794, 66.1830652216622, 106.917958610739, 89.4544458566931, 34.2121510489786, 87.5612581119189, 60.5637237767687, 33.8034654209032, 99.2844647305164, 104.97303205735, 6.04617859719163, 138.811261697691, 71.2893972974008, 59.417366566882, 85.355111220543, 50.1413604581927, 102.920060652383, 93.0914947236245, 70.1509627285045, 11.6186828960635, 117.810957879192, 79.1966502763724, 70.2131986632539, 72.5040290039477, 63.6963355882566, 48.4038689280691, 105.297596037192, 87.9566889097355, 87.2650426632532, 92.1659510753509, 51.9553483976797, 69.1607656479071, 69.8590431888949, 63.1696994924036, 83.3814974744098, 77.8342636732521, 54.3557740053106, 75.9564039272592, 153.295740162045, 125.192966195081, 84.9856299114578, 86.854354257246, 98.0159599592548, 3.77678567039993, 74.3992868304184, 61.7221737834943, 75.0682654590025, 107.067397020988, 66.3698849697294, 78.3453997857911, 83.9149525731405, 69.5344335662347, 105.933049927905, 26.5327793191245, 54.4009262051242, 89.3584519067692, 77.5365813897511, 66.5148556530152, 75.574562907648, 69.8755523955032, 75.15359174989, 55.0871908145974, 76.301388721602, 84.6034786632286, 86.412293254073, 89.8760641259544, 73.4531945546952, 103.905973398154, 76.6907578549793, 81.9676998718682, 78.9370335717157, 20.7797553590814, 96.0968795553399, 86.6087907592008, 70.4507347850656, 107.294695066862, 79.0020795665946, 61.8228011685283, 70.9975241569947, 76.1327812702326, 74.3139042973535, 56.2911048478489, 121.512535634237, 92.9142895046758, 88.7950084218728, 63.705929356528, 84.3367159226979, 66.7354953690805, 102.711710544246, 97.0527842334408, 69.4933842704733, 84.4978321017289, 74.067511177418, 75.1669385237322, 134.226620060869, 107.962687902957, 97.4269493483909 };

        // Create data frame
        var dfX = new DataFrame();
        var dfY = new DataFrame();
        // Add exact data
        dfX.ExactSeries = new ExactSeries(dataX);
        dfY.ExactSeries = new ExactSeries(dataY);
        // Calculate plotting positions for pseudo likelihood
        dfX.CalculatePlottingPositions();
        dfY.CalculatePlottingPositions();

        // Fit each marginal univariate distribution
        // Fit marginal-X
        var distX = new UnivariateDistribution(dfX, Numerics.Distributions.UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(distX);
        mle.Estimate();
        distX.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Fit marginal-Y
        var distY = new UnivariateDistribution(dfY, Numerics.Distributions.UnivariateDistributionType.Normal);
        mle = new MaximumLikelihood(distY);
        mle.Estimate();
        distY.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Note: fitting of the marginals is not necessary for the pseudo likelihood method. 
        // But the method uses univariate distribution marginals for input. 
        // The data frame of the marginal univariate plotting positions are used to define the 
        // pseudo likelihood values.

        // Create bivariate distribution
        var bivariateDist = new BivariateDistribution(distX, distY, CopulaType.Joe)
        {
            CopulaEstimationMethod = CopulaEstimationMethod.PseudoLikelihood
        };

        // Estimate using MLE
        mle = new MaximumLikelihood(bivariateDist, OptimizationMethod.Brent);
        mle.Estimate();
        bivariateDist.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Bivariate distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        Assert.AreEqual(2.664326, bivariateDist.Copula.Theta, 1E-3, "Copula dependency parameter is incorrect.");
    }


    /// <summary>
    /// Tests Joe copula fitting using Inference From Margins (IFM) method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates <see cref="BivariateDistribution"/> with <see cref="CopulaEstimationMethod.InferenceFromMargins"/>
    /// against R 'copula' package results. The IFM method uses a two-stage approach: first estimating marginal
    /// distribution parameters independently, then estimating copula parameters conditional on the fitted marginals.
    /// This method provides full maximum likelihood estimates when marginal distributions are correctly specified.
    /// </para>
    /// <para>
    /// </para>
    /// <para>
    /// IFM advantages: Statistically efficient under correct specification, theoretically sound, separate
    /// marginal and dependence modeling. IFM limitations: Sensitive to marginal misspecification, computationally
    /// more intensive than MPL, requires parametric marginal distributions.
    /// </para>
    /// <para>
    /// The IFM estimate (θ = 0.8393) is slightly higher than the MPL estimate (θ = 0.8322), reflecting
    /// the efficiency gain from using parametric marginals rather than empirical CDFs.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Joe_IFM()
    {
        // Set up data
        var dataX = new double[] { 105.080527801019, 115.026257454374, 117.358129524541, 84.3469768155338, 84.5362454182564, 115.832013303087, 103.698610148398, 88.8411298970013, 94.7422294382998, 109.79929825752, 94.0976045955494, 112.417576846617, 110.180042813334, 105.093201216588, 136.275040650675, 91.0470309608862, 84.2474349276179, 100.535501865457, 80.9058461473664, 119.213220466183, 100.572669469592, 103.131258346566, 100.810317630538, 114.061624071483, 83.5975402855976, 89.0115333472513, 66.3268165070727, 84.7329927826247, 69.7680817872407, 115.650963119626, 92.5472957212446, 106.114531288404, 108.032885546296, 110.38969090409, 99.6051228521885, 71.2293045424982, 93.7583750323159, 88.56905726009, 101.688577044379, 88.1152932378495, 102.179951148463, 147.661448558484, 130.057283273608, 109.854007734265, 81.2705409811028, 117.66598298105, 88.8769701106725, 102.802427495432, 93.4008884560194, 90.2277365987588, 103.036719364918, 77.5184782680141, 91.5324658931925, 101.835989095901, 101.800043749889, 109.764650578377, 79.7834884356497, 80.6891470677998, 116.183753310048, 97.7251444184232, 101.93962603208, 91.479747920405, 106.616497717471, 88.9732889869284, 100.347155748138, 64.2776497298067, 97.8630230693096, 115.039994756502, 100.160509753111, 99.0744120656348, 111.633096916686, 99.3446937251762, 113.010250612283, 91.3689738704099, 93.0857066194397, 112.761306730353, 111.792394529326, 51.8176166632693, 107.422232145344, 102.659904000871, 82.1117724410236, 99.3163646335942, 113.555145795822, 116.501871333875, 95.4376498231323, 118.137910178593, 101.824522891831, 98.675229136211, 87.3054670597499, 107.50132629382, 88.6217672049816, 108.587609616577, 117.070978807494, 100.829377608782, 85.9716443690607, 87.1615186321506, 101.929033409522, 129.887046688029, 106.165881910737, 110.123691041869 };
        var dataY = new double[] { 65.3627311420053, 109.338093441946, 110.187130564663, 72.6572491437794, 66.1830652216622, 106.917958610739, 89.4544458566931, 34.2121510489786, 87.5612581119189, 60.5637237767687, 33.8034654209032, 99.2844647305164, 104.97303205735, 6.04617859719163, 138.811261697691, 71.2893972974008, 59.417366566882, 85.355111220543, 50.1413604581927, 102.920060652383, 93.0914947236245, 70.1509627285045, 11.6186828960635, 117.810957879192, 79.1966502763724, 70.2131986632539, 72.5040290039477, 63.6963355882566, 48.4038689280691, 105.297596037192, 87.9566889097355, 87.2650426632532, 92.1659510753509, 51.9553483976797, 69.1607656479071, 69.8590431888949, 63.1696994924036, 83.3814974744098, 77.8342636732521, 54.3557740053106, 75.9564039272592, 153.295740162045, 125.192966195081, 84.9856299114578, 86.854354257246, 98.0159599592548, 3.77678567039993, 74.3992868304184, 61.7221737834943, 75.0682654590025, 107.067397020988, 66.3698849697294, 78.3453997857911, 83.9149525731405, 69.5344335662347, 105.933049927905, 26.5327793191245, 54.4009262051242, 89.3584519067692, 77.5365813897511, 66.5148556530152, 75.574562907648, 69.8755523955032, 75.15359174989, 55.0871908145974, 76.301388721602, 84.6034786632286, 86.412293254073, 89.8760641259544, 73.4531945546952, 103.905973398154, 76.6907578549793, 81.9676998718682, 78.9370335717157, 20.7797553590814, 96.0968795553399, 86.6087907592008, 70.4507347850656, 107.294695066862, 79.0020795665946, 61.8228011685283, 70.9975241569947, 76.1327812702326, 74.3139042973535, 56.2911048478489, 121.512535634237, 92.9142895046758, 88.7950084218728, 63.705929356528, 84.3367159226979, 66.7354953690805, 102.711710544246, 97.0527842334408, 69.4933842704733, 84.4978321017289, 74.067511177418, 75.1669385237322, 134.226620060869, 107.962687902957, 97.4269493483909 };

        // Create data frame
        var dfX = new DataFrame();
        var dfY = new DataFrame();
        // Add exact data
        dfX.ExactSeries = new ExactSeries(dataX);
        dfY.ExactSeries = new ExactSeries(dataY);
        // Calculate plotting positions for pseudo likelihood
        dfX.CalculatePlottingPositions();
        dfY.CalculatePlottingPositions();

        // Fit each marginal univariate distribution
        // Fit marginal-X
        var distX = new UnivariateDistribution(dfX, Numerics.Distributions.UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(distX);
        mle.Estimate();
        distX.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Fit marginal-Y
        var distY = new UnivariateDistribution(dfY, Numerics.Distributions.UnivariateDistributionType.Normal);
        mle = new MaximumLikelihood(distY);
        mle.Estimate();
        distY.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Create bivariate distribution
        var bivariateDist = new BivariateDistribution(distX, distY, CopulaType.Joe)
        {
            CopulaEstimationMethod = CopulaEstimationMethod.InferenceFromMargins
        };

        // Estimate using MLE
        mle = new MaximumLikelihood(bivariateDist, OptimizationMethod.Brent);
        mle.Estimate();
        bivariateDist.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Bivariate distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        Assert.AreEqual(2.965269, bivariateDist.Copula.Theta, 1E-3, "Copula dependency parameter is incorrect.");
    }

    /// <summary>
    /// Tests Normal (Gaussian) copula fitting using Maximum Pseudo Likelihood (MPL) method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates <see cref="BivariateDistribution"/> with <see cref="CopulaEstimationMethod.PseudoLikelihood"/>
    /// against R 'copula' package results. The pseudo-likelihood method estimates copula parameters using
    /// empirical CDFs (plotting positions) rather than parametric marginal distributions, making it
    /// semi-parametric and robust to marginal distribution misspecification.
    /// </para>
    /// <para>
    /// </para>
    /// <para>
    /// MPL advantages: Distribution-free treatment of marginals, computational efficiency, robustness.
    /// MPL limitations: Less efficient than full MLE when marginals are correctly specified.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Normal_MPL()
    {
        // Set up data
        var dataX = new double[] { 122.094066003419, 92.8321267206161, 86.4920318705377, 87.6183663113541, 102.558777787492, 103.627475117762, 127.084948716539, 105.908684131013, 110.065795957654, 105.924647125867, 110.009738155469, 126.490833800772, 64.1264871206211, 81.3150800229481, 92.0780134395721, 106.040322550555, 113.158086143066, 117.051057784044, 127.110531266645, 108.907371862136, 105.476247114194, 108.629403495407, 98.7803988364997, 93.217925588845, 97.7219451830075, 109.178093756809, 137.69504856252, 106.884615327674, 112.139177456202, 85.7416217661797, 71.0610938629716, 112.644166631765, 119.545871678548, 70.5169833274982, 99.6896817997206, 100.987892854545, 103.659280253554, 75.6075621013066, 118.810868919796, 109.113664695226, 113.636425353944, 100.008375355612, 113.178917359795, 80.4269472604342, 88.3638384448237, 90.2905074656314, 98.7995143316863, 98.4698060067802, 108.279297570816, 86.1578437055905, 101.183725242941, 85.5531148952956, 111.024195253862, 121.934506174556, 104.169993666179, 84.4652994609478, 99.6099259747033, 95.3130792386208, 115.45680252817, 120.213139478586, 95.5691788140058, 92.7950300448044, 102.58430893827, 86.7105161576407, 82.8059368562185, 107.335705516294, 112.603259240932, 102.780778760832, 128.958090528336, 105.139162595628, 118.272661482198, 99.8275937885748, 94.2856024560543, 108.48679008009, 100.147734981682, 88.7006383425785, 89.6441478272035, 112.24266306884, 99.8184811468069, 120.592090049738, 124.023170133661, 101.250961381805, 90.0000027551006, 108.781064635426, 94.9203320035987, 99.9491821782837, 88.7473944659517, 94.3643253649856, 105.814317118952, 92.6866900633813, 111.020330544613, 111.676189456988, 115.70235103978, 124.659106152655, 81.3866270495082, 120.178528245778, 93.6511977805724, 114.099368762143, 119.062045395294, 74.1998497412903 };
        var dataY = new double[] { 127.869024514059, 53.5970265830273, 35.6871183968043, 77.5937820397885, 84.619117510857, 110.477376636164, 114.679535976765, 109.338354392258, 88.5987759167264, 72.6695216679034, 111.932652280673, 86.3677960278751, 23.9336347978345, 51.2377830227977, 82.4565771813309, 92.9162733515069, 117.465381827514, 104.862362549521, 131.059266136887, 67.2743851584176, 100.263235166171, 113.734275000025, 73.1582387829997, 78.4353197703676, 60.0180359279642, 106.709991071405, 123.175455301514, 98.7006449949188, 99.860486991242, 55.7603096813567, 53.7716423706874, 104.659445447656, 119.899401349887, 59.8670226375024, 94.0117104763717, 101.424610891155, 114.256354904191, 53.5051841563538, 118.35993465227, 73.1605008375787, 87.4677698350712, 75.4031529479113, 105.404958657365, 53.336411944238, 61.2731445424292, 72.377272009744, 88.959659863884, 80.1301183393358, 98.624093971352, 81.9603074727622, 52.0788199186743, 75.49358652998, 90.2428259997917, 101.326931349259, 48.1343463500222, 56.9295918059918, 89.0348875829931, 69.0012535890253, 100.355241744174, 74.00820280539, 63.9482913881998, 64.4973782209222, 95.8934144135508, 85.4028102356618, 37.8958459664423, 99.2194777630975, 126.581868541047, 91.8287794302242, 143.543939198862, 108.751405708845, 100.951567564812, 73.5051068155712, 83.419507788205, 84.9090133796832, 59.3886126411711, 84.0348703304947, 78.1503115396303, 104.953483626903, 77.6450718557069, 117.615613165515, 118.131904013699, 76.3190144944821, 62.0183469143453, 97.4729901076061, 49.3396925267253, 58.6790714873228, 45.0596168059506, 85.3857426310419, 65.0772008397323, 58.8836242438228, 79.2838406333912, 102.608529398935, 83.7509120512927, 103.106132785215, 52.8403092456187, 88.4802383528401, 64.2906982187616, 93.0489784548541, 116.065815369284, 26.2779209375887 };

        // Create data frame
        var dfX = new DataFrame();
        var dfY = new DataFrame();
        // Add exact data
        dfX.ExactSeries = new ExactSeries(dataX);
        dfY.ExactSeries = new ExactSeries(dataY);
        // Calculate plotting positions for pseudo likelihood
        dfX.CalculatePlottingPositions();
        dfY.CalculatePlottingPositions();

        // Fit each marginal univariate distribution
        // Fit marginal-X
        var distX = new UnivariateDistribution(dfX, Numerics.Distributions.UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(distX);
        mle.Estimate();
        distX.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Fit marginal-Y
        var distY = new UnivariateDistribution(dfY, Numerics.Distributions.UnivariateDistributionType.Normal);
        mle = new MaximumLikelihood(distY);
        mle.Estimate();
        distY.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Note: fitting of the marginals is not necessary for the pseudo likelihood method. 
        // But the method uses univariate distribution marginals for input. 
        // The data frame of the marginal univariate plotting positions are used to define the 
        // pseudo likelihood values.

        // Create bivariate distribution
        var bivariateDist = new BivariateDistribution(distX, distY, CopulaType.Normal)
        {
            CopulaEstimationMethod = CopulaEstimationMethod.PseudoLikelihood
        };

        // Estimate using MLE
        mle = new MaximumLikelihood(bivariateDist, OptimizationMethod.Brent);
        mle.Estimate();
        bivariateDist.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Bivariate distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        Assert.AreEqual(0.800082, bivariateDist.Copula.Theta, 1E-3, "Copula dependency parameter is incorrect.");
    }


    /// <summary>
    /// Tests Normal (Gaussian) copula fitting using Inference From Margins (IFM) method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Validates <see cref="BivariateDistribution"/> with <see cref="CopulaEstimationMethod.InferenceFromMargins"/>
    /// against R 'copula' package results. The IFM method uses a two-stage approach: first estimating marginal
    /// distribution parameters independently, then estimating copula parameters conditional on the fitted marginals.
    /// This method provides full maximum likelihood estimates when marginal distributions are correctly specified.
    /// </para>
    /// <para>
    /// </para>
    /// <para>
    /// IFM advantages: Statistically efficient under correct specification, theoretically sound, separate
    /// marginal and dependence modeling. IFM limitations: Sensitive to marginal misspecification, computationally
    /// more intensive than MPL, requires parametric marginal distributions.
    /// </para>
    /// <para>
    /// The IFM estimate (θ = 0.8393) is slightly higher than the MPL estimate (θ = 0.8322), reflecting
    /// the efficiency gain from using parametric marginals rather than empirical CDFs.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_Normal_IFM()
    {
        // Set up data
        var dataX = new double[] { 122.094066003419, 92.8321267206161, 86.4920318705377, 87.6183663113541, 102.558777787492, 103.627475117762, 127.084948716539, 105.908684131013, 110.065795957654, 105.924647125867, 110.009738155469, 126.490833800772, 64.1264871206211, 81.3150800229481, 92.0780134395721, 106.040322550555, 113.158086143066, 117.051057784044, 127.110531266645, 108.907371862136, 105.476247114194, 108.629403495407, 98.7803988364997, 93.217925588845, 97.7219451830075, 109.178093756809, 137.69504856252, 106.884615327674, 112.139177456202, 85.7416217661797, 71.0610938629716, 112.644166631765, 119.545871678548, 70.5169833274982, 99.6896817997206, 100.987892854545, 103.659280253554, 75.6075621013066, 118.810868919796, 109.113664695226, 113.636425353944, 100.008375355612, 113.178917359795, 80.4269472604342, 88.3638384448237, 90.2905074656314, 98.7995143316863, 98.4698060067802, 108.279297570816, 86.1578437055905, 101.183725242941, 85.5531148952956, 111.024195253862, 121.934506174556, 104.169993666179, 84.4652994609478, 99.6099259747033, 95.3130792386208, 115.45680252817, 120.213139478586, 95.5691788140058, 92.7950300448044, 102.58430893827, 86.7105161576407, 82.8059368562185, 107.335705516294, 112.603259240932, 102.780778760832, 128.958090528336, 105.139162595628, 118.272661482198, 99.8275937885748, 94.2856024560543, 108.48679008009, 100.147734981682, 88.7006383425785, 89.6441478272035, 112.24266306884, 99.8184811468069, 120.592090049738, 124.023170133661, 101.250961381805, 90.0000027551006, 108.781064635426, 94.9203320035987, 99.9491821782837, 88.7473944659517, 94.3643253649856, 105.814317118952, 92.6866900633813, 111.020330544613, 111.676189456988, 115.70235103978, 124.659106152655, 81.3866270495082, 120.178528245778, 93.6511977805724, 114.099368762143, 119.062045395294, 74.1998497412903 };
        var dataY = new double[] { 127.869024514059, 53.5970265830273, 35.6871183968043, 77.5937820397885, 84.619117510857, 110.477376636164, 114.679535976765, 109.338354392258, 88.5987759167264, 72.6695216679034, 111.932652280673, 86.3677960278751, 23.9336347978345, 51.2377830227977, 82.4565771813309, 92.9162733515069, 117.465381827514, 104.862362549521, 131.059266136887, 67.2743851584176, 100.263235166171, 113.734275000025, 73.1582387829997, 78.4353197703676, 60.0180359279642, 106.709991071405, 123.175455301514, 98.7006449949188, 99.860486991242, 55.7603096813567, 53.7716423706874, 104.659445447656, 119.899401349887, 59.8670226375024, 94.0117104763717, 101.424610891155, 114.256354904191, 53.5051841563538, 118.35993465227, 73.1605008375787, 87.4677698350712, 75.4031529479113, 105.404958657365, 53.336411944238, 61.2731445424292, 72.377272009744, 88.959659863884, 80.1301183393358, 98.624093971352, 81.9603074727622, 52.0788199186743, 75.49358652998, 90.2428259997917, 101.326931349259, 48.1343463500222, 56.9295918059918, 89.0348875829931, 69.0012535890253, 100.355241744174, 74.00820280539, 63.9482913881998, 64.4973782209222, 95.8934144135508, 85.4028102356618, 37.8958459664423, 99.2194777630975, 126.581868541047, 91.8287794302242, 143.543939198862, 108.751405708845, 100.951567564812, 73.5051068155712, 83.419507788205, 84.9090133796832, 59.3886126411711, 84.0348703304947, 78.1503115396303, 104.953483626903, 77.6450718557069, 117.615613165515, 118.131904013699, 76.3190144944821, 62.0183469143453, 97.4729901076061, 49.3396925267253, 58.6790714873228, 45.0596168059506, 85.3857426310419, 65.0772008397323, 58.8836242438228, 79.2838406333912, 102.608529398935, 83.7509120512927, 103.106132785215, 52.8403092456187, 88.4802383528401, 64.2906982187616, 93.0489784548541, 116.065815369284, 26.2779209375887 };

        // Create data frame
        var dfX = new DataFrame();
        var dfY = new DataFrame();
        // Add exact data
        dfX.ExactSeries = new ExactSeries(dataX);
        dfY.ExactSeries = new ExactSeries(dataY);
        // Calculate plotting positions for pseudo likelihood
        dfX.CalculatePlottingPositions();
        dfY.CalculatePlottingPositions();

        // Fit each marginal univariate distribution
        // Fit marginal-X
        var distX = new UnivariateDistribution(dfX, Numerics.Distributions.UnivariateDistributionType.Normal);
        var mle = new MaximumLikelihood(distX);
        mle.Estimate();
        distX.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Fit marginal-Y
        var distY = new UnivariateDistribution(dfY, Numerics.Distributions.UnivariateDistributionType.Normal);
        mle = new MaximumLikelihood(distY);
        mle.Estimate();
        distY.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Univariate distribution fitting failed.");

        // Create bivariate distribution
        var bivariateDist = new BivariateDistribution(distX, distY, CopulaType.Normal)
        {
            CopulaEstimationMethod = CopulaEstimationMethod.InferenceFromMargins
        };

        // Estimate using MLE
        mle = new MaximumLikelihood(bivariateDist, OptimizationMethod.Brent);
        mle.Estimate();
        bivariateDist.SetParameterValues(mle.BestParameterSet.Values);

        // Assert that the distribution was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Bivariate distribution fitting failed.");

        // Assert that the fitted parameters are close to the true parameters
        Assert.AreEqual(0.7871479, bivariateDist.Copula.Theta, 1E-3, "Copula dependency parameter is incorrect.");
    }

}
