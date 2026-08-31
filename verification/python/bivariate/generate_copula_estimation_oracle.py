"""Generate an independent copula-estimation oracle for the RMC.BestFit bivariate tests.

``BivariateDistributionMLETests`` fits six bivariate copula families (Ali-Mikhail-Haq, Clayton,
Frank, Gumbel, Joe, and Gaussian) by maximum pseudo-likelihood (MPL, Weibull plotting-position
complements) and by inference from margins (IFM, Normal marginals fitted by maximum likelihood) and
compares the fitted dependence parameter with historical R ``copula`` package values embedded in the
test source. This script transcribes those fixtures from the test source and adds an independently
generated N=1000 Student-t copula fixture for MPL and IFM. It implements every copula
density independently in closed form (each density is self-checked against the numerical mixed
partial derivative of its copula distribution function), maximizes the pseudo- or IFM log likelihood
with SciPy, and writes a committed artifact that records the data, the independent optimum, the
maximized log likelihood, the closed-form marginal maximum-likelihood estimates, and the historical
R target with its difference from the independent optimum.

RMC.BestFit verification tests consume only the committed JSON artifact and never import Python or
SciPy at test runtime. No runtime random sampling is performed.
"""

from __future__ import annotations

import hashlib
import json
import math
import platform
import re
from datetime import date
from pathlib import Path
from typing import Any, Callable

import numpy as np
import scipy
from scipy import optimize, special, stats


SCRIPT_PATH = Path(__file__).resolve()
REPOSITORY_ROOT = SCRIPT_PATH.parents[3]
SOURCE_PATH = (
    REPOSITORY_ROOT
    / "src"
    / "RMC.BestFit.Verification"
    / "Bivariate"
    / "BivariateDistributionMLETests.cs"
)
OUTPUT_PATH = REPOSITORY_ROOT / "verification" / "data" / "bivariate" / "copula-estimation-oracle.json"

EPSILON = 1e-9
SEARCH_BOUNDS = {
    "AliMikhailHaq": (-1.0 + EPSILON, 1.0 - EPSILON),
    "Clayton": (1e-6, 30.0),
    "Frank": (-40.0, 40.0),
    "Gumbel": (1.0 + EPSILON, 30.0),
    "Joe": (1.0 + EPSILON, 30.0),
    "Normal": (-1.0 + EPSILON, 1.0 - EPSILON),
}
FAMILY_BY_TEST_NAME = {
    "AMH": "AliMikhailHaq",
    "Clayton": "Clayton",
    "Frank": "Frank",
    "Gumbel": "Gumbel",
    "Joe": "Joe",
    "Normal": "Normal",
}
SELF_CHECK_POINTS = [(0.2, 0.3), (0.5, 0.5), (0.7, 0.4), (0.9, 0.8), (0.35, 0.65)]
SELF_CHECK_THETAS = {
    "AliMikhailHaq": [-0.6, 0.3, 0.8],
    "Clayton": [0.5, 1.5, 4.0],
    "Frank": [-3.0, 2.0, 8.0],
    "Gumbel": [1.2, 2.0, 3.5],
    "Joe": [1.3, 2.0, 4.0],
    "Normal": [-0.5, 0.3, 0.8],
}
SELF_CHECK_TOLERANCE = 1e-5
STUDENT_T_SEED = 20260830
STUDENT_T_SAMPLE_SIZE = 1000
STUDENT_T_PARENT = (0.8, 4.0)
STUDENT_T_CROSS_SOLVER_PARAMETER_TOLERANCES = (5e-4, 2e-2)
STUDENT_T_CROSS_SOLVER_LOGLIK_TOLERANCE = 1e-5


# --------------------------------------------------------------------------- copula functions
def cdf_amh(u: np.ndarray, v: np.ndarray, theta: float) -> np.ndarray:
    """Ali-Mikhail-Haq copula distribution function."""

    return u * v / (1.0 - theta * (1.0 - u) * (1.0 - v))


