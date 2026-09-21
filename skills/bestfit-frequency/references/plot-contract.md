# Default BestFit frequency plot contract

The renderer targets one standard B17C or stationary Bayesian univariate analysis.
It matches default desktop semantics and styling, not saved project customizations,
WPF font rasterization, interactive tooltips, alternative overlays, or other plot
families. Python never estimates a distribution, computes confidence limits,
reruns MGBT, or reconstructs plotting positions.

| Element | Rendering contract |
|---|---|
| X axis | Annual exceedance probability, decreasing left to right; normal-probability coordinate `Phi^-1(1-AEP)`. Implementation uses equivalent `-Phi^-1(AEP)` to avoid cancellation. |
| X ticks | Major AEPs 0.9, 0.5, 0.1 and decimal tails/complements; intermediate minor ticks. AEP labels, not percentages or return periods. |
| Y axis | Logarithmic flow; limits padded to powers of ten, whole-number labels; solid major and dashed minor horizontal grids. Blank unit label unless supplied. |
| Point curve | Black solid, width 1. `Computed` for B17C; `Posterior Mean` or `Posterior Mode` according to `fittedDistribution.pointEstimator` for Bayesian. The legacy array name `modeCurve` does not identify which estimator was used. |
| Mean curve | Blue dashed, width 1. `Expected Probability` for B17C; `Posterior Predictive` for Bayesian. |
| Uncertainty | Fill RGBA `(104,140,175,75)/255`, outline `#353b7a`; percentage from `credibleIntervalWidth`. B17C says **Confidence Intervals**, Bayesian **Credible Intervals**. |
| Exact data | Black circles at API `plottingPosition`/`value`. |
| Low outliers | Red crosses for exact records with `isLowOutlier:true`. |
| Uncertain data | Green diamonds with black bounds from API `lowerBound`/`upperBound`. |
| Interval data | Cyan circles with black interval endpoints. |
| Quantile annotations | Red squares with model-computed bounds from `quantileAnnotations`; desktop legend `Quantile Prior` also used for B17C penalties. |
| Layout | White background, legend upper left, title `Frequency`; axis title/tick fonts 16/12 pt. PNG and SVG exports. |

The API returns probabilities and aligned curve arrays; a stable display ordering
keeps these paired. It returns the data frame's computed observation positions,
including historical/threshold and low-outlier treatment. Threshold records affect
those positions but have no separate marker in this default desktop plot.

Zero/negative/nonfinite magnitudes cannot appear on a log axis. The renderer also
uses the desktop's `1e-16` display cutoff, omitting positive values at or below it.
It omits
their markers with a count and leaves gaps in non-displayable curve ordinates.
Original inputs/results stay unchanged. It never moves zeros to an arbitrary
positive floor. Unrepresentable error bounds are omitted and reported; remaining
valid markers stay visible. Missing bounds produce a compatibility error rather
than an approximation. Failed responses, invalid AEPs, mismatched arrays, inverted
intervals, and fewer than two displayable point-curve values are rejected.

For B17C the JSON field name `credibleIntervalWidth` is a compatibility name;
the ensemble is not a Bayesian MCMC posterior. Quantile penalty display coordinates
are in physical units even when a penalty is specified in log10 space. Univariate
annotations are present only when quantile priors are enabled; B17C includes only
enabled penalties.

Source anchors in the BestFit repository:

- `src/RMC.BestFit.UI/Elements/UnivariateAnalysis/{B17CAnalysis,UnivariateAnalysis}.cs`,
  `CreateDefaultFrequencyPlot` / `ApplyDefaultPlotStyle`.
- `src/RMC.BestFit.App/GUI/UnivariateAnalysis/B17C/B17CAnalysisControl.xaml.cs` and
  `Univariate/UnivariateAnalysisControl.xaml.cs`, `UpdateFrequencyPlot`.
- WPF-Framework `src/OxyPlot/OxyPlot/Axes/NormalProbabilityAxis.cs`,
  `PreTransform`, `GetTickValues`, `FormatValueOverride`.
- Model `QuantilePrior`, `QuantilePenalty`, `UncertainData` display properties.

Matplotlib uses its available sans-serif font and drawing engine; exact pixels and
automatic label layout can differ from WPF. The renderer intentionally handles
invalid log bounds explicitly. Custom desktop axis/series settings are outside
this contract.
