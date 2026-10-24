# Changelog

## 3.0.2 — 2026-10-25

- Select the dark theme on every application launch by default. The theme button still switches to the light theme manually.
- Include a launcher and Ukrainian quick-start instructions in portable packages.
- Fix `Unknown option` when double-clicking `Запустити програму.cmd` without arguments: normalize an unset mode environment variable to an empty string.
- Keep hash checks before unblocking or running an EXE. Newly built CI packages add their own executable hash to their packaged launcher.
- Verify the ordinary no-argument launcher path locally: application process started, the download marker was removed, and EXE bytes remained unchanged.

## 3.0.1 — 2026-10-07

- Publish the source under the MIT license.
- Include an original vector application icon and a multi-resolution Windows EXE icon.
- Exclude the monitor itself from the process list, charts, and resource leaders.
- Build portable x64 and ARM64 executables and publish privacy, dependency, and signing notices.
