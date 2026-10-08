"""Generate independent Chunk 14 spatial correlation, prediction, and uncertainty oracles."""

from __future__ import annotations

import hashlib
import json
import math
import platform
from pathlib import Path

import numpy as np
import scipy
from scipy import optimize, stats


ROOT = Path(__file__).resolve().parents[3]
OUTPUT = ROOT / "verification/data/spatial-extremes/chunk14-independent-oracle.json"
EARTH_RADIUS_KM = 6371.0088
LOG_TWO_PI = math.log(2.0 * math.pi)


def distance(first: np.ndarray, second: np.ndarray, metric: str) -> float:
    """Return Cartesian or haversine distance under the declared metric."""
    if metric == "Cartesian":
        return float(np.linalg.norm(first - second))
    lat1, lon1 = np.radians(first)
    lat2, lon2 = np.radians(second)
    dlat = lat2 - lat1
    dlon = lon2 - lon1
    value = math.sin(dlat / 2.0) ** 2 + math.cos(lat1) * math.cos(lat2) * math.sin(dlon / 2.0) ** 2
    return 2.0 * EARTH_RADIUS_KM * math.asin(min(1.0, math.sqrt(value)))


def correlation(h: float, family: str, parameters: list[float]) -> float:
    """Evaluate one independently implemented spatial correlation family."""
    range_parameter = parameters[0]
    if family == "BasicExponential":
        return math.exp(-h / range_parameter)
    if family == "PoweredExponential":
        return math.exp(-((h / range_parameter) ** parameters[1]))
    ratio = h / range_parameter
    return 0.0 if ratio >= 1.0 else 1.0 - 1.5 * ratio + 0.5 * ratio**3


def correlation_grid(family: str, parameters: list[float]) -> dict[str, object]:
    """Return targets at zero, interior, boundary, and beyond-range distances."""
    distances = [0.0, 5.0, 10.0, 20.0, 30.0]
    return {
        "family": family,
        "parameter_order": ["range"] if len(parameters) == 1 else ["range", "smoothness"],
        "parameters": parameters,
        "distance_metric": "Cartesian",
        "distances": distances,
        "correlations": [correlation(value, family, parameters) for value in distances],
    }


def conditional_gp(
    coordinates: np.ndarray,
    target: np.ndarray,
    errors: np.ndarray,
    sigma: float,
    range_parameter: float,
    metric: str,
) -> tuple[float, float, list[list[float]], list[float]]:
    """Return the independent simple-kriging conditional mean and variance."""
    sites = coordinates.shape[0]
    matrix = np.eye(sites)
    vector = np.zeros(sites)
    for row in range(sites):
        vector[row] = correlation(distance(coordinates[row], target, metric), "BasicExponential", [range_parameter])
        for column in range(row):
            value = correlation(distance(coordinates[row], coordinates[column], metric), "BasicExponential", [range_parameter])
            matrix[row, column] = value
            matrix[column, row] = value
    solved = np.linalg.solve(matrix, errors)
    conditional_mean = float(vector @ solved)
    conditional_variance = float(sigma**2 * (1.0 - vector @ np.linalg.solve(matrix, vector)))
    return conditional_mean, conditional_variance, matrix.tolist(), vector.tolist()


def gev_cdf(value: float, location: float, scale: float, shape: float) -> float:
    """Return the Numerics-kappa GEV distribution function independently."""
    y = (value - location) / scale
    if abs(shape) > 1e-12:
        support = 1.0 - shape * y
        if support <= 0.0:
            return 0.0 if shape > 0.0 else 1.0
        y = -math.log(support) / shape
    return math.exp(-math.exp(-y))


def gev_quantile(probability: float, location: float, scale: float, shape: float) -> float:
    """Return the Numerics-kappa GEV quantile independently."""
    reduced = -math.log(-math.log(probability))
    if abs(shape) <= 1e-12:
        return location + scale * reduced
    return location + scale / shape * (1.0 - math.exp(-shape * reduced))


def exponential_correlation_matrix(coordinates: np.ndarray, range_parameter: float) -> np.ndarray:
    """Build an exponential Cartesian correlation matrix."""
    sites = coordinates.shape[0]
    matrix = np.eye(sites)
    for row in range(sites):
        for column in range(row):
            value = math.exp(-float(np.linalg.norm(coordinates[row] - coordinates[column])) / range_parameter)
            matrix[row, column] = value
            matrix[column, row] = value
    return matrix


