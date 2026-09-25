"""List or export views from a local saved plotting artifact; never fit a model."""
import argparse
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from bestfit_plots import export_plot, render_plot
from bestfit_plots.source import add_frequency_comparison, load_plots


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path, help="PlotSpec, saved case, or API plot-source JSON[.gz]")
    parser.add_argument("--plot", help="Named view or unambiguous plotId; use --list to discover")
    parser.add_argument("--list", action="store_true")
    parser.add_argument("--output", type=Path, help="Output prefix for PNG, SVG, PlotSpec JSON")
    parser.add_argument("--compare-source", type=Path, help="Second saved/API frequency source to overlay")
    parser.add_argument("--compare-name", help="Explicit alternative analysis label")
    parser.add_argument("--unit-label", help="Known source unit label for API data only")
    parser.add_argument("--parameter", type=int, default=0)
    parser.add_argument("--second-parameter", type=int, default=1)
    parser.add_argument("--include-warmup", action="store_true")
    parser.add_argument("--show-prior", action="store_true")
    parser.add_argument("--influence-view", choices=["bayesian_leverage", "bayesian_fit", "bayesian_variance", "gmm_fit", "gmm_variance", "leave_one_out"])
    args = parser.parse_args()
    plots = load_plots(args.source, unit_label=args.unit_label, parameter=args.parameter,
                       second_parameter=args.second_parameter, include_warmup=args.include_warmup,
                       show_prior=args.show_prior, influence_view=args.influence_view)
    if args.list:
        for name, spec in plots.items():
            print(f"{name}\t{spec['plotId']}\t{spec['variant']}\t{'; '.join(spec['omissions'])}")
        return
    if not args.plot or not args.output:
        parser.error("Use --list, or provide both --plot and --output")
    matches = [spec for name, spec in plots.items() if args.plot == name]
    if not matches:
        matches = [spec for spec in plots.values() if args.plot == spec["plotId"]]
    if len(matches) != 1:
        parser.error(f"Expected one view for {args.plot!r}; found {len(matches)}. Use --list.")
    spec = matches[0]
    if args.compare_source:
        if not args.compare_name:
            parser.error("--compare-source requires --compare-name")
        alternatives = load_plots(args.compare_source, unit_label=args.unit_label)
        candidates = [view for key, view in alternatives.items() if key == "frequency"]
        if not candidates:
            candidates = [view for view in alternatives.values() if view["plotId"].endswith(".frequency")]
        if len(candidates) != 1:
            parser.error("Comparison source must identify one frequency view")
        spec = add_frequency_comparison(spec, candidates[0], args.compare_name)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    export_plot(spec, args.output)
    import matplotlib.pyplot as plt
    preview = render_plot(spec)
    omissions = list(preview.bestfit_omissions)
    plt.close(preview)
    Path(str(args.output) + ".json").write_text(json.dumps(spec, ensure_ascii=False, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    print(f"{args.output}.png\n{args.output}.svg\n{args.output}.json")
    for note in omissions:
        print("Display omission:", note)


if __name__ == "__main__":
    main()