def logpdf_amh(u: np.ndarray, v: np.ndarray, theta: float) -> np.ndarray:
    """Ali-Mikhail-Haq copula log density (closed form derived from the mixed partial)."""

    d = 1.0 - theta * (1.0 - u) * (1.0 - v)
    numerator = (1.0 - theta + 2.0 * theta * v) * d - 2.0 * theta * (1.0 - u) * v * (1.0 - theta + theta * v)
    return np.log(numerator) - 3.0 * np.log(d)


def cdf_clayton(u: np.ndarray, v: np.ndarray, theta: float) -> np.ndarray:
    """Clayton copula distribution function for theta > 0."""

    return np.power(np.power(u, -theta) + np.power(v, -theta) - 1.0, -1.0 / theta)


def logpdf_clayton(u: np.ndarray, v: np.ndarray, theta: float) -> np.ndarray:
    """Clayton copula log density for theta > 0."""

    s = np.power(u, -theta) + np.power(v, -theta) - 1.0
    return np.log1p(theta) + (-theta - 1.0) * (np.log(u) + np.log(v)) + (-1.0 / theta - 2.0) * np.log(s)


def cdf_frank(u: np.ndarray, v: np.ndarray, theta: float) -> np.ndarray:
    """Frank copula distribution function for theta != 0."""

    return -np.log1p(np.expm1(-theta * u) * np.expm1(-theta * v) / np.expm1(-theta)) / theta


def logpdf_frank(u: np.ndarray, v: np.ndarray, theta: float) -> np.ndarray:
    """Frank copula log density for theta != 0 (independence at theta = 0)."""

    if abs(theta) < 1e-10:
        return np.zeros_like(u)
    g = -np.expm1(-theta)
    denominator = g - (-np.expm1(-theta * u)) * (-np.expm1(-theta * v))
    return np.log(abs(theta)) + np.log(abs(g)) - theta * (u + v) - 2.0 * np.log(abs(denominator))


def cdf_gumbel(u: np.ndarray, v: np.ndarray, theta: float) -> np.ndarray:
    """Gumbel copula distribution function for theta >= 1."""

    s = np.power(-np.log(u), theta) + np.power(-np.log(v), theta)
    return np.exp(-np.power(s, 1.0 / theta))


def logpdf_gumbel(u: np.ndarray, v: np.ndarray, theta: float) -> np.ndarray:
    """Gumbel copula log density for theta >= 1."""

    lu = -np.log(u)
    lv = -np.log(v)
    s = np.power(lu, theta) + np.power(lv, theta)
    a = np.power(s, 1.0 / theta)
    return -a - np.log(u) - np.log(v) + (theta - 1.0) * (np.log(lu) + np.log(lv)) + (1.0 / theta - 2.0) * np.log(s) + np.log(a + theta - 1.0)


def cdf_joe(u: np.ndarray, v: np.ndarray, theta: float) -> np.ndarray:
    """Joe copula distribution function for theta >= 1."""

    a = np.power(1.0 - u, theta)
    b = np.power(1.0 - v, theta)
    return 1.0 - np.power(a + b - a * b, 1.0 / theta)


def logpdf_joe(u: np.ndarray, v: np.ndarray, theta: float) -> np.ndarray:
    """Joe copula log density for theta >= 1."""

    a = np.power(1.0 - u, theta)
    b = np.power(1.0 - v, theta)
    s = a + b - a * b
    return (1.0 / theta - 2.0) * np.log(s) + (theta - 1.0) * (np.log1p(-u) + np.log1p(-v)) + np.log(theta - 1.0 + s)


def cdf_normal(u: np.ndarray, v: np.ndarray, theta: float) -> np.ndarray:
    """Gaussian copula distribution function for -1 < theta < 1."""

    x = stats.norm.ppf(u)
    y = stats.norm.ppf(v)
    distribution = stats.multivariate_normal(mean=[0.0, 0.0], cov=[[1.0, theta], [theta, 1.0]])
    points = np.column_stack([np.atleast_1d(x), np.atleast_1d(y)])
    return np.asarray(distribution.cdf(points), dtype=float).reshape(np.shape(u))


def logpdf_normal(u: np.ndarray, v: np.ndarray, theta: float) -> np.ndarray:
    """Gaussian copula log density for -1 < theta < 1."""

    x = stats.norm.ppf(u)
    y = stats.norm.ppf(v)
    r2 = 1.0 - theta * theta
    return -0.5 * np.log(r2) - (theta * theta * (x * x + y * y) - 2.0 * theta * x * y) / (2.0 * r2)


def logpdf_student_t(u: np.ndarray, v: np.ndarray, rho: float, nu: float) -> np.ndarray:
    """Student-t copula log density in the physical parameter order [rho, nu]."""

    x = stats.t.ppf(u, df=nu)
    y = stats.t.ppf(v, df=nu)
    r2 = rho * rho
    quadratic = x * x - 2.0 * rho * x * y + y * y
    log_density = (
        special.gammaln((nu + 2.0) / 2.0)
        + special.gammaln(nu / 2.0)
        - 2.0 * special.gammaln((nu + 1.0) / 2.0)
        - 0.5 * np.log1p(-r2)
    )
    log_density -= ((nu + 2.0) / 2.0) * np.log1p(quadratic / (nu * (1.0 - r2)))
    log_density += ((nu + 1.0) / 2.0) * np.log1p(x * x / nu)
    log_density += ((nu + 1.0) / 2.0) * np.log1p(y * y / nu)
    return log_density


FAMILIES: dict[str, tuple[Callable[..., np.ndarray], Callable[..., np.ndarray]]] = {
    "AliMikhailHaq": (cdf_amh, logpdf_amh),
    "Clayton": (cdf_clayton, logpdf_clayton),
    "Frank": (cdf_frank, logpdf_frank),
    "Gumbel": (cdf_gumbel, logpdf_gumbel),
    "Joe": (cdf_joe, logpdf_joe),
    "Normal": (cdf_normal, logpdf_normal),
}


def self_check_density(family: str) -> float:
    """Compare each closed-form density with the numerical mixed partial of its distribution function."""

    cdf, logpdf = FAMILIES[family]
    h = 1e-4
    worst = 0.0
    for theta in SELF_CHECK_THETAS[family]:
        for u, v in SELF_CHECK_POINTS:
            if family == "Normal":
                x, y = stats.norm.ppf(u), stats.norm.ppf(v)
                distribution = stats.multivariate_normal(mean=[0.0, 0.0], cov=[[1.0, theta], [theta, 1.0]])
                numerical = distribution.pdf([x, y]) / (stats.norm.pdf(x) * stats.norm.pdf(y))
            else:
                numerical = (
                    cdf(np.array(u + h), np.array(v + h), theta)
                    - cdf(np.array(u + h), np.array(v - h), theta)
                    - cdf(np.array(u - h), np.array(v + h), theta)
                    + cdf(np.array(u - h), np.array(v - h), theta)
                ) / (4.0 * h * h)
            analytical = math.exp(float(logpdf(np.array(u), np.array(v), theta)))
            relative = abs(analytical - float(numerical)) / abs(float(numerical))
            worst = max(worst, relative)
    if worst > SELF_CHECK_TOLERANCE:
        raise RuntimeError(f"{family}: closed-form density disagrees with the numerical mixed partial ({worst})")
    return worst


# --------------------------------------------------------------------------- fixtures
def parse_fixtures(source: str) -> list[dict[str, Any]]:
    """Transcribe the twelve test fixtures (data, family, method, R target) from the C# source."""

    method_pattern = re.compile(r"public void Test_(\w+?)_(MPL|IFM)\(\)")
    fixtures: list[dict[str, Any]] = []
    matches = list(method_pattern.finditer(source))
    for index, match in enumerate(matches):
        start = match.end()
        end = matches[index + 1].start() if index + 1 < len(matches) else len(source)
        body = source[start:end]
        data_x = re.search(r"var dataX = new double\[\] \{([^}]*)\};", body)
        data_y = re.search(r"var dataY = new double\[\] \{([^}]*)\};", body)
        copula_type = re.search(r"CopulaType\.(\w+)", body)
        target = re.search(r"Assert\.AreEqual\(([-0-9.eE]+), bivariateDist\.Copula\.Theta, ([-0-9.eE]+)", body)
        if not (data_x and data_y and copula_type and target):
            raise RuntimeError(f"Could not parse fixture Test_{match.group(1)}_{match.group(2)}")
        family = FAMILY_BY_TEST_NAME[match.group(1)]
        if copula_type.group(1) != family:
            raise RuntimeError(f"Copula type {copula_type.group(1)} does not match family {family}")
        fixtures.append(
            {
                "test_method": f"Test_{match.group(1)}_{match.group(2)}",
                "family": family,
                "method": match.group(2),
                "data_x": [float(value) for value in data_x.group(1).split(",")],
                "data_y": [float(value) for value in data_y.group(1).split(",")],
                "r_copula_target": float(target.group(1)),
                "r_copula_tolerance": float(target.group(2)),
            }
        )
    if len(fixtures) != 12:
        raise RuntimeError(f"Expected 12 fixtures, parsed {len(fixtures)}")
    return fixtures


