"""Generate the independent Chunk 13 time-series oracle artifact.

The implementation intentionally reproduces the declared conditional recurrences without
calling RMC.BestFit.  It fixes every seed, parameter order, initialization convention, and
likelihood contribution count used by the corresponding C# verification cells.
"""

from __future__ import annotations

import hashlib
import json
import math
import platform
from pathlib import Path

import numpy as np
import scipy
from scipy.optimize import differential_evolution, minimize


ROOT = Path(__file__).resolve().parents[3]
INPUT = ROOT / "verification/data/time-series/phase5-recovery-fixtures.json"
OUTPUT = ROOT / "verification/data/time-series/chunk13-independent-oracle.json"
LOG_TWO_PI = math.log(2.0 * math.pi)


def gaussian_sum(residuals: np.ndarray, sigma: float) -> float:
    """Return the independent Gaussian log-likelihood sum."""
    with np.errstate(over="ignore", invalid="ignore"):
        contributions = -0.5 * LOG_TWO_PI - math.log(sigma) - 0.5 * (residuals / sigma) ** 2
    return float(np.sum(contributions)) if np.all(np.isfinite(contributions)) else -math.inf


def ar_residuals(values: np.ndarray, parameters: np.ndarray) -> np.ndarray:
    """Return conditional AR residuals in [mu, phi..., sigma] order."""
    order = len(parameters) - 2
    mu = parameters[0]
    phi = parameters[1:-1]
    residuals = np.zeros(values.size)
    for index in range(order, values.size):
        prediction = mu + sum(
            phi[lag - 1] * (values[index - lag] - mu)
            for lag in range(1, order + 1)
        )
        residuals[index] = values[index] - prediction
    return residuals


def ar_log_likelihood(values: np.ndarray, parameters: np.ndarray) -> float:
    """Return the conditional AR Gaussian log likelihood."""
    order = len(parameters) - 2
    return gaussian_sum(ar_residuals(values, parameters)[order:], parameters[-1])


def ma_residuals(values: np.ndarray, parameters: np.ndarray) -> np.ndarray:
    """Return conditional-sum-of-squares MA residuals in [mu, theta..., sigma] order."""
    order = len(parameters) - 2
    mu = parameters[0]
    theta = parameters[1:-1]
    residuals = np.zeros(values.size)
    for index in range(values.size):
        prediction = mu + sum(
            theta[lag - 1] * residuals[index - lag]
            for lag in range(1, min(index, order) + 1)
        )
        residuals[index] = values[index] - prediction
    return residuals


def ma_log_likelihood(values: np.ndarray, parameters: np.ndarray) -> float:
    """Return the conditional-sum-of-squares MA Gaussian log likelihood."""
    return gaussian_sum(ma_residuals(values, parameters), parameters[-1])


def arima_residuals(values: np.ndarray, parameters: np.ndarray, p_order: int, q_order: int) -> np.ndarray:
    """Return the production-declared conditional ARIMA residual recurrence for d=0."""
    mu = parameters[0]
    phi = parameters[1 : 1 + p_order]
    theta = parameters[1 + p_order : 1 + p_order + q_order]
    residuals = np.zeros(values.size)
    for index in range(max(p_order, q_order), values.size):
        prediction = mu
        prediction += sum(phi[lag - 1] * (values[index - lag] - mu) for lag in range(1, p_order + 1))
        prediction += sum(theta[lag - 1] * residuals[index - lag] for lag in range(1, q_order + 1))
        residuals[index] = values[index] - prediction
    return residuals


