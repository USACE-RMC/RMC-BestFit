# Desktop plot reference evidence

The [plot map](../../skills/bestfit-frequency/references/app-plot-map.json) records
45 plot slots and 85 acceptance variants. There are 84 populated references and
one expected empty stationary chronology. The companion BestFit-Python-Examples
repository compares source-bound PlotSpecs with these independent desktop exports;
all 84 populated variants pass at absolute tolerance 1e-10 and relative tolerance
1e-8. This is geometry and presentation evidence, not model convergence approval.

`tools/PlotReferenceExporter` copies each source project to disposable storage,
loads the real WPF control, resets the in-memory plot to its application factory,
selects the requested variant, and calls the application's population methods.
It does not run model fitting or alter source projects. JSON includes source,
application and exporter hashes; PNG/SVG display exports are local scratch.

The references retain their actual capture provenance: 71 use exporter SHA
`45b72adb2e983dd40014a29665dfca3ca28af9b638a4a1e862595f41ffec67d4`;
13 use the later exporter hash recorded in their JSON. The later exporter invokes
the app's axis-title binding after selecting a bivariate probability variant.
Six bivariate views were refreshed intentionally and seven other views were
captured before a redundant refresh was stopped. Their unchanged geometry was
compared afresh. No provenance hashes are rewritten to suggest one capture.

Build on Windows with the repository's required .NET/WPF dependencies:

```powershell
dotnet build tools/PlotReferenceExporter -c Debug -p:UseLocalRmcNumerics=false
python tools/PlotReferenceExporter/export_references.py --only bivariate
python -m pytest tests/skills validation/plot-parity/test_compare.py tools/PlotReferenceExporter/test_smoke.py
```

In the companion examples checkout:

```powershell
python scripts/build_plot_gallery.py --images
python scripts/check_plot_parity.py
python scripts/update_plot_map.py
```

The comparison checks named visible series, coordinates, grids, interval bounds,
annotation geometry, axis scale/title, labels, and supported styles. Date values
are compared using the app's OLE Automation epoch. Nonpositive logarithmic display
cutoffs and genuinely empty app views retain explicit treatment. Font rasterization,
interactive controls, custom desktop styles and pixel identity are outside scope.

Known inherited presentation detail: the time-series residual factory uses raw
OLE date numbers on a linear axis labeled with the response unit. The adapter
preserves that default; those coordinates are dates, not fitted responses.