def copula_fold_log_likelihood(parameters: np.ndarray, data: np.ndarray, coordinates: np.ndarray) -> float:
    """Evaluate an intercept-only GEV plus Gaussian-copula training-fold likelihood."""
    range_parameter, log_location, log_scale, shape = parameters
    location = math.exp(log_location)
    scale = math.exp(log_scale)
    correlation_matrix = exponential_correlation_matrix(coordinates, range_parameter)
    sign, log_determinant = np.linalg.slogdet(correlation_matrix)
    if sign <= 0.0:
        return -math.inf
    inverse = np.linalg.inv(correlation_matrix)
    correction = inverse - np.eye(coordinates.shape[0])
    total = 0.0
    for observation in data:
        marginal = [gev_logpdf(value, location, scale, shape) for value in observation]
        if not all(math.isfinite(value) for value in marginal):
            return -math.inf
        probabilities = np.array([gev_cdf(value, location, scale, shape) for value in observation])
        if np.any(probabilities <= 0.0) or np.any(probabilities >= 1.0):
            return -math.inf
        normal_scores = stats.norm.ppf(probabilities)
        copula = -0.5 * log_determinant - 0.5 * float(normal_scores @ correction @ normal_scores)
        total += sum(marginal) + copula
    return total


def finite_difference_hessian(function, point: np.ndarray) -> np.ndarray:
    """Compute a symmetric central-difference Hessian at one point."""
    steps = np.abs(point) * 1e-4 + 1e-5
    center = function(point)
    hessian = np.zeros((point.size, point.size))
    for row in range(point.size):
        for column in range(row, point.size):
            if row == column:
                plus = point.copy()
                minus = point.copy()
                plus[row] += steps[row]
                minus[row] -= steps[row]
                value = (function(plus) - 2.0 * center + function(minus)) / steps[row] ** 2
            else:
                pp = point.copy()
                pm = point.copy()
                mp = point.copy()
                mm = point.copy()
                pp[row] += steps[row]
                pp[column] += steps[column]
                pm[row] += steps[row]
                pm[column] -= steps[column]
                mp[row] -= steps[row]
                mp[column] += steps[column]
                mm[row] -= steps[row]
                mm[column] -= steps[column]
                value = (function(pp) - function(pm) - function(mp) + function(mm)) / (4.0 * steps[row] * steps[column])
            hessian[row, column] = value
            hessian[column, row] = value
    return hessian