def generate_pure_arima(case: dict[str, object], p_order: int, q_order: int) -> dict[str, object]:
    """Create a pure-AR or pure-MA oracle that exercises the ARIMA implementation contract."""
    values = np.asarray(case["values"], dtype=float)
    parameters = np.asarray(case["parameters"], dtype=float)
    residuals = arima_residuals(values, parameters, p_order, q_order)
    start = max(p_order, q_order)
    mu = parameters[0]
    phi = parameters[1 : 1 + p_order]
    theta = parameters[1 + p_order : 1 + p_order + q_order]
    next_prediction = mu
    next_prediction += sum(phi[lag - 1] * (values[-lag] - mu) for lag in range(1, p_order + 1))
    next_prediction += sum(theta[lag - 1] * residuals[-lag] for lag in range(1, q_order + 1))
    return {
        "p_order": p_order,
        "d_order": 0,
        "q_order": q_order,
        "include_intercept": True,
        "retained_raw_observations": int(values.size),
        "conditional_likelihood_contributions": int(values.size - start),
        "parameter_order": case["parameter_order"],
        "parameters": parameters.tolist(),
        "values": values.tolist(),
        "residual_prefix": residuals[:8].tolist(),
        "log_likelihood": gaussian_sum(residuals[start:], parameters[-1]),
        "one_step_conditional_mean": next_prediction,
        "root_moduli": case["root_moduli"],
    }


def observed_information(
    values: np.ndarray,
    optimum: np.ndarray,
    objective,
) -> tuple[np.ndarray, np.ndarray]:
    """Return a central-difference observed-information matrix and its inverse."""
    dimension = optimum.size
    steps = 1e-4 * np.maximum(1.0, np.abs(optimum))
    information = np.zeros((dimension, dimension))
    negative_log_likelihood = lambda parameters: -objective(values, parameters)
    center = negative_log_likelihood(optimum)
    for row in range(dimension):
        plus = optimum.copy()
        minus = optimum.copy()
        plus[row] += steps[row]
        minus[row] -= steps[row]
        information[row, row] = (
            negative_log_likelihood(plus) - 2.0 * center + negative_log_likelihood(minus)
        ) / steps[row] ** 2
        for column in range(row):
            plus_plus = optimum.copy()
            plus_minus = optimum.copy()
            minus_plus = optimum.copy()
            minus_minus = optimum.copy()
            plus_plus[row] += steps[row]
            plus_plus[column] += steps[column]
            plus_minus[row] += steps[row]
            plus_minus[column] -= steps[column]
            minus_plus[row] -= steps[row]
            minus_plus[column] += steps[column]
            minus_minus[row] -= steps[row]
            minus_minus[column] -= steps[column]
            value = (
                negative_log_likelihood(plus_plus)
                - negative_log_likelihood(plus_minus)
                - negative_log_likelihood(minus_plus)
                + negative_log_likelihood(minus_minus)
            ) / (4.0 * steps[row] * steps[column])
            information[row, column] = value
            information[column, row] = value
    covariance = np.linalg.inv(information)
    return information, covariance


def fit_ar_closed_form(values: np.ndarray) -> tuple[np.ndarray, dict[str, object]]:
    """Return the exact conditional AR(1) regression/profile optimum."""
    design = np.column_stack((np.ones(values.size - 1), values[:-1]))
    coefficients, _, _, _ = np.linalg.lstsq(design, values[1:], rcond=None)
    conditional_intercept, phi = coefficients
    mu = conditional_intercept / (1.0 - phi)
    residuals = values[1:] - design @ coefficients
    sigma = math.sqrt(float(residuals @ residuals) / residuals.size)
    return np.array([mu, phi, sigma]), {
        "package": "numpy.linalg.lstsq",
        "method": "closed-form conditional Gaussian regression and profiled innovation scale",
        "success": True,
        "rank": int(np.linalg.matrix_rank(design)),
        "conditional_intercept": float(conditional_intercept),
    }


