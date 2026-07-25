# Generate the independent R gmm oracle for Log10-Normal influence diagnostics.
# The C# verification project consumes only the committed JSON artifact.

script_path <- normalizePath(sys.frame(1)$ofile, winslash = "/", mustWork = TRUE)
repository_root <- normalizePath(
  file.path(dirname(script_path), "..", "..", ".."),
  winslash = "/",
  mustWork = TRUE
)
output_path <- file.path(
  repository_root,
  "verification",
  "data",
  "model-estimation",
  "gmm-influence-oracle.json"
)

log10_values <- c(1.1, 1.4, 1.7, 2.0, 2.3, 2.6, 2.9)
sample_size <- length(log10_values)
parameter_count <- 2L
c2 <- sample_size / (sample_size - 1)
initial_parameters <- c(mean(log10_values), sqrt(stats::var(log10_values)))

moment_function <- function(parameters, data) {
  residual <- data - parameters[[1L]]
  cbind(
    residual,
    c2 * residual^2 - parameters[[2L]]^2
  )
}

moment_gradient <- function(parameters, data) {
  matrix(
    c(
      -1.0,
      0.0,
      -2.0 * c2 * mean(data - parameters[[1L]]),
      -2.0 * parameters[[2L]]
    ),
    nrow = 2L,
    byrow = TRUE
  )
}

weighting_matrix <- diag(
  c(
    1.0 / initial_parameters[[2L]]^2,
    1.0 / (2.0 * initial_parameters[[2L]]^4)
  )
)

fit <- gmm::gmm(
  g = moment_function,
  x = log10_values,
  t0 = initial_parameters,
  gradv = moment_gradient,
  type = "twoStep",
  vcov = "TrueFixed",
  weightsMatrix = weighting_matrix,
  optfct = "optim",
  method = "BFGS",
  control = list(reltol = 1e-13, maxit = 100000)
)

parameters <- unname(stats::coef(fit))
scores <- unname(sandwich::estfun(fit))
jacobian <- fit$gradv(parameters, fit$dat)
bread <- t(jacobian) %*% fit$w %*% jacobian
bread_inverse <- solve(bread)
score_quadratic <- rowSums((scores %*% bread_inverse) * scores)
fit_influence <- score_quadratic / (sample_size^2 * parameter_count)
variance_influence <- score_quadratic / (sample_size * parameter_count)
combined_leverage <- fit_influence + variance_influence

git_revision <- function(repository) {
  result <- system2(
    "git",
    c("-C", shQuote(repository), "rev-parse", "HEAD"),
    stdout = TRUE,
    stderr = TRUE
  )
  if (!identical(attr(result, "status"), NULL) || length(result) != 1L) {
    stop("Unable to determine Git revision for ", repository)
  }
  unname(result[[1L]])
}

artifact <- list(
  metadata = list(
    artifact_id = "ME-GMM-INFLUENCE-001",
    generated_utc = format(Sys.Date(), "%Y-%m-%d"),
    command = "scripts/generate-verification-data.ps1 -Family model-estimation",
    generator = "verification/r/model-estimation/generate_gmm_influence_oracle.R",
    generator_sha256 = digest::digest(
      file = script_path,
      algo = "sha256",
      serialize = FALSE
    ),
    bestfit_source_commit = git_revision(repository_root),
    numerics_source_commit = git_revision(file.path(repository_root, "..", "Numerics")),
    r_version = R.version.string,
    gmm_version = as.character(utils::packageVersion("gmm")),
    sandwich_version = as.character(utils::packageVersion("sandwich")),
    jsonlite_version = as.character(utils::packageVersion("jsonlite")),
    fixture = paste(
      "Just-identified Log10-Normal mean and unbiased-variance moments;",
      "fixed theoretical Normal-moment weighting matrix"
    ),
    fit_influence_absolute_tolerance = 1e-5,
    variance_influence_absolute_tolerance = 1e-5,
    combined_leverage_absolute_tolerance = 1e-5,
    vanishing_penalty_equivalence_absolute_tolerance = 1e-4,
    tolerance_rationale = paste(
      "R uses the supplied analytical moment Jacobian; BestFit obtains the diagnostic",
      "objective Hessian by bounded numerical differentiation. The 1e-5 tolerance is",
      "predeclared for that algorithmic difference."
    )
  ),
  data_log10 = unname(log10_values),
  c2 = unname(c2),
  fitted_parameters = list(
    mu = unname(parameters[[1L]]),
    sigma = unname(parameters[[2L]])
  ),
  weighting_matrix = unname(weighting_matrix),
  formulas = list(
    pointwise_moments = "g1_i=x_i-mu; g2_i=c2*(x_i-mu)^2-sigma^2",
    pointwise_score = "e_i=D' W g_i",
    bread = "B=D' W D",
    fit_influence = "e_i' B^-1 e_i/(n^2*p)",
    variance_influence = "e_i' B^-1 e_i/(n*p)",
    combined_leverage = "fit_influence+variance_influence"
  ),
  observations = lapply(seq_along(log10_values), function(index) {
    list(
      index = index - 1L,
      x = unname(log10_values[[index]]),
      fit_influence = unname(fit_influence[[index]]),
      variance_influence = unname(variance_influence[[index]]),
      combined_leverage = unname(combined_leverage[[index]])
    )
  }),
  totals = list(
    fit_influence = unname(sum(fit_influence)),
    variance_influence = unname(sum(variance_influence)),
    combined_leverage = unname(sum(combined_leverage))
  ),
  interpretation = paste(
    "Cook distance and variance influence are calibrated on the GMM estimating-equation scale.",
    "They are not compared numerically with likelihood-based MAP Cook distance."
  ),
  status = "Independent R oracle"
)

dir.create(dirname(output_path), recursive = TRUE, showWarnings = FALSE)
jsonlite::write_json(
  artifact,
  output_path,
  pretty = TRUE,
  auto_unbox = TRUE,
  digits = NA
)
cat("Wrote", output_path, "\n")
cat("R gmm total fit influence:", format(sum(fit_influence), digits = 17), "\n")
cat("R gmm total variance influence:", format(sum(variance_influence), digits = 17), "\n")
