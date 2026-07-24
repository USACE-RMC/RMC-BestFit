"""Generate SciPy distribution-fitting oracles for RMC.BestFit.

The output contains deterministic quantile-based datasets, independently fitted
SciPy parameters, original-measure maximum log likelihoods, and fixed-parameter
PDF/CDF/quantile checks. RMC.BestFit verification tests consume only the committed
JSON artifact and never import Python or SciPy at test runtime.
"""

from __future__ import annotations

import hashlib
import json
import platform
from datetime import date
from pathlib import Path
from typing import Any, Callable

import numpy as np
import scipy
from scipy import optimize, stats


SCRIPT_PATH = Path(__file__).resolve()
REPOSITORY_ROOT = SCRIPT_PATH.parents[3]
OUTPUT_PATH = (
    REPOSITORY_ROOT
    / "verification"
    / "data"
    / "distribution-fitting"
    / "scipy-family-oracles.json"
)
PROBABILITIES = np.linspace(0.025, 0.975, 39)
CDF_EVALUATION_PROBABILITY = 0.35
QUANTILE_EVALUATION_PROBABILITY = 0.90


def as_floats(values: np.ndarray | tuple[float, ...] | list[float]) -> list[float]:
    """Convert NumPy or tuple values to JSON-safe Python floats."""

    return [float(value) for value in values]


def add_scipy_family(
    destination: dict[str, Any],
    name: str,
    source_distribution: Any,
    fit: Callable[[np.ndarray], tuple[float, ...]],
    fitted_distribution: Callable[[tuple[float, ...]], Any],
    to_numerics: Callable[[tuple[float, ...]], list[float]],
    scipy_order: list[str],
    numerics_order: list[str],
    crosswalk: str,
    fit_method: str = "SciPy distribution.fit default maximum-likelihood optimizer",
) -> None:
    """Generate one standard SciPy family benchmark and add it to the output."""

    data = np.asarray(source_distribution.ppf(PROBABILITIES), dtype=float)
    fitted = tuple(float(value) for value in fit(data))
    oracle_distribution = fitted_distribution(fitted)
    evaluation_x = float(source_distribution.ppf(CDF_EVALUATION_PROBABILITY))
    destination[name] = {
        "data": as_floats(data),
        "scipy_parameter_order": scipy_order,
        "scipy_parameters": as_floats(fitted),
        "numerics_parameter_order": numerics_order,
        "numerics_parameters": as_floats(to_numerics(fitted)),
        "parameter_crosswalk": crosswalk,
        "fit_method": fit_method,
        "maximum_log_likelihood": float(np.sum(oracle_distribution.logpdf(data))),
        "evaluation": {
            "x": evaluation_x,
            "pdf": float(oracle_distribution.pdf(evaluation_x)),
            "cdf": float(oracle_distribution.cdf(evaluation_x)),
            "probability": QUANTILE_EVALUATION_PROBABILITY,
            "quantile": float(oracle_distribution.ppf(QUANTILE_EVALUATION_PROBABILITY)),
        },
    }


def fit_generalized_pareto(data: np.ndarray) -> tuple[float, ...]:
    """Fit a free-location GPD with a deterministic bounded global search."""

    minimum = float(np.min(data))
    span = float(np.max(data) - minimum)
    lower_scale = max(span * 1E-6, 1E-8)

    def negative_log_likelihood(parameters: np.ndarray) -> float:
        """Return the supported SciPy GPD negative log likelihood."""

        shape, location, scale = parameters
        if scale <= 0.0 or location > minimum:
            return float("inf")
        log_densities = stats.genpareto.logpdf(
            data,
            shape,
            loc=location,
            scale=scale,
        )
        if not np.all(np.isfinite(log_densities)):
            return float("inf")
        return float(-np.sum(log_densities))

    result = optimize.differential_evolution(
        negative_log_likelihood,
        bounds=[(-2.0, 2.0), (minimum - span, minimum), (lower_scale, 5.0 * span)],
        seed=20260724,
        tol=1E-12,
        atol=1E-12,
        popsize=30,
        maxiter=3000,
        polish=True,
        workers=1,
        updating="immediate",
    )
    if not result.success:
        raise RuntimeError(f"SciPy GPD global fit failed: {result.message}")
    return tuple(float(value) for value in result.x)


