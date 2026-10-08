# Generate a deterministic true-profile likelihood oracle for TR-023.
# The nuisance parameter is reoptimized at every fixed value of theta1.

script_argument <- grep("^--file=", commandArgs(trailingOnly = FALSE), value = TRUE)
script_path <- normalizePath(sub("^--file=", "", script_argument[[1L]]), winslash = "/", mustWork = TRUE)
repository_root <- normalizePath(
  file.path(dirname(script_path), "..", "..", ".."),
  winslash = "/",
  mustWork = TRUE
)
renv::load(file.path(repository_root, "verification", "r"), quiet = TRUE)

output_path <- file.path(
  repository_root,
  "verification",
  "data",
  "model-estimation",
  "profile-likelihood-oracle.json"
)

rho <- 0.8
lower_bound <- -4.0
upper_bound <- 4.0
bin_count <- 8L
alpha <- 0.1

minus_log_likelihood <- function(theta1, theta2) {
  (theta1^2 - 2.0 * rho * theta1 * theta2 + theta2^2) /
    (2.0 * (1.0 - rho^2))
}

fit <- bbmle::mle2(
  minuslogl = minus_log_likelihood,
  start = list(theta1 = 0.25, theta2 = -0.25),
  method = "L-BFGS-B",
  lower = c(theta1 = lower_bound, theta2 = lower_bound),
  upper = c(theta1 = upper_bound, theta2 = upper_bound)
)

grid <- seq(-3.5, 3.5, length.out = bin_count)

profile_at <- function(theta1) {
  nuisance_fit <- stats::optimize(
    function(theta2) minus_log_likelihood(theta1, theta2),
    interval = c(lower_bound, upper_bound),
    tol = 1e-14
  )

  list(
    theta1 = unname(theta1),
    conditional_log_likelihood = unname(-minus_log_likelihood(theta1, 0.0)),
    true_profile_log_likelihood = unname(-nuisance_fit$objective),
    optimized_nuisance = unname(nuisance_fit$minimum)
  )
}

profile_grid <- lapply(grid, profile_at)
critical_value <- stats::qchisq(1.0 - alpha, df = 1L)
target_log_likelihood <- -0.5 * critical_value
true_bound <- stats::uniroot(
  function(theta1) profile_at(theta1)$true_profile_log_likelihood - target_log_likelihood,
  interval = c(0.0, upper_bound),
  tol = 1e-14
)$root
conditional_bound <- stats::uniroot(
  function(theta1) -minus_log_likelihood(theta1, 0.0) - target_log_likelihood,
  interval = c(0.0, upper_bound),
  tol = 1e-14
)$root

git_revision <- function(path) {
  revision <- tryCatch(
    system2("git", c("-C", shQuote(path), "rev-parse", "HEAD"), stdout = TRUE),
    error = function(...) character()
  )
  if (length(revision) == 0L) NA_character_ else revision[[1L]]
}

artifact <- list(
  metadata = list(
    artifact_id = "ME-PROFILE-LIKELIHOOD-001",
    generator = "verification/r/model-estimation/generate_profile_likelihood_oracle.R",
    generator_sha256 = unname(digest::digest(file = script_path, algo = "sha256")),
    source_commit = git_revision(repository_root),
    r_version = R.version.string,
    bbmle_version = as.character(utils::packageVersion("bbmle")),
    jsonlite_version = as.character(utils::packageVersion("jsonlite")),
    generated_utc = format(Sys.time(), tz = "UTC", usetz = TRUE),
    tolerances = list(
      fitted_parameter_absolute = 1e-8,
      log_likelihood_absolute = 1e-10,
      interval_absolute = 1e-8
    )
  ),
  fixture = list(
    rho = rho,
    lower_bound = lower_bound,
    upper_bound = upper_bound,
    bin_count = bin_count,
    alpha = alpha,
    start = c(theta1 = 0.25, theta2 = -0.25)
  ),
  bbmle_fit = list(
    theta1 = unname(fit@coef[["theta1"]]),
    theta2 = unname(fit@coef[["theta2"]]),
    maximum_log_likelihood = unname(-fit@min)
  ),
  profile_grid = profile_grid,
  intervals = list(
    likelihood_ratio_critical_value = unname(critical_value),
    target_log_likelihood = unname(target_log_likelihood),
    true_profile = c(lower = -true_bound, upper = true_bound),
    conditional_slice = c(lower = -conditional_bound, upper = conditional_bound)
  ),
  status = list(
    true_profile_reoptimizes_nuisance = TRUE,
    conditional_slice_holds_nuisance_at_joint_mle = TRUE
  )
)

dir.create(dirname(output_path), recursive = TRUE, showWarnings = FALSE)
jsonlite::write_json(
  artifact,
  output_path,
  auto_unbox = TRUE,
  pretty = TRUE,
  digits = NA
)

message(sprintf("Wrote %s", output_path))
