using SQLite;

namespace DamunsaAttendance
{
    public class Db
    {
        private readonly SQLiteAsyncConnection _db;
        private bool _initialized = false;

        public Db(string dbPath)
        {
            _db = new SQLiteAsyncConnection(dbPath);
        }

        // ── Init & Migration ────────────────────────────────────────────────

        public async Task InitAsync()
        {
            if (_initialized) return;

            await _db.CreateTableAsync<AttendanceRecord>();
            await _db.CreateTableAsync<LeaveRequest>();
            await _db.CreateTableAsync<User>();

            await MigrateAsync();

            _initialized = true;
        }

        private async Task MigrateAsync()
        {
            var migrations = new[]
            {
                "ALTER TABLE AttendanceRecord ADD COLUMN StaffName TEXT NOT NULL DEFAULT ''",
                "ALTER TABLE AttendanceRecord ADD COLUMN Department TEXT NOT NULL DEFAULT ''",
                "ALTER TABLE LeaveRequest ADD COLUMN StaffName TEXT NOT NULL DEFAULT ''",
                "ALTER TABLE LeaveRequest ADD COLUMN Department TEXT NOT NULL DEFAULT ''",
            };

            foreach (var sql in migrations)
            {
                try { await _db.ExecuteAsync(sql); }
                catch { /* Column already exists — safe to ignore */ }
            }
        }

        // ── User ────────────────────────────────────────────────────────────

        public Task<int> AddUserAsync(User user)
            => _db.InsertAsync(user);

        public Task<int> GetUserCountAsync()
            => _db.Table<User>().CountAsync();

        public Task<User?> GetUserByIdAsync(int id)
            => _db.Table<User>().Where(u => u.Id == id).FirstOrDefaultAsync();

        public Task<User?> GetUserByCredentialsAsync(string username, string password, string role)
            => _db.Table<User>()
                  .Where(u => u.Username == username
                           && u.Password == password
                           && u.Role == role)
                  .FirstOrDefaultAsync();

        // ── Attendance ──────────────────────────────────────────────────────

        public Task<int> AddAttendanceAsync(AttendanceRecord rec)
            => _db.InsertAsync(rec);

        public Task<int> UpdateAttendanceAsync(AttendanceRecord rec)
            => _db.UpdateAsync(rec);

        public Task<AttendanceRecord?> GetOpenAttendanceAsync(int userId)
            => _db.Table<AttendanceRecord>()
                  .Where(a => a.UserId == userId && a.CheckOutTime == null)
                  .FirstOrDefaultAsync();

        public Task<List<AttendanceRecord>> GetAttendanceByUserAsync(int userId)
            => _db.Table<AttendanceRecord>()
                  .Where(a => a.UserId == userId)
                  .OrderByDescending(a => a.CheckInTime)
                  .ToListAsync();

        public Task<List<AttendanceRecord>> GetAllAttendanceAsync()
            => _db.Table<AttendanceRecord>()
                  .OrderByDescending(a => a.CheckInTime)
                  .ToListAsync();

        // ── Leave Requests ──────────────────────────────────────────────────

        public Task<int> AddLeaveAsync(LeaveRequest req)
            => _db.InsertAsync(req);

        public Task<int> UpdateLeaveAsync(LeaveRequest req)
            => _db.UpdateAsync(req);

        public Task<List<LeaveRequest>> GetAllLeaveAsync()
            => _db.Table<LeaveRequest>()
                  .OrderByDescending(x => x.CreatedAt)
                  .ToListAsync();

        public Task<List<LeaveRequest>> GetLeaveByUserAsync(int userId)
            => _db.Table<LeaveRequest>()
                  .Where(l => l.UserId == userId)
                  .OrderByDescending(x => x.CreatedAt)
                  .ToListAsync();

        // ── Annual Leave Balance ────────────────────────────────────────────

        public async Task<double> GetUsedAnnualLeaveAsync(int userId)
        {
            var approved = await _db.Table<LeaveRequest>()
                .Where(l => l.UserId == userId
                         && l.LeaveType == "Annual Leave"
                         && l.Status == "Approved")
                .ToListAsync();

            return approved.Sum(l => l.EstimatedDays);
        }

        // ── Dashboard Summary ───────────────────────────────────────────────

        public Task<int> GetTotalAttendanceCountAsync()
            => _db.Table<AttendanceRecord>().CountAsync();

        public Task<int> GetLeaveCountByStatusAsync(string status)
            => _db.Table<LeaveRequest>()
                  .Where(l => l.Status == status)
                  .CountAsync();

        public async Task<int> GetStaffOnLeaveTodayCountAsync()
        {
            var today = DateTime.Today;
            var onLeave = await _db.Table<LeaveRequest>()
                .Where(l => l.Status == "Approved"
                         && l.StartDate <= today
                         && l.EndDate >= today)
                .ToListAsync();

            return onLeave.Select(l => l.UserId).Distinct().Count();
        }
    }
}
