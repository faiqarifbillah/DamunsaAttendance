using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace DamunsaAttendance.Pages
{
    public partial class LeavePage : ContentPage
    {
        private readonly Db _db;
        private string _selectedDuration = "Full Day";
        private string _selectedLeaveType = "";
        private const double AnnualEntitlement = 20.0;
        private int _userId = 1;
        private string _staffName = "";
        private string _staffDept = "";

        public LeavePage(Db db)
        {
            InitializeComponent();
            _db = db;
            InitializePage();
        }

        private void InitializePage()
        {
            StartDatePicker.Date = DateTime.Today;
            EndDatePicker.Date = DateTime.Today;
            StartDatePicker.MinimumDate = DateTime.Today;
            EndDatePicker.MinimumDate = DateTime.Today;
            _selectedDuration = "Full Day";
            _selectedLeaveType = "";
            UpdateDurationButtons();
            UpdateLeaveTypeButtons();
            StartDatePicker.DateSelected += OnDateChanged;
            EndDatePicker.DateSelected += OnDateChanged;
            CalculateEstimatedDays();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _userId = Preferences.Get("LoggedInStaffId", 1);
            _staffName = Preferences.Get("LoggedInName", "Staff");
            _staffDept = Preferences.Get("LoggedInDept", "");
            _ = UpdateAnnualBalanceAsync();
        }

        private async Task UpdateAnnualBalanceAsync()
        {
            try
            {
                await _db.InitAsync();
                double used = await _db.GetUsedAnnualLeaveAsync(_userId);
                double balance = Math.Max(AnnualEntitlement - used, 0);
                AnnualBalanceLabel.Text = $"Annual Leave Balance: {balance:0.##} days remaining";
            }
            catch { AnnualBalanceLabel.Text = "Annual Leave Balance: 20 days remaining"; }
        }

        private async Task<double> GetRemainingBalanceAsync()
        {
            await _db.InitAsync();
            double used = await _db.GetUsedAnnualLeaveAsync(_userId);
            return Math.Max(AnnualEntitlement - used, 0);
        }

        private void OnAnnualLeaveClicked(object sender, EventArgs e) { _selectedLeaveType = "Annual Leave"; UpdateLeaveTypeButtons(); }
        private void OnSickLeaveClicked(object sender, EventArgs e) { _selectedLeaveType = "Sick Leave"; UpdateLeaveTypeButtons(); }
        private void OnEmergencyLeaveClicked(object sender, EventArgs e) { _selectedLeaveType = "Emergency Leave"; UpdateLeaveTypeButtons(); }
        private void OnUnpaidLeaveClicked(object sender, EventArgs e) { _selectedLeaveType = "Unpaid Leave"; UpdateLeaveTypeButtons(); }

        private void UpdateLeaveTypeButtons()
        {
            SetUnselected(AnnualLeaveBtn); SetUnselected(SickLeaveBtn);
            SetUnselected(EmergencyLeaveBtn); SetUnselected(UnpaidLeaveBtn);
            switch (_selectedLeaveType)
            {
                case "Annual Leave": SetSelected(AnnualLeaveBtn); break;
                case "Sick Leave": SetSelected(SickLeaveBtn); break;
                case "Emergency Leave": SetSelected(EmergencyLeaveBtn); break;
                case "Unpaid Leave": SetSelected(UnpaidLeaveBtn); break;
            }
        }

        private void OnFullDay(object sender, EventArgs e) { _selectedDuration = "Full Day"; UpdateDurationButtons(); CalculateEstimatedDays(); }
        private void OnHalfDay(object sender, EventArgs e) { _selectedDuration = "Half Day"; UpdateDurationButtons(); CalculateEstimatedDays(); }

        private void UpdateDurationButtons()
        {
            SetUnselected(FullDayBtn); SetUnselected(HalfDayBtn);
            if (_selectedDuration == "Full Day") SetSelected(FullDayBtn);
            else SetSelected(HalfDayBtn);
        }

        private static void SetSelected(Button btn)
        {
            btn.BackgroundColor = Color.FromArgb("#1E3A8A");
            btn.TextColor = Colors.White;
            btn.BorderColor = Color.FromArgb("#1E3A8A");
        }

        private static void SetUnselected(Button btn)
        {
            btn.BackgroundColor = Colors.White;
            btn.TextColor = Color.FromArgb("#64748B");
            btn.BorderColor = Color.FromArgb("#CBD5E1");
        }

        private void OnDateChanged(object? sender, DateChangedEventArgs e)
        {
            if (StartDatePicker.Date < DateTime.Today) StartDatePicker.Date = DateTime.Today;
            if (EndDatePicker.Date < DateTime.Today) EndDatePicker.Date = DateTime.Today;
            CalculateEstimatedDays();
        }

        private double CalculateEstimatedDays()
        {
            var start = StartDatePicker.Date;
            var end = EndDatePicker.Date;
            if (end < start)
            {
                DaysLabel.Text = "End date cannot be earlier than start date.";
                DaysLabel.TextColor = Color.FromArgb("#EF4444");
                return 0;
            }
            int totalDays = (end - start).Days + 1;
            double estimated = (start == end && _selectedDuration == "Half Day") ? 0.5 : totalDays;
            DaysLabel.Text = $"Estimated days: {estimated}";
            DaysLabel.TextColor = Color.FromArgb("#166534");
            return estimated;
        }

        private async void OnSubmitClicked(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_selectedLeaveType)) { await DisplayAlert("Validation", "Please select a leave type.", "OK"); return; }
                if (string.IsNullOrWhiteSpace(_selectedDuration)) { await DisplayAlert("Validation", "Please select a duration.", "OK"); return; }
                var start = StartDatePicker.Date;
                var end = EndDatePicker.Date;
                if (start < DateTime.Today || end < DateTime.Today) { await DisplayAlert("Invalid Date", "Past dates are not allowed.", "OK"); return; }
                if (end < start) { await DisplayAlert("Invalid Date", "End date cannot be earlier than start date.", "OK"); return; }
                if (string.IsNullOrWhiteSpace(ReasonEntry.Text)) { await DisplayAlert("Required", "Please enter your reason.", "OK"); return; }
                double estimatedDays = CalculateEstimatedDays();
                if (_selectedLeaveType == "Annual Leave")
                {
                    double balance = await GetRemainingBalanceAsync();
                    if (estimatedDays > balance) { await DisplayAlert("Insufficient Balance", "Insufficient annual leave balance.", "OK"); return; }
                }
                var existing = (await _db.GetLeaveByUserAsync(_userId))
                    .FirstOrDefault(l => l.StartDate.Date == start.Date && l.EndDate.Date == end.Date
                                      && l.LeaveType == _selectedLeaveType && l.Status != "Rejected");
                if (existing != null) { await DisplayAlert("Duplicate", "A similar leave request already exists.", "OK"); return; }
                var request = new LeaveRequest
                {
                    UserId = _userId,
                    StaffName = _staffName,
                    Department = _staffDept,
                    LeaveType = _selectedLeaveType,
                    Duration = _selectedDuration,
                    StartDate = start,
                    EndDate = end,
                    EstimatedDays = estimatedDays,
                    Reason = ReasonEntry.Text.Trim(),
                    Status = "Pending",
                    CreatedAt = DateTime.Now
                };
                await _db.AddLeaveAsync(request);
                await DisplayAlert("Success", "Your leave request has been submitted successfully.", "OK");
                ResetForm();
                await UpdateAnnualBalanceAsync();
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex) { await DisplayAlert("Error", $"Failed to submit: {ex.Message}", "OK"); }
        }

        private void ResetForm()
        {
            _selectedLeaveType = ""; _selectedDuration = "Full Day";
            UpdateLeaveTypeButtons(); UpdateDurationButtons();
            StartDatePicker.Date = DateTime.Today; EndDatePicker.Date = DateTime.Today;
            ReasonEntry.Text = ""; CalculateEstimatedDays();
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Logout", "Are you sure you want to logout?", "Logout", "Cancel");
            if (confirm)
            {
                Preferences.Remove("LoggedInUserId"); Preferences.Remove("LoggedInStaffId");
                Preferences.Remove("LoggedInRole"); Preferences.Remove("LoggedInName");
                Preferences.Remove("LoggedInDept");
                await Shell.Current.GoToAsync("//LoginPage");
            }
        }
    }
}