def fit_log_pearson(log_data: np.ndarray) -> tuple[float, ...]:
    """Fit Pearson III in log10 space with a deterministic global search."""

    minimum = float(np.min(log_data))
    maximum = float(np.max(log_data))
    span = maximum - minimum

    def negative_log_likelihood(parameters: np.ndarray) -> float:
        """Return the supported SciPy Pearson III negative log likelihood."""

        skew, location, scale = parameters
        if scale <= 0.0:
            return float("inf")
        log_densities = stats.pearson3.logpdf(
            log_data,
            skew,
            loc=location,
            scale=scale,
        )
        if not np.all(np.isfinite(log_densities)):
            return float("inf")
        return float(-np.sum(log_densities))

    result = optimize.differential_evolution(
        negative_log_likelihood,
        bounds=[(-3.0, 3.0), (minimum - span, maximum + span), (1E-4, 3.0 * span)],
        seed=20260724,
        tol=1E-12,
        atol=1E-12,
        popsize=40,
        maxiter=5000,
        polish=True,
        workers=1,
        updating="immediate",
    )
    if not result.success:
        raise RuntimeError(f"SciPy Log-Pearson III global fit failed: {result.message}")
    return tuple(float(value) for value in result.x)


def add_log_pearson_family(destination: dict[str, Any]) -> None:
    """Generate the base-10 Log-Pearson III benchmark with its Jacobian."""

    source_log_distribution = stats.pearson3(skew=0.8, loc=2.0, scale=0.3)
    log_data = np.asarray(source_log_distribution.ppf(PROBABILITIES), dtype=float)
    data = np.power(10.0, log_data)
    fitted = fit_log_pearson(log_data)
    fitted_log_distribution = stats.pearson3(*fitted)
    evaluation_x = float(np.power(10.0, source_log_distribution.ppf(CDF_EVALUATION_PROBABILITY)))
    evaluation_log_x = float(np.log10(evaluation_x))
    jacobian_log = np.log(data * np.log(10.0))
    destination["LogPearsonTypeIII"] = {
        "data": as_floats(data),
        "scipy_parameter_order": ["skew", "loc_log10", "scale_log10"],
        "scipy_parameters": as_floats(fitted),
        "numerics_parameter_order": ["mu_log10", "sigma_log10", "skew_log10"],
        "numerics_parameters": [fitted[1], fitted[2], fitted[0]],
        "parameter_crosswalk": "Numerics=(SciPy loc, SciPy scale, SciPy skew) in log10 space; original-measure density divides by y*ln(10).",
        "fit_method": "SciPy differential_evolution bounded global maximum-likelihood search in log10 space",
        "maximum_log_likelihood": float(
            np.sum(fitted_log_distribution.logpdf(log_data) - jacobian_log)
        ),
        "evaluation": {
            "x": evaluation_x,
            "pdf": float(
                fitted_log_distribution.pdf(evaluation_log_x)
                / (evaluation_x * np.log(10.0))
            ),
            "cdf": float(fitted_log_distribution.cdf(evaluation_log_x)),
            "probability": QUANTILE_EVALUATION_PROBABILITY,
            "quantile": float(
                np.power(
                    10.0,
                    fitted_log_distribution.ppf(QUANTILE_EVALUATION_PROBABILITY),
                )
            ),
        },
    }


def create_fitting_analysis_oracle() -> dict[str, Any]:
    """Generate a common-data oracle for criteria, ranking, and RMSE weights."""

    data = np.asarray(stats.norm(loc=100.0, scale=15.0).ppf(PROBABILITIES), dtype=float)
    candidate_definitions = [
        ("Gumbel", stats.gumbel_r.fit, lambda parameters: stats.gumbel_r(*parameters)),
        ("Normal", stats.norm.fit, lambda parameters: stats.norm(*parameters)),
        ("Logistic", stats.logistic.fit, lambda parameters: stats.logistic(*parameters)),
    ]
    candidates: dict[str, Any] = {}
    inverse_mse_values: list[float] = []

    for name, fit, create_distribution in candidate_definitions:
        parameters = tuple(float(value) for value in fit(data))
        distribution = create_distribution(parameters)
        log_likelihood = float(np.sum(distribution.logpdf(data)))
        parameter_count = len(parameters)
        residuals = distribution.ppf(PROBABILITIES) - data
        rmse = float(np.sqrt(np.sum(residuals**2) / (len(data) - parameter_count)))
        candidates[name] = {
            "numerics_parameters": as_floats(parameters),
            "maximum_log_likelihood": log_likelihood,
            "aic": float(-2.0 * log_likelihood + 2.0 * parameter_count),
            "bic": float(-2.0 * log_likelihood + parameter_count * np.log(len(data))),
            "rmse": rmse,
        }
        inverse_mse_values.append(1.0 / (rmse * rmse))

    inverse_mse_total = float(sum(inverse_mse_values))
    for index, (name, _, _) in enumerate(candidate_definitions):
        candidates[name]["inverse_mse_weight"] = float(
            inverse_mse_values[index] / inverse_mse_total
        )

    return {
        "data": as_floats(data),
        "plotting_positions": as_floats(PROBABILITIES),
        "configured_order": [name for name, _, _ in candidate_definitions],
        "aic_ranking": sorted(candidates, key=lambda name: (candidates[name]["aic"], name)),
        "rmse_weight_definition": "(1 / RMSE^2) / sum_j(1 / RMSE_j^2)",
        "candidates": candidates,
    }


