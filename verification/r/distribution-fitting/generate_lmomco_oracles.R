# Generate independent lmomco fitting oracles for the GLO and GNO families.
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
  "distribution-fitting",
  "lmomco-family-oracles.json"
)

probabilities <- seq(0.025, 0.975, length.out = 39)
cdf_evaluation_probability <- 0.35
quantile_evaluation_probability <- 0.90

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

generate_family <- function(name, type, source_parameters) {
  source <- lmomco::vec2par(source_parameters, type = type)
  data <- lmomco::par2qua(probabilities, source)
  fitted <- lmomco::mle2par(
    data,
    type = type,
    silent = TRUE,
    null.on.not.converge = TRUE,
    method = "Nelder-Mead",
    control = list(reltol = 1e-13, maxit = 100000)
  )
  if (is.null(fitted)) {
    stop("lmomco MLE failed for ", name)
  }

  evaluation_x <- lmomco::par2qua(cdf_evaluation_probability, source)
  densities <- lmomco::par2pdf(data, fitted)
  if (any(!is.finite(densities)) || any(densities <= 0)) {
    stop("lmomco returned an invalid fitted density for ", name)
  }

  list(
    data = unname(data),
    lmomco_parameter_order = c("xi", "alpha", "kappa"),
    lmomco_parameters = unname(fitted$para),
    numerics_parameter_order = c("xi", "alpha", "kappa"),
    numerics_parameters = unname(fitted$para),
    parameter_crosswalk = paste(
      "Direct lmomco-to-Numerics mapping (xi, alpha, kappa);",
      "the Hosking shape sign and support convention are identical."
    ),
    fit_method = paste(
      "lmomco::mle2par 2.5.7 with R optim Nelder-Mead,",
      "reltol=1e-13, maxit=100000, L-moment initialization"
    ),
    convergence_code = unname(fitted$optim$convergence),
    maximum_log_likelihood = unname(sum(log(densities))),
    evaluation = list(
      x = unname(evaluation_x),
      pdf = unname(lmomco::par2pdf(evaluation_x, fitted)),
      cdf = unname(lmomco::par2cdf(evaluation_x, fitted)),
      probability = quantile_evaluation_probability,
      quantile = unname(lmomco::par2qua(quantile_evaluation_probability, fitted))
    )
  )
}

artifact <- list(
  metadata = list(
    artifact_id = "DF-LMOMCO-FAMILY-ORACLES-001",
    generated_utc = format(Sys.Date(), "%Y-%m-%d"),
    command = "scripts/generate-verification-data.ps1 -Family distribution-fitting",
    generator = "verification/r/distribution-fitting/generate_lmomco_oracles.R",
    generator_sha256 = digest::digest(
      file = script_path,
      algo = "sha256",
      serialize = FALSE
    ),
    bestfit_source_commit = git_revision(repository_root),
    numerics_source_commit = git_revision(file.path(repository_root, "..", "Numerics")),
    r_version = R.version.string,
    lmomco_version = as.character(utils::packageVersion("lmomco")),
    jsonlite_version = as.character(utils::packageVersion("jsonlite")),
    probabilities = "39 equally spaced nonexceedance probabilities from 0.025 through 0.975",
    absolute_tolerance = 1e-8,
    relative_tolerance = 1e-7,
    scaled_parameter_tolerance = 1e-5
  ),
  families = list(
    GeneralizedLogistic = generate_family(
      "GeneralizedLogistic",
      "glo",
      c(75.0, 10.0, 0.15)
    ),
    GeneralizedNormal = generate_family(
      "GeneralizedNormal",
      "gno",
      c(50.0, 10.0, -0.30)
    )
  )
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
cat("Families:", length(artifact$families), "\n")
