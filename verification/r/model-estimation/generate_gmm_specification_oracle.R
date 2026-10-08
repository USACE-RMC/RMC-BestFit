# Generate the independent R gmm oracle for overidentified fixed-weight fitting,
# IID sandwich covariance, and Hansen's J specification test. The C# verification
# project consumes only the committed JSON artifact.

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
  "gmm-specification-oracle.json"
)

x_values <- c(0.2, 0.5, 0.7, 1.1, 1.4, 1.8, 2.1, 2.6, 3.2, 4.0)
z_values <- c(0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0)
fixture <- cbind(x = x_values, z = z_values)
sample_size <- nrow(fixture)

moment_function <- function(parameters, data) {
  residual <- data[, "x"] - parameters[[1L]]
  cbind(
    residual,
    data[, "z"] * residual
  )
}

moment_gradient <- function(parameters, data) {
  matrix(
    c(-1.0, -mean(data[, "z"])),
    nrow = 2L,
    ncol = 1L
  )
}

optimizer_control <- list(reltol = 1e-13, maxit = 100000)
fixed_weighting_matrix <- diag(2L)

# gmm treats a supplied weightsMatrix as fixed and ignores the iterative type
# choice. vcov="iid" requests the robust IID sandwich for that arbitrary fixed
# weight; vcov="TrueFixed" would incorrectly assert that the supplied matrix is
# already the inverse moment covariance.
fixed_weight_fit <- gmm::gmm(
  g = moment_function,
  x = fixture,
  t0 = 1.5,
  gradv = moment_gradient,
  type = "twoStep",
  vcov = "iid",
  weightsMatrix = fixed_weighting_matrix,
  optfct = "optim",
  method = "BFGS",
  control = optimizer_control
)

two_step_fit <- gmm::gmm(
  g = moment_function,
  x = fixture,
  t0 = 1.5,
  gradv = moment_gradient,
  type = "twoStep",
  vcov = "iid",
  wmatrix = "optimal",
  optfct = "optim",
  method = "BFGS",
  control = optimizer_control
)

fixed_specification_test <- gmm::specTest(fixed_weight_fit)$test
two_step_specification_test <- gmm::specTest(two_step_fit)$test
two_step_objective_weight <- solve(unclass(two_step_fit$w0))

fixed_parameters <- unname(stats::coef(fixed_weight_fit))
two_step_parameters <- unname(stats::coef(two_step_fit))
fixed_moments <- unname(colMeans(moment_function(fixed_parameters, fixture)))
two_step_moments <- unname(colMeans(moment_function(two_step_parameters, fixture)))
fixed_covariance <- unname(stats::vcov(fixed_weight_fit))
two_step_covariance <- unname(stats::vcov(two_step_fit))

centered_moment_covariance <- function(parameters) {
  pointwise_moments <- moment_function(parameters, fixture)
  mean_moments <- colMeans(pointwise_moments)
  centered_moments <- sweep(pointwise_moments, 2L, mean_moments, FUN = "-")
  crossprod(centered_moments) / sample_size
}

closed_form_covariance <- function(parameters, weighting_matrix) {
  moment_covariance <- centered_moment_covariance(parameters)
  gradient <- moment_gradient(parameters, fixture)
  inverse_bread <- solve(crossprod(gradient, weighting_matrix %*% gradient))
  meat <- crossprod(
    gradient,
    weighting_matrix %*% moment_covariance %*% weighting_matrix %*% gradient
  )
  inverse_bread %*% meat %*% inverse_bread / sample_size
}

fixed_covariance_weight <- fixed_weighting_matrix
two_step_covariance_weight <- solve(centered_moment_covariance(two_step_parameters))
fixed_covariance_closed_form <- closed_form_covariance(fixed_parameters, fixed_covariance_weight)
two_step_covariance_closed_form <- closed_form_covariance(two_step_parameters, two_step_covariance_weight)

stopifnot(
  isTRUE(all.equal(
    unname(fixed_weight_fit$objective),
    drop(t(fixed_moments) %*% fixed_weighting_matrix %*% fixed_moments),
    tolerance = 1e-12
  )),
  isTRUE(all.equal(
    unname(two_step_fit$objective),
    drop(t(two_step_moments) %*% two_step_objective_weight %*% two_step_moments),
    tolerance = 1e-12
  )),
  isTRUE(all.equal(
    unname(two_step_specification_test[[1L]]),
    sample_size * unname(two_step_fit$objective),
    tolerance = 1e-12
  )),
  isTRUE(all.equal(
    unclass(two_step_fit$w),
    two_step_covariance_weight,
    tolerance = 1e-12
  )),
  isTRUE(all.equal(
    fixed_covariance,
    fixed_covariance_closed_form,
    tolerance = 1e-12
  )),
  isTRUE(all.equal(
    two_step_covariance,
    two_step_covariance_closed_form,
    tolerance = 1e-12
  )),
  all(is.finite(fixed_covariance)),
  all(is.finite(two_step_covariance)),
  all(diag(fixed_covariance) > 0),
  all(diag(two_step_covariance) > 0)
)

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

