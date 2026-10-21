using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace DamunsaAttendance
{
    public partial class LoginPage : ContentPage
    {
        private readonly Db _db;
        private string _selectedRole = "";

        public LoginPage(Db db)
        {
            InitializeComponent();
            _db = db;
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e) { }
        private void OnCompleted(object sender, EventArgs e) { }

        private void OnTogglePasswordClicked(object sender, EventArgs e)
        {
            PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
            TogglePasswordBtn.Text = PasswordEntry.IsPassword ? "Show" : "Hide";
        }

        private void OnRoleStaff(object sender, EventArgs e) => SetRole("Staff");
        private void OnRoleAdmin(object sender, EventArgs e) => SetRole("Admin");
        private void OnRoleOwner(object sender, EventArgs e) => SetRole("Owner");

        private void SetRole(string role)
        {
            _selectedRole = role;

            // Reset all buttons
            foreach (var (btn, r) in new[] {
                (StaffRoleBtn, "Staff"),
                (AdminRoleBtn, "Admin"),
                (OwnerRoleBtn, "Owner") })
            {
                bool active = r == role;
                btn.BackgroundColor = active
                    ? Color.FromArgb("#3B82F6")
                    : Color.FromArgb("#F1F5F9");
                btn.TextColor = active
                    ? Colors.White
                    : Color.FromArgb("#1E293B");
            }
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            string username = UsernameEntry.Text?.Trim() ?? "";
            string password = PasswordEntry.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("Please enter both username and password.");
                return;
            }

            if (string.IsNullOrEmpty(_selectedRole))
            {
                ShowError("Please select a role.");
                return;
            }

            await _db.InitAsync();

            var user = await _db.GetUserByCredentialsAsync(username, password, _selectedRole);

            if (user == null)
            {
                ShowError("Invalid credentials. Please try again.");
                return;
            }

            // Save session to Preferences
            Preferences.Set("LoggedInUserId", user.Id);
            Preferences.Set("LoggedInStaffId", user.StaffUserId);
            Preferences.Set("LoggedInRole", user.Role);
            Preferences.Set("LoggedInName", user.FullName);
            Preferences.Set("LoggedInDept", user.Department);

            // Navigate based on role
            switch (user.Role)
            {
                case "Staff":
                    await Shell.Current.GoToAsync(nameof(StaffPage));
                    break;
                case "Admin":
                    await Shell.Current.GoToAsync(nameof(AdminPage));
                    break;
                case "Owner":
                    await Shell.Current.GoToAsync(nameof(OwnerPage));
                    break;
            }
        }

        private void ShowError(string message)
        {
            LoginHintLabel.Text = message;
            LoginHintLabel.TextColor = Color.FromArgb("#EF4444");
        }
    }
}
