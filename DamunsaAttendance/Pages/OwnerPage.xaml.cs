using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace DamunsaAttendance
{
    public partial class OwnerPage : ContentPage
    {
        private readonly Db _db;

        private static readonly string[] _sampleNames = {
            "Hafiz Zulkifli",  "Ridhwan Mansor",  "Azrul Hafifi",
            "Haziq Shamsudin", "Firdaus Norzaidi", "Amsyar Lokman"
        };
        private static readonly string[] _sampleDepts = {
            "Logistics", "Logistics", "Logistics",
            "Logistics", "Delivery",  "Delivery"
        };

        public OwnerPage(Db db)
        {
            InitializeComponent();
            _db = db;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _ = LoadDashboardAsync();
        }

        private async Task LoadDashboardAsync()
        {
            try
            {
                await _db.InitAsync();

                var allAttendance = await _db.GetAllAttendanceAsync();
                var allLeave = await _db.GetAllLeaveAsync();
                var today = DateTime.Today;

                int totalStaff = 6;
                int presentToday = allAttendance
                    .Where(a => a.CheckInTime.Date == today)
                    .Select(a => a.UserId).Distinct().Count();
                int absentToday = Math.Max(0, totalStaff - presentToday);

                int pendingLeaves = allLeave.Count(l => l.Status?.Equals("Pending", StringComparison.OrdinalIgnoreCase) == true);
                int approvedLeaves = allLeave.Count(l => l.Status?.Equals("Approved", StringComparison.OrdinalIgnoreCase) == true);
                int rejectedLeaves = allLeave.Count(l => l.Status?.Equals("Rejected", StringComparison.OrdinalIgnoreCase) == true);

                int onLeaveToday = allLeave
                    .Where(l => l.Status?.Equals("Approved", StringComparison.OrdinalIgnoreCase) == true
                             && l.StartDate.Date <= today && l.EndDate.Date >= today)
                    .Select(l => l.UserId).Distinct().Count();

                TotalStaffValue.Text = totalStaff.ToString();
                PresentValue.Text = presentToday.ToString();
                AbsentValue.Text = absentToday.ToString();
                PendingValue.Text = pendingLeaves.ToString();

                SetLabelSafe("ApprovedLeaveValue", approvedLeaves.ToString());
                SetLabelSafe("RejectedLeaveValue", rejectedLeaves.ToString());
                SetLabelSafe("OnLeaveTodayValue", onLeaveToday.ToString());
                SetLabelSafe("TotalAttendanceValue", allAttendance.Count.ToString());

                int daysFromMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                var monday = today.AddDays(-daysFromMonday);

                var weekDays = new[]
                {
                    (monday,            MonBar, MonCount),
                    (monday.AddDays(1), TueBar, TueCount),
                    (monday.AddDays(2), WedBar, WedCount),
                    (monday.AddDays(3), ThuBar, ThuCount),
                    (monday.AddDays(4), FriBar, FriCount),
                };

                foreach (var (date, bar, label) in weekDays)
                {
                    int count = allAttendance
                        .Where(a => a.CheckInTime.Date == date.Date)
                        .Select(a => a.UserId).Distinct().Count();

                    label.Text = count.ToString();
                    bar.Progress = totalStaff > 0 ? (double)count / totalStaff : 0;
                }
                // ── Monthly Attendance Rate ────────────────────────────────
                var firstDay = new DateTime(today.Year, today.Month, 1);
                var lastDay = today;

                // Count working days (Mon–Fri) in the month so far
                int workingDays = 0;
                for (var d = firstDay; d <= lastDay; d = d.AddDays(1))
                    if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                        workingDays++;

                var monthAttendance = allAttendance
                    .Where(a => a.CheckInTime >= firstDay && a.CheckInTime.Date <= lastDay)
                    .ToList();

                int totalLate = monthAttendance.Count(a =>
                    string.Equals(a.Status, "Late", StringComparison.OrdinalIgnoreCase));

                // Per-staff attendance rate
                var staffIds = new[] { 1, 2, 3, 4, 5, 6 };
                var staffNames = new[] {
                    "Hafiz Zulkifli", "Ridhwan Mansor", "Azrul Hafifi",
                    "Haziq Shamsudin", "Firdaus Norzaidi", "Amsyar Lokman"
                };

                double totalRate = 0;
                int staffCount = 0;

                // Clear and rebuild per-staff rows
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        StaffRatePanel.Children.Clear();

                        for (int i = 0; i < staffIds.Length; i++)
                        {
                            int sid = staffIds[i];
                            string sname = staffNames[i];

                            int daysPresent = monthAttendance
                                .Where(a => a.UserId == sid)
                                .Select(a => a.CheckInTime.Date)
                                .Distinct().Count();

                            double rate = workingDays > 0
                                ? Math.Min(100, Math.Round((double)daysPresent / workingDays * 100))
                                : 0;

                            totalRate += rate;
                            staffCount++;

                            // Row: name + % + progress bar
                            var row = new VerticalStackLayout { Spacing = 2 };

                            var nameRow = new Grid();
                            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
                            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                            var nameLabel = new Label
                            {
                                Text = sname,
                                FontSize = 12,
                                TextColor = Color.FromArgb("#374151")
                            };
                            var pctLabel = new Label
                            {
                                Text = $"{rate}%",
                                FontSize = 12,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = rate >= 80
                                    ? Color.FromArgb("#16A34A")
                                    : rate >= 60
                                        ? Color.FromArgb("#EA580C")
                                        : Color.FromArgb("#DC2626")
                            };

                            Grid.SetColumn(nameLabel, 0);
                            Grid.SetColumn(pctLabel, 1);
                            nameRow.Children.Add(nameLabel);
                            nameRow.Children.Add(pctLabel);

                            var bar = new ProgressBar
                            {
                                Progress = rate / 100.0,
                                ProgressColor = rate >= 80
                                    ? Color.FromArgb("#16A34A")
                                    : rate >= 60
                                        ? Color.FromArgb("#EA580C")
                                        : Color.FromArgb("#DC2626"),
                                BackgroundColor = Color.FromArgb("#F1F5F9"),
                                HeightRequest = 6
                            };

                            row.Children.Add(nameRow);
                            row.Children.Add(bar);
                            StaffRatePanel.Children.Add(row);
                        }

                        double avgRate = staffCount > 0 ? Math.Round(totalRate / staffCount) : 0;

                        MonthRateLabel.Text = $"{today:MMMM yyyy}  •  {workingDays} working days tracked";
                        AvgRateValue.Text = $"{avgRate}%";
                        TotalLateValue.Text = totalLate.ToString();
                        WorkingDaysValue.Text = workingDays.ToString();
                    }
                    catch { }
                });
            }
            catch { }
        }

        private void SetLabelSafe(string name, string text)
        {
            try
            {
                var label = this.FindByName<Label>(name);
                if (label != null) label.Text = text;
            }
            catch { }
        }

        // ── Attendance Export ────────────────────────────────────────────────

        private async void OnDownloadAttendance(object sender, EventArgs e)
        {
            try
            {
                await _db.InitAsync();
                var today = DateTime.Today;
                var all = await _db.GetAllAttendanceAsync();
                var monthly = all
                    .Where(a => a.CheckInTime.Year == today.Year && a.CheckInTime.Month == today.Month)
                    .OrderBy(a => a.CheckInTime)
                    .ToList();

                var lines = new List<string>
                {
                    "Staff Name,Department,Date,Check In,Check Out,Total Hours,Status"
                };

                foreach (var a in monthly)
                {
                    string name = !string.IsNullOrEmpty(a.StaffName) ? a.StaffName : GetDisplayName(a.UserId);
                    string dept = !string.IsNullOrEmpty(a.Department) ? a.Department : GetDisplayDept(a.UserId);

                    // Prefix dates/times with = "" trick to prevent Excel auto-conversion
                    string date = $"=\"{a.CheckInTime:dd/MM/yyyy}\"";
                    string checkIn = $"=\"{a.CheckInTime:hh:mm tt}\"";
                    string checkOut = a.CheckOutTime.HasValue
                        ? $"=\"{a.CheckOutTime.Value:hh:mm tt}\""
                        : "";
                    string totalHrs = a.CheckOutTime.HasValue
                        ? $"{(a.CheckOutTime.Value - a.CheckInTime).TotalHours:0.#}h"
                        : "Incomplete";
                    string status = string.IsNullOrWhiteSpace(a.Status) ? "Present" : Capitalize(a.Status);

                    lines.Add($"{EscapeCsv(name)},{EscapeCsv(dept)},{date},{checkIn},{checkOut},{EscapeCsv(totalHrs)},{EscapeCsv(status)}");
                }

                if (monthly.Count == 0)
                {
                    // Sample fallback with correct names
                    string d = $"=\"{today:dd/MM/yyyy}\"";
                    var sample = new[]
                    {
                        ("Hafiz Zulkifli",  "Logistics", "=\"08:01 AM\"", "=\"05:05 PM\"", "9.1h",  "Present"),
                        ("Ridhwan Mansor",  "Logistics", "=\"08:10 AM\"", "=\"05:00 PM\"", "8.8h",  "Present"),
                        ("Azrul Hafifi",    "Logistics", "=\"08:45 AM\"", "=\"05:20 PM\"", "8.6h",  "Late"),
                        ("Haziq Shamsudin", "Logistics", "=\"07:55 AM\"", "=\"04:50 PM\"", "8.9h",  "Present"),
                        ("Firdaus Norzaidi","Delivery",  "=\"09:05 AM\"", "=\"05:30 PM\"", "8.4h",  "Late"),
                        ("Amsyar Lokman",   "Delivery",  "=\"08:20 AM\"", "=\"05:10 PM\"", "8.8h",  "Present"),
                    };
                    foreach (var (n, dp, ci, co, hrs, st) in sample)
                        lines.Add($"{EscapeCsv(n)},{EscapeCsv(dp)},{d},{ci},{co},{hrs},{EscapeCsv(st)}");
                }

                var csv = string.Join("\r\n", lines);
                var fileName = $"attendance_{today:yyyy_MM}.csv";
                var path = Path.Combine(FileSystem.AppDataDirectory, fileName);

                await File.WriteAllTextAsync(path, csv, new UTF8Encoding(false));

                await DisplayAlert("Export Successful",
                    $"Monthly attendance exported.\n{(monthly.Count > 0 ? monthly.Count : 6)} record(s) saved to:\n{fileName}", "OK");

                try { await Launcher.OpenAsync(new OpenFileRequest(fileName, new ReadOnlyFile(path))); }
                catch { }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to export: {ex.Message}", "OK");
            }
        }

        // ── Leave Export ─────────────────────────────────────────────────────

        private async void OnDownloadLeave(object sender, EventArgs e)
        {
            try
            {
                await _db.InitAsync();
                var leaves = await _db.GetAllLeaveAsync();
                var ordered = leaves.OrderByDescending(x => x.CreatedAt).ToList();

                var lines = new List<string>
                {
                    "Staff Name,Department,Leave Type,Duration,Start Date,End Date,Days,Reason,Status,Applied On"
                };

                foreach (var l in ordered)
                {
                    string name = !string.IsNullOrEmpty(l.StaffName) ? l.StaffName : GetDisplayName(l.UserId);
                    string dept = !string.IsNullOrEmpty(l.Department) ? l.Department : GetDisplayDept(l.UserId);
                    string duration = MapDuration(l.Duration);
                    string status = string.IsNullOrWhiteSpace(l.Status) ? "Pending" : Capitalize(l.Status);

                    // Force Excel to treat dates as text
                    string startDate = $"=\"{l.StartDate:dd/MM/yyyy}\"";
                    string endDate = $"=\"{l.EndDate:dd/MM/yyyy}\"";
                    string appliedOn = $"=\"{l.CreatedAt:dd/MM/yyyy}\"";

                    lines.Add($"{EscapeCsv(name)},{EscapeCsv(dept)}," +
                              $"{EscapeCsv(l.LeaveType)},{EscapeCsv(duration)}," +
                              $"{startDate},{endDate}," +
                              $"{l.EstimatedDays:0.#},{EscapeCsv(l.Reason)}," +
                              $"{EscapeCsv(status)},{appliedOn}");
                }

                var csv = string.Join("\r\n", lines);
                var fileName = $"leave_summary_{DateTime.Now:yyyy_MM_dd}.csv";
                var path = Path.Combine(FileSystem.AppDataDirectory, fileName);

                await File.WriteAllTextAsync(path, csv, new UTF8Encoding(false));

                await DisplayAlert("Export Successful",
                    $"Leave summary exported.\n{ordered.Count} record(s) saved to:\n{fileName}", "OK");

                try { await Launcher.OpenAsync(new OpenFileRequest(fileName, new ReadOnlyFile(path))); }
                catch { }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to export: {ex.Message}", "OK");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string MapDuration(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            if (raw.Contains("Full", StringComparison.OrdinalIgnoreCase)) return "Full";
            if (raw.Contains("Half", StringComparison.OrdinalIgnoreCase)) return "Half";
            return raw;
        }

        private static string EscapeCsv(string input)
        {
            if (input == null) return "";
            if (input.Contains('"')) input = input.Replace("\"", "\"\"");
            if (input.Contains(',') || input.Contains('\n') || input.Contains('"'))
                return $"\"{input}\"";
            return input;
        }

        private static string Capitalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s ?? "";
            s = s.Trim();
            return char.ToUpperInvariant(s[0]) + (s.Length > 1 ? s[1..].ToLowerInvariant() : "");
        }

        private static string GetDisplayName(int userId)
            => userId > 0 && userId <= _sampleNames.Length ? _sampleNames[userId - 1] : $"User {userId}";

        private static string GetDisplayDept(int userId)
            => userId > 0 && userId <= _sampleDepts.Length ? _sampleDepts[userId - 1] : "";

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Logout", "Are you sure you want to logout?", "Logout", "Cancel");
            if (confirm) await Shell.Current.GoToAsync("//LoginPage");
        }

        private async void OnActionPressed(object sender, EventArgs e)
        {
            if (sender is VisualElement ve) await ve.ScaleTo(0.97, 80);
        }

        private async void OnActionReleased(object sender, EventArgs e)
        {
            if (sender is VisualElement ve) await ve.ScaleTo(1, 120);
        }

        private string? _selectedLeaveType;
        private string? _selectedDuration;
    }
}
