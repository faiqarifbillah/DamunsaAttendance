using SQLite;

namespace DamunsaAttendance
{
    public static class SeedData
    {
        // ── 6-staff roster (StaffUserId 1–6) ────────────────────────────────
        private static readonly (int Id, string Name, string Dept, string Position)[] Staff = {
            (1, "Hafiz Zulkifli",  "Logistics", "Driver"),
            (2, "Ridhwan Mansor",  "Logistics", "Driver"),
            (3, "Azrul Hafifi",    "Logistics", "Runner"),
            (4, "Haziq Shamsudin", "Logistics", "Runner"),
            (5, "Firdaus Norzaidi","Delivery",  "Rider"),
            (6, "Amsyar Lokman",   "Delivery",  "Rider"),
        };

        /// <summary>
        /// Seeds User accounts, attendance, and leave records.
        /// Safe to call multiple times — skips if data already exists.
        /// </summary>
        public static async Task SeedAsync(Db db)
        {
            await SeedUsersAsync(db);
            await SeedAttendanceAndLeaveAsync(db);
        }

        // ── User Accounts ────────────────────────────────────────────────────

        private static async Task SeedUsersAsync(Db db)
        {
            int count = await db.GetUserCountAsync();
            if (count > 0) return;

            // 6 staff accounts — username = firstname lowercase.
            // Passwords are placeholders: set real values locally before first run (see README).
            var staffUsers = new[]
            {
                ("hafiz",   "CHANGE_ME", 1),
                ("ridhwan", "CHANGE_ME", 2),
                ("azrul",   "CHANGE_ME", 3),
                ("haziq",   "CHANGE_ME", 4),
                ("firdaus", "CHANGE_ME", 5),
                ("amsyar",  "CHANGE_ME", 6),
            };

            foreach (var (username, password, staffId) in staffUsers)
            {
                var s = Staff.First(x => x.Id == staffId);
                await db.AddUserAsync(new User
                {
                    Username = username,
                    Password = password,
                    Role = "Staff",
                    StaffUserId = staffId,
                    FullName = s.Name,
                    Department = s.Dept,
                    Position = s.Position
                });
            }

            // Admin account
            await db.AddUserAsync(new User
            {
                Username = "admin",
                Password = "CHANGE_ME",
                Role = "Admin",
                StaffUserId = 0,
                FullName = "Admin",
                Department = "Management",
                Position = "Admin"
            });

            // Owner account
            await db.AddUserAsync(new User
            {
                Username = "owner",
                Password = "CHANGE_ME",
                Role = "Owner",
                StaffUserId = 0,
                FullName = "Owner",
                Department = "Management",
                Position = "Owner"
            });
        }

        // ── Attendance & Leave ───────────────────────────────────────────────