def fitted_copula_fold_oracle() -> dict[str, object]:
    """Generate and independently fit a reduced Gaussian-copula training fold."""
    generator_seed = 20260834
    optimizer_seed = 20260836
    coordinates = np.array([[0.0, 0.0], [10.0, 0.0], [0.0, 12.0], [9.0, 9.0]])
    training_coordinates = coordinates[:3]
    held_out_coordinates = coordinates[3]
    parent = np.array([18.0, 3.0, 0.9, 0.05])
    rng = np.random.default_rng(generator_seed)
    normal_scores = rng.multivariate_normal(np.zeros(4), exponential_correlation_matrix(coordinates, parent[0]), size=120)
    uniforms = stats.norm.cdf(normal_scores)
    location = math.exp(parent[1])
    scale = math.exp(parent[2])
    full_data = np.empty_like(uniforms)
    for row in range(full_data.shape[0]):
        for site in range(full_data.shape[1]):
            full_data[row, site] = gev_quantile(uniforms[row, site], location, scale, parent[3])
    training_data = full_data[:, :3]
    bounds = [(2.0, 80.0), (2.5, 3.5), (0.3, 1.5), (-0.2, 0.2)]
    objective = lambda values: copula_fold_log_likelihood(np.asarray(values), training_data, training_coordinates)
    differential = optimize.differential_evolution(
        lambda values: -objective(values),
        bounds,
        seed=optimizer_seed,
        tol=1e-11,
        polish=False,
        updating="immediate",
        workers=1,
    )
    refined = optimize.minimize(lambda values: -objective(values), differential.x, method="L-BFGS-B", bounds=bounds)
    if not refined.success:
        raise RuntimeError(refined.message)
    optimum = np.asarray(refined.x)
    maximum = objective(optimum)
    hessian = finite_difference_hessian(objective, optimum)
    covariance = np.linalg.inv(-hessian)
    probabilities = np.array([0.5, 0.1, 0.02])
    quantiles = []
    standard_errors = []
    for exceedance in probabilities:
        function = lambda values: gev_quantile(1.0 - exceedance, math.exp(values[1]), math.exp(values[2]), values[3])
        steps = np.abs(optimum) * 1e-5 + 1e-6
        gradient = np.zeros(optimum.size)
        for index in range(optimum.size):
            plus = optimum.copy()
            minus = optimum.copy()
            plus[index] += steps[index]
            minus[index] -= steps[index]
            gradient[index] = (function(plus) - function(minus)) / (2.0 * steps[index])
        quantiles.append(function(optimum))
        standard_errors.append(math.sqrt(float(gradient @ covariance @ gradient)))
    return {
        "objective": "sum of three marginal Numerics-kappa GEV log densities plus the three-site Gaussian-copula log density",
        "distance_metric": "Cartesian",
        "correlation_family": "BasicExponential",
        "parameter_order": ["copula_range", "log_location_intercept", "log_scale_intercept", "shape_kappa"],
        "parameter_bounds": bounds,
        "parent_parameters": parent.tolist(),
        "generator_seed": generator_seed,
        "optimizer_seed": optimizer_seed,
        "raw_row_year_vectors": 120,
        "training_network_dimensions": [3, 2],
        "training_coordinates": training_coordinates.tolist(),
        "held_out_coordinates": held_out_coordinates.tolist(),
        "training_data": training_data.tolist(),
        "independent_optimum": optimum.tolist(),
        "independent_maximum_log_likelihood": maximum,
        "observed_information_covariance": covariance.tolist(),
        "prediction_exceedance_probabilities": probabilities.tolist(),
        "held_out_quantiles": quantiles,
        "held_out_quantile_standard_errors": standard_errors,
        "joint_95_likelihood_ratio_cutoff": 9.487729036781154,
        "optimizer_acceptance": "2*(independent maximum log likelihood - production maximum log likelihood) must not exceed the four-coordinate 95% chi-square cutoff",
        "prediction_acceptance": "production held-out quantile must lie within independent optimum plus or minus 1.96 observed-information standard errors",
        "uncertainty_source": "unregularized inverse negative Hessian of the independently implemented reduced-fold objective",
    }


def cross_validation_oracles() -> dict[str, object]:
    """Create a minimal complete/missing and covariate/dependence fold matrix."""
    training_covariates = np.array([[-1.0, 0.0], [0.0, 1.0], [1.0, -1.0], [2.0, 0.5]])
    observed_log_locations = np.array([2.10, 2.50, 2.70, 3.15])
    design = np.column_stack((np.ones(training_covariates.shape[0]), training_covariates))
    coefficients, _, _, _ = np.linalg.lstsq(design, observed_log_locations, rcond=None)
    residuals = observed_log_locations - design @ coefficients
    residual_variance = float(residuals @ residuals / (design.shape[0] - design.shape[1]))
    held_out_covariates = np.array([0.5, -0.25])
    held_out_design = np.concatenate(([1.0], held_out_covariates))
    link_mean = float(held_out_design @ coefficients)
    parameter_variance = float(residual_variance * held_out_design @ np.linalg.inv(design.T @ design) @ held_out_design)

    return {
        "copula_complete_fold": fitted_copula_fold_oracle(),
        "covariate_complete_fold": {
            "objective": "ordinary least squares on fixed independently supplied site log-location summaries",
            "parameter_order": ["intercept", "x1", "x2"],
            "training_covariates": training_covariates.tolist(),
            "training_log_locations": observed_log_locations.tolist(),
            "training_network_dimensions": [4, 2],
            "coefficients": coefficients.tolist(),
            "residual_variance": residual_variance,
            "held_out_covariates": held_out_covariates.tolist(),
            "held_out_link_mean": link_mean,
            "held_out_physical_location": math.exp(link_mean),
            "parameter_prediction_variance": parameter_variance,
            "observation_prediction_variance": parameter_variance + residual_variance,
        },
        "missing_fold": {
            "held_out_site": 2,
            "finite_observations": 0,
            "scored": False,
            "owner": "fast SpatialGEV cross-validation accounting contracts",
        },
    }


