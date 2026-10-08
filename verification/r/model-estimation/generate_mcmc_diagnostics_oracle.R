# Generate independent rank-normalized R-hat and ESS reference values with the
# pinned R posterior package. C# tests consume only the committed JSON artifact.

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
  "mcmc-diagnostics-oracle.json"
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

finite_or_null <- function(value) {
  value <- unname(value)
  if (length(value) == 1L && is.finite(value)) value else NULL
}

matrix_as_chains <- function(value) {
  lapply(seq_len(ncol(value)), function(chain) unname(value[, chain]))
}

diagnostic_result <- function(name, draws, warmup = 0L, description) {
  evaluated <- if (warmup > 0L) {
    draws[(warmup + 1L):nrow(draws), , drop = FALSE]
  } else {
    draws
  }

  rank_rhat <- suppressWarnings(posterior::rhat(evaluated))
  bulk_ess <- suppressWarnings(posterior::ess_bulk(evaluated))
  lower_tail_ess <- suppressWarnings(posterior::ess_quantile(evaluated, 0.05))
  upper_tail_ess <- suppressWarnings(posterior::ess_quantile(evaluated, 0.95))
  tail_ess <- suppressWarnings(posterior::ess_tail(evaluated))
  scalar_ess <- suppressWarnings(min(bulk_ess, tail_ess))

  if (all(is.finite(c(bulk_ess, lower_tail_ess, upper_tail_ess, tail_ess)))) {
    stopifnot(
      isTRUE(all.equal(tail_ess, min(lower_tail_ess, upper_tail_ess), tolerance = 0)),
      isTRUE(all.equal(scalar_ess, min(bulk_ess, lower_tail_ess, upper_tail_ess), tolerance = 0))
    )
  }

  list(
    name = name,
    description = description,
    warmup = warmup,
    iteration_count = nrow(draws),
    chain_count = ncol(draws),
    chains = matrix_as_chains(draws),
    expected = list(
      rhat = finite_or_null(rank_rhat),
      bulk_ess = finite_or_null(bulk_ess),
      lower_tail_ess = finite_or_null(lower_tail_ess),
      upper_tail_ess = finite_or_null(upper_tail_ess),
      tail_ess = finite_or_null(tail_ess),
      scalar_ess = finite_or_null(scalar_ess)
    )
  )
}

make_ar1 <- function(iterations, chains, phi, seed) {
  set.seed(seed)
  innovations <- matrix(stats::rnorm(iterations * chains), nrow = iterations)
  draws <- matrix(0, nrow = iterations, ncol = chains)
  for (chain in seq_len(chains)) {
    for (iteration in 2:iterations) {
      draws[iteration, chain] <-
        phi * draws[iteration - 1L, chain] + innovations[iteration, chain]
    }
  }
  draws
}

set.seed(1201)
iid_normal <- matrix(stats::rnorm(200L * 4L), nrow = 200L, ncol = 4L)

set.seed(1202)
shifted_mean <- matrix(stats::rnorm(200L * 4L), nrow = 200L, ncol = 4L)
shifted_mean[, 4L] <- shifted_mean[, 4L] + 0.75

scale_mismatch <- sapply(0:3, function(chain) {
  centered <- (((1:100) * 37L + chain * 13L) %% 101L - 50L) / 10
  c(0.5, 0.5, 2, 2)[chain + 1L] * centered
})

ar1 <- make_ar1(240L, 4L, 0.85, 1203)

set.seed(1204)
sticky_tail <- matrix(stats::rnorm(240L * 4L), nrow = 240L, ncol = 4L)
sticky_tail[181:240, 4L] <- sticky_tail[181:240, 4L] + 5

set.seed(1205)
ties <- round(matrix(stats::rnorm(160L * 4L), nrow = 160L, ncol = 4L), 1)

