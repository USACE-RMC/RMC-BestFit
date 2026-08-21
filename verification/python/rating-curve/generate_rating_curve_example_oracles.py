"""Generate the rating-curve example fixtures and likelihood oracle for RMC.BestFit.

The synthetic rating-curve examples shipped in ``examples/6-rating-curve-analysis``
were generated in a workbook from a Manning triangular channel with one or two
rectangular overbank controls. This script reads the workbook's uniform draws
(``r1`` for stage, ``r2`` for the log10-discharge error), regenerates the three
cases from the documented recipe, asserts that the regenerated values match the
workbook, and writes two committed artifacts:

* ``rating-curve-example-fixtures.json`` - dates, stage, the three discharge
  series, the generating parameters in the BestFit layout, the true curve on a
  stage grid, the replicated BestFit default bounds, and an independent SciPy
  conditional maximum-likelihood optimum (least squares on log10 discharge with
  the profiled scale) for every case;
* ``rating-curve-likelihood-oracle.json`` - per-observation log-space Gaussian
  terms, base-10 change-of-variables terms, and discharge-space lognormal
  density terms evaluated at the generating parameters and at the independent
  optimum, for the TR-043 observation-measure verification.

RMC.BestFit verification tests consume only the committed JSON artifacts and
never import Python or SciPy at test runtime. No runtime random sampling is
performed: the only randomness is the workbook's stored draws.
"""

from __future__ import annotations

import hashlib
import json
import math
import platform
import re
import zipfile
from datetime import date, timedelta
from pathlib import Path
from typing import Any
from xml.etree import ElementTree

import numpy as np
import scipy
from scipy import optimize, stats


SCRIPT_PATH = Path(__file__).resolve()
REPOSITORY_ROOT = SCRIPT_PATH.parents[3]
WORKBOOK_PATH = REPOSITORY_ROOT / "examples" / "6-rating-curve-analysis" / "Synthetic Data.xlsx"
PROJECT_PATH = (
    REPOSITORY_ROOT / "examples" / "6-rating-curve-analysis" / "synthetic-rating-curve-examples.bestfit"
)
OUTPUT_DIRECTORY = REPOSITORY_ROOT / "verification" / "data" / "rating-curve"
FIXTURES_PATH = OUTPUT_DIRECTORY / "rating-curve-example-fixtures.json"
LIKELIHOOD_PATH = OUTPUT_DIRECTORY / "rating-curve-likelihood-oracle.json"

START_DATE = date(2000, 1, 1)
OBSERVATIONS = 300
STAGE_MINIMUM = 1.0
STAGE_RANGE = 19.0
LOG10_SIGMA = 0.05
RECIPE_TOLERANCE = 1e-9
LN10 = math.log(10.0)
TRUE_CURVE_STAGES = [1.5 + 0.5 * index for index in range(37)]
MULTISTART_SEED = 20260821
MULTISTART_COUNT = 24

# Control definitions transcribed from the workbook parameter blocks.
TRIANGULAR_CHANNEL = {
    "name": "Triangular channel (Manning n 0.035, slope 0.05, side slope 0.5)",
    "activation_stage": 1.0,
    "alpha": 1.7534628349561987,
    "beta": 8.0 / 3.0,
    "workbook_log10_alpha": 0.24389656534469958,
}
OVERBANK_ONE = {
    "name": "Rectangular overbank (Manning n 0.035, slope 0.05, width 20 ft)",
    "activation_stage": 10.0,
    "alpha": 883.6898943878617,
    "beta": 1.67,
    "workbook_log10_alpha": 2.9462998885606555,
}
OVERBANK_TWO = {
    "name": "Rectangular overbank (Manning n 0.035, slope 0.05, width 50 ft)",
    "activation_stage": 15.0,
    "alpha": 4069.4267574438463,
    "beta": 1.67,
    "workbook_log10_alpha": 3.6095332363473847,
}
CASES = [
    ("one_segment", "1 Segment", [TRIANGULAR_CHANNEL]),
    ("two_segment", "2 Segment", [TRIANGULAR_CHANNEL, OVERBANK_ONE]),
    ("three_segment", "3 Segment", [TRIANGULAR_CHANNEL, OVERBANK_ONE, OVERBANK_TWO]),
]