def weibull_complements(values: np.ndarray) -> np.ndarray:
    """Return Weibull plotting-position nonexceedance values rank/(n + 1) (ascending ranks)."""

    n = len(values)
    ranks = stats.rankdata(values, method="ordinal")
    return ranks / (n + 1.0)


def normal_mle(values: np.ndarray) -> tuple[float, float]:
    """Closed-form Normal maximum-likelihood mean and (1/n) standard deviation."""

    mean = float(np.mean(values))
    sigma = float(math.sqrt(np.mean(np.square(values - mean))))
    return mean, sigma


def maximize(family: str, u: np.ndarray, v: np.ndarray) -> dict[str, Any]:
    """Maximize the copula log likelihood over the family support with a grid scan plus bounded refinement."""

    _, logpdf = FAMILIES[family]
    lower, upper = SEARCH_BOUNDS[family]

    def negative(theta: float) -> float:
        """Negative log likelihood, infinite when the density is not finite."""

        if family == "Frank" and abs(theta) < 1e-10:
            return 0.0
        values = logpdf(u, v, float(theta))
        if not np.all(np.isfinite(values)):
            return float("inf")
        return float(-np.sum(values))

    grid = np.linspace(lower, upper, 401)
    objective = np.array([negative(theta) for theta in grid])
    best_index = int(np.nanargmin(np.where(np.isfinite(objective), objective, np.inf)))
    bracket_lower = grid[max(best_index - 1, 0)]
    bracket_upper = grid[min(best_index + 1, len(grid) - 1)]
    result = optimize.minimize_scalar(
        negative,
        bounds=(bracket_lower, bracket_upper),
        method="bounded",
        options={"xatol": 1e-12, "maxiter": 1000},
    )
    if not result.success:
        raise RuntimeError(f"{family}: bounded minimization failed: {result.message}")
    theta = float(result.x)
    return {
        "theta": theta,
        "maximum_log_likelihood": float(-negative(theta)),
        "search_bounds": [lower, upper],
        "grid_points": int(len(grid)),
        "refinement_bracket": [float(bracket_lower), float(bracket_upper)],
        "optimizer": "scipy.optimize.minimize_scalar(method='bounded', xatol=1e-12) after a 401-point grid scan",
    }


def generate_student_t_data() -> tuple[np.ndarray, np.ndarray]:
    """Generate N=1000 Normal-margin observations from a Student-t copula independently with NumPy/SciPy."""

    rho, nu = STUDENT_T_PARENT
    rng = np.random.default_rng(STUDENT_T_SEED)
    normal = rng.multivariate_normal([0.0, 0.0], [[1.0, rho], [rho, 1.0]], STUDENT_T_SAMPLE_SIZE)
    scale = np.sqrt(rng.chisquare(nu, STUDENT_T_SAMPLE_SIZE) / nu)
    latent = normal / scale[:, np.newaxis]
    uniforms = stats.t.cdf(latent, df=nu)
    x = stats.norm.ppf(uniforms[:, 0], loc=100.0, scale=15.0)
    y = stats.norm.ppf(uniforms[:, 1], loc=80.0, scale=25.0)
    return x, y


