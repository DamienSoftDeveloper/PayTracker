# PayTracker

**A Windows and Android app for tracking bills, planning a monthly budget, and understanding spending.**

PayTracker brings recurring payments, income, allowance purchases, and monthly statistics into one place. It started with a practical need: knowing what has been paid, what is still due, and how much money could remain after meeting those commitments.

Built with **C#, .NET 10 MAUI, and SQLite**, with a shared codebase and a navy-and-teal interface that supports light and dark themes.

**[Download PayTracker V1.0.0](https://github.com/DamienSoftDeveloper/PayTracker/releases/tag/v1.0.0)** · [Windows installation guide](INSTALL-WINDOWS.txt) · [Report an issue](https://github.com/DamienSoftDeveloper/PayTracker/issues)

## Features

| Screen | What it does |
|---|---|
| **Dashboard** | Shows scheduled, paid, remaining, and overdue bill totals, plus the next unpaid payments. |
| **Track Payments** | Adds, edits, and removes bills; records paid dates; and manages recurring monthly schedules. |
| **Income** | Saves monthly income and balance inputs, tracks allowance purchases, and calculates projected balances and savings-target shortfalls. |
| **Statistics** | Displays up to six months of recorded spending and projected surplus, with exact figures and previous-month comparisons. |

### Recurring payments

- Each month has its own payment record and paid status.
- A due day of 31 uses the last available day in shorter months.
- Repeated refreshes do not create duplicate occurrences.
- Editing a schedule changes defaults for records that have not yet been generated; existing monthly records keep their values.
- Deleting one month's occurrence does not delete the schedule or cause that occurrence to reappear.

### Monthly budgeting

Choose between a **monthly plan** using the opening bank balance and a **current-month forecast** using today's manually entered balance and income still expected.

The allowance calculation reserves the unused budget and accounts for actual overspending without deducting both the budget and its purchases. For example, spending £110 against a £100 allowance shows a £10 overspend.

Savings targets are reservations, not recorded transfers. Results are projections based on the figures entered, and a positive month-end forecast does not guarantee funds are available before every bill's due date.

### Statistics that keep dates meaningful

Recorded spending groups bills by their **paid date**, while scheduled totals use their **billing month**. A bill due in October but paid in September therefore appears in September's spending and October's scheduled commitments.

Missing income inputs are shown as **Not entered**, and the current month is marked as in progress. Browsing Statistics does not generate additional recurring payment records.

## Download and install

Open the **[V1.0.0 release page](https://github.com/DamienSoftDeveloper/PayTracker/releases/tag/v1.0.0)** and select the Windows installer ZIP or Android APK from its assets. The automatically generated source-code archives contain the project source rather than ready-to-install apps.

### Windows

1. Download and extract the Windows installer ZIP.
2. Read the included `INSTALL-WINDOWS.txt`.
3. Follow its instructions to trust the supplied public certificate and install the MSIX package and any required dependencies.
4. Launch **PayTracker** from Start and optionally pin it to the taskbar.

This is a **self-signed personal/test release**. Only trust its certificate if you trust the source of the download. Certificate trust does not bypass Smart App Control or an organisation's application-control policy. Ask an administrator for an approved installation route on a managed computer.

### Android

1. Download the signed APK from the release page to your phone.
2. Open it and, if prompted, allow installation from the browser or file manager used to open it.
3. Install PayTracker and launch it from the app drawer.

Only the signed APK is needed for Android installation. V1's local APK uses the **Android debug signing key**; permanent release signing remains a future distribution improvement. Changing signing keys can require a reinstall, so preserve any data you need before replacing an existing installation.

Visual Studio is not required to use either installed app. Windows and Android installations have separate local data.

## Technology and structure

| Technology | Role |
|---|---|
| C# / .NET 10 | Application code and calculations |
| .NET MAUI / Shell | Shared Windows and Android UI and navigation |
| XAML and C# layouts | Resources, styling, and screen composition |
| SQLite / sqlite-net-pcl | Persistent payments, budgets, and allowance purchases |
| MAUI GraphicsView / ICanvas | Spending and projection charts |

The project separates data models, storage and calculation services, and page presentation. Shared UI helpers provide themed controls and adaptive card layouts. Monetary values are stored as whole pence to preserve exact amounts.

## Run from source

Use Visual Studio with the **.NET MAUI workload**, the **.NET 10 SDK**, and the relevant Windows/Android development tools installed.

1. Clone this repository and open `PayTracker.csproj` in Visual Studio.
2. Restore NuGet packages.
3. Select **Debug** and either **Windows Machine**, an Android emulator, or an authorised physical Android device.
4. Build and run.

To build the Windows Debug target from the project directory:

```powershell
dotnet build PayTracker.csproj -c Debug -f net10.0-windows10.0.19041.0
```

Windows Release publishing references the original developer's signing certificate. Configure your own matching publisher identity and signing certificate before packaging a release. Keep private certificates, Android keystores, passwords, and personal financial data outside the repository.

The project also contains MAUI template targets for iOS and Mac Catalyst. **Windows and Android are the platforms tested for V1**; the other targets are not advertised as supported releases.

## Current scope and limitations

- Financial figures are entered manually; there is no bank integration.
- SQLite provides local persistence, with no cloud or cross-device synchronisation.
- Payment tracking is in-app; scheduled push notifications are not implemented.
- Forecasts depend on complete, consistent entries and do not model daily cash flow, credit-card debt, or multiple bank accounts.
- Editing or deleting entries revises statistics; there is no immutable transaction ledger.
- Backup/export and confirmed savings-transfer tracking are not yet implemented. Uninstalling or resetting the app can remove local records.

## Project background

PayTracker was built as a practical learning and portfolio project, progressing from a four-page MAUI application to persistent storage, recurring payments, budgeting calculations, statistics, and installable builds.

The work covers cross-platform development, asynchronous SQLite operations, input validation, date handling, financial calculations, chart rendering, responsive layouts, and application packaging. V1 has been installed and tested on a Windows laptop and an Android phone.

## Future improvements

- Permanent Android release signing and trusted Windows distribution.
- Backup, export, and restore.
- Payment notifications and income-date cash-flow planning.
- Optional secure synchronisation and richer spending categories.

## Author and licence

Created by **[Damien Sobha](https://github.com/DamienSoftDeveloper)**.

Licensed under the **[MIT License](LICENSE)**.

