using SQLite;

namespace DamunsaAttendance
{
    public class LeaveRequest
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int UserId { get; set; }

        // Stored at submission time so records remain readable even if staff details change later
        public string StaffName { get; set; } = "";
        public string Department { get; set; } = "";

        // "Annual Leave" | "Sick Leave" | "Emergency Leave" | "Unpaid Leave"
        public string LeaveType { get; set; } = "";

        // "Full Day" | "Half Day"
        public string Duration { get; set; } = "";

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Calculated: 0.5 for half day, 1+ for full day range
        public double EstimatedDays { get; set; }

        public string Reason { get; set; } = "";

        // "Pending" | "Approved" | "Rejected"
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}