"""Validate an evidence-backed stationary annual FFA study and prepare explicit API requests.

This is input preparation, not statistical inference. It does not infer historical
completeness, independence, discharge from stage, or scientific applicability.
"""
import argparse
import copy
from datetime import date
import json
import math
from pathlib import Path
import re


def finite(value, label):
    """Require a finite number rather than JSON booleans, strings or missing values."""
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError(f"{label} must be a finite number")
    return value


def integer(value, label):
    """Require an explicit integer index/count, including negative paleoflood year indexes."""
    finite(value, label)
    if int(value) != value:
        raise ValueError(f"{label} must be an integer")
    return int(value)


def evidence(row, sources, label):
    """Require traceable source IDs for accepted numerical inputs."""
    ids = row.get("evidenceIds", [])
    if not isinstance(ids, list) or not ids or any(i not in sources for i in ids):
        raise ValueError(f"{label}: evidenceIds must reference retained sources")


def known_fields(value, allowed, label):
    """Reject typos instead of allowing the REST serializer to silently ignore a scientific setting."""
    if not isinstance(value, dict):
        raise ValueError(f"{label} must be an object")
    unknown = set(value) - set(allowed)
    if unknown:
        raise ValueError(f"Unknown {label} fields: {sorted(unknown)}")


def validate_options(options, kind):
    """Validate supported stationary FFA request names including nested prior and sampler settings."""
    common = {"distribution", "probabilityOrdinates", "name", "description"}
    bayesian = {"bayesianOptions", "parameterPriors", "quantilePriors", "useSingleQuantile", "useJeffreysRuleForScale"}
    penalties = {"uncertaintyMethod", "parameterPenalties", "quantilePenalties"}
    known_fields(options, common | (bayesian if kind == "univariate" else penalties), "analysis options")
    if options.get("bayesianOptions") is not None:
        known_fields(options["bayesianOptions"], {"sampler", "iterations", "warmupIterations", "thinningInterval",
                     "numberOfChains", "prngSeed", "credibleIntervalWidth", "outputLength", "pointEstimator"}, "bayesianOptions")
    fields = {"parameterPriors": {"parameterName", "distribution", "isFixed"},
              "quantilePriors": {"alpha", "distribution"},
              "parameterPenalties": {"parameterName", "mean", "mse", "useLog"},
              "quantilePenalties": {"aep", "mean", "mse", "useLog10"}}
    for key, allowed in fields.items():
        for row in options.get(key) or []:
            known_fields(row, allowed, key)
            if "distribution" in row:
                known_fields(row["distribution"], {"type", "parameters"}, key + ".distribution")


def skew_information(mean, mse, kind, parameter_name="Skew (of log)"):
    """Map a documented LP3 regional skew MSE to SD for a Normal prior, or retain MSE for B17C."""
    finite(mean, "skew mean")
    if finite(mse, "skew mse") <= 0:
        raise ValueError("skew mse must be positive; missing uncertainty is not zero")
    if kind == "univariate":
        return {"parameterPriors": [{"parameterName": parameter_name,
                 "distribution": {"type": "normal", "parameters": [mean, math.sqrt(mse)]}}]}
    if kind == "bulletin17c":
        return {"parameterPenalties": [{"parameterName": parameter_name, "mean": mean, "mse": mse, "useLog": False}]}
    raise ValueError("Unsupported analysis kind")


def quantile_information(aep, mean, uncertainty, uncertainty_kind, space, kind):
    """Map an explicitly justified Normal uncertainty model; never invent a log-space conversion."""
    if not 0 < finite(aep, "aep") < 1:
        raise ValueError("aep must be between 0 and 1")
    finite(mean, "quantile mean")
    if finite(uncertainty, "quantile uncertainty") <= 0 or uncertainty_kind not in ("sd", "variance"):
        raise ValueError("quantile uncertainty requires positive sd or variance; SEP/SEE/intervals need source-specific interpretation")
    if space not in ("physical", "log10"):
        raise ValueError("quantile space must be physical or log10")
    variance = uncertainty ** 2 if uncertainty_kind == "sd" else uncertainty
    if kind == "univariate":
        if space != "physical":
            raise ValueError("A log10 uncertainty model needs an explicitly constructed physical-quantile distribution; no automatic conversion")
        return {"useSingleQuantile": True, "quantilePriors": [{"alpha": aep,
                "distribution": {"type": "normal", "parameters": [mean, math.sqrt(variance)]}}]}
    if kind == "bulletin17c":
        return {"quantilePenalties": [{"aep": aep, "mean": mean, "mse": variance, "useLog10": space == "log10"}]}
    raise ValueError("Unsupported analysis kind")


def annual_index(row, definition):
    """Convert a dated observation using the declared ending-year convention, checking any supplied index."""
    supplied = row.get("index")
    if supplied is not None:
        supplied = integer(supplied, "index")
    if row.get("dateTime"):
        observed = date.fromisoformat(row["dateTime"][:10])
        month = definition.get("waterYearStartMonth", 10)
        derived = observed.year + (definition["yearConvention"] == "waterYear" and month != 1 and observed.month >= month)
        if supplied is not None and supplied != derived:
            raise ValueError(f"index {supplied} conflicts with declared year convention for {row['dateTime']}")
        return derived
    if supplied is None:
        raise ValueError("Each observation needs an explicit index or an unambiguous date with a year convention")
    return supplied


def prepare_input(raw, definition, sources):
    """Check annual coverage and create a detached request without evidence-only metadata."""
    permitted = {"name", "description", "exactData", "uncertainData", "intervalData", "thresholdData",
                 "plottingParameter", "lambda", "lowOutlierThreshold", "useMultipleGrubbsBeckTest"}
    if set(raw) - permitted:
        raise ValueError(f"Unknown input fields: {sorted(set(raw) - permitted)}")
    output = {k: copy.deepcopy(v) for k, v in raw.items() if k not in ("exactData", "uncertainData", "intervalData", "thresholdData")}
    years = set()
    historical_exact = []
    for key, fields in (("exactData", {"index", "dateTime", "value", "isLowOutlier"}),
                        ("intervalData", {"index", "lowerBound", "upperBound", "value"}),
                        ("uncertainData", {"index", "dateTime", "distribution"})):
        output[key] = []
        for row in raw.get(key, []):
            evidence(row, sources, key)
            if row.get("units", definition["units"]) != definition["units"]:
                raise ValueError("Mixed units require a documented conversion before preparation")
            if set(row) - fields - {"units", "evidenceIds", "rationale", "recordType"}:
                raise ValueError(f"Unknown {key} fields; uncertain dates or ranks cannot be silently mapped")
            index = annual_index(row, definition)
            if row.get("recordType", "systematic") not in ("systematic", "historical", "paleo"):
                raise ValueError("recordType must be systematic, historical or paleo")
            if key == "exactData" and row.get("recordType") in ("historical", "paleo"):
                historical_exact.append(index)
            if index in years:
                raise ValueError(f"duplicate annual index {index}; resolve event identity and annual maximum first")
            years.add(index)
            value = {k: copy.deepcopy(v) for k, v in row.items() if k in fields}
            value["index"] = index
            if key == "exactData" and finite(value.get("value"), "value") < 0:
                raise ValueError("Discharge cannot be negative")
            if key == "intervalData":
                low = finite(value.get("lowerBound"), "lowerBound")
                high = finite(value.get("upperBound"), "upperBound")
                if not 0 <= low <= high or not low <= value.get("value", (low + high) / 2) <= high:
                    raise ValueError("Invalid interval bounds or display value")
            if key == "uncertainData" and not isinstance(value.get("distribution"), dict):
                raise ValueError("uncertainData needs an explicit measurement-error distribution")
            output[key].append(value)
    if not output["exactData"]:
        raise ValueError("The API requires exact data; an ungaged regional lookup cannot invent pseudo-observations")
    windows, counts = [], []
    output["thresholdData"] = []
    for row in raw.get("thresholdData", []):
        evidence(row, sources, "thresholdData")
        if not str(row.get("completenessRationale", "")).strip():
            raise ValueError("A perception threshold needs a completenessRationale, not just an event list")
        if row.get("units", definition["units"]) != definition["units"]:
            raise ValueError("Threshold units differ from study units")
        fields = {"startIndex", "endIndex", "value", "numberAbove"}
        if set(row) - fields - {"evidenceIds", "completenessRationale", "rationale", "units"}:
            raise ValueError("Unsupported threshold fields; the API cannot represent every B17C two-sided perception interval")
        start, end = integer(row.get("startIndex"), "startIndex"), integer(row.get("endIndex"), "endIndex")
        if start > end:
            raise ValueError("Threshold startIndex exceeds endIndex")
        if any(start <= b and end >= a for a, b in windows):
            raise ValueError("Perception windows overlap; split them into justified nonoverlapping periods")
        windows.append((start, end))
        if finite(row.get("value"), "threshold value") <= 0:
            raise ValueError("Perception threshold must be positive")
        above = integer(row.get("numberAbove", 0), "numberAbove")
        remaining = end - start + 1 - sum(start <= y <= end for y in years)
        if not 0 <= above <= remaining:
            raise ValueError("Aggregate exceedance count exceeds years not already represented by explicit observations")
        if above > 0 and remaining == above:
            raise ValueError("An all-exceedance aggregate window is unsupported by current threshold processing; retain evidence for review")
        output["thresholdData"].append({"startIndex": start, "endIndex": end, "value": row["value"], "numberAbove": above})
        counts.append({"startIndex": start, "endIndex": end, "remainingYears": remaining, "expectedNumberBelow": remaining - above})
    return output, {"explicitYears": sorted(years), "historicalExactYears": sorted(historical_exact), "thresholds": counts,
                    "note": "Other years are unknown, not zero or inferred nonexceedances."}