set.seed(1206)
warmup_fixture <- matrix(stats::rnorm(240L * 4L), nrow = 240L, ncol = 4L)
warmup_fixture[1:80, ] <- sweep(
  warmup_fixture[1:80, , drop = FALSE],
  2L,
  c(-4, -1, 2, 5),
  FUN = "+"
)

modulo_fixture <- sapply(0:3, function(chain) {
  ((1:64) * 17L + chain * 11L) %% 31L + chain * 31L
})

constant_fixture <- matrix(1, nrow = 40L, ncol = 4L)

fixtures <- list(
  diagnostic_result(
    "iid_normal",
    iid_normal,
    description = "Four independent seeded standard-normal chains."
  ),
  diagnostic_result(
    "shifted_mean",
    shifted_mean,
    description = "One chain has a persistent location shift."
  ),
  diagnostic_result(
    "scale_mismatch",
    scale_mismatch,
    description = "Chains share a center but have two materially different scales."
  ),
  diagnostic_result(
    "ar1",
    ar1,
    description = "Four seeded AR(1) chains with phi=0.85."
  ),
  diagnostic_result(
    "sticky_tail",
    sticky_tail,
    description = "One chain remains in an elevated upper-tail state for its final quarter."
  ),
  diagnostic_result(
    "ties",
    ties,
    description = "Rounded normal draws exercise pooled average ranks."
  ),
  diagnostic_result(
    "warmup",
    warmup_fixture,
    warmup = 80L,
    description = "Dispersed first 80 draws are removed before R-hat and parity evaluation."
  ),
  diagnostic_result(
    "modulo",
    modulo_fixture,
    description = "Integer-valued modulo fixture avoids serialization-dependent floating tie ranks."
  ),
  diagnostic_result(
    "constant",
    constant_fixture,
    description = "Constant chains produce unavailable R-hat and ESS diagnostics."
  )
)

artifact <- list(
  metadata = list(
    artifact_id = "ME-MCMC-DIAGNOSTICS-001",
    generated_utc = format(Sys.Date(), "%Y-%m-%d"),
    command = paste(
      "Rscript -e \"renv::load(project='verification/r');",
      "source('verification/r/model-estimation/generate_mcmc_diagnostics_oracle.R')\""
    ),
    generator = "verification/r/model-estimation/generate_mcmc_diagnostics_oracle.R",
    generator_sha256 = digest::digest(
      file = script_path,
      algo = "sha256",
      serialize = FALSE
    ),
    bestfit_source_commit = git_revision(repository_root),
    numerics_source_commit = git_revision(file.path(repository_root, "..", "Numerics")),
    r_version = R.version.string,
    posterior_version = as.character(utils::packageVersion("posterior")),
    jsonlite_version = as.character(utils::packageVersion("jsonlite")),
    rhat_absolute_tolerance = 1e-10,
    ess_absolute_tolerance = 1e-8,
    scalar_ess_definition = "min(ess_bulk, ess_quantile_05, ess_quantile_95)",
    single_chain_rhat_contract = "NaN for compatibility; independent-chain comparison unavailable",
    references = list(
      paper = "Vehtari et al. (2021), Bayesian Analysis 16(2), 667-718",
      rhat = "https://mc-stan.org/posterior/reference/rhat.html",
      ess = "https://mc-stan.org/posterior/reference/ess_bulk.html"
    )
  ),
  fixtures = fixtures,
  status = "Independent R posterior 1.7.0 oracle"
)

dir.create(dirname(output_path), recursive = TRUE, showWarnings = FALSE)
jsonlite::write_json(
  artifact,
  output_path,
  pretty = TRUE,
  auto_unbox = TRUE,
  digits = NA,
  na = "null",
  null = "null"
)

cat("Wrote", output_path, "\n")
for (fixture in fixtures) {
  cat(
    fixture$name,
    "R-hat=", format(fixture$expected$rhat, digits = 17),
    "scalar ESS=", format(fixture$expected$scalar_ess, digits = 17),
    "\n"
  )
}