        private static async Task SeedAttendanceAndLeaveAsync(Db db)
        {
            var existing = await db.GetAllAttendanceAsync();
            if (existing.Count > 0) return;

            var today = DateTime.Today;

            // Seed 4 weeks of Mon–Fri attendance
            for (int weekOffset = -3; weekOffset <= 0; weekOffset++)
            {
                int daysFromMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                var monday = today.AddDays(-daysFromMonday + (weekOffset * 7));

                for (int dayOffset = 0; dayOffset < 5; dayOffset++)
                {
                    var date = monday.AddDays(dayOffset);
                    if (date > today) continue;
                    if (date.DayOfWeek == DayOfWeek.Saturday ||
                        date.DayOfWeek == DayOfWeek.Sunday) continue;

                    foreach (var (userId, name, dept, _) in Staff)
                    {
                        var pattern = GetDayPattern(userId, date);
                        if (pattern.skip) continue;

                        var checkIn = date.Add(pattern.inTime);
                        var checkOut = date.Add(pattern.outTime);
                        string status = pattern.inTime > TimeSpan.FromHours(8.5)
                            ? "Late" : "Present";

                        await db.AddAttendanceAsync(new AttendanceRecord
                        {
                            UserId = userId,
                            StaffName = name,
                            Department = dept,
                            AttendanceDate = date,
                            CheckInTime = checkIn,
                            CheckOutTime = checkOut,
                            Status = status
                        });
                    }
                }
            }

            // Seed leave requests
            var leaveRecords = new List<(int uid, string type, string dur,
                DateTime start, DateTime end, double days,
                string reason, string status, DateTime applied)>
            {
                (1, "Annual Leave",    "Full Day",
                    today.AddDays(-10), today.AddDays(-9), 2,
                    "Family matters",        "Approved", today.AddDays(-12)),

                (2, "Sick Leave",      "Full Day",
                    today.AddDays(-5),  today.AddDays(-5), 1,
                    "Fever and flu",         "Approved", today.AddDays(-5)),

                (3, "Annual Leave",    "Full Day",
                    today.AddDays(7),   today.AddDays(8), 2,
                    "School holiday trip",   "Pending",  today),

                (4, "Emergency Leave", "Full Day",
                    today.AddDays(-3),  today.AddDays(-3), 1,
                    "Family emergency",      "Approved", today.AddDays(-3)),

                (5, "Unpaid Leave",    "Full Day",
                    today.AddDays(-7),  today.AddDays(-6), 2,
                    "Personal matter",       "Rejected", today.AddDays(-8)),

                (6, "Annual Leave",    "Half Day",
                    today.AddDays(-2),  today.AddDays(-2), 0.5,
                    "Personal appointment",  "Approved", today.AddDays(-3)),

                (2, "Annual Leave",    "Full Day",
                    today.AddDays(14),  today.AddDays(15), 2,
                    "Holiday",               "Pending",  today),

                (1, "Unpaid Leave",    "Full Day",
                    today.AddDays(-20), today.AddDays(-19), 2,
                    "Hobby",                 "Rejected", today.AddDays(-21)),

                (3, "Sick Leave",      "Half Day",
                    today.AddDays(-14), today.AddDays(-14), 0.5,
                    "Doctor appointment",    "Approved", today.AddDays(-14)),

                (5, "Emergency Leave", "Full Day",
                    today.AddDays(3),   today.AddDays(3), 1,
                    "Urgent family matter",  "Pending",  today),
            };

            foreach (var (uid, type, dur, start, end, days, reason, status, applied) in leaveRecords)
            {
                var s = Staff.First(x => x.Id == uid);
                await db.AddLeaveAsync(new LeaveRequest
                {
                    UserId = uid,
                    StaffName = s.Name,
                    Department = s.Dept,
                    LeaveType = type,
                    Duration = dur,
                    StartDate = start,
                    EndDate = end,
                    EstimatedDays = days,
                    Reason = reason,
                    Status = status,
                    CreatedAt = applied
                });
            }
        }

        // ── Deterministic daily pattern per staff ────────────────────────────

        private static (TimeSpan inTime, TimeSpan outTime, bool skip)
            GetDayPattern(int userId, DateTime date)
        {
            int seed = (userId * 31 + date.DayOfYear + date.Year) % 100;

            // ~10% absence chance for non-Hafiz
            if (userId != 1 && seed < 10)
                return (TimeSpan.Zero, TimeSpan.Zero, true);

            var checkIn = userId switch
            {
                1 => TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(1)),    // always on time
                2 => seed < 15
                    ? TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(45))   // occasionally late
                    : TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(10)),
                3 => seed < 25
                    ? TimeSpan.FromHours(9).Add(TimeSpan.FromMinutes(seed % 30)) // often late
                    : TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(30)),
                4 => TimeSpan.FromHours(7).Add(TimeSpan.FromMinutes(55)),   // early bird
                5 => seed < 20
                    ? TimeSpan.FromHours(9).Add(TimeSpan.FromMinutes(5))
                    : TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(20)),
                6 => TimeSpan.FromHours(8).Add(TimeSpan.FromMinutes(15)),   // consistent
                _ => TimeSpan.FromHours(8)
            };

            var workHours = TimeSpan.FromHours(8 + (seed % 10) * 0.1);
            var checkOut = checkIn.Add(workHours);

            return (checkIn, checkOut, false);
        }
    }
}