def prepare(document):
    """Validate provenance and supported representations without claiming scientific approval."""
    if document.get("schemaVersion") != 1:
        raise ValueError("study schemaVersion must be 1")
    definition = document["study"]
    for key in ("location", "flowDefinition", "units", "yearConvention", "regulation"):
        if not definition.get(key):
            raise ValueError(f"study.{key} is required")
    if definition["yearConvention"] not in ("waterYear", "calendarYear"):
        raise ValueError("Declare calendarYear or waterYear; ambiguous paleoflood dates require review")
    if definition["yearConvention"] == "waterYear" and not 1 <= integer(definition.get("waterYearStartMonth", 10), "waterYearStartMonth") <= 12:
        raise ValueError("waterYearStartMonth must be in 1..12")
    sources = {}
    for source in document.get("sources", []):
        if any(not source.get(k) for k in ("id", "url", "title", "retrievedUtc", "locator")):
            raise ValueError("Each source requires id, url, title, retrievedUtc and locator (page/table/section)")
        if source["id"] in sources:
            raise ValueError("Source IDs must be unique")
        sources[source["id"]] = source
    result = copy.deepcopy(document)
    result["inputs"], result["coverage"] = {}, {}
    for name, value in document["inputs"].items():
        if not re.fullmatch(r"[A-Za-z0-9_-]+", name):
            raise ValueError("Input names must be safe directory names")
        result["inputs"][name], result["coverage"][name] = prepare_input(value, definition, sources)
    names = set()
    if not result.get("scenarios"):
        raise ValueError("At least one scenario is required")
    for scenario in result["scenarios"]:
        name = scenario["name"]
        if not re.fullmatch(r"[A-Za-z0-9_-]+", name) or name in names:
            raise ValueError("Scenario names must be unique safe directory names")
        names.add(name)
        kind = scenario.get("kind", "univariate")
        scenario["kind"] = kind
        if kind not in ("univariate", "bulletin17c") or scenario["input"] not in result["inputs"]:
            raise ValueError("Unknown scenario kind or input")
        evidence(scenario, sources, name)
        if not scenario.get("rationale"):
            raise ValueError("Each scenario needs a rationale")
        data = result["inputs"][scenario["input"]]
        options = scenario.setdefault("options", {})
        if "inputDataId" in options:
            raise ValueError("inputDataId is assigned at run time")
        validate_options(options, kind)
        information = scenario.get("information", [])
        if len(information) > 1 and not scenario.get("dependenceAssessment"):
            raise ValueError("Combined information needs an explicit dependenceAssessment; use separate candidates if unresolved")
        for item in information:
            evidence(item, sources, "information")
            if not item.get("rationale"):
                raise ValueError("Information needs an applicability and uncertainty rationale")
            if item["type"] == "skew":
                if options.get("distribution", "logPearsonTypeIII") != "logPearsonTypeIII":
                    raise ValueError("Regional LP3 skew does not map to this parent distribution")
                addition = skew_information(item["mean"], item["mse"], kind, item.get("parameterName", "Skew (of log)"))
            elif item["type"] in ("quantile", "causalQuantile", "regionalQuantile"):
                addition = quantile_information(item["aep"], item["mean"], item["uncertainty"], item["uncertaintyKind"], item["space"], kind)
            else:
                raise ValueError("Unsupported information type")
            if set(addition) & set(options):
                raise ValueError("Conflicting information/options; run separate single-quantile candidates or supply a justified supported multi-quantile formulation")
            options.update(addition)
        if kind == "bulletin17c":
            if data.get("uncertainData"):
                raise ValueError("B17C ignores uncertain observations; use Bayesian analysis or a separately justified interval representation")
            if any(k in options for k in ("parameterPriors", "quantilePriors", "useSingleQuantile", "useJeffreysRuleForScale", "bayesianOptions")):
                raise ValueError("B17C options must use penalties rather than Bayesian prior settings")
        elif any(k in options for k in ("parameterPenalties", "quantilePenalties", "uncertaintyMethod")):
            raise ValueError("Bayesian options must use priors rather than B17C penalties")
        terms = sum(len(options.get(k, [])) for k in ("parameterPriors", "quantilePriors", "parameterPenalties", "quantilePenalties"))
        if terms > 1 and not scenario.get("dependenceAssessment"):
            raise ValueError("Multiple prior/penalty terms require a dependenceAssessment")
        augmented = bool(data.get("intervalData") or data.get("thresholdData") or data.get("uncertainData") or
                         result["coverage"][scenario["input"]]["historicalExactYears"])
        if scenario.get("screeningInput"):
            cohort = result["inputs"].get(scenario["screeningInput"])
            if (cohort is None or any(cohort.get(k) for k in ("intervalData", "thresholdData", "uncertainData")) or
                    result["coverage"][scenario["screeningInput"]]["historicalExactYears"]):
                raise ValueError("screeningInput must name a documented exact systematic cohort")
            current = {r["index"]: r["value"] for r in data["exactData"]}
            if any(current.get(r["index"]) != r["value"] for r in cohort["exactData"]):
                raise ValueError("Screening cohort values must match the candidate exact observations")
        elif augmented and (data.get("useMultipleGrubbsBeckTest") or (kind == "bulletin17c" and "useMultipleGrubbsBeckTest" not in data and "lowOutlierThreshold" not in data)):
            raise ValueError("Augmented input needs screeningInput or explicit screening off/preserved flags; do not screen a mixed historical cohort")
    result["preparationStatus"] = "ready-for-api-validation"
    return result


def main():
    """Write a prepared study while retaining the original evidence and all unresolved decisions."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--study", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True, help="New output directory")
    args = parser.parse_args()
    try:
        document = json.loads(args.study.read_text(encoding="utf-8-sig"))
        prepared = prepare(document)
        args.output.mkdir(parents=True, exist_ok=False)
        for name, value in (("study.json", document), ("prepared.json", prepared)):
            (args.output / name).write_text(json.dumps(value, indent=2, allow_nan=False) + "\n", encoding="utf-8")
        print(args.output / "prepared.json")
    except (ValueError, KeyError, TypeError, OSError) as error:
        parser.exit(1, f"Study preparation stopped: {error}\n")


if __name__ == "__main__":
    main()
