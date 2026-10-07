# IT_viddil_monitoring 3.0.1 — переносна версія

## Запуск на іншому комп’ютері

Розпакуйте ZIP і відкрийте файл `IT_viddil_monitoring*.exe` подвійним кліком. Установлювати .NET чи Windows App SDK окремо не потрібно: збірка містить .NET 10.0.12 та WPF. Налаштування й дані старої версії не потрібні. Версії 1.0 та 2.0 залишено без змін. Версія 1.0 збережена окремо в `releases/v1.0`; вихідний код — у `versions/v1.0`.

Виберіть архітектуру в **Параметри Windows → Система → Про систему → Тип системи**:

| Комп’ютер | Архів | Папка з EXE |
| --- | --- | --- |
| 64-бітний процесор Intel або AMD | `IT_viddil_monitoring_v3.0.1_win-x64_portable.zip` | `windows-x64` |
| Windows на ARM, наприклад Snapdragon | `IT_viddil_monitoring_v3.0.1_win-arm64_portable.zip` | `windows-arm64` |

Основна ціль — актуальна Windows 11; Windows 10 потребує окремої перевірки на конкретному ПК. На дату збірки офіційна підтримка .NET 10 на Windows 10 обмежена підтримуваними LTSC/Enterprise випусками. Windows 7/8.1 та 32-бітна Windows не підтримуються цими архівами. Архітектуру EXE не можна довільно змінити перейменуванням файлу. [Підтримка Windows у .NET](https://learn.microsoft.com/en-us/dotnet/core/install/windows), [архітектури переносних збірок](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview).

Програма запускається зі звичайними правами користувача. Деякі захищені процеси й служби обмежують доступ до лічильників; цифровий підпис не знімає цих обмежень. Збірку ARM64 потрібно перевіряти на ARM-комп’ютері; створення файлу на x64 не замінює таку перевірку.

Під час першого запуску .NET розпаковує вбудовані системні бібліотеки у `%TEMP%/.net`. Потрібні вільне місце та доступ до тимчасової папки. Встановлення програми у системні каталоги не потрібне. Самодостатній EXE забезпечує наявність залежностей, але корпоративні політики, антивірус і засоби контролю запуску можуть окремо обмежувати виконання. [Розпакування single-file у .NET](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview).

## Підпис, сертифікат і Windows SmartScreen

Поточний випуск не має довіреного цифрового підпису видавця. Під час завантаження або запуску Windows може показати попередження SmartScreen; на корпоративних ПК політика може повністю заборонити запуск. Наявність усіх залежностей у EXE та довіра Windows до видавця — окремі умови.

Самопідписаний «власний» сертифікат не має автоматичної довіри на інших комп’ютерах і не усуває попередження SmartScreen. Цей випуск не додає сертифікати до Windows і не змінює налаштування захисту. [Офіційні варіанти підпису програм Windows](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options).

Для подальшого поширення можна застосувати довірений Authenticode-підпис від центру сертифікації або Microsoft Artifact Signing, якщо видавець відповідає умовам сервісу. Для Microsoft Store є окремий шлях через MSIX. Навіть новий файл із довіреним підписом може певний час показувати попередження, доки накопичується репутація; дорогий EV-сертифікат також не гарантує їх відсутність. Windows Smart App Control враховує підписи від довірених постачальників і наразі потребує RSA. [Репутація SmartScreen](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation), [підпис для Smart App Control](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control).

Підписують остаточний EXE після збірки, із SHA-256 і часовою міткою RFC 3161. Після підпису потрібно заново сформувати ZIP та SHA256. Приватний ключ або пароль сертифіката не додають до вихідного коду, архіву чи програми. [Часові мітки Authenticode](https://learn.microsoft.com/en-us/windows/win32/seccrypto/time-stamping-authenticode-signatures).

## Перевірка файлу

`SHA256SUMS.txt` містить контрольні суми EXE й архівів. Щоб порівняти EXE з сумою в його папці, відкрийте PowerShell у цій папці:

```powershell
Get-FileHash -LiteralPath '.\IT_viddil_monitoring_v3.exe' -Algorithm SHA256
```

Якщо EXE має іншу назву, підставте її у команду. Контрольна сума дозволяє виявити зміну файлу, коли її порівнюють із сумою з довіреного джерела; вона не замінює цифровий підпис.

## Повторна збірка

Потрібні Windows, стабільний .NET 10 SDK і доступ до офіційного NuGet. Із папки `v3`:

```powershell
powershell -NoProfile -File .\scripts\Build-Portable.ps1
```

Скрипт послідовно створює самодостатні стиснені single-file збірки для x64 та ARM64, ZIP і SHA256 у `releases/v3.0.1`. Він не змінює випуск 1.0. Для однієї архітектури використайте `-RuntimeIdentifiers win-x64` або `-RuntimeIdentifiers win-arm64`.

Якщо політика комп’ютера забороняє запуск сценаріїв PowerShell, можна виконати команди компілятора без зміни цієї політики (із папки `v3`):

```powershell
dotnet restore IT_viddil_monitoring.csproj -r win-x64 --source https://api.nuget.org/v3/index.json
dotnet publish IT_viddil_monitoring.csproj -c Release -r win-x64 --self-contained true --no-restore -o ../releases/v3.0.1/windows-x64
```

Для ARM64 замініть `win-x64` на `win-arm64` і папку призначення на `windows-arm64`. Параметри самодостатньої збірки та версію runtime задано в проєкті.

Версію runtime явно зафіксовано на **10.0.12**: це останній стабільний випуск за офіційними метаданими на **07.10.2026**, опублікований 08.09.2026. Самодостатні програми отримують виправлення runtime через нову збірку програми, тому перед наступним випуском потрібно перевірити актуальний патч .NET. WPF збирається без trimming, оскільки .NET SDK не підтримує його для WPF. [Метадані випусків .NET 10](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json), [вибір RuntimeFrameworkVersion](https://learn.microsoft.com/en-us/dotnet/core/tools/csproj#runtimeframeworkversion), [обмеження trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities#wpf).



