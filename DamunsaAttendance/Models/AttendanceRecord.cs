using SQLite;

namespace DamunsaAttendance
{
    public class AttendanceRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int UserId { get; set; }

        // Stored at check-in time so records remain readable even if staff details change later
        public string StaffName { get; set; } = "";
        public string Department { get; set; } = "";

        public DateTime AttendanceDate { get; set; }
        public DateTime CheckInTime { get; set; }

        // Null until the user checks out
        public DateTime? CheckOutTime { get; set; }

        // "Present" | "Late" | "Absent" | "On Leave"
        public string Status { get; set; } = "Present";
    }
}