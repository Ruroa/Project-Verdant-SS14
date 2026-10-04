# Disabled GitHub Actions workflows

These inherited workflows are preserved here but do not run because GitHub only
loads workflow files directly inside `.github/workflows`.

- `yaml-linter.yml`: Disabled because its trigger block is commented out, making
  the workflow invalid and causing an immediate failure on pushes.
- `labeler-review.yml`: Disabled because two job conditions reference an `env`
  context that is unavailable at that level, causing workflow validation errors.
- `benchmarks.yml`: Disabled because it relies on the upstream Space Wizards
  CentComm benchmark host and repository secrets.
- `build-docfx.yml`: Disabled because the scheduled documentation build is not
  currently used by Project Verdant and repeatedly fails during DocFX generation.

Move a corrected file back into `.github/workflows` to enable it again.