def fit_ma_multistart(values: np.ndarray, truth: np.ndarray, bounds) -> tuple[np.ndarray, dict[str, object]]:
    """Return a bounded global-plus-multistart MA(1) conditional optimum."""
    def negative_log_likelihood(parameters: np.ndarray) -> float:
        """Return a finite penalty outside the numerically evaluable CSS region."""
        log_likelihood = ma_log_likelihood(values, parameters)
        return min(-log_likelihood, 1e100) if math.isfinite(log_likelihood) else 1e100
    global_result = differential_evolution(
        negative_log_likelihood,
        bounds,
        seed=20260834,
        maxiter=300,
        popsize=15,
        tol=1e-10,
        atol=1e-9,
        polish=False,
        updating="immediate",
    )
    starts = [
        truth,
        np.asarray(global_result.x),
        *[
            np.array([mu, theta, sigma])
            for mu in (5.0, 10.0, 20.0)
            for theta in (-0.9, -0.3, 0.3, 0.9)
            for sigma in (3.0, 7.0)
        ],
    ]
    local_results = [
        minimize(
            negative_log_likelihood,
            start,
            method="L-BFGS-B",
            bounds=bounds,
            options={"ftol": 1e-14, "gtol": 1e-9, "maxiter": 20_000},
        )
        for start in starts
    ]
    best = min(local_results, key=lambda result: result.fun)
    return np.asarray(best.x), {
        "package": "scipy.optimize.differential_evolution and minimize",
        "method": "fixed-seed bounded differential evolution followed by 26 bounded L-BFGS-B starts",
        "success": bool(global_result.success and best.success),
        "global_message": str(global_result.message),
        "local_message": str(best.message),
        "global_evaluations": int(global_result.nfev),
        "multistart_count": len(local_results),
        "successful_local_starts": sum(bool(result.success) for result in local_results),
        "objective_spread": float(max(result.fun for result in local_results if result.fun < 1e99) - best.fun),
    }


def fit_case(values: np.ndarray, truth: np.ndarray, kind: str) -> dict[str, object]:
    """Fit an independently implemented first-order conditional objective."""
    objective = ar_log_likelihood if kind == "ar" else ma_log_likelihood
    bounds = [(1.0, 1000.0), (-2.0, 2.0), (1e-6, 100.0)]
    if kind == "ar":
        optimum, optimizer = fit_ar_closed_form(values)
    else:
        optimum, optimizer = fit_ma_multistart(values, truth, bounds)
    information, covariance = observed_information(values, optimum, objective)
    standard_errors = np.sqrt(np.diag(covariance))
    return {
        "parameter_order": ["mu", "phi" if kind == "ar" else "theta", "sigma"],
        "bounds": bounds,
        "truth": truth.tolist(),
        "truth_log_likelihood": objective(values, truth),
        "independent_optimum": optimum.tolist(),
        "independent_optimum_log_likelihood": objective(values, optimum),
        "optimizer": optimizer,
        "observed_information": information.tolist(),
        "covariance": covariance.tolist(),
        "standard_errors": standard_errors.tolist(),
        "absolute_standardized_parent_errors": (np.abs(optimum - truth) / standard_errors).tolist(),
        "residual_prefix_at_truth": (
            ar_residuals(values, truth) if kind == "ar" else ma_residuals(values, truth)
        )[:8].tolist(),
    }


def generate_higher_order(kind: str) -> dict[str, object]:
    """Generate one identified higher-order recurrence and dynamic-response target."""
    seed = 20260831 if kind == "ar" else 20260832
    rng = np.random.default_rng(seed)
    burn_in = 110
    retained = 1000
    mu = 12.0
    coefficients = np.array([0.55, -0.22] if kind == "ar" else [0.50, -0.28])
    sigma = 1.75
    innovations = rng.normal(0.0, sigma, burn_in + retained)
    values = np.full(burn_in + retained, mu)
    errors = np.zeros(burn_in + retained)
    for index in range(2, values.size):
        if kind == "ar":
            values[index] = mu + sum(
                coefficients[lag - 1] * (values[index - lag] - mu)
                for lag in (1, 2)
            ) + innovations[index]
        else:
            errors[index] = innovations[index]
            values[index] = mu + innovations[index] + sum(
                coefficients[lag - 1] * errors[index - lag]
                for lag in (1, 2)
            )
    retained_values = values[burn_in:]
    parameters = np.concatenate(([mu], coefficients, [sigma]))
    residuals = (
        ar_residuals(retained_values, parameters)
        if kind == "ar"
        else ma_residuals(retained_values, parameters)
    )
    likelihood = (
        ar_log_likelihood(retained_values, parameters)
        if kind == "ar"
        else ma_log_likelihood(retained_values, parameters)
    )
    if kind == "ar":
        impulse = [1.0, coefficients[0]]
        for _ in range(2, 8):
            impulse.append(coefficients[0] * impulse[-1] + coefficients[1] * impulse[-2])
    else:
        impulse = [1.0, *coefficients.tolist(), 0.0, 0.0, 0.0, 0.0, 0.0]
    next_prediction = (
        mu
        + sum(coefficients[lag - 1] * (retained_values[-lag] - mu) for lag in (1, 2))
        if kind == "ar"
        else mu + sum(coefficients[lag - 1] * residuals[-lag] for lag in (1, 2))
    )
    return {
        "kind": kind,
        "order": 2,
        "seed": seed,
        "burn_in": burn_in,
        "retained_raw_observations": retained,
        "conditional_likelihood_contributions": retained - 2 if kind == "ar" else retained,
        "parameter_order": ["mu", f"{kind[0]}1", f"{kind[0]}2", "sigma"],
        "parameters": parameters.tolist(),
        "values": retained_values.tolist(),
        "residual_prefix": residuals[:8].tolist(),
        "log_likelihood": likelihood,
        "impulse_response_0_to_7": impulse,
        "one_step_conditional_mean": next_prediction,
        "root_moduli": np.abs(
            np.roots(
                np.array([-coefficients[1], -coefficients[0], 1.0])
                if kind == "ar"
                else np.array([coefficients[1], coefficients[0], 1.0])
            )
        ).tolist(),
    }