def self_check_student_t_density() -> float:
    """Compare the independent closed form with SciPy's bivariate/univariate t density ratio."""

    worst = 0.0
    for rho, nu in ((-0.4, 3.5), (0.3, 8.0), (0.8, 4.0)):
        for u, v in SELF_CHECK_POINTS:
            x, y = stats.t.ppf([u, v], df=nu)
            scipy_log = stats.multivariate_t.logpdf(
                [x, y], loc=[0.0, 0.0], shape=[[1.0, rho], [rho, 1.0]], df=nu
            ) - stats.t.logpdf(x, df=nu) - stats.t.logpdf(y, df=nu)
            closed_log = float(logpdf_student_t(np.array(u), np.array(v), rho, nu))
            worst = max(worst, abs(math.exp(closed_log) - math.exp(scipy_log)) / math.exp(scipy_log))
    if worst > 1e-12:
        raise RuntimeError(f"StudentT: closed-form density disagrees with SciPy's density ratio ({worst})")
    return worst


def maximize_student_t(u: np.ndarray, v: np.ndarray) -> dict[str, Any]:
    """Maximize the two-coordinate Student-t copula likelihood with deterministic global and local solvers."""

    bounds = [(-1.0 + EPSILON, 1.0 - EPSILON), (2.0 + 1e-10, 30.0)]

    def negative(parameters: np.ndarray) -> float:
        values = logpdf_student_t(u, v, float(parameters[0]), float(parameters[1]))
        if not np.all(np.isfinite(values)):
            return float("inf")
        return float(-np.sum(values))

    global_result = optimize.differential_evolution(
        negative,
        bounds=bounds,
        seed=STUDENT_T_SEED,
        popsize=20,
        maxiter=500,
        tol=1e-10,
        polish=False,
        updating="immediate",
        workers=1,
    )
    if not global_result.success:
        raise RuntimeError(f"StudentT: differential evolution failed: {global_result.message}")
    local_result = optimize.minimize(
        negative,
        global_result.x,
        method="L-BFGS-B",
        bounds=bounds,
        options={"ftol": 1e-14, "gtol": 1e-8, "maxiter": 2000, "maxls": 50},
    )
    if not local_result.success:
        raise RuntimeError(f"StudentT: L-BFGS-B refinement failed: {local_result.message}")
    return {
        "parameters": [float(value) for value in local_result.x],
        "maximum_log_likelihood": float(-local_result.fun),
        "search_bounds": bounds,
        "optimizer": (
            "scipy.optimize.differential_evolution(seed=20260830, popsize=20, maxiter=500, tol=1e-10) "
            "followed by L-BFGS-B(ftol=1e-14, gtol=1e-8)"
        ),
    }