def main() -> None:
    """Generate and write the complete SciPy-overlap oracle artifact."""

    families: dict[str, Any] = {}

    add_scipy_family(
        families,
        "Normal",
        stats.norm(loc=100.0, scale=15.0),
        stats.norm.fit,
        lambda parameters: stats.norm(*parameters),
        lambda parameters: [parameters[0], parameters[1]],
        ["loc", "scale"],
        ["mu", "sigma"],
        "Direct location-scale mapping.",
    )
    add_scipy_family(
        families,
        "LogNormal",
        stats.lognorm(s=0.35 * np.log(10.0), loc=0.0, scale=np.power(10.0, 2.0)),
        lambda data: stats.lognorm.fit(data, floc=0.0),
        lambda parameters: stats.lognorm(*parameters),
        lambda parameters: [
            float(np.log(parameters[2]) / np.log(10.0)),
            float(parameters[0] / np.log(10.0)),
        ],
        ["shape_sigma_ln", "loc_fixed_zero", "scale_exp_mu_ln"],
        ["mu_log10", "sigma_log10"],
        "mu_log10=ln(scale)/ln(10); sigma_log10=shape/ln(10); SciPy loc is fixed at zero.",
    )

    ln_sigma = float(np.sqrt(np.log(1.0 + (30.0 / 100.0) ** 2)))
    ln_scale = float(np.exp(np.log(100.0) - 0.5 * ln_sigma**2))
    add_scipy_family(
        families,
        "LnNormal",
        stats.lognorm(s=ln_sigma, loc=0.0, scale=ln_scale),
        lambda data: stats.lognorm.fit(data, floc=0.0),
        lambda parameters: stats.lognorm(*parameters),
        lambda parameters: [
            float(parameters[2] * np.exp(0.5 * parameters[0] ** 2)),
            float(
                parameters[2]
                * np.exp(0.5 * parameters[0] ** 2)
                * np.sqrt(np.exp(parameters[0] ** 2) - 1.0)
            ),
        ],
        ["shape_sigma_ln", "loc_fixed_zero", "scale_exp_mu_ln"],
        ["arithmetic_mean", "arithmetic_standard_deviation"],
        "Numerics exposes natural-space moments derived from SciPy's log-space shape and scale; SciPy loc is fixed at zero.",
    )
    add_scipy_family(
        families,
        "Exponential",
        stats.expon(loc=5.0, scale=20.0),
        stats.expon.fit,
        lambda parameters: stats.expon(*parameters),
        lambda parameters: [parameters[0], parameters[1]],
        ["loc", "scale"],
        ["xi", "alpha"],
        "Direct location-scale mapping.",
    )
    add_scipy_family(
        families,
        "Gamma",
        stats.gamma(a=3.5, loc=0.0, scale=12.0),
        lambda data: stats.gamma.fit(data, floc=0.0),
        lambda parameters: stats.gamma(*parameters),
        lambda parameters: [parameters[2], parameters[0]],
        ["shape", "loc_fixed_zero", "scale"],
        ["theta_scale", "k_shape"],
        "Numerics reverses SciPy's shape/scale order; SciPy loc is fixed at zero.",
    )
    add_scipy_family(
        families,
        "GeneralizedExtremeValue",
        stats.genextreme(c=-0.15, loc=100.0, scale=20.0),
        lambda data: stats.genextreme.fit(data, -0.15),
        lambda parameters: stats.genextreme(*parameters),
        lambda parameters: [parameters[1], parameters[2], parameters[0]],
        ["c", "loc", "scale"],
        ["xi", "alpha", "kappa"],
        "SciPy c equals Numerics kappa and is the negative of the Coles extreme-value shape.",
    )
    add_scipy_family(
        families,
        "GeneralizedPareto",
        stats.genpareto(c=0.20, loc=10.0, scale=5.0),
        fit_generalized_pareto,
        lambda parameters: stats.genpareto(*parameters),
        lambda parameters: [parameters[1], parameters[2], -parameters[0]],
        ["c_common", "loc", "scale"],
        ["xi", "alpha", "kappa"],
        "Numerics kappa is the negative of SciPy's common generalized-Pareto shape c.",
        fit_method="Seeded scipy.optimize.differential_evolution global MLE with explicit support bounds",
    )
    add_scipy_family(
        families,
        "Gumbel",
        stats.gumbel_r(loc=100.0, scale=20.0),
        stats.gumbel_r.fit,
        lambda parameters: stats.gumbel_r(*parameters),
        lambda parameters: [parameters[0], parameters[1]],
        ["loc", "scale"],
        ["xi", "alpha"],
        "Direct location-scale mapping to SciPy gumbel_r.",
    )
    add_scipy_family(
        families,
        "Logistic",
        stats.logistic(loc=100.0, scale=15.0),
        stats.logistic.fit,
        lambda parameters: stats.logistic(*parameters),
        lambda parameters: [parameters[0], parameters[1]],
        ["loc", "scale"],
        ["xi", "alpha"],
        "Direct location-scale mapping.",
    )
    add_scipy_family(
        families,
        "Weibull",
        stats.weibull_min(c=2.2, loc=0.0, scale=100.0),
        lambda data: stats.weibull_min.fit(data, floc=0.0),
        lambda parameters: stats.weibull_min(*parameters),
        lambda parameters: [parameters[2], parameters[0]],
        ["shape", "loc_fixed_zero", "scale"],
        ["lambda_scale", "k_shape"],
        "Numerics reverses SciPy's shape/scale order; SciPy loc is fixed at zero.",
    )
    add_scipy_family(
        families,
        "PearsonTypeIII",
        stats.pearson3(skew=1.0, loc=100.0, scale=20.0),
        lambda data: stats.pearson3.fit(data, 1.0),
        lambda parameters: stats.pearson3(*parameters),
        lambda parameters: [parameters[1], parameters[2], parameters[0]],
        ["skew", "loc_mean", "scale_standard_deviation"],
        ["mu", "sigma", "skew"],
        "SciPy pearson3 loc and scale are the mean and standard deviation; reorder skew to the final Numerics coordinate.",
    )
    add_log_pearson_family(families)
    add_scipy_family(
        families,
        "KappaFour",
        stats.kappa4(h=0.2, k=-0.1, loc=100.0, scale=20.0),
        lambda data: stats.kappa4.fit(data, 0.2, -0.1),
        lambda parameters: stats.kappa4(*parameters),
        lambda parameters: [parameters[2], parameters[3], parameters[1], parameters[0]],
        ["h", "k", "loc", "scale"],
        ["xi", "alpha", "kappa", "hondo"],
        "SciPy and Numerics use the same h and k signs; constructor ordering differs.",
    )

    payload = {
        "metadata": {
            "generated": date.today().isoformat(),
            "generator": "verification/python/distribution-fitting/generate_scipy_oracles.py",
            "generator_sha256": hashlib.sha256(SCRIPT_PATH.read_bytes()).hexdigest(),
            "python": platform.python_version(),
            "numpy": np.__version__,
            "scipy": scipy.__version__,
            "dataset_design": "39 equally spaced nonexceedance probabilities from 0.025 through 0.975",
            "seed": "deterministic-no-random-sampling",
        },
        "families": families,
        "fitting_analysis": create_fitting_analysis_oracle(),
    }
    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(payload, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    print(f"Wrote {OUTPUT_PATH}")
    print(f"Families: {len(families)}")


if __name__ == "__main__":
    main()
