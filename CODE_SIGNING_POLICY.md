# Code signing policy

## Current status

The project is preparing an application to [SignPath Foundation](https://signpath.org/). Approval has not been granted; current builds are **unsigned**. Mentioning the service does not imply endorsement or an issued certificate.

If accepted, the intended credit is: **Free code signing provided by SignPath.io, certificate by SignPath Foundation.** Signed Windows releases will identify SignPath Foundation as the certificate subject. Each release page will state whether its actual artifacts are signed.

## Maintainer and release roles

The current maintainer is [Oleksii Yevtukhivskyi (@alexfromvn)](https://github.com/alexfromvn), responsible for the source, code review, and release approval. The project currently has one maintainer; these roles are not independently staffed. Any additional reviewer or approver will be listed here before receiving access. Manual release approval is required for each signing request once signing is enabled.

## Planned signing process

1. Review the release source and version metadata in this public repository.
2. Build the application on GitHub-hosted Windows runners from the reviewed commit. Publish the unsigned executable as a GitHub Actions artifact so SignPath can verify its origin.
3. Submit that verified artifact through the SignPath GitHub integration after enrollment and service configuration are complete.
4. Require the designated maintainer's explicit approval in SignPath for each release. Do not automatically approve requests from pull requests or arbitrary forks.
5. Verify the returned Authenticode signature, certificate subject, timestamp, and product/version fields. Package the signed executable and generate fresh SHA-256 sums afterward.
6. Publish the signed portable packages with their source commit and signing status on GitHub Releases.

Only artifacts produced from this project's reviewed source and declared dependencies are eligible. The app will not install trust roots, weaken operating system protections, or distribute tools to bypass platform security policies.

GitHub, build, and signing accounts with maintainer access must use MFA before signing is enabled. This policy does not claim MFA or signing integration has already been configured. Signing credentials belong in protected service/CI secrets, never in the repository, application, or download archives. New access requires maintainer approval and the minimum needed scope.

The Foundation can revoke the certificate if its terms are violated. See its [terms and Code of Conduct](https://signpath.org/terms.html).
