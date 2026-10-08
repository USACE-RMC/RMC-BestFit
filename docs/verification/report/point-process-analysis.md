<!-- verification-status: publication-draft -->

# Point-Process Analysis

## Question and model

Point-process analysis uses all events above a selected threshold rather than only one maximum
per year. It combines an event rate with a distribution of event magnitudes. A Poisson process
describes the count of exceedances during the observed exposure, and a generalized-Pareto (GPA)
distribution describes the excess above the threshold. The corresponding annual-maximum
distribution is expressed using generalized extreme value (GEV) parameters.

Verification asks whether BestFit calculates this likelihood correctly, simulates the right rates
and tails, and recovers known generating processes. Seasonal tests also ask whether events are
assigned to the right part of the year and whether changing the year origin preserves the analysis.
All magnitudes below are synthetic and have arbitrary, consistent units; exposure is measured in
years and intensity in events per year.

### Generating processes and exposure

Every recovery experiment contains exactly 1,000 threshold exceedances, with threshold $u=80$.
The nonseasonal parent has GEV location 100, scale 20, and BestFit shape $\kappa=-0.10$.
Its equivalent GPA scale at the threshold is 18, and its annual exceedance rate is
2.867971991. The declared observation exposure is $1{,}000/2.867971991$ years.

Seasonal designs use two GPA populations. The first has scale 12 and BestFit shape $\kappa=-0.10$;
the second has scale 40 and shape $\kappa=0.05$. In the alternative Coles convention, their
shapes are $\xi=0.10$ and $\xi=-0.05$, respectively: $\kappa=-\xi$. This sign conversion matters
because it determines whether the tail is bounded.

| Recovery design | First/second seasonal intensity | Effective changepoint days | Generation |
|---|---|---|---|
| Nonseasonal | One rate, 2.867971991 per year | Not applicable | BestFit magnitude generator; seed 41001 |
| Seasonal, equal intensity | 8 / 8 per year of seasonal exposure | 80 and 260 | BestFit dated-event generator; seed 42001 |
| Seasonal, unequal intensity | 12 / 4 per year of seasonal exposure | 80 and 260 | BestFit dated-event generator; seed 42101 |
| Calendar-year automatic-prior recovery | 8 / 8 | 170 and 350 | Independent counts and GPA magnitudes, uniform within-season dates; seed 47001 |
| October-water-year automatic-prior recovery | 8 / 8 | 170 and 350, in water-year coordinates | Same independent generation and seed, with the date origin shifted |

The first season wraps across the year boundary; the second lies between the two changepoints.
On the model's 366-day axis their exposure fractions are 186/366 and 180/366 for these designs.
Equal-intensity recovery therefore uses 125 exposure years. For unequal intensities, the annual
rate is the exposure-weighted sum of the two rates, and exposure is 1,000 divided by that rate.
The two production seasonal fits use flat changepoint priors on days 10-100 and 200-360.
The calendar/water-year pair instead uses the automatically placed priors.

The fixed-size recovery samples use the declared exposure when estimating magnitude and rate
parameters. Separate simulations with random event counts test whether the Poisson count mechanism
itself behaves correctly.

## Independent likelihood comparisons

The stationary external comparison uses three exact events, with magnitudes 85, 100, and 120,
over 1.25 years. It uses threshold 80 and the nonseasonal GEV parent above. SciPy 1.17.1 supplies
independent Poisson, GPA, and GEV calculations with the explicit shape-sign conversion.

| Quantity | Frozen SciPy reference | BestFit comparison |
|---|---:|---|
| Threshold exceedance intensity | 2.867971990792441 events/year | Passed scaled $10^{-12}$ agreement |
| Log likelihood for the three events and exposure | -12.762996830833266 | Passed scaled $10^{-12}$ agreement |
| Probability an exceedance above 80 also exceeds 120 | 0.1344306327493119 | Passed scaled $10^{-12}$ agreement |
| Annual maximum at nonexceedance probability 0.99 | 216.8195247592646 | Passed scaled $10^{-12}$ agreement |

These numbers are stored external references, not saved BestFit fitted parameters. This comparison
evaluates the model at specified parameters and does not estimate them from three events.

Two additional calculations test mixed observation types over ten years of exposure:

| Comparison | Exact events | Uncertain annual magnitude | Interval observation | Threshold information |
|---|---|---|---|---|
| Nonseasonal | 105 and 125 | Normal measurement distribution: mean 115, standard deviation 4 | Magnitude between 110 and 126 | Threshold 112 over five indexed years, with two above |
| Seasonal | 105 on 20 January and 140 on 30 May | Normal measurement distribution: mean 125, standard deviation 4 | Magnitude between 110 and 150 | Threshold 120 over three years, with one below and two above |

