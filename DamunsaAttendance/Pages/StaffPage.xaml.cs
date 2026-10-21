using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace DamunsaAttendance;

public partial class StaffPage : ContentPage
{
    private readonly Db _db;
    private bool _isCheckedIn = false;
    private AttendanceRecord? _openAttendance;
    private const double AnnualEntitlement = 20.0;

    // Loaded from Preferences (set at login)
    private int _userId = 1;
    private string _staffName = "";
    private string _staffDept = "";

    private IDispatcherTimer? _clockTimer;

    public StaffPage(Db db)
    {
        InitializeComponent();
        _db = db;
        UpdateTabButtons();
    }

    // ── Page lifecycle ───────────────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Read session from Preferences — set by LoginPage on successful login
        _userId = Preferences.Get("LoggedInStaffId", 1);
        _staffName = Preferences.Get("LoggedInName", "Staff");
        _staffDept = Preferences.Get("LoggedInDept", "");

        TodayLabel.Text = DateTime.Now.ToString("dddd, dd MMM yyyy");
        WelcomeLabel.Text = $"Welcome, {_staffName}";

        // Live clock
        _clockTimer = Dispatcher.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (s, e) =>
            CurrentTimeLabel.Text = DateTime.Now.ToString("hh:mm tt, d MMM yyyy") + " (MYT)";
        _clockTimer.Start();
        CurrentTimeLabel.Text = DateTime.Now.ToString("hh:mm tt, d MMM yyyy") + " (MYT)";

        await RefreshAttendanceStateAsync();
        await LoadAttendanceAsync();
        await UpdateAnnualBalanceAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _clockTimer?.Stop();
        _clockTimer = null;
    }

    // ── Annual Leave Balance ─────────────────────────────────────────────────

    private async Task UpdateAnnualBalanceAsync()
    {
        try
        {
            await _db.InitAsync();
            double used = await _db.GetUsedAnnualLeaveAsync(_userId);
            double balance = Math.Max(AnnualEntitlement - used, 0);
            AnnualBalanceLabel.Text = $"Annual Leave Balance: {balance:0.##} days remaining";
        }
        catch
        {
            AnnualBalanceLabel.Text = "Annual Leave Balance: 20 days remaining";
        }
    }

    // ── Check In / Check Out ─────────────────────────────────────────────────

    private async Task RefreshAttendanceStateAsync()
    {
        await _db.InitAsync();
        _openAttendance = await _db.GetOpenAttendanceAsync(_userId);
        _isCheckedIn = _openAttendance != null;

        if (_isCheckedIn && _openAttendance != null)
        {
            StatusLabel.Text = $"Status: Checked in at {_openAttendance.CheckInTime:hh:mm tt}";
            StatusLabel.TextColor = Colors.Green;
            ClockButton.Text = "Check Out";
            ClockButton.BackgroundColor = Colors.OrangeRed;
            ResultLabel.Text = "You are currently checked in.";
            ResultLabel.TextColor = Colors.Green;
        }
        else
        {
            StatusLabel.Text = "Status: Not checked in";
            StatusLabel.TextColor = Colors.Black;
            ClockButton.Text = "Check In";
            ClockButton.BackgroundColor = Color.FromArgb("#2563EB");
            ResultLabel.Text = "Ready for attendance action.";
            ResultLabel.TextColor = Color.FromArgb("#6B7280");
        }
    }

    private async void OnClockClicked(object sender, EventArgs e)
    {
        var now = DateTime.Now;
        var cutoff = now.Date.AddHours(8).AddMinutes(30);

        if (!_isCheckedIn)
        {
            string status = now > cutoff ? "Late" : "Present";

            var record = new AttendanceRecord
            {
                UserId = _userId,
                StaffName = _staffName,
                Department = _staffDept,
                AttendanceDate = now.Date,
                CheckInTime = now,
                CheckOutTime = null,
                Status = status
            };

            await _db.AddAttendanceAsync(record);
            ResultLabel.Text = $"Check in successful! Status: {status}";
            ResultLabel.TextColor = Colors.Green;
        }
        else if (_openAttendance != null)
        {
            _openAttendance.CheckOutTime = now;
            await _db.UpdateAttendanceAsync(_openAttendance);
            ResultLabel.Text = "Check out successful.";
            ResultLabel.TextColor = Colors.Green;
        }

        await RefreshAttendanceStateAsync();
        await LoadAttendanceAsync();
    }

    // ── Attendance List ──────────────────────────────────────────────────────

    private async Task LoadAttendanceAsync()
    {
        var today = DateTime.Now.Date;
        var records = await _db.GetAttendanceByUserAsync(_userId);

        var items = records
            .Where(a => a.AttendanceDate.Date == today)
            .OrderByDescending(x => x.CheckInTime)
            .Select(x =>
            {
                string outTime = x.CheckOutTime.HasValue
                    ? x.CheckOutTime.Value.ToString("HH:mm") : "Still working";
                string hoursLine = x.CheckOutTime.HasValue
                    ? $"Worked: {(x.CheckOutTime.Value - x.CheckInTime).TotalHours:F1} hours"
                    : "Worked: In progress";
                return new AttendanceVm
                {
                    DateLine = x.CheckInTime.ToString("dddd, dd MMM yyyy"),
                    TimeLine = $"In: {x.CheckInTime:HH:mm} | Out: {outTime}",
                    HoursLine = hoursLine
                };
            })
            .ToList();

        AttendanceList.ItemsSource = items;
    }

    // ── Tab Buttons ──────────────────────────────────────────────────────────

    private void OnAttendanceTabClicked(object sender, EventArgs e)
    {
        AttendanceSection.IsVisible = true;
        UpdateTabButtons();
    }

    private void UpdateTabButtons()
    {
        bool active = AttendanceSection?.IsVisible ?? true;
        if (AttendanceTabBtn != null)
        {
            AttendanceTabBtn.BackgroundColor = active
                ? Color.FromArgb("#2563EB") : Colors.White;
            AttendanceTabBtn.TextColor = active ? Colors.White : Colors.Black;
            AttendanceTabBtn.BorderColor = Color.FromArgb("#E5E7EB");
            AttendanceTabBtn.BorderWidth = 1;
        }
    }

    // ── Misc Handlers ────────────────────────────────────────────────────────

    private async void OnRefreshAttendanceClicked(object sender, EventArgs e)
    {
        await LoadAttendanceAsync();
        await RefreshAttendanceStateAsync();
    }

    private async void OnGoToLeaveClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("LeavePage");

    private void OnRefreshLeaveClicked(object sender, EventArgs e) { }
    private void OnRequestLeaveClicked(object sender, EventArgs e) { }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Logout", "Are you sure you want to logout?",
            "Logout", "Cancel");
        if (confirm)
        {
            // Clear session
            Preferences.Remove("LoggedInUserId");
            Preferences.Remove("LoggedInStaffId");
            Preferences.Remove("LoggedInRole");
            Preferences.Remove("LoggedInName");
            Preferences.Remove("LoggedInDept");

            await Shell.Current.GoToAsync("//LoginPage");
        }
    }

    // ── View Model ───────────────────────────────────────────────────────────

    private class AttendanceVm
    {
        public string DateLine { get; set; } = "";
        public string TimeLine { get; set; } = "";
        public string HoursLine { get; set; } = "";
    }
}
