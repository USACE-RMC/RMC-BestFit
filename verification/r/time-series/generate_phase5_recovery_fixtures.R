# Generate the independent Phase 5 time-series recovery fixtures.
#
# Every fixture contains exactly 1,000 retained raw observations. A conservative
# 110-step initialization period is discarded from each stationary ARMA
# recursion. The recurrences are implemented directly in R and do not call
# Numerics or RMC.BestFit.

retained_sample_size <- 1000L
initialization_steps <- 110L
maximum_inference_steps <- 1000L
recurrence_tolerance <- 1e-12

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
  sample_size <- retained_sample_size
  burn_in <- initialization_steps
  seed <- 51037L
  phi <- 0.45
  theta <- 0.25
  sigma <- 0.04
  initial_raw <- 100.0
  dates <- seq(as.Date("2004-01-01"), by = "day", length.out = sample_size)

  retained_model_steps <- sample_size - 1L
  total_model_steps <- burn_in + retained_model_steps
  set.seed(seed)
  all_innovations <- stats::rnorm(total_model_steps, mean = 0.0, sd = sigma)
  all_differences <- numeric(total_model_steps)
  for (position in seq_along(all_differences)) {
    previous_difference <- if (position == 1L) 0.0 else all_differences[[position - 1L]]
    previous_innovation <- if (position == 1L) 0.0 else all_innovations[[position - 1L]]
    all_differences[[position]] <- phi * previous_difference +
      theta * previous_innovation + all_innovations[[position]]
  }

  retained_indices <- (burn_in + 1L):total_model_steps
  differences <- all_differences[retained_indices]
  innovations <- all_innovations[retained_indices]
  initial_difference <- all_differences[[burn_in]]
  initial_innovation <- all_innovations[[burn_in]]

  transformed_levels <- c(log(initial_raw), log(initial_raw) + cumsum(differences))
  next_difference <- phi * tail(differences, 1L) + theta * tail(innovations, 1L)

  list(
    dates = format(dates, "%Y-%m-%d"),
    raw = unname(exp(transformed_levels)),
    innovations = unname(innovations),
    differences = unname(differences),
    sample_size = sample_size,
    burn_in = burn_in,
    retained_model_steps = retained_model_steps,
    total_model_steps = total_model_steps,
    initial_difference = unname(initial_difference),
    initial_innovation = unname(initial_innovation),
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

generate_ar_fixture <- function() {
  sample_size <- retained_sample_size
  burn_in <- initialization_steps
  seed <- 12345L
  mu <- 10.0
  phi <- 0.6
  sigma <- 5.0
  dates <- seq(as.Date("2002-01-01"), by = "month", length.out = sample_size)

  total_model_steps <- burn_in + sample_size
  set.seed(seed)
  all_innovations <- stats::rnorm(total_model_steps, mean = 0.0, sd = sigma)
  all_raw <- numeric(total_model_steps)
  all_raw[[1L]] <- mu + all_innovations[[1L]]
  for (position in 2:total_model_steps) {
    all_raw[[position]] <- mu + phi * (all_raw[[position - 1L]] - mu) +
      all_innovations[[position]]
  }

  retained_indices <- (burn_in + 1L):total_model_steps
  raw <- all_raw[retained_indices]
  innovations <- all_innovations[retained_indices]

  list(
    dates = format(dates, "%Y-%m-%d"),
    raw = unname(raw),
    innovations = unname(innovations),
    sample_size = sample_size,
    burn_in = burn_in,
    retained_model_steps = sample_size,
    total_model_steps = total_model_steps,
    initial_raw_state = unname(all_raw[[burn_in]]),
    seed = seed,
    ar_order = 1L,
    include_intercept = TRUE,
    mu = mu,
    phi = phi,
    sigma = sigma,
    next_raw_zero_innovation = unname(mu + phi * (tail(raw, 1L) - mu))
  )
}

generate_ma_fixture <- function() {
  sample_size <- retained_sample_size
  burn_in <- initialization_steps
  seed <- 12345L
  mu <- 10.0
  theta <- 0.6
  sigma <- 5.0
  dates <- seq(as.Date("2003-01-01"), by = "month", length.out = sample_size)

  total_model_steps <- burn_in + sample_size
  set.seed(seed)
  all_innovations <- stats::rnorm(total_model_steps, mean = 0.0, sd = sigma)
  previous_innovations <- c(0.0, all_innovations[-total_model_steps])
  all_raw <- mu + all_innovations + theta * previous_innovations
  retained_indices <- (burn_in + 1L):total_model_steps
  raw <- all_raw[retained_indices]
  innovations <- all_innovations[retained_indices]

  list(
    dates = format(dates, "%Y-%m-%d"),
    raw = unname(raw),
    innovations = unname(innovations),
    sample_size = sample_size,
    burn_in = burn_in,
    retained_model_steps = sample_size,
    total_model_steps = total_model_steps,
    initial_innovation = unname(all_innovations[[burn_in]]),
    seed = seed,
    ma_order = 1L,
    include_intercept = TRUE,
    mu = mu,
    theta = theta,
    sigma = sigma,
    next_raw_zero_innovation = unname(mu + theta * tail(innovations, 1L))
  )
}

generate_arimax_fixture <- function() {
  sample_size <- retained_sample_size
  burn_in <- initialization_steps
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

  retained_model_steps <- sample_size - 1L
  total_model_steps <- burn_in + retained_model_steps
  model_raw_index <- (1L - burn_in):(sample_size - 1L)
  model_covariate <- sin(2.0 * pi * model_raw_index / 17.0) +
    0.5 * cos(2.0 * pi * model_raw_index / 31.0)
  set.seed(seed)
  all_innovations <- stats::rnorm(total_model_steps, mean = 0.0, sd = sigma)
  all_differences <- numeric(total_model_steps)
  all_means <- intercept + beta * model_covariate
  for (position in seq_along(all_differences)) {
    if (position == 1L) {
      ar_part <- 0.0
    } else {
      ar_part <- phi * (all_differences[[position - 1L]] - all_means[[position - 1L]])
    }
    all_differences[[position]] <- all_means[[position]] + ar_part +
      all_innovations[[position]]
  }

  retained_indices <- (burn_in + 1L):total_model_steps
  differences <- all_differences[retained_indices]
  innovations <- all_innovations[retained_indices]
  means <- all_means[retained_indices]
  initial_difference <- all_differences[[burn_in]]
  initial_mean <- all_means[[burn_in]]

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
    burn_in = burn_in,
    retained_model_steps = retained_model_steps,
    total_model_steps = total_model_steps,
    initial_difference = unname(initial_difference),
    initial_mean = unname(initial_mean),
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

assert_close <- function(actual, expected, label) {
  residual <- max(abs(actual - expected))
  if (!is.finite(residual) || residual > recurrence_tolerance) {
    stop(sprintf("%s recurrence residual %.17g exceeds %.17g", label, residual, recurrence_tolerance))
  }
}

validate_fixture_contract <- function(ar, ma, arima, arimax) {
  fixtures <- list(ar = ar, ma = ma, arima = arima, arimax = arimax)
  for (name in names(fixtures)) {
    fixture <- fixtures[[name]]
    stopifnot(
      fixture$sample_size == retained_sample_size,
      fixture$burn_in == initialization_steps,
      length(fixture$dates) == retained_sample_size,
      length(fixture$raw) == retained_sample_size,
      all(is.finite(fixture$raw))
    )
  }

  ar_expected <- ar$mu + ar$phi *
    (c(ar$initial_raw_state, ar$raw[-retained_sample_size]) - ar$mu) +
    ar$innovations
  assert_close(ar$raw, ar_expected, "AR")

  ma_expected <- ma$mu + ma$innovations + ma$theta *
    c(ma$initial_innovation, ma$innovations[-retained_sample_size])
  assert_close(ma$raw, ma_expected, "MA")

  arima_expected <- arima$phi *
    c(arima$initial_difference, arima$differences[-arima$retained_model_steps]) +
    arima$theta *
    c(arima$initial_innovation, arima$innovations[-arima$retained_model_steps]) +
    arima$innovations
  assert_close(arima$differences, arima_expected, "ARIMA difference")
  assert_close(
    arima$raw,
    exp(c(log(arima$initial_raw), log(arima$initial_raw) + cumsum(arima$differences))),
    "ARIMA integration"
  )
  stopifnot(is.finite(arima$next_raw_zero_innovation))

  arimax_means <- arimax$intercept + arimax$beta * arimax$covariate[2:retained_sample_size]
  arimax_expected <- arimax_means + arimax$phi *
    (c(arimax$initial_difference, arimax$differences[-arimax$retained_model_steps]) -
      c(arimax$initial_mean, arimax_means[-arimax$retained_model_steps])) +
    arimax$innovations
  assert_close(arimax$differences, arimax_expected, "ARIMAX difference")
  assert_close(
    arimax$raw,
    c(arimax$initial_raw, arimax$initial_raw + cumsum(arimax$differences)),
    "ARIMAX integration"
  )
  stopifnot(is.finite(arimax$next_raw_zero_innovation))
}

ar_fixture <- generate_ar_fixture()
ma_fixture <- generate_ma_fixture()
arima_fixture <- generate_arima_fixture()
arimax_fixture <- generate_arimax_fixture()
validate_fixture_contract(ar_fixture, ma_fixture, arima_fixture, arimax_fixture)

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
    initialization_policy = "discarded stationary ARMA recursion",
    burn_in = initialization_steps,
    retained_sample_size = retained_sample_size,
    maximum_steps = maximum_inference_steps,
    recurrence_tolerance = recurrence_tolerance,
    mle_coefficient_relative_tolerance = 0.15,
    mle_scale_relative_tolerance = 0.10,
    bayesian_map_relative_tolerance = 0.25,
    bayesian_central_interval = 0.95,
    bayesian_rhat_maximum = 1.1,
    bayesian_ess_minimum = 100L
  ),
  ar = ar_fixture,
  ma = ma_fixture,
  arima = arima_fixture,
  arimax = arimax_fixture
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