For the seasonal calculation, the first GEV has location 100, scale 20, and BestFit shape -0.10;
the second has location 130, scale 30, and shape 0.05. Effective changepoints are 80 and 260.
An independent calculation sums the Poisson exposure and exact-event terms, integrates over measurement uncertainty,
and calculates the probabilities of the stated intervals and threshold counts. Seasonal non-exact
records use the annual maximum of both exposure-adjusted processes. Only the two exact events
contribute to the event count. Both mixed-observation comparisons passed the absolute log-likelihood
tolerance $2\times10^{-7}$.

## Recovery and deterministic results

### Recovery procedure and acceptance

1. Generate each sample and establish agreement among event count, exposure, threshold, season
   definition, and shape convention. Confirm that the generating parameters are permitted by the
   priors and explain the sample better than a collapsed alternative.
2. Run Bayesian estimation with the production DEMCzs simulation defaults and seed 12345.
3. For the nonseasonal model, compare all three GEV parameters with their central 95% posterior
   intervals. Also require the generating intensity and conditional tail probability above 120
   inside their corresponding central 95% bands.
4. For seasonal models, compare posterior-mean intensity, GPA scale, and shape with the generating
   values using their standard errors. Check changepoints, intensity bands, and conditional-tail bands
   separately, and check the sampling diagnostics.

A season receives information from only its share of the 1,000 events. Its effective event count is

$$
N_s=N\frac{w_s\Lambda_s}{\sum_j w_j\Lambda_j},\tag{P.1}
$$

where $w_s$ is the fraction of annual exposure and $\Lambda_s$ is the seasonal intensity.
Equal rates give effective counts 508.197 and 491.803. Rates 12 and 4 give effective counts
756.098 and 243.902; exposure fraction alone would understate the first season's contribution.

Seasonal intensity uses Poisson uncertainty; GPA scale and shape use analytical GPA
maximum-likelihood covariance with the effective counts above. Each absolute standardized error
must be at most 1.96. Effective changepoints, obtained by flooring their continuous parameters,
must lie in their central 95% posterior sets. Each seasonal intensity and conditional tail response
must lie in its central 95% band. All monitored GEV and changepoint coordinates require
$\widehat R<1.10$ and ESS at least 100.

### Timing, prior placement, and simulation

The paired calendar/water-year recovery tests hold changepoints at days 170 and 350.
Generated magnitudes and block days agree exactly; calendar dates differ by 92 days; the parent
log likelihoods agree within $10^{-10}$. This isolates the effect of changing the year origin.

Two separate prior-placement experiments each generate 4,000 dated events. They use seasonally
clustered PERT timing to create a monthly occurrence pattern: calendar-year targets are days
170 and 350 with seed 45001, and October-water-year targets are days 80 and 260 with seed 46001.
An independent calculation rotates and smooths the monthly counts, locates separated peaks and
the intervening valleys, and constructs the five-month prior windows. BestFit's initial changepoints
and support bounds must match exactly and contain the generating days. These tests assess placement
of prior ranges; they do not estimate changepoints from the likelihood.

Three simulation experiments each generate 500 independent 20-year records: nonseasonal,
equal-intensity seasonal, and unequal-intensity seasonal. Event totals are compared with their
Poisson expectations within five standard errors. Conditional exceedance fractions are compared
within five binomial standard errors, using magnitude 120 for the first population and 165 for the
second. Seasonal assignment and date-to-block mapping must be exact.

| Test group | Number of tests | Outcome |
|---|---:|---|
| Stationary external likelihood and responses | 1 | Passed |
| Mixed-observation likelihood | 2 | Passed |
| Automatic seasonal prior placement | 2 | Passed |
| Generating-process recovery | 5 | Passed |
| Poisson counts, conditional tails, and seasonal dates | 3 | Passed |

All thirteen comparisons passed. SciPy verifies the stationary calculations; independent analytical
and recovery designs support the seasonal and year-origin results. Simulation checks alone do not
establish parameter recovery. Fitted estimates, posterior endpoints, simulated counts, and prior-window
outputs are not retained as result tables. The [supporting calculations](../point-process.md) and
[stationary reference](../../../verification/data/point-process/stationary-poisson-gpa-scipy-oracle.json)
document the comparisons.