def sha256_of(path: Path) -> str:
    """Return the SHA-256 hex digest of a file."""

    return hashlib.sha256(path.read_bytes()).hexdigest()


def as_floats(values: Any) -> list[float]:
    """Convert array-like values to JSON-safe Python floats."""

    return [float(value) for value in np.asarray(values, dtype=float).ravel()]


def column_index(reference: str) -> int:
    """Convert a cell reference such as ``H3`` to a zero-based column index."""

    letters = re.match(r"[A-Z]+", reference).group(0)
    index = 0
    for letter in letters:
        index = index * 26 + (ord(letter) - ord("A") + 1)
    return index - 1


def read_workbook_sheets(path: Path) -> dict[str, dict[int, dict[int, float]]]:
    """Read every numeric cell of every worksheet with the standard library only."""

    namespace = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
    relationship_namespace = {
        "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
    }
    sheets: dict[str, dict[int, dict[int, float]]] = {}
    with zipfile.ZipFile(path) as archive:
        workbook = ElementTree.fromstring(archive.read("xl/workbook.xml"))
        relationships = ElementTree.fromstring(archive.read("xl/_rels/workbook.xml.rels"))
        targets = {
            relationship.attrib["Id"]: relationship.attrib["Target"]
            for relationship in relationships
        }
        for sheet in workbook.find("m:sheets", namespace):
            name = sheet.attrib["name"]
            relationship_id = sheet.attrib[
                "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id"
            ]
            target = targets[relationship_id]
            member = target if target.startswith("xl/") else "xl/" + target.lstrip("/")
            root = ElementTree.fromstring(archive.read(member))
            cells: dict[int, dict[int, float]] = {}
            for row in root.iter("{%s}row" % namespace["m"]):
                row_number = int(row.attrib["r"])
                for cell in row.iter("{%s}c" % namespace["m"]):
                    value = cell.find("m:v", namespace)
                    if value is None or cell.attrib.get("t") == "s":
                        continue
                    try:
                        numeric = float(value.text)
                    except (TypeError, ValueError):
                        continue
                    cells.setdefault(row_number, {})[column_index(cell.attrib["r"])] = numeric
            sheets[name] = cells
    del relationship_namespace
    return sheets


def read_sheet_columns(cells: dict[int, dict[int, float]]) -> dict[str, np.ndarray]:
    """Extract the draw, stage, and discharge columns of one workbook sheet."""

    # The header row (row 2) holds text labels, which are shared strings and are not
    # parsed as numbers; the numeric layout is fixed: G = index, H = r1, I = r2, J = h,
    # then one log10-flow column per control, the log10 total for multi-control
    # sheets, and finally the noisy discharge Q+e in the last populated column.
    rows = sorted(row for row in cells if row >= 3 and 7 in cells[row] and 8 in cells[row])
    if len(rows) != OBSERVATIONS:
        raise RuntimeError(f"Expected {OBSERVATIONS} data rows, found {len(rows)}.")
    r1 = np.array([cells[row][7] for row in rows], dtype=float)
    r2 = np.array([cells[row][8] for row in rows], dtype=float)
    stage = np.array([cells[row][9] for row in rows], dtype=float)
    last_column = max(max(cells[row]) for row in rows)
    discharge = np.array([cells[row][last_column] for row in rows], dtype=float)
    log10_total = np.array([cells[row][last_column - 1] for row in rows], dtype=float)
    return {"r1": r1, "r2": r2, "stage": stage, "discharge": discharge, "log10_total": log10_total}


def true_discharge(stage: np.ndarray, controls: list[dict[str, float]]) -> np.ndarray:
    """Evaluate the addition-mode piecewise power law for the given controls."""

    total = np.zeros_like(stage, dtype=float)
    for control in controls:
        depth = stage - control["activation_stage"]
        active = depth > 0.0
        total = total + np.where(active, control["alpha"] * np.power(np.clip(depth, 0.0, None), control["beta"]), 0.0)
    return total


def bestfit_parameters(controls: list[dict[str, float]], sigma: float) -> list[float]:
    """Return the generating parameters in the BestFit layout."""

    values: list[float] = []
    for control in controls:
        values.extend([control["activation_stage"], math.log10(control["alpha"]), control["beta"]])
    values.append(sigma)
    return values


def parameter_names(segments: int) -> list[str]:
    """Return BestFit-style parameter names for the layout."""

    names = ["h1", "log10_alpha1", "beta1"]
    for segment in range(2, segments + 1):
        names.extend([f"h{segment}", f"log10_alpha{segment}", f"beta{segment}"])
    names.append("sigma")
    return names


def predict_from_parameters(parameters: np.ndarray, stage: np.ndarray, segments: int) -> np.ndarray:
    """Evaluate the BestFit addition-mode curve from a BestFit-layout parameter vector."""

    total = np.zeros_like(stage, dtype=float)
    for segment in range(segments):
        activation, log10_alpha, beta = parameters[3 * segment : 3 * segment + 3]
        depth = stage - activation
        active = depth > 0.0
        total = total + np.where(active, math.pow(10.0, log10_alpha) * np.power(np.clip(depth, 0.0, None), beta), 0.0)
    return total


def default_bounds(stage: np.ndarray, discharge: np.ndarray, segments: int) -> tuple[list[float], list[float]]:
    """Replicate the BestFit default flat-prior bounds used for the rating-curve search."""

    minimum_stage = float(np.min(stage))
    span = float(np.max(stage) - minimum_stage)
    if span < 1.0:
        span = 1.0
    log_q = np.log10(discharge)
    sigma_upper = math.ceil(float(np.std(log_q, ddof=1)) * 3.0)
    lower = [minimum_stage - span, -10.0, 0.0]
    upper = [minimum_stage + 0.1 * span, 10.0, 5.0]
    if segments >= 2:
        lower += [minimum_stage + 0.2 * span, -10.0, 0.0]
        upper += [minimum_stage + 0.7 * span, 10.0, 5.0]
    if segments >= 3:
        lower += [minimum_stage + 0.5 * span, -10.0, 0.0]
        upper += [minimum_stage + span, 10.0, 5.0]
    lower.append(np.finfo(float).eps)
    upper.append(float(sigma_upper))
    return lower, upper


def log_space_log_likelihood(residuals: np.ndarray, sigma: float) -> float:
    """Return the BestFit log-space Gaussian log likelihood (RC.4)."""

    return float(np.sum(stats.norm.logpdf(residuals, loc=0.0, scale=sigma)))


