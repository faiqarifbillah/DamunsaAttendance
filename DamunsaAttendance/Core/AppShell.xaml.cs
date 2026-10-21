using DamunsaAttendance.Pages;
using Microsoft.Maui.Controls;

namespace DamunsaAttendance
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(StaffPage), typeof(StaffPage));
            Routing.RegisterRoute(nameof(AdminPage), typeof(AdminPage));
            Routing.RegisterRoute(nameof(OwnerPage), typeof(OwnerPage));
            Routing.RegisterRoute(nameof(LeavePage), typeof(LeavePage));
        }
    }
}