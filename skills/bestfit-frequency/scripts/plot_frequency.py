"""CLI and import compatibility for the canonical bestfit_plots implementation."""
import sys
from pathlib import Path

SKILL_ROOT = Path(__file__).resolve().parents[1]
if str(SKILL_ROOT) not in sys.path:
    sys.path.insert(0, str(SKILL_ROOT))

from bestfit_plots.legacy_frequency import (  # noqa: E402
    NORMAL,
    DISPLAY_MINIMUM,
    read_json,
    checked_response,
    probability_coordinates,
    curve_values,
    draw_observations,
    set_probability_ticks,
    build_figure,
    save_figure,
    main,
)

if __name__ == "__main__":
    main()