def independent_mle(
    stage: np.ndarray,
    discharge: np.ndarray,
    segments: int,
    lower: list[float],
    upper: list[float],
    truth: list[float],
) -> dict[str, Any]:
    """Locate the conditional maximum-likelihood optimum with a deterministic multistart."""

    log10_q = np.log10(discharge)
    shape_lower = np.array(lower[:-1], dtype=float)
    shape_upper = np.array(upper[:-1], dtype=float)
    # Keep least-squares starts strictly inside the box so the exponent bound of zero
    # never participates; the optimum is interior for every case.
    search_lower = shape_lower.copy()
    search_upper = shape_upper.copy()
    for segment in range(segments):
        search_lower[3 * segment + 2] = max(search_lower[3 * segment + 2], 1e-3)
    observations = len(stage)

    def residuals(theta: np.ndarray) -> np.ndarray:
        """Return log10 residuals, with an explicit penalty for invalid curves."""

        predicted = predict_from_parameters(theta, stage, segments)
        if segments >= 2 and not all(theta[3 * k] < theta[3 * (k + 1)] for k in range(segments - 1)):
            return np.full(observations, 1e3)
        if np.any(predicted <= 0.0) or not np.all(np.isfinite(predicted)):
            return np.full(observations, 1e3)
        return log10_q - np.log10(predicted)

    generator = np.random.default_rng(MULTISTART_SEED)
    starts = [np.array(truth[:-1], dtype=float)]
    for _ in range(MULTISTART_COUNT):
        starts.append(search_lower + generator.random(len(search_lower)) * (search_upper - search_lower))
    attempts: list[dict[str, Any]] = []
    best: dict[str, Any] | None = None
    for start in starts:
        try:
            result = optimize.least_squares(
                residuals,
                start,
                bounds=(search_lower, search_upper),
                method="trf",
                xtol=1e-15,
                ftol=1e-15,
                gtol=1e-15,
                max_nfev=20000,
            )
        except ValueError:
            continue
        rss = float(np.sum(np.square(residuals(result.x))))
        attempt = {"start": as_floats(start), "residual_sum_of_squares": rss, "success": bool(result.success)}
        attempts.append(attempt)
        if np.isfinite(rss) and rss < 1e5 and (best is None or rss < best["residual_sum_of_squares"]):
            best = {"parameters": result.x.copy(), "residual_sum_of_squares": rss, "status": int(result.status)}
    if best is None:
        raise RuntimeError("No least-squares start converged.")
    theta = best["parameters"]
    rss = best["residual_sum_of_squares"]
    sigma_hat = math.sqrt(rss / observations)
    optimum = np.concatenate([theta, [sigma_hat]])
    residual_vector = residuals(theta)
    log_space = log_space_log_likelihood(residual_vector, sigma_hat)
    jacobian_sum = float(np.sum(np.log(discharge * LN10)))
    return {
        "parameters": as_floats(optimum),
        "residual_sum_of_squares": rss,
        "profiled_sigma": sigma_hat,
        "log_space_log_likelihood": log_space,
        "discharge_space_log_likelihood": log_space - jacobian_sum,
        "optimizer": "scipy.optimize.least_squares(method='trf') on log10 residuals with the profiled scale sigma = sqrt(RSS/n)",
        "starts": len(starts),
        "multistart_seed": MULTISTART_SEED,
        "distinct_start_optima_rss": sorted({round(attempt["residual_sum_of_squares"], 10) for attempt in attempts if attempt["residual_sum_of_squares"] < 1e5}),
        "search_lower": as_floats(search_lower),
        "search_upper": as_floats(search_upper),
        "status": best["status"],
    }


def likelihood_terms(stage: np.ndarray, discharge: np.ndarray, parameters: np.ndarray, segments: int) -> dict[str, Any]:
    """Evaluate the RC.4 and RC.5 per-observation terms at one parameter vector."""

    sigma = float(parameters[-1])
    predicted = predict_from_parameters(parameters, stage, segments)
    if np.any(predicted <= 0.0):
        raise RuntimeError("Predicted discharge must be positive for the likelihood oracle.")
    residual = np.log10(discharge) - np.log10(predicted)
    log_space_terms = stats.norm.logpdf(residual, loc=0.0, scale=sigma)
    jacobian_terms = np.log(discharge * LN10)
    discharge_space_terms = stats.lognorm.logpdf(discharge, s=sigma * LN10, scale=predicted)
    consistency = float(np.max(np.abs(discharge_space_terms - (log_space_terms - jacobian_terms))))
    if consistency > 1e-10:
        raise RuntimeError(f"SciPy lognormal terms disagree with the algebraic identity: {consistency}")
    return {
        "parameters": as_floats(parameters),
        "log_space_terms": as_floats(log_space_terms),
        "jacobian_terms": as_floats(jacobian_terms),
        "discharge_space_terms": as_floats(discharge_space_terms),
        "log_space_log_likelihood": float(np.sum(log_space_terms)),
        "jacobian_sum": float(np.sum(jacobian_terms)),
        "discharge_space_log_likelihood": float(np.sum(discharge_space_terms)),
        "identity_max_abs_difference": consistency,
    }