def prediction_oracles() -> dict[str, object]:
    """Create draw-specific geodesic-GP and regional fixed-draw targets."""
    coordinates = np.array([[39.7392, -104.9903], [40.0150, -105.2705], [38.8339, -104.8214]])
    target = np.array([39.5501, -105.7821])
    raw_draws = [
        (0.80, 95.0, [0.20, -0.10, 0.05], 3.00),
        (1.10, 140.0, [0.25, -0.05, 0.12], 3.05),
        (0.65, 75.0, [0.10, -0.18, 0.08], 2.95),
        (0.90, 180.0, [0.30, 0.02, -0.04], 3.10),
    ]
    draws: list[dict[str, object]] = []
    for sigma, range_parameter, errors, log_trend in raw_draws:
        mean, variance, matrix, vector = conditional_gp(
            coordinates, target, np.asarray(errors), sigma, range_parameter, "Geodesic"
        )
        draws.append(
            {
                "parameter_order": ["error_scale", "range", "epsilon_1", "epsilon_2", "epsilon_3"],
                "parameters": [sigma, range_parameter, *errors],
                "log_location_trend": log_trend,
                "conditional_mean": mean,
                "conditional_variance": variance,
                "physical_location": math.exp(log_trend + mean),
                "correlation_matrix": matrix,
                "target_correlation_vector": vector,
            }
        )

    site_covariates = np.array([-1.0, 0.5, 1.5])
    parameter_draws = np.array(
        [
            [3.00, 0.10, 1.05, 0.04],
            [3.04, 0.08, 1.00, 0.02],
            [2.96, 0.12, 1.10, 0.06],
            [3.08, 0.05, 1.02, 0.01],
            [2.92, 0.15, 1.08, 0.08],
            [3.02, 0.09, 0.98, 0.03],
            [2.98, 0.11, 1.12, 0.05],
            [3.06, 0.07, 1.04, 0.00],
            [2.94, 0.14, 1.06, 0.07],
        ]
    )
    exceedance_probabilities = np.array([0.5, 0.1, 0.02])
    regional = np.zeros((exceedance_probabilities.size, parameter_draws.shape[0]))
    for draw_index, (loc_intercept, loc_slope, log_scale, shape) in enumerate(parameter_draws):
        scale = math.exp(log_scale)
        for probability_index, exceedance in enumerate(exceedance_probabilities):
            nonexceedance = 1.0 - exceedance
            values = []
            for covariate in site_covariates:
                location = math.exp(loc_intercept + loc_slope * covariate)
                if abs(shape) <= 1e-12:
                    quantile = location - scale * math.log(-math.log(nonexceedance))
                else:
                    quantile = location + scale / shape * (1.0 - (-math.log(nonexceedance)) ** shape)
                values.append(quantile)
            regional[probability_index, draw_index] = np.mean(values)
    summaries = []
    for index, exceedance in enumerate(exceedance_probabilities):
        summaries.append(
            {
                "exceedance_probability": float(exceedance),
                "per_draw_regional_mean": regional[index].tolist(),
                "mean": float(np.mean(regional[index])),
                "lower_95": float(np.quantile(regional[index], 0.025, method="linear")),
                "upper_95": float(np.quantile(regional[index], 0.975, method="linear")),
            }
        )

    return {
        "draw_specific_gp": {
            "distance_metric": "Geodesic",
            "earth_radius_km": EARTH_RADIUS_KM,
            "correlation_family": "BasicExponential",
            "training_coordinates": coordinates.tolist(),
            "target_coordinates": target.tolist(),
            "draws": draws,
        },
        "regional_fixed_draws": {
            "site_covariates": site_covariates.tolist(),
            "parameter_order": ["log_location_intercept", "log_location_slope", "log_scale", "shape_kappa"],
            "draws": parameter_draws.tolist(),
            "credible_interval_width": 0.95,
            "summaries": summaries,
        },
    }


def gev_logpdf(value: float, location: float, scale: float, shape: float) -> float:
    """Return the Numerics-kappa GEV log density independently."""
    y = (value - location) / scale
    if abs(shape) > 1e-12:
        support = 1.0 - shape * y
        if support <= 0.0:
            return -math.inf
        y = -math.log(support) / shape
    if -y > math.log(np.finfo(float).max):
        return -math.inf
    return -(1.0 - shape) * y - math.exp(-y) - math.log(scale)


def pointwise_spatial_likelihood(data: np.ndarray, parameters: np.ndarray) -> np.ndarray:
    """Return row/year marginal log-likelihood terms for an intercept-only spatial GEV."""
    location = math.exp(parameters[0])
    scale = math.exp(parameters[1])
    shape = parameters[2]
    return np.array(
        [sum(gev_logpdf(value, location, scale, shape) for value in row) for row in data],
        dtype=float,
    )


