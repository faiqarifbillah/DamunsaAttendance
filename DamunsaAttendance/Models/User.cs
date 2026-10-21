using SQLite;

namespace DamunsaAttendance
{
    public class User
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Username { get; set; } = "";
        public string Password { get; set; } = "";

        // "Staff" | "Admin" | "Owner"
        public string Role { get; set; } = "";

        // Links to staff roster (0 for Admin/OwnerText="0"
        public int StaffUserId { get; set; } = 0;

        public string FullName { get; set; } = "";
        public string Department { get; set; } = "";
        public string Position { get; set; } = "";
    }
}
