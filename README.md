# IT_viddil_monitoring

<img src="v3/assets/icon-preview-128.png" width="80" alt="IT_viddil_monitoring icon" />

A portable Windows resource monitor that helps identify applications and services consuming CPU, memory, and I/O. The interface is in Ukrainian.

Maintained by [Oleksii Yevtukhivskyi (@alexfromvn)](https://github.com/alexfromvn). Source code is available under the [MIT license](LICENSE).

## Features

- Average resource usage since launch, with an observation timer.
- A real-time mode that updates approximately once per second.
- Processes with the same name grouped into one entry.
- Ranked horizontal resource charts and separate CPU, memory, and I/O leaders.
- Search, light/dark themes, original vector icon, and translucent WPF panels.
- The monitor itself is excluded from rankings, resource leaders, and the process table.

## Download and run / Завантаження та запуск

Published versions are listed on the [Releases page](https://github.com/alexfromvn/IT_viddil_monitoring/releases). Until the first release is published, the page may be empty. Successful [GitHub Actions builds](https://github.com/alexfromvn/IT_viddil_monitoring/actions/workflows/build.yml) also provide temporary unsigned portable ZIP artifacts.

Для Intel/AMD виберіть `win-x64`; для Windows на Snapdragon/ARM — `win-arm64`. Розпакуйте ZIP і відкрийте `IT_viddil_monitoring_v3.exe`. .NET та шрифти включено до збірки. Основна ціль — Windows 11; запуск ARM64 потребує перевірки на ARM-комп'ютері.

**Signing status: unsigned; preparing an application to SignPath Foundation.** No certificate has been granted and no release is currently signed by the Foundation. If accepted, we intend to use free code signing provided by SignPath.io, with a certificate provided by SignPath Foundation. See the [code signing policy](CODE_SIGNING_POLICY.md). A signature does not guarantee that SmartScreen will never warn.

## Measurement limits

I/O is process read/write activity, not physical disk utilization. Working set can include shared memory pages. Services hosted in one process share its counters. Protected processes can deny access to counters. The combined leader adds the group's shares of measured CPU, memory, and I/O with equal weights; it is a diagnostic ranking, not proof that one application caused a slowdown.

The app monitors locally and does not send monitoring data to an external service. See [privacy](PRIVACY.md) and [distribution details](v3/DISTRIBUTION.md).

## Build

Requires Windows and a stable .NET 10 SDK. The runtime dependency is pinned in the project. From the repository root:

```powershell
dotnet restore v3/IT_viddil_monitoring.csproj -r win-x64 --source https://api.nuget.org/v3/index.json
dotnet publish v3/IT_viddil_monitoring.csproj -c Release -r win-x64 --self-contained true --no-restore -o releases/v3.0.1/windows-x64
```

To build portable packages for both architectures where local PowerShell policy permits scripts:

```powershell
pwsh -NoProfile -File v3/scripts/Build-Portable.ps1
```

The GitHub workflow builds from checked-in source on GitHub-hosted Windows runners and uploads unsigned packages. Signing will be configured after approval, using verified build artifacts and manual release approval. Existing private/local versions 1.0, 2.0, and 3.0 remain separate; this repository contains the current 3.0.1 source.

## Dependencies and design

Built with C#, WPF, .NET, and native Windows process/service counters. Inter fonts are included under SIL OFL 1.1. See [third-party notices](THIRD_PARTY_NOTICES.md). Graphics are original; Apple SF fonts and Apple design resource files are not distributed.

## Contributions and support

Please use GitHub issues for bug reports without uploading private process lists or diagnostic logs. Proposed changes are reviewed by the maintainer before release. New releases should update the pinned .NET runtime to a verified supported patch and retain the required license notices.