def godambe_oracle() -> dict[str, object]:
    """Compute H, J, and H^-1 J H^-1 independently for one fixed spatial GEV."""
    seed = 20260835
    rng = np.random.default_rng(seed)
    parameters = np.array([math.log(20.0), math.log(3.0), 0.08])
    uniforms = rng.uniform(0.03, 0.97, size=(24, 3))
    location = math.exp(parameters[0])
    scale = math.exp(parameters[1])
    shape = parameters[2]
    data = location + scale / shape * (1.0 - (-np.log(uniforms)) ** shape)
    n_params = parameters.size
    steps = np.abs(parameters) * 1e-5 + 1e-5
    center_terms = pointwise_spatial_likelihood(data, parameters)
    center = float(np.sum(center_terms))
    hessian = np.zeros((n_params, n_params))
    for row in range(n_params):
        for column in range(row, n_params):
            if row == column:
                plus = parameters.copy()
                minus = parameters.copy()
                plus[row] += steps[row]
                minus[row] -= steps[row]
                value = (
                    np.sum(pointwise_spatial_likelihood(data, plus))
                    - 2.0 * center
                    + np.sum(pointwise_spatial_likelihood(data, minus))
                ) / steps[row] ** 2
            else:
                pp = parameters.copy()
                pm = parameters.copy()
                mp = parameters.copy()
                mm = parameters.copy()
                pp[row] += steps[row]
                pp[column] += steps[column]
                pm[row] += steps[row]
                pm[column] -= steps[column]
                mp[row] -= steps[row]
                mp[column] += steps[column]
                mm[row] -= steps[row]
                mm[column] -= steps[column]
                value = (
                    np.sum(pointwise_spatial_likelihood(data, pp))
                    - np.sum(pointwise_spatial_likelihood(data, pm))
                    - np.sum(pointwise_spatial_likelihood(data, mp))
                    + np.sum(pointwise_spatial_likelihood(data, mm))
                ) / (4.0 * steps[row] * steps[column])
            hessian[row, column] = value
            hessian[column, row] = value
    scores = np.zeros((data.shape[0], n_params))
    for parameter in range(n_params):
        plus = parameters.copy()
        minus = parameters.copy()
        plus[parameter] += steps[parameter]
        minus[parameter] -= steps[parameter]
        scores[:, parameter] = (
            pointwise_spatial_likelihood(data, plus) - pointwise_spatial_likelihood(data, minus)
        ) / (2.0 * steps[parameter])
    variability = scores.T @ scores
    inverse_hessian = np.linalg.inv(hessian)
    sandwich = inverse_hessian @ variability @ inverse_hessian
    return {
        "seed": seed,
        "parameter_order": ["log_location", "log_scale", "shape_kappa"],
        "parameters": parameters.tolist(),
        "data": data.tolist(),
        "coordinates": [[0.0, 0.0], [10.0, 0.0], [0.0, 12.0]],
        "row_year_blocks": int(data.shape[0]),
        "finite_difference_relative_step": 1e-5,
        "finite_difference_steps": steps.tolist(),
        "h_sensitivity": hessian.tolist(),
        "j_variability": variability.tolist(),
        "sandwich_covariance": sandwich.tolist(),
    }


class MT19937:
    """Minimal independent port of the 2002 MT19937 integer stream."""

    def __init__(self, seed: int):
        self.state = [0] * 624
        self.state[0] = seed & 0xFFFFFFFF
        for index in range(1, 624):
            self.state[index] = (1812433253 * (self.state[index - 1] ^ (self.state[index - 1] >> 30)) + index) & 0xFFFFFFFF
        self.index = 624

    def uint32(self) -> int:
        """Return the next tempered unsigned integer."""
        if self.index >= 624:
            for item in range(624):
                y = (self.state[item] & 0x80000000) | (self.state[(item + 1) % 624] & 0x7FFFFFFF)
                self.state[item] = self.state[(item + 397) % 624] ^ (y >> 1) ^ (0x9908B0DF if y & 1 else 0)
            self.index = 0
        value = self.state[self.index]
        self.index += 1
        value ^= value >> 11
        value ^= (value << 7) & 0x9D2C5680
        value ^= (value << 15) & 0xEFC60000
        value ^= value >> 18
        return value & 0xFFFFFFFF

    def next(self, maximum: int) -> int:
        """Match the unbiased Numerics Next(maxExclusive) rejection rule."""
        threshold = 0xFFFFFFFF - (0xFFFFFFFF % maximum)
        value = self.uint32()
        while value >= threshold:
            value = self.uint32()
        return value % maximum


def percentile(values: np.ndarray, probability: float) -> float:
    """Return the zero-based linear-interpolation (Type 7) percentile."""
    return float(np.quantile(values, probability, method="linear"))


def bootstrap_oracle() -> dict[str, object]:
    """Fit independent MAP models to temporal whole-row wrapping-block replicates."""
    observations = 12
    block_size = 4
    replicates = 5
    seed = 24681357
    optimizer_seed = 20260837
    rows = np.arange(observations, dtype=float)
    data = np.column_stack(
        (
            10.0 + 0.5 * rows,
            20.0 + np.sin(rows / 2.0),
            30.0 - 0.25 * rows + 0.1 * (rows % 3.0),
        )
    )
    coordinates = np.array([[0.0, 0.0], [10.0, 0.0], [0.0, 12.0]])
    average_location = float(np.mean(data))
    average_scale = float(np.mean(np.std(data, axis=0, ddof=1)))
    bounds = [
        (math.log(0.01), float(math.ceil(math.log(abs(average_location)) + 3.0))),
        (math.log(0.01), float(math.ceil(math.log(abs(average_scale)) + 3.0))),
        (-0.5, 0.5),
    ]

    def objective(parameters: np.ndarray, sample: np.ndarray) -> float:
        """Return the independent flat-prior MAP objective for one homogeneous network."""
        pointwise = pointwise_spatial_likelihood(sample, parameters)
        return float(np.sum(pointwise)) if np.all(np.isfinite(pointwise)) else -math.inf

    def fit(sample: np.ndarray, fit_seed: int) -> tuple[np.ndarray, float]:
        """Obtain an independent high-precision SciPy optimum under the declared production bounds."""
        differential = optimize.differential_evolution(
            lambda values: -objective(np.asarray(values), sample),
            bounds,
            seed=fit_seed,
            tol=1e-11,
            polish=False,
            updating="immediate",
            workers=1,
        )
        refined = optimize.minimize(
            lambda values: -objective(np.asarray(values), sample),
            differential.x,
            method="L-BFGS-B",
            bounds=bounds,
        )
        optimum = np.asarray(refined.x if refined.success else differential.x)
        return optimum, objective(optimum, sample)

    full_optimum, full_maximum = fit(data, optimizer_seed)
    exceedance_probabilities = np.array([0.5, 0.1, 0.02])
    generator = MT19937(seed)
    outputs = []
    for replicate in range(replicates):
        selected: list[int] = []
        while len(selected) < observations:
            start = generator.next(observations)
            for offset in range(block_size):
                if len(selected) == observations:
                    break
                selected.append((start + offset) % observations)
        sample = data[selected, :]
        optimum, maximum = fit(sample, optimizer_seed + replicate + 1)
        physical = np.array([math.exp(optimum[0]), math.exp(optimum[1]), optimum[2]])
        quantiles = np.array(
            [gev_quantile(1.0 - probability, physical[0], physical[1], physical[2]) for probability in exceedance_probabilities]
        )
        outputs.append(
            {
                "replicate": replicate,
                "source_rows": selected,
                "optimizer_seed": optimizer_seed + replicate + 1,
                "optimum": optimum.tolist(),
                "maximum_log_posterior_without_uniform_constants": maximum,
                "physical_parameters": physical.tolist(),
                "quantiles": quantiles.tolist(),
            }
        )
    physical_fits = np.array([item["physical_parameters"] for item in outputs])
    quantile_fits = np.array([item["quantiles"] for item in outputs])
    parameter_intervals = {
        "location_95": [percentile(physical_fits[:, 0], 0.025), percentile(physical_fits[:, 0], 0.975)],
        "scale_95": [percentile(physical_fits[:, 1], 0.025), percentile(physical_fits[:, 1], 0.975)],
        "shape_95": [percentile(physical_fits[:, 2], 0.025), percentile(physical_fits[:, 2], 0.975)],
        "quantile_95": [
            [percentile(quantile_fits[:, column], 0.025), percentile(quantile_fits[:, column], 0.975)]
            for column in range(quantile_fits.shape[1])
        ],
        "regional_quantile_95": [
            [percentile(quantile_fits[:, column], 0.025), percentile(quantile_fits[:, column], 0.975)]
            for column in range(quantile_fits.shape[1])
        ],
    }
    return {
        "seed": seed,
        "optimizer_seed": optimizer_seed,
        "observations": observations,
        "sites": 3,
        "block_size": block_size,
        "wrapping": True,
        "replicates": replicates,
        "successful_replicates": replicates,
        "coordinates": coordinates.tolist(),
        "data": data.tolist(),
        "parameter_order": ["log_location_intercept", "log_scale_intercept", "shape_kappa"],
        "parameter_bounds": bounds,
        "prior": "independent proper Uniform bounds; constants do not change the optimum",
        "objective": "sum of all complete-site marginal Numerics-kappa GEV log densities; no copula",
        "independent_full_optimum": full_optimum.tolist(),
        "independent_full_maximum_log_posterior_without_uniform_constants": full_maximum,
        "exceedance_probabilities": exceedance_probabilities.tolist(),
        "independent_replicate_fits": outputs,
        "fitted_output_intervals": parameter_intervals,
        "fitted_output_relative_tolerance": 0.02,
        "tolerance_rationale": "Two-percent relative (and 0.02 absolute near zero) separates independent SciPy and unchanged production Differential Evolution convergence error from the materially wider five-replicate fitted-output spread; no observed bootstrap interval is narrower than this tolerance.",
    }


