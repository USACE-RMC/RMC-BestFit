# Generate the independent Phase 5 ARIMA and ARIMAX recovery fixtures.
#
# Both fixtures contain exactly 1,000 raw observations. The recurrences are
# implemented directly in R and do not call Numerics or RMC.BestFit.

script_argument <- grep("^--file=", commandArgs(trailingOnly = FALSE), value = TRUE)
script_path <- normalizePath(sub("^--file=", "", script_argument[[1L]]), winslash = "/", mustWork = TRUE)
repository_root <- normalizePath(
  file.path(dirname(script_path), "..", "..", ".."),
  winslash = "/",
  mustWork = TRUE
)
verification_r_root <- file.path(repository_root, "verification", "r")
project_library <- file.path(
  verification_r_root,
  "renv",
  "library",
  "windows",
  paste0("R-", R.version$major, ".", strsplit(R.version$minor, "\\.")[[1L]][[1L]]),
  R.version$platform
)
.libPaths(c(project_library, .libPaths()))

output_path <- file.path(
  repository_root,
  "verification",
  "data",
  "time-series",
  "phase5-recovery-fixtures.json"
)

git_revision <- function(path) {
  revision <- tryCatch(
    system2("git", c("-C", shQuote(path), "rev-parse", "HEAD"), stdout = TRUE),
    error = function(...) character()
  )
  if (length(revision) == 0L) NA_character_ else revision[[1L]]
}

generate_arima_fixture <- function() {
  sample_size <- 1000L
  seed <- 51037L
  phi <- 0.45
  theta <- 0.25
  sigma <- 0.04
  initial_raw <- 100.0
  dates <- seq(as.Date("2004-01-01"), by = "day", length.out = sample_size)

  set.seed(seed)
  innovations <- stats::rnorm(sample_size - 1L, mean = 0.0, sd = sigma)
  differences <- numeric(sample_size - 1L)
  for (position in seq_along(differences)) {
    previous_difference <- if (position == 1L) 0.0 else differences[[position - 1L]]
    previous_innovation <- if (position == 1L) 0.0 else innovations[[position - 1L]]
    differences[[position]] <- phi * previous_difference +
      theta * previous_innovation + innovations[[position]]
  }

  transformed_levels <- c(log(initial_raw), log(initial_raw) + cumsum(differences))
  next_difference <- phi * tail(differences, 1L) + theta * tail(innovations, 1L)

  list(
    dates = format(dates, "%Y-%m-%d"),
    raw = unname(exp(transformed_levels)),
    innovations = unname(innovations),
    differences = unname(differences),
    sample_size = sample_size,
    seed = seed,
    transform = "Logarithmic",
    differencing_order = 1L,
    ar_order = 1L,
    ma_order = 1L,
    include_intercept = FALSE,
    initial_raw = initial_raw,
    phi = phi,
    theta = theta,
    sigma = sigma,
    next_difference_zero_innovation = unname(next_difference),
    next_raw_zero_innovation = unname(exp(tail(transformed_levels, 1L) + next_difference))
  )
}

generate_arimax_fixture <- function() {
  sample_size <- 1000L
  seed <- 51038L
  intercept <- 0.25
  beta <- 1.5
  phi <- 0.4
  sigma <- 0.5
  initial_raw <- 10.0
  dates <- seq(as.Date("2005-01-01"), by = "day", length.out = sample_size)
  raw_index <- 0:(sample_size - 1L)
  covariate <- sin(2.0 * pi * raw_index / 17.0) +
    0.5 * cos(2.0 * pi * raw_index / 31.0)

  set.seed(seed)
  innovations <- stats::rnorm(sample_size - 1L, mean = 0.0, sd = sigma)
  differences <- numeric(sample_size - 1L)
  means <- intercept + beta * covariate[2:sample_size]
  for (position in seq_along(differences)) {
    if (position == 1L) {
      ar_part <- 0.0
    } else {
      ar_part <- phi * (differences[[position - 1L]] - means[[position - 1L]])
    }
    differences[[position]] <- means[[position]] + ar_part + innovations[[position]]
  }

  raw <- c(initial_raw, initial_raw + cumsum(differences))
  next_index <- sample_size
  next_covariate <- sin(2.0 * pi * next_index / 17.0) +
    0.5 * cos(2.0 * pi * next_index / 31.0)
  next_mean <- intercept + beta * next_covariate
  next_difference <- next_mean + phi * (tail(differences, 1L) - tail(means, 1L))

  list(
    dates = format(dates, "%Y-%m-%d"),
    raw = unname(raw),
    covariate = unname(covariate),
    innovations = unname(innovations),
    differences = unname(differences),
    sample_size = sample_size,
    seed = seed,
    transform = "None",
    differencing_order = 1L,
    ar_order = 1L,
    ma_order = 0L,
    covariate_lag_order = 0L,
    include_intercept = TRUE,
    initial_raw = initial_raw,
    intercept = intercept,
    beta = beta,
    phi = phi,
    sigma = sigma,
    next_covariate = unname(next_covariate),
    next_difference_zero_innovation = unname(next_difference),
    next_raw_zero_innovation = unname(tail(raw, 1L) + next_difference)
  )
}

artifact <- list(
  metadata = list(
    artifact_id = "TS-PHASE5-RECOVERY-001",
    findings = c("TR-035", "TR-036", "TR-037", "TR-038", "TR-039", "TR-040", "TR-041", "TR-046"),
    generator = "verification/r/time-series/generate_phase5_recovery_fixtures.R",
    generator_sha256 = unname(digest::digest(file = script_path, algo = "sha256")),
    source_commit = git_revision(repository_root),
    r_version = R.version.string,
    jsonlite_version = as.character(utils::packageVersion("jsonlite")),
    digest_version = as.character(utils::packageVersion("digest")),
    generated_utc = format(Sys.time(), tz = "UTC", usetz = TRUE),
    maximum_steps = 1000L,
    mle_coefficient_relative_tolerance = 0.15,
    mle_scale_relative_tolerance = 0.10,
    bayesian_map_relative_tolerance = 0.25,
    bayesian_central_interval = 0.95,
    bayesian_rhat_maximum = 1.1,
    bayesian_ess_minimum = 100L
  ),
  arima = generate_arima_fixture(),
  arimax = generate_arimax_fixture()
)

dir.create(dirname(output_path), recursive = TRUE, showWarnings = FALSE)
jsonlite::write_json(
  artifact,
  output_path,
  auto_unbox = TRUE,
  pretty = TRUE,
  digits = NA,
  na = "null"
)

message(sprintf("Wrote %s", output_path))