def main() -> None:
    """Parse the fixtures, fit every copula independently, and write the artifact."""

    source = SOURCE_PATH.read_text(encoding="utf-8")
    fixtures = parse_fixtures(source)
    self_checks = {family: self_check_density(family) for family in FAMILIES}
    self_checks["StudentT"] = self_check_student_t_density()

    records: list[dict[str, Any]] = []
    for fixture in fixtures:
        x = np.asarray(fixture["data_x"], dtype=float)
        y = np.asarray(fixture["data_y"], dtype=float)
        if fixture["method"] == "MPL":
            u = weibull_complements(x)
            v = weibull_complements(y)
            marginals = None
        else:
            mu_x, sigma_x = normal_mle(x)
            mu_y, sigma_y = normal_mle(y)
            u = stats.norm.cdf((x - mu_x) / sigma_x)
            v = stats.norm.cdf((y - mu_y) / sigma_y)
            marginals = {"mu_x": mu_x, "sigma_x": sigma_x, "mu_y": mu_y, "sigma_y": sigma_y}
        fit = maximize(fixture["family"], u, v)
        records.append(
            {
                **fixture,
                "sample_size": int(len(x)),
                "pseudo_observations": (
                    "Weibull plotting-position complements rank/(n + 1) with ascending ranks"
                    if fixture["method"] == "MPL"
                    else "Normal marginal CDFs at the closed-form maximum-likelihood mean and (1/n) standard deviation"
                ),
                "marginal_mle": marginals,
                "independent_theta": fit["theta"],
                "independent_maximum_log_likelihood": fit["maximum_log_likelihood"],
                "independent_minus_r_target": fit["theta"] - fixture["r_copula_target"],
                "search_bounds": fit["search_bounds"],
                "optimizer": fit["optimizer"],
                "grid_points": fit["grid_points"],
                "refinement_bracket": fit["refinement_bracket"],
            }
        )

    student_x, student_y = generate_student_t_data()
    for method in ("MPL", "IFM"):
        if method == "MPL":
            student_u = weibull_complements(student_x)
            student_v = weibull_complements(student_y)
            student_marginals = None
        else:
            mu_x, sigma_x = normal_mle(student_x)
            mu_y, sigma_y = normal_mle(student_y)
            student_u = stats.norm.cdf((student_x - mu_x) / sigma_x)
            student_v = stats.norm.cdf((student_y - mu_y) / sigma_y)
            student_marginals = {"mu_x": mu_x, "sigma_x": sigma_x, "mu_y": mu_y, "sigma_y": sigma_y}
        student_fit = maximize_student_t(student_u, student_v)
        records.append(
            {
                "test_method": f"StudentT_{method}",
                "family": "StudentT",
                "method": method,
                "data_x": [float(value) for value in student_x],
                "data_y": [float(value) for value in student_y],
                "sample_size": STUDENT_T_SAMPLE_SIZE,
                "generator_seed": STUDENT_T_SEED,
                "generator_parent_parameters": list(STUDENT_T_PARENT),
                "generator_parameter_order": ["rho", "nu"],
                "generator_marginals": {"x": ["Normal", 100.0, 15.0], "y": ["Normal", 80.0, 25.0]},
                "coordinate_order": ["rho", "nu"],
                "pseudo_observations": (
                    "Weibull plotting-position complements rank/(n + 1) with ascending ranks"
                    if method == "MPL"
                    else "Normal marginal CDFs at the closed-form maximum-likelihood mean and (1/n) standard deviation"
                ),
                "marginal_mle": student_marginals,
                "independent_parameters": student_fit["parameters"],
                "independent_maximum_log_likelihood": student_fit["maximum_log_likelihood"],
                "search_bounds": student_fit["search_bounds"],
                "optimizer": student_fit["optimizer"],
                "cross_solver_parameter_tolerances": list(STUDENT_T_CROSS_SOLVER_PARAMETER_TOLERANCES),
                "cross_solver_log_likelihood_tolerance": STUDENT_T_CROSS_SOLVER_LOGLIK_TOLERANCE,
            }
        )

    payload = {
        "metadata": {
            "generated": date.today().isoformat(),
            "generator": "verification/python/bivariate/generate_copula_estimation_oracle.py",
            "generator_sha256": hashlib.sha256(SCRIPT_PATH.read_bytes()).hexdigest(),
            "python": platform.python_version(),
            "numpy": np.__version__,
            "scipy": scipy.__version__,
            "fixture_source": "src/RMC.BestFit.Verification/Bivariate/BivariateDistributionMLETests.cs",
            "fixture_source_sha256": hashlib.sha256(SOURCE_PATH.read_bytes()).hexdigest(),
            "seed": "historical cells: deterministic-no-random-sampling; Student-t generator: 20260830",
            "density_self_check": (
                "each closed-form density equals the numerical mixed partial derivative of its distribution "
                "function (Gaussian: bivariate-normal density ratio) at five points and three parameters; "
                "Student-t equals SciPy's bivariate-t over univariate-t density ratio"
            ),
            "density_self_check_max_relative_error": self_checks,
            "density_self_check_tolerance": SELF_CHECK_TOLERANCE,
            "r_copula_targets": "historical R copula package values embedded in the C# test source (package version not recorded)",
        },
        "fixtures": records,
    }
    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(payload, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    print(f"Wrote {OUTPUT_PATH}")
    for record in records:
        if record["family"] == "StudentT":
            print(
                f"{record['test_method']:<22} parameters={record['independent_parameters']} "
                f"loglik={record['independent_maximum_log_likelihood']:.6f}"
            )
        else:
            print(
                f"{record['test_method']:<22} theta={record['independent_theta']:.7f} "
                f"R={record['r_copula_target']:.7f} diff={record['independent_minus_r_target']:+.2e} "
                f"loglik={record['independent_maximum_log_likelihood']:.6f}"
            )
    print("self-checks:", {k: f"{v:.2e}" for k, v in self_checks.items()})


if __name__ == "__main__":
    main()