def vif_oracle() -> dict[str, object]:
    """Create a fixed complete-site matrix and its analytical VIF transformation."""
    first = np.array([10.0, 11.0, 13.0, 12.0, 15.0, 14.0, 16.0, 18.0, 17.0, 19.0])
    data = np.column_stack((first, 1.8 * first + np.array([0.2, -0.1, 0.3, -0.2, 0.1, 0.0, 0.2, -0.1, 0.1, 0.0]), 30.0 - 0.7 * first))
    matrix = np.corrcoef(data, rowvar=False)
    off_diagonal = [abs(matrix[row, column]) for row in range(3) for column in range(row)]
    average = float(np.mean(off_diagonal))
    vif = 1.0 + 2.0 * average
    return {
        "data": data.tolist(),
        "correlation_matrix": matrix.tolist(),
        "average_absolute_correlation": average,
        "vif": vif,
        "sqrt_vif": math.sqrt(vif),
        "transformation": "midpoint +/- original_half_width * sqrt(vif); scale lower bound truncated at zero",
    }


def main() -> None:
    """Generate and write the complete deterministic artifact."""
    artifact = {
        "metadata": {
            "artifact_id": "SPATIAL-CHUNK14-INDEPENDENT-001",
            "generated_utc": "2026-08-31T00:00:00Z",
            "python": platform.python_version(),
            "numpy": np.__version__,
            "scipy": scipy.__version__,
            "generator": str(Path(__file__).relative_to(ROOT)).replace("\\", "/"),
            "distance_conventions": {
                "Cartesian": "Euclidean distance in the coordinate unit",
                "Geodesic": "haversine distance in kilometres with Earth radius 6371.0088 km",
            },
            "absolute_tolerance": 1e-9,
            "absolute_tolerance_rationale": "Deterministic closed-form, dense-linear-algebra, and serialized-double comparisons; 1E-9 covers cross-runtime summation roundoff while remaining far below the reported effects.",
            "godambe_relative_tolerance": 2e-5,
            "godambe_tolerance_rationale": "The Python and production implementations differentiate the same accumulated row objective independently; 2E-5 relative covers finite-difference cancellation and step-rounding without regularizing H or the sandwich covariance.",
        },
        "correlations": {
            "basic_exponential": correlation_grid("BasicExponential", [20.0]),
            "powered_exponential": correlation_grid("PoweredExponential", [20.0, 1.6]),
            "spherical": correlation_grid("Spherical", [20.0]),
        },
        "cross_validation": cross_validation_oracles(),
        "prediction": prediction_oracles(),
        "godambe": godambe_oracle(),
        "bootstrap": bootstrap_oracle(),
        "variance_inflation": vif_oracle(),
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(artifact, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    print(f"artifact_sha256={hashlib.sha256(OUTPUT.read_bytes()).hexdigest()}")


if __name__ == "__main__":
    main()
