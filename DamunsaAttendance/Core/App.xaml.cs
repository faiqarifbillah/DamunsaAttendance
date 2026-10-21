using System.Threading.Tasks;

namespace DamunsaAttendance;

public partial class App : Application
{
    public App(AppShell shell, Db db)
    {
        InitializeComponent();
        MainPage = shell;

        Task.Run(async () =>
        {
            await db.InitAsync();
            await SeedData.SeedAsync(db);
        });
    }
}
