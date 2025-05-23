# RMC-BestFit Version 2.0 Example Projects
This repository contains several example projects demonstrating the capabilities of **RMC-BestFit Version 2.0**. These examples highlight a range of applications, from nonstationary flood frequency analysis (NSFFA) to mixture distributions and bivariate copula modeling. The datasets used in these examples are a combination of real flow data and synthetic data, with known properties, for controlled analysis. Below is an overview of each example:

## 1. Examples of Temporal, Spatial, and Causal Expansion
* **Blakely Mountain Dam Example**: This example follows the workflow provided in the Quick Start User Guide for version 1.0. The dataset includes systematic data, historical data dating back to 1870, and paleoflood information extending back 5,000 years. The skew prior is set using regional skew information from the USGS. Quantile priors are established based on stochastic rainfall-runoff data.

* **Viglione et al. 2013 Example**: This example follows the methodology outlined by Viglione et al. (2013) and is also included in the RMC-BestFit Verification Report for version 1.0. It includes a comparison with Evdbayes using 3 quantile priors and utilizes the Kamp at Zwettl dataset from 1951–2001, as detailed in Viglione et al. (2013).

## 2. ARR-FLIKE Examples
This project follows the methodology provided by Australian Rainfall and Runoff (ARR) using the Flike software, as included in the RMC-BestFit Verification Report for version 1.0.

## 3. Bulletin 17C Examples
This example follows the approach outlined in Bulletin 17C, utilizing the Expected Moments Algorithm (EMA). The flow data is imported directly from the USGS, using the most recent available data. Additionally, daily USGS data is imported for testing and demonstration purposes. For a direct comparison using the data from B17C (2018), refer to the RMC-BestFit Verification Report for version 1.0.

## 4. Measurement Error Example
This project demonstrates how to incorporate measurement errors (or prediction errors) derived from a MOVE.3 analysis.

## 5. Nonstationary FFA Examples
* **Brays Bayou, Texas**: This example demonstrates a nonstationary flood frequency analysis (NSFFA) for a gage in Texas that exhibits an upward trend.

* **Cherry Creek Dam**: This example performs an NSFFA on data showing a slight downward trend; however, it is unclear if the NSFFA approach improves upon a stationary analysis. Both AIC and BIC criteria indicate a preference for the stationary fit.

* **O.C. Fisher – ANCOLD**: This example follows the NSFFA approach outlined in the ANCOLD paper.

## 6. Mixture Distribution Example
This project demonstrates fitting a mixture of distributions, including a 2-component Normal mixture, a 3-component Normal mixture, and a zero-inflated 2-component Normal mixture. The datasets used are synthetic with known properties for controlled analysis. 

## 7. Composite Distribution Examples
This project demonstrates various scenarios, including competing failure modes, competing flood types, mixtures of flood types, and model averaging. Synthetic datasets with known properties are used to allow for controlled analysis.

## 8. Bivariate Distribution Examples
This project demonstrates fitting bivariate copulas for various copula types. Each dataset is synthetic, with marginal normal distributions and differing copula dependencies.

## 9. Point Process Example
This example demonstrates a point process workflow using GHCN precipitation gages in California. Daily GHCN precipitation time series are imported and processed to create both Annual Maximum Series (AMS) and Peaks-Over-Threshold (POT) datasets. A point process model is fit to the POT data, estimating GEV parameters based on the full exceedance record. Results can be compared to traditional GEV fits to the AMS data, with the point process approach typically resulting in reduced uncertainty in the upper tail. In addition, a seasonal point process model is fit by dividing the year into two seasons and combining the results. These results can be compared to a composite distribution of GEVs fit separately to seasonal AMS data. 

## 10. Rating Curve Examples
This project demonstrates fitting rating curves to both synthetic and real-world datasets. A synthetic dataset is generated with known rating curve parameters, and 1-, 2-, and 3-segment rating curves are fit to recover the original relationships. BestFit is able to accurately recover the true parameters for each case. Additional examples include fitting a 1-segment rating curve to data from the Mississippi River at New Madrid and the Middle Fork Willamette River. These examples demonstrate the flexibility of BestFit’s rating curve fitting capabilities across simple and complex flow-stage relationships.

