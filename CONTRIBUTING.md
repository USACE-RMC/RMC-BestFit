# Contributing to RMC-BestFit

Thank you for your interest in contributing to RMC-BestFit. We welcome bug reports, feature requests, documentation improvements, validation results, and other feedback from the community.

## Review Capacity

RMC-BestFit is maintained by a small team within the U.S. Army Corps of Engineers Risk Management Center (USACE-RMC). Our capacity to review external pull requests is limited. We prioritize issues and bug reports, which are always welcome and will be reviewed as resources permit.

If you plan to submit a pull request, please open an issue first to discuss your proposed change. This helps avoid duplicated effort and ensures the contribution aligns with the project's direction.

## How to Contribute

### Report a Bug

If you find a bug, please open a bug report and include:

- Steps to reproduce the problem
- Input data and configuration, if applicable
- Expected behavior and actual behavior
- Software version, operating system, and .NET SDK version
- Relevant error messages, logs, screenshots, or small sample files

### Request a Feature

Feature requests are welcome. Please open a feature request describing:

- The use case or problem you are trying to solve
- How you envision the feature working
- How important the request is to your work
- Relevant statistical methods, guidance documents, or published literature

### Report Validation Results

Given the life-safety applications of this software, independent validation is especially valuable. If you have compared RMC-BestFit results against R packages, published tables, analytical solutions, or other flood-frequency software, please share the result through an issue.

### Submit a Pull Request

Pull requests may take several weeks or longer to review. Before submitting code:

1. Open an issue first to discuss the proposed change.
2. Keep changes focused and avoid unrelated formatting or refactoring.
3. Preserve the public API unless the issue explicitly discusses a breaking change.
4. Include XML documentation for public types and members.
5. Include unit tests for new model-library behavior.
6. Use MSTest for tests in this repository.
7. Ensure a clean build with zero errors and zero warnings.
8. For model-library changes, run the public model test project locally.

## Coding Standards

RMC-BestFit is used for flood risk and life-safety engineering decisions. Code should be explicit, well-tested, and numerically defensive.

- Prefer clear validation at method entry.
- Use `double.NegativeInfinity` for impossible log-likelihood values.
- Guard logarithms, divisions, transformations, and integration bounds.
- Keep statistical notation clear in names and comments.
- Add comments for non-obvious numerical or domain decisions, not for routine assignments.
- Do not suppress XML documentation warnings to pass a build.

## Developer Certificate of Origin

By submitting a pull request, you certify under the [Developer Certificate of Origin (DCO) Version 1.1](https://developercertificate.org/) that you have the right to submit the work under the license associated with this project and that you agree to the DCO.

All contributions will be released under the same license as the project. See [LICENSE](LICENSE).

## Federal Government Contributors

U.S. Federal law prevents the government from accepting gratuitous services unless certain conditions are met. By submitting a pull request, you acknowledge that your services are offered without expectation of payment and that you expressly waive any future pay claims against the U.S. Federal government related to your contribution.

If you are a U.S. Federal government employee and use a `*.mil` or `*.gov` email address, your contribution is understood to have been created in whole or in part as part of your official duties and is not subject to domestic copyright protection under 17 USC 105.

## Security

If you discover a security vulnerability, please do not open a public issue. See [SECURITY.md](SECURITY.md) for reporting guidance.

## License

RMC-BestFit is released under the [Zero-Clause BSD (0BSD)](LICENSE) license.