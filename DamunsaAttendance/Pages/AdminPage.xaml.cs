using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;

namespace DamunsaAttendance;

public partial class AdminPage : ContentPage
{
    private readonly Db _db;
    private List<AttendanceRecord> _allAttendance = new();
    private List<LeaveRequest> _allLeaveRequests = new();
    private List<StaffViewModel> _staffDisplay = new();

    private readonly ObservableCollection<AdminAttendanceVm> _todayAttendanceRows = new();
    private readonly ObservableCollection<LeaveManageVm> _leaveItems = new();
    private readonly ObservableCollection<StaffViewModel> _staffItems = new();

    private CancellationTokenSource? _loadCts;

    private readonly string[] _sampleNames = {
        "Hafiz Zulkifli",  "Ridhwan Mansor",  "Azrul Hafifi",
        "Haziq Shamsudin", "Firdaus Norzaidi", "Amsyar Lokman"
    };
    private readonly string[] _sampleDepts = {
        "Logistics", "Logistics", "Logistics",
        "Logistics", "Delivery",  "Delivery"
    };
    private readonly string[] _samplePositions = {
        "Driver", "Driver", "Runner",
        "Runner", "Rider",  "Rider"
    };
    private readonly string[] _sampleStaffIds = {
        "STF001","STF002","STF003",
        "STF004","STF005","STF006"
    };

    public AdminPage(Db db)
    {
        InitializeComponent();
        _db = db;
        TodayAttendanceList.ItemsSource = _todayAttendanceRows;
        LeaveRequestsList.ItemsSource = _leaveItems;
        StaffList.ItemsSource = _staffItems;
        OnAttendanceTabTapped(this, EventArgs.Empty);
    }

    partial void InitializeComponent();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        _ = LoadDataAsync(_loadCts.Token).ContinueWith(t =>
        {
            if (t.Exception != null)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                    await DisplayAlert("Error", $"Failed to load data: {t.Exception.InnerException?.Message ?? t.Exception.Message}", "OK"));
            }
        }, TaskScheduler.Current);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
    }

    private async Task LoadDataAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await _db.InitAsync();
        ct.ThrowIfCancellationRequested();
        _allAttendance = await _db.GetAllAttendanceAsync();
        ct.ThrowIfCancellationRequested();
        _allLeaveRequests = await _db.GetAllLeaveAsync();
        ct.ThrowIfCancellationRequested();
        UpdateAttendanceTab();
        UpdateLeaveTab();
        UpdateStaffTab();
        MainThread.BeginInvokeOnMainThread(() => UpdateLateAnalytics());
    }

    private void UpdateAttendanceTab()
    {
        TodayDateLabel.Text = DateTime.Now.ToString("dddd, MMMM d, yyyy");
        var today = DateTime.Today;

        static string HoursText(DateTime? inDt, DateTime? outDt)
        {
            if (!inDt.HasValue || !outDt.HasValue) return "";
            var span = outDt.Value - inDt.Value;
            if (span.TotalMinutes <= 0) return "";
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        }

        static string DeriveStatus(DateTime checkIn)
            => checkIn > checkIn.Date.AddHours(8).AddMinutes(30) ? AttendanceStatuses.Late : AttendanceStatuses.Present;

        var realToday = _allAttendance
            .Where(a => a.AttendanceDate.Date == today)
            .GroupBy(a => a.UserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CheckInTime).First());

        var onLeaveIds = _allLeaveRequests
            .Where(l => string.Equals(l.Status, LeaveStatuses.Approved, StringComparison.OrdinalIgnoreCase)
                     && l.StartDate.Date <= today
                     && l.EndDate.Date >= today)
            .Select(l => l.UserId)
            .ToHashSet();

        _todayAttendanceRows.Clear();

        for (int i = 0; i < _sampleNames.Length; i++)
        {
            int userId = i + 1;
            string name = _sampleNames[i];
            string dept = _sampleDepts[i];

            if (realToday.TryGetValue(userId, out var rec))
            {
                string status = string.IsNullOrWhiteSpace(rec.Status)
                    ? DeriveStatus(rec.CheckInTime)
                    : rec.Status;

                _todayAttendanceRows.Add(new AdminAttendanceVm
                {
                    UserName = name,
                    Department = dept,
                    CheckInDisplay = $"In:  {rec.CheckInTime:hh:mm tt}",
                    CheckOutDisplay = rec.CheckOutTime.HasValue ? $"Out: {rec.CheckOutTime:hh:mm tt}" : "Out: —",
                    HoursDisplay = HoursText(rec.CheckInTime, rec.CheckOutTime),
                    Status = status,
                    StatusColor = GetAttendanceStatusColor(status),
                    IsRealRecord = true
                });
            }
            else if (onLeaveIds.Contains(userId))
            {
                _todayAttendanceRows.Add(new AdminAttendanceVm
                {
                    UserName = name,
                    Department = dept,
                    CheckInDisplay = "In:  —",
                    CheckOutDisplay = "Out: —",
                    Status = AttendanceStatuses.OnLeave,
                    StatusColor = GetAttendanceStatusColor(AttendanceStatuses.OnLeave)
                });
            }
            else
            {
                var (inTime, outTime, sampleStatus) = GetSampleAttendance(i, today);
                _todayAttendanceRows.Add(new AdminAttendanceVm
                {
                    UserName = name,
                    Department = dept,
                    CheckInDisplay = inTime.HasValue ? $"In:  {inTime.Value:hh:mm tt}" : "In:  —",
                    CheckOutDisplay = outTime.HasValue ? $"Out: {outTime.Value:hh:mm tt}" : "Out: —",
                    HoursDisplay = HoursText(inTime, outTime),
                    Status = sampleStatus,
                    StatusColor = GetAttendanceStatusColor(sampleStatus)
                });
            }
        }

        SummaryPresentCount.Text = _todayAttendanceRows.Count(r =>
            string.Equals(r.Status, AttendanceStatuses.Present, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r.Status, AttendanceStatuses.Late, StringComparison.OrdinalIgnoreCase)).ToString();
        SummaryLateCount.Text = _todayAttendanceRows.Count(r => string.Equals(r.Status, AttendanceStatuses.Late, StringComparison.OrdinalIgnoreCase)).ToString();
        SummaryOnLeaveCount.Text = _todayAttendanceRows.Count(r => string.Equals(r.Status, AttendanceStatuses.OnLeave, StringComparison.OrdinalIgnoreCase)).ToString();
        SummaryTotalCount.Text = _todayAttendanceRows.Count.ToString();
    }

    private static (DateTime? inTime, DateTime? outTime, string status)
        GetSampleAttendance(int index, DateTime today) => index switch
        {
            0 => (today.AddHours(8).AddMinutes(1), today.AddHours(17).AddMinutes(5), AttendanceStatuses.Present),
            1 => (today.AddHours(8).AddMinutes(10), today.AddHours(17).AddMinutes(0), AttendanceStatuses.Present),
            2 => (today.AddHours(8).AddMinutes(45), today.AddHours(17).AddMinutes(20), AttendanceStatuses.Late),
            3 => (today.AddHours(7).AddMinutes(55), today.AddHours(16).AddMinutes(50), AttendanceStatuses.Present),
            4 => (today.AddHours(9).AddMinutes(5), today.AddHours(17).AddMinutes(30), AttendanceStatuses.Late),
            5 => (today.AddHours(8).AddMinutes(20), today.AddHours(17).AddMinutes(10), AttendanceStatuses.Present),
            6 => (null, null, AttendanceStatuses.OnLeave),
            7 => (today.AddHours(8).AddMinutes(5), today.AddHours(17).AddMinutes(0), AttendanceStatuses.Present),
            8 => (null, null, AttendanceStatuses.Absent),
            9 => (null, null, AttendanceStatuses.OnLeave),
            _ => (null, null, AttendanceStatuses.Absent)
        };

    private void UpdateLeaveTab()
    {
        _leaveItems.Clear();
        foreach (var l in _allLeaveRequests)
        {
            var status = string.IsNullOrWhiteSpace(l.Status) ? LeaveStatuses.Pending : l.Status;
            _leaveItems.Add(new LeaveManageVm
            {
                LeaveId = l.Id,
                UserName = GetUserName(l.UserId),
                LeaveType = l.LeaveType ?? "Unknown",
                PeriodDisplay = $"Period: {l.StartDate:dd/MM/yyyy} to {l.EndDate:dd/MM/yyyy}  ({l.EstimatedDays} day{(l.EstimatedDays == 1 ? "" : "s")})",
                SubmittedDisplay = $"Submitted: {l.CreatedAt:dd/MM/yyyy}",
                ReasonDisplay = $"Reason: {l.Reason ?? "N/A"}",
                Status = status,
                StatusColor = GetLeaveStatusColor(status),
                IsPending = string.Equals(status, LeaveStatuses.Pending, StringComparison.OrdinalIgnoreCase)
            });
        }
    }

    private async Task RefreshLeaveList()
    {
        _allLeaveRequests = await _db.GetAllLeaveAsync();
        UpdateLeaveTab();
    }

    private async void OnApproveClicked(object sender, EventArgs e)
    {
        var vm = (sender as Button)?.BindingContext as LeaveManageVm;
        if (vm == null) return;
        bool confirmed = await DisplayAlert("Approve Leave", $"Approve the {vm.LeaveType} request for {vm.UserName}?", "Yes, Approve", "Cancel");
        if (!confirmed) return;
        var request = _allLeaveRequests.FirstOrDefault(l => l.Id == vm.LeaveId);
        if (request == null) return;
        request.Status = LeaveStatuses.Approved;
        await _db.UpdateLeaveAsync(request);
        await RefreshLeaveList();
        await DisplayAlert("Approved", $"Leave for {vm.UserName} has been approved.", "OK");
    }

    private async void OnRejectClicked(object sender, EventArgs e)
    {
        var vm = (sender as Button)?.BindingContext as LeaveManageVm;
        if (vm == null) return;
        bool confirmed = await DisplayAlert("Reject Leave", $"Reject the {vm.LeaveType} request for {vm.UserName}?", "Yes, Reject", "Cancel");
        if (!confirmed) return;
        var request = _allLeaveRequests.FirstOrDefault(l => l.Id == vm.LeaveId);
        if (request == null) return;
        request.Status = LeaveStatuses.Rejected;
        await _db.UpdateLeaveAsync(request);
        await RefreshLeaveList();
    }

    private void OnActionPressed(object sender, EventArgs e) { }
    private void OnActionReleased(object sender, EventArgs e) { }

    private void UpdateStaffTab()
    {
        _staffDisplay = _sampleNames
            .Select((name, i) => new StaffViewModel(name, _sampleDepts[i])
            {
                StaffCode = _sampleStaffIds[i],
                Position = _samplePositions[i]
            })
            .ToList();
        _staffItems.Clear();
        foreach (var s in _staffDisplay) _staffItems.Add(s);
    }

    private void OnStaffSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var q = e.NewTextValue?.ToLower() ?? "";
        var filtered = string.IsNullOrWhiteSpace(q)
            ? _staffDisplay
            : _staffDisplay.Where(s => s.UserName.ToLower().Contains(q) || s.Department.ToLower().Contains(q)).ToList();
        _staffItems.Clear();
        foreach (var s in filtered) _staffItems.Add(s);
    }

    private void UpdateLateAnalytics()
    {
        try
        {
            var today = DateTime.Today;
            var firstDay = new DateTime(today.Year, today.Month, 1);

            var monthRecords = _allAttendance
                .Where(a => a.CheckInTime >= firstDay && a.CheckInTime.Date <= today)
                .ToList();

            int workingDays = 0;
            for (var d = firstDay; d <= today; d = d.AddDays(1))
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                    workingDays++;

            LateAnalyticsSubLabel.Text = $"{today:MMMM yyyy}  •  {workingDays} working days";
            LateAnalyticsPanel.Children.Clear();

            for (int i = 0; i < _sampleNames.Length; i++)
            {
                int userId = i + 1;
                string name = _sampleNames[i];

                int lateCount = monthRecords.Count(a =>
                    a.UserId == userId &&
                    string.Equals(a.Status, "Late", StringComparison.OrdinalIgnoreCase));

                int daysPresent = monthRecords
                    .Where(a => a.UserId == userId)
                    .Select(a => a.CheckInTime.Date)
                    .Distinct().Count();

                double lateRate = daysPresent > 0
                    ? Math.Round((double)lateCount / daysPresent * 100)
                    : 0;

                string badgeColor = lateRate == 0 ? "#16A34A" :
                                    lateRate <= 25 ? "#F59E0B" :
                                    lateRate <= 50 ? "#EA580C" : "#DC2626";

                var nameRow = new Grid();
                nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
                nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });

                var nameLabel = new Label { Text = name, FontSize = 12, TextColor = Color.FromArgb("#374151"), VerticalOptions = LayoutOptions.Center };
                var badge = new Border { BackgroundColor = Color.FromArgb(badgeColor), Padding = new Thickness(6, 2), Margin = new Thickness(4, 0), VerticalOptions = LayoutOptions.Center, Stroke = Colors.Transparent };
                badge.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(6) };
                badge.Content = new Label { Text = $"{lateCount}x late", FontSize = 10, TextColor = Colors.White };
                var pctLabel = new Label { Text = $"{lateRate}%", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(badgeColor), HorizontalTextAlignment = TextAlignment.End, VerticalOptions = LayoutOptions.Center };

                Grid.SetColumn(nameLabel, 0);
                Grid.SetColumn(badge, 1);
                Grid.SetColumn(pctLabel, 2);
                nameRow.Children.Add(nameLabel);
                nameRow.Children.Add(badge);
                nameRow.Children.Add(pctLabel);

                var bar = new ProgressBar { Progress = lateRate / 100.0, ProgressColor = Color.FromArgb(badgeColor), BackgroundColor = Color.FromArgb("#F1F5F9"), HeightRequest = 5 };
                var row = new VerticalStackLayout { Spacing = 3 };
                row.Children.Add(nameRow);
                row.Children.Add(bar);
                LateAnalyticsPanel.Children.Add(row);
            }
        }
        catch { }
    }

    private static string GetAttendanceStatusColor(string status) => status switch
    {
        AttendanceStatuses.Present => "#10B981",
        AttendanceStatuses.Late => "#F59E0B",
        AttendanceStatuses.OnLeave => "#3B82F6",
        AttendanceStatuses.Absent => "#EF4444",
        _ => "#64748B"
    };

    private static string GetLeaveStatusColor(string status) =>
        (status?.ToLower()) switch
        {
            "pending" => "#F59E0B",
            "approved" => "#10B981",
            "rejected" => "#EF4444",
            _ => "#64748B"
        };

    private string GetUserName(int userId)
    {
        if (userId < 1 || userId > _sampleNames.Length) return $"User {userId}";
        return _sampleNames[userId - 1];
    }

    private void OnAttendanceTabTapped(object sender, EventArgs e)
    {
        AttendanceContent.IsVisible = true;
        LeaveContent.IsVisible = false;
        StaffContent.IsVisible = false;
        ActivateTab(AttendanceTabBorder, AttendanceTabButton);
        DeactivateTab(LeaveTabBorder, LeaveTabButton);
        DeactivateTab(StaffTabBorder, StaffTabButton);
    }

    private void OnLeaveTabTapped(object sender, EventArgs e)
    {
        AttendanceContent.IsVisible = false;
        LeaveContent.IsVisible = true;
        StaffContent.IsVisible = false;
        DeactivateTab(AttendanceTabBorder, AttendanceTabButton);
        ActivateTab(LeaveTabBorder, LeaveTabButton);
        DeactivateTab(StaffTabBorder, StaffTabButton);
    }

    private void OnStaffTabTapped(object sender, EventArgs e)
    {
        AttendanceContent.IsVisible = false;
        LeaveContent.IsVisible = false;
        StaffContent.IsVisible = true;
        DeactivateTab(AttendanceTabBorder, AttendanceTabButton);
        DeactivateTab(LeaveTabBorder, LeaveTabButton);
        ActivateTab(StaffTabBorder, StaffTabButton);
    }

    private void OnAttendanceTabClicked(object sender, EventArgs e) => OnAttendanceTabTapped(sender, e);
    private void OnLeaveRequestsTabClicked(object sender, EventArgs e) => OnLeaveTabTapped(sender, e);
    private void OnStaffTabClicked(object sender, EventArgs e) => OnStaffTabTapped(sender, e);

    private static void ActivateTab(Border border, Button button)
    {
        border.BackgroundColor = Colors.White;
        border.Stroke = new SolidColorBrush(Color.FromArgb("#CBD5E1"));
        button.TextColor = Color.FromArgb("#111827");
        button.FontAttributes = FontAttributes.Bold;
    }

    private static void DeactivateTab(Border border, Button button)
    {
        border.BackgroundColor = Colors.Transparent;
        border.Stroke = Brush.Transparent;
        button.TextColor = Color.FromArgb("#64748B");
        button.FontAttributes = FontAttributes.None;
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Logout", "Are you sure you want to logout?", "Logout", "Cancel");
        if (confirm) await Shell.Current.GoToAsync("//LoginPage");
    }
}

public static class AttendanceStatuses
{
    public const string Present = "Present";
    public const string Late = "Late";
    public const string OnLeave = "On Leave";
    public const string Absent = "Absent";
}

public static class LeaveStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public class AdminAttendanceVm
{
    public string UserName { get; set; } = "";
    public string Department { get; set; } = "";
    public string CheckInDisplay { get; set; } = "";
    public string CheckOutDisplay { get; set; } = "";
    public string HoursDisplay { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusColor { get; set; } = "";
    public bool IsRealRecord { get; set; } = false;
}

public class StaffViewModel
{
    public string UserName { get; set; } = "";
    public string Department { get; set; } = "";
    public string Initials { get; set; } = "";
    public string StaffCode { get; set; } = "";
    public string? Position { get; set; }

    public StaffViewModel(string name, string dept)
    {
        UserName = name;
        Department = dept;
        Initials = ComputeInitials(name);
    }

    private static string ComputeInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0].Length <= 2 ? parts[0].ToUpper() : parts[0].Substring(0, 2).ToUpper();
        return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[^1][0])}";
    }
}

public class LeaveManageVm
{
    public int LeaveId { get; set; }
    public string UserName { get; set; } = "";
    public string LeaveType { get; set; } = "";
    public string PeriodDisplay { get; set; } = "";
    public string SubmittedDisplay { get; set; } = "";
    public string ReasonDisplay { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusColor { get; set; } = "";
    public bool IsPending { get; set; }
}

public class AttendanceTodayVm
{
    public string UserName { get; set; } = "";
    public string CheckInDisplay { get; set; } = "";
    public string CheckOutDisplay { get; set; } = "";
    public string HoursDisplay { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusColor { get; set; } = "";
}

public class StaffItemVm
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Department { get; set; } = "";
}