def generate_arimax_interaction() -> dict[str, object]:
    """Generate the predeclared ARIMAX trend-seasonality-covariate interaction."""
    seed = 20260833
    rng = np.random.default_rng(seed)
    burn_in = 110
    retained = 1000
    start = -burn_in
    times = np.arange(start, retained, dtype=float)
    covariate_1 = np.sin(2.0 * math.pi * times / 17.0) + 0.002 * times
    covariate_2 = np.cos(2.0 * math.pi * times / 29.0) - 0.001 * times
    parameters = np.array([20.0, 0.015, 3.0, -1.5, 1.25, -0.80, 0.45, 0.30, 1.20])
    mu, trend, sin_coef, cos_coef, beta_1, beta_2, phi, theta, sigma = parameters
    means = (
        mu
        + trend * times
        + sin_coef * np.sin(2.0 * math.pi * times / 12.0)
        + cos_coef * np.cos(2.0 * math.pi * times / 12.0)
        + beta_1 * covariate_1
        + beta_2 * covariate_2
    )
    innovations = rng.normal(0.0, sigma, times.size)
    values = means.copy()
    errors = np.zeros(times.size)
    for index in range(1, times.size):
        errors[index] = innovations[index]
        values[index] = (
            means[index]
            + phi * (values[index - 1] - means[index - 1])
            + theta * errors[index - 1]
            + innovations[index]
        )
    values = values[burn_in:]
    covariate_1 = covariate_1[burn_in:]
    covariate_2 = covariate_2[burn_in:]
    # The production conditional recurrence fixes epsilon[0]=0 and scores t=1..999.
    expected_mean = (
        mu
        + trend * np.arange(retained)
        + sin_coef * np.sin(2.0 * math.pi * np.arange(retained) / 12.0)
        + cos_coef * np.cos(2.0 * math.pi * np.arange(retained) / 12.0)
        + beta_1 * covariate_1
        + beta_2 * covariate_2
    )
    residuals = np.zeros(retained)
    for index in range(1, retained):
        prediction = (
            expected_mean[index]
            + phi * (values[index - 1] - expected_mean[index - 1])
            + theta * residuals[index - 1]
        )
        residuals[index] = values[index] - prediction
    covariate_1_storage = np.concatenate(([-700.0], covariate_1))
    covariate_2_storage = np.concatenate(([900.0, -900.0], covariate_2))
    index_mean = (
        mu
        + trend * np.arange(retained)
        + sin_coef * np.sin(2.0 * math.pi * np.arange(retained) / 12.0)
        + cos_coef * np.cos(2.0 * math.pi * np.arange(retained) / 12.0)
        + beta_1 * covariate_1_storage[:retained]
        + beta_2 * covariate_2_storage[:retained]
    )
    index_residuals = np.zeros(retained)
    for index in range(1, retained):
        prediction = (
            index_mean[index]
            + phi * (values[index - 1] - index_mean[index - 1])
            + theta * index_residuals[index - 1]
        )
        index_residuals[index] = values[index] - prediction
    return {
        "seed": seed,
        "burn_in": burn_in,
        "retained_raw_observations": retained,
        "differencing_order": 0,
        "conditional_likelihood_contributions": retained - 1,
        "model": "ARIMAX(1,0,1) with intercept, linear trend, one Fourier harmonic, and two current level covariates",
        "time_interval": "OneMonth",
        "response_start_date": "2000-01-01",
        "covariate_1_start_date": "1999-12-01",
        "covariate_2_start_date": "1999-11-01",
        "seasonal_period": 12,
        "parameter_order": [
            "intercept", "linear_trend", "seasonal_sin", "seasonal_cos",
            "beta_1", "beta_2", "phi_1", "theta_1", "sigma",
        ],
        "parameters": parameters.tolist(),
        "values": values.tolist(),
        "covariate_1": covariate_1_storage.tolist(),
        "covariate_2": covariate_2_storage.tolist(),
        "date_aligned_covariate_1_prefix": covariate_1[:8].tolist(),
        "date_aligned_covariate_2_prefix": covariate_2[:8].tolist(),
        "mean_prefix": expected_mean[:8].tolist(),
        "residual_prefix": residuals[:8].tolist(),
        "log_likelihood": gaussian_sum(residuals[1:], sigma),
        "index_aligned_log_likelihood": gaussian_sum(index_residuals[1:], sigma),
        "ar_root_modulus": abs(1.0 / phi),
        "ma_root_modulus": abs(1.0 / theta),
    }


def main() -> None:
    """Generate and persist the deterministic artifact."""
    fixtures = json.loads(INPUT.read_text(encoding="utf-8"))
    ar = fixtures["ar"]
    ma = fixtures["ma"]
    ar2 = generate_higher_order("ar")
    ma2 = generate_higher_order("ma")
    artifact = {
        "metadata": {
            "artifact_id": "TS-CHUNK13-INDEPENDENT-001",
            "generated_utc": "2026-08-31T00:00:00Z",
            "python": platform.python_version(),
            "numpy": np.__version__,
            "scipy": scipy.__version__,
            "input": str(INPUT.relative_to(ROOT)).replace("\\", "/"),
            "input_sha256": hashlib.sha256(INPUT.read_bytes()).hexdigest(),
            "generator": str(Path(__file__).relative_to(ROOT)).replace("\\", "/"),
            "coordinate_conventions": {
                "ar": "conditional intercept is the unconditional mean; positive phi multiplies y[t-p]-mu",
                "ma": "positive theta adds prior recursively computed innovations; all N residuals are scored",
                "arimax": "intercept plus trend and Fourier mean; level covariates align by raw timestamp and are not differenced",
            },
            "absolute_tolerance": 1e-9,
        },
        "first_order": {
            "ar": fit_case(
                np.asarray(ar["raw"], dtype=float),
                np.array([ar["mu"], ar["phi"], ar["sigma"]], dtype=float),
                "ar",
            ),
            "ma": fit_case(
                np.asarray(ma["raw"], dtype=float),
                np.array([ma["mu"], ma["theta"], ma["sigma"]], dtype=float),
                "ma",
            ),
        },
        "higher_order": {
            "ar2": ar2,
            "ma2": ma2,
        },
        "pure_arima": {
            "ar2": generate_pure_arima(ar2, p_order=2, q_order=0),
            "ma2": generate_pure_arima(ma2, p_order=0, q_order=2),
        },
        "arimax_interaction": generate_arimax_interaction(),
        "known_default_de_failure": {
            "identity": "RMC.BestFit.Verification.TimeSeriesAnalysis.AutoRegressiveMLERecoveryTests.Test_EstimateParameters_AR1",
            "evaluations": 1500,
            "reported_success": True,
            "parameters": [1.0, 0.8761272668, 5.330319728],
            "reported_attained_log_likelihood": -3089.265435,
            "independent_coordinate_log_likelihood": ar_log_likelihood(
                np.asarray(ar["raw"], dtype=float),
                np.array([1.0, 0.8761272668, 5.330319728]),
            ),
            "parent_log_likelihood": -3013.536326,
            "disposition": "preserved open; changing the default optimizer policy or evaluation budget requires technical-authority approval",
        },
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(artifact, indent=2, allow_nan=False) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
