# Privacy policy

Effective date: 2026-10-07.

IT_viddil_monitoring runs locally on Windows. It reads process names, process IDs, CPU time, working set memory, process I/O counters, and names of Windows services associated with processes in order to display resource usage. Session measurements are held in memory until the application exits.

The application has no analytics, advertising, account system, automatic updater, or application code that sends monitoring data over the network. It does not upload process lists, usage readings, or error logs. It does not install certificates or change Windows security settings.

If an exception occurs, the app may append its time and exception details to `%TEMP%/IT_viddil_monitoring_3.0.1-error.log`. Such details may contain local file paths. This diagnostic file stays on the computer and may be deleted by the user. Users should review and redact any diagnostic file before choosing to share it in a bug report.

The self-contained .NET runtime may extract embedded runtime libraries into `%TEMP%/.net` during startup. These files are used to run the app locally.

Downloading the application or participating in this project's GitHub repository is subject to GitHub's own privacy practices. Any future code signing service processes release artifacts and developer account information; it does not receive users' live monitoring data from this app.

Questions can be raised through [GitHub issues](https://github.com/alexfromvn/IT_viddil_monitoring/issues), without including private process lists or logs.