fit_result <- function(
    fit,
    parameters,
    moments,
    objective_weight,
    covariance_weight,
    specification_test,
    covariance,
    hansen_interpretation) {
  result <- list(
    parameter = unname(parameters[[1L]]),
    objective = unname(fit$objective),
    mean_moments = moments,
    objective_weighting_matrix = unname(objective_weight),
    covariance_weighting_matrix = unname(covariance_weight),
    covariance = covariance,
    standard_error = sqrt(unname(covariance[[1L, 1L]])),
    degrees_of_freedom = 1L
  )
  if (hansen_interpretation) {
    result$j_statistic <- unname(specification_test[[1L]])
    result$p_value <- unname(specification_test[[2L]])
  } else {
    result$r_specification_statistic <- unname(specification_test[[1L]])
    result$r_specification_p_value <- unname(specification_test[[2L]])
  }
  result
}

artifact <- list(
  metadata = list(
    artifact_id = "ME-GMM-SPECIFICATION-001",
    generated_utc = format(Sys.Date(), "%Y-%m-%d"),
    command = paste(
      "Rscript -e \"renv::load(project='verification/r');",
      "source('verification/r/model-estimation/generate_gmm_specification_oracle.R')\""
    ),
    generator = "verification/r/model-estimation/generate_gmm_specification_oracle.R",
    generator_sha256 = digest::digest(
      file = script_path,
      algo = "sha256",
      serialize = FALSE
    ),
    bestfit_source_commit = git_revision(repository_root),
    numerics_source_commit = git_revision(file.path(repository_root, "..", "Numerics")),
    r_version = R.version.string,
    gmm_version = as.character(utils::packageVersion("gmm")),
    jsonlite_version = as.character(utils::packageVersion("jsonlite")),
    fixture = paste(
      "One parameter and two moment restrictions:",
      "g1_i=x_i-theta and g2_i=z_i*(x_i-theta)."
    ),
    parameter_absolute_tolerance = 1e-5,
    objective_absolute_tolerance = 1e-8,
    covariance_absolute_tolerance = 1e-8,
    j_statistic_absolute_tolerance = 1e-7,
    p_value_absolute_tolerance = 1e-8,
    tolerance_rationale = paste(
      "The deterministic linear fixture has an analytical constant Jacobian.",
      "The parameter tolerance permits minor optimizer termination differences;",
      "the objective and covariance use 1e-8 absolute tolerances,",
      "and J=n*Q propagates the objective tolerance",
      "by the deterministic sample size n=10 to 1e-7."
    )
  ),
  data = lapply(seq_along(x_values), function(index) {
    list(x = unname(x_values[[index]]), z = unname(z_values[[index]]))
  }),
  initial_parameter = 1.5,
  formulas = list(
    moments = "g1_i=x_i-theta; g2_i=z_i*(x_i-theta)",
    objective = "Q(theta)=gBar(theta)'*W*gBar(theta)",
    hansen_j = "For the efficient two-step fit, J=n*Q(thetaHat)",
    fixed_weight_specification = "R gmm::specTest robust statistic; not n*Q for an arbitrary fixed weight",
    covariance = "Sandwich covariance reported by stats::vcov(gmm fit)",
    degrees_of_freedom = "q-p=1"
  ),
  fixed_weight = fit_result(
    fixed_weight_fit,
    fixed_parameters,
    fixed_moments,
    fixed_weighting_matrix,
    fixed_covariance_weight,
    fixed_specification_test,
    fixed_covariance,
    FALSE
  ),
  two_step = fit_result(
    two_step_fit,
    two_step_parameters,
    two_step_moments,
    two_step_objective_weight,
    two_step_covariance_weight,
    two_step_specification_test,
    two_step_covariance,
    TRUE
  ),
  interpretation = paste(
    "A positive-definite fixed weighting matrix is valid for overidentified GMM estimation.",
    "The chi-square Hansen J interpretation requires an asymptotically efficient/consistent",
    "weight such as the second-step inverse moment covariance; a generic fixed-weight",
    "objective is recorded for fitting parity but should not automatically be labeled Hansen J."
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
cat("Fixed-weight parameter:", format(fixed_parameters[[1L]], digits = 17), "\n")
cat("Fixed-weight covariance:", format(fixed_covariance[[1L, 1L]], digits = 17), "\n")
cat("Two-step Hansen J:", format(two_step_specification_test[[1L]], digits = 17), "\n")
cat("Two-step covariance:", format(two_step_covariance[[1L, 1L]], digits = 17), "\n")