def example_posterior_means() -> dict[str, list[float]]:
    """Transcribe the example project's stored posterior means for documentation only."""

    return {
        "one_segment": [0.98658830118317131, 0.22064258765060768, 2.6925119923916152, 0.051060220338175338],
        "two_segment": [
            0.98455938478077898, 0.21581881446087922, 2.7003922701492615,
            9.8462596660970547, 2.8379675038557926, 1.7882349617005755, 0.050679618488658935,
        ],
        "three_segment": [
            0.98493772969031756, 0.21662156996261933, 2.6987993013108329,
            9.7050122761885707, 2.7172731927614926, 1.9533262339590027,
            14.648033778968117, 3.1559684147849603, 2.1715529261903739, 0.050661441625089572,
        ],
    }


def main() -> None:
    """Regenerate the example fixtures, verify the recipe, and write both artifacts."""

    sheets = read_workbook_sheets(WORKBOOK_PATH)
    first_columns = read_sheet_columns(sheets["1 Segment"])
    r1 = first_columns["r1"]
    r2 = first_columns["r2"]
    stage = STAGE_MINIMUM + STAGE_RANGE * r1
    error = LOG10_SIGMA * stats.norm.ppf(r2)
    dates = [(START_DATE + timedelta(days=index)).isoformat() for index in range(OBSERVATIONS)]

    stage_mismatch = float(np.max(np.abs(stage - first_columns["stage"])))
    if stage_mismatch > RECIPE_TOLERANCE:
        raise RuntimeError(f"Stage recipe mismatch {stage_mismatch}")

    fixtures: dict[str, Any] = {}
    likelihood: dict[str, Any] = {}
    for key, sheet_name, controls in CASES:
        columns = read_sheet_columns(sheets[sheet_name])
        if float(np.max(np.abs(columns["r1"] - r1))) > 0.0 or float(np.max(np.abs(columns["r2"] - r2))) > 0.0:
            raise RuntimeError(f"Sheet {sheet_name} does not share the draws of the first sheet.")
        segments = len(controls)
        for control in controls:
            if abs(math.log10(control["alpha"]) - control["workbook_log10_alpha"]) > 1e-12:
                raise RuntimeError(f"log10 alpha transcription mismatch for {control['name']}")
        q_true = true_discharge(stage, controls)
        log10_q_true = np.log10(q_true)
        discharge = np.power(10.0, log10_q_true + error)
        total_mismatch = float(np.max(np.abs(log10_q_true - columns["log10_total"])))
        discharge_mismatch = float(np.max(np.abs(discharge - columns["discharge"]) / columns["discharge"]))
        if total_mismatch > RECIPE_TOLERANCE or discharge_mismatch > RECIPE_TOLERANCE:
            raise RuntimeError(
                f"{sheet_name}: recipe mismatch (log10 total {total_mismatch}, relative discharge {discharge_mismatch})"
            )
        # Use the workbook's stored discharge as the fixture so the artifact equals the shipped example exactly.
        fixture_discharge = columns["discharge"]
        truth = bestfit_parameters(controls, LOG10_SIGMA)
        lower, upper = default_bounds(stage, fixture_discharge, segments)
        mle = independent_mle(stage, fixture_discharge, segments, lower, upper, truth)
        truth_terms = likelihood_terms(stage, fixture_discharge, np.array(truth), segments)
        optimum_terms = likelihood_terms(stage, fixture_discharge, np.array(mle["parameters"]), segments)
        fixtures[key] = {
            "segments": segments,
            "controls": [
                {
                    "name": control["name"],
                    "activation_stage": control["activation_stage"],
                    "alpha": control["alpha"],
                    "log10_alpha": math.log10(control["alpha"]),
                    "beta": control["beta"],
                }
                for control in controls
            ],
            "discharge": as_floats(fixture_discharge),
            "parameter_names": parameter_names(segments),
            "true_parameters": truth,
            "true_curve_discharge": as_floats(true_discharge(np.array(TRUE_CURVE_STAGES), controls)),
            "recipe_reproduction": {
                "max_abs_log10_total_difference": total_mismatch,
                "max_relative_discharge_difference": discharge_mismatch,
            },
            "default_bounds": {"lower": lower, "upper": upper},
            "log_likelihood_at_truth": {
                "log_space": truth_terms["log_space_log_likelihood"],
                "jacobian_sum": truth_terms["jacobian_sum"],
                "discharge_space": truth_terms["discharge_space_log_likelihood"],
            },
            "independent_mle": mle,
            "example_project_posterior_mean": example_posterior_means()[key],
        }
        likelihood[key] = {
            "segments": segments,
            "at_truth": truth_terms,
            "at_independent_mle": {
                name: value
                for name, value in optimum_terms.items()
                if name not in {"log_space_terms", "jacobian_terms", "discharge_space_terms"}
            },
        }

    metadata = {
        "generated": date.today().isoformat(),
        "generator": "verification/python/rating-curve/generate_rating_curve_example_oracles.py",
        "generator_sha256": sha256_of(SCRIPT_PATH),
        "python": platform.python_version(),
        "numpy": np.__version__,
        "scipy": scipy.__version__,
        "source_workbook": "examples/6-rating-curve-analysis/Synthetic Data.xlsx",
        "source_workbook_sha256": sha256_of(WORKBOOK_PATH),
        "source_project": "examples/6-rating-curve-analysis/synthetic-rating-curve-examples.bestfit",
        "source_project_sha256": sha256_of(PROJECT_PATH),
        "recipe": (
            "stage h = 1 + 19 r1; log10 discharge = log10(sum over active controls of alpha_k (h - h_k)^beta_k) "
            "+ 0.05 * PhiInverse(r2); r1 and r2 are the workbook's stored uniform draws; "
            "300 daily observations from 2000-01-01"
        ),
        "seed": "workbook uniform draws r1 and r2; no runtime random sampling",
        "recipe_reproduction_tolerance": RECIPE_TOLERANCE,
        "observations": OBSERVATIONS,
        "start_date": START_DATE.isoformat(),
        "time_interval": "OneDay",
        "log10_sigma": LOG10_SIGMA,
        "parameter_layout": "[h1, log10 alpha1, beta1, (h2, log10 alpha2, beta2, (h3, log10 alpha3, beta3,)) sigma]",
        "independent_mle_multistart_seed": MULTISTART_SEED,
        "true_curve_stages": TRUE_CURVE_STAGES,
        "example_project_analysis_settings": (
            "UseDefaultFlatPriors=true, UseJeffreysRuleForScale=true, production BayesianAnalysis defaults "
            "(DEMCzs, seed 12345, 3500 iterations, 1750 warmup, 90% interval, posterior mean)"
        ),
    }
    fixtures_payload = {"metadata": metadata, "stage": {"dates": dates, "values": as_floats(stage)}, "cases": fixtures}
    likelihood_payload = {
        "metadata": {
            **metadata,
            "oracle": (
                "per-observation log-space Gaussian terms (RC.4), base-10 change-of-variables terms log(Q ln 10), "
                "and SciPy lognorm(s = sigma ln 10, scale = predicted discharge) discharge-space terms (RC.5)"
            ),
        },
        "cases": likelihood,
    }
    OUTPUT_DIRECTORY.mkdir(parents=True, exist_ok=True)
    FIXTURES_PATH.write_text(json.dumps(fixtures_payload, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    LIKELIHOOD_PATH.write_text(json.dumps(likelihood_payload, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    print(f"Wrote {FIXTURES_PATH}")
    print(f"Wrote {LIKELIHOOD_PATH}")
    for key, case in fixtures.items():
        print(
            f"{key}: truth={np.round(case['true_parameters'], 6).tolist()} "
            f"mle={np.round(case['independent_mle']['parameters'], 6).tolist()} "
            f"rss={case['independent_mle']['residual_sum_of_squares']:.10f} "
            f"optima={case['independent_mle']['distinct_start_optima_rss'][:5]}"
        )


if __name__ == "__main__":
    main()
