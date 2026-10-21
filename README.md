# DamunsaAttendance

A .NET MAUI attendance and leave management app (FYP2) for a small transit/delivery team.

## Features
- Three roles: **Staff**, **Admin**, **Owner**
- Staff clock in/out and submit leave requests
- Admin approves/rejects leave and monitors today's attendance and late analytics
- Owner dashboard with summary and export
- Local storage with SQLite (`sqlite-net-pcl`)

## Tech stack
- .NET 9 / .NET MAUI
- SQLite (`sqlite-net-pcl`)
- CommunityToolkit.Maui, CommunityToolkit.Mvvm

## Getting started
1. Install Visual Studio 2022 (or VS Code with the .NET MAUI extension) with the .NET MAUI workload.
2. Open `DamunsaAttendance.sln`.
3. **Set the seed passwords before the first run.** In `DamunsaAttendance/SeedData.cs`, replace every `CHANGE_ME` with your own password (for admin, owner, and the six staff accounts). Do not commit your real passwords.
4. Build and run on Android (recommended) or Windows.

> The database is created on first run. To reseed, clear the app data (or uninstall the app) so the seed runs again.

## Project structure
```
DamunsaAttendance/
  Core/          App, AppShell, MauiProgram (startup and DI)
  Pages/         Login, Staff, Admin, Owner, Leave pages
  Models/        User, AttendanceRecord, LeaveRequest
  Services/      Db.cs (SQLite access)
  Converters/    SelectionConverter (UI highlight for selected buttons)
  SeedData.cs    Demo roster and accounts
```

## Notes
- Demo data only. Staff names are sample records for the FYP demo.
- Business rules: annual leave is 20 days by default and is deducted only when approved.
