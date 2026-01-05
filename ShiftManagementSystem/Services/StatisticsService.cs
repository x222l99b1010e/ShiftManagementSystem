using Microsoft.EntityFrameworkCore; // 修正 CS1061: 必須引用這行才能用 Async 方法
using ShiftManagementSystem.Models;
using ShiftManagementSystem.Models.DTOs;

namespace ShiftManagementSystem.Services
{
	public class StatisticsService : IStatisticsService
	{
		private readonly ScheduleDBContext _context;
		private readonly IHolidayService _holidayService;
		private readonly ILogger<StatisticsService> _logger;

		private const int MIN_SHIFTS_PER_MONTH = 6;
		private const int MAX_SHIFTS_PER_MONTH = 15;

		public StatisticsService(
			ScheduleDBContext context,
			IHolidayService holidayService,
			ILogger<StatisticsService> logger)
		{
			_context = context;
			_holidayService = holidayService;
			_logger = logger;
		}

		// 在 StatisticsService 類別內修改
		private IQueryable<ShiftRecord> GetValidShifts()
		{
			// 統一狀態過濾：只抓 Pending (或未來你定義的 Approved)
			return _context.ShiftRecords.Where(s => s.ShiftStatus == "Pending");
		}

		/// <summary>
		/// 取得月度班表 - 老闆一覽無遺
		/// </summary>
		public async Task<MonthlyScheduleDto> GetMonthlyScheduleAsync(int year, int month)
		{
			try
			{
				_logger.LogInformation($"取得 {year}-{month:D2} 月班表");

				// 修正 CS0019/CS1503: 改用 DateOnly 來建立日期範圍
				var daysInMonth = DateTime.DaysInMonth(year, month);
				var firstDay = new DateOnly(year, month, 1);
				var lastDay = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

				// 2️⃣ 取得所有員工
				var employees = await _context.Users
					// 修正 CS0019: IsActive 是 bool?，需明確比對 == true
					.Where(u => u.Role == "Employee" && u.IsActive == true)
					.OrderBy(u => u.FullName)
					.ToListAsync();

				// 3️⃣ 取得該月所有排班記錄
				var shiftRecords = await GetValidShifts()
					// 修正: 這裡現在都是 DateOnly，可以直接比較
					.AsNoTracking()
					.Where(s => s.ShiftDate >= firstDay && s.ShiftDate <= lastDay)
					.Include(s => s.User)
					.ToListAsync();

				// 4️⃣ 取得該月假日
				var holidays = await _holidayService.GetMonthHolidaysAsync(year, month);
				var holidayDates = holidays
					.ToDictionary(h => h.HolidayDate, h => h.HolidayName);

				// 5️⃣ 構建日程視圖
				var daySchedules = new List<DayScheduleDto>();
				for (int day = 1; day <= daysInMonth; day++)
				{
					// 修正: 迴圈內也使用 DateOnly
					var date = new DateOnly(year, month, day);
					// DayOfWeek 需要轉回 DateTime 或是 DateOnly (DateOnly 也有 DayOfWeek 屬性)
					var isWeekend = date.DayOfWeek == DayOfWeek.Saturday
								 || date.DayOfWeek == DayOfWeek.Sunday;

					// 修正: Dictionary Key 也是 DateOnly，現在可以直接查
					var isHoliday = holidayDates.ContainsKey(date);
					var holidayName = isHoliday ? holidayDates[date] : "";

					var dayShifts = shiftRecords
						.Where(s => s.ShiftDate == date)
						.ToList();

					var daySchedule = new DayScheduleDto
					{
						// 注意: DTO 裡的 Date 如果是 DateTime 型別，需轉換
						Date = date.ToDateTime(TimeOnly.MinValue),
						DayOfMonth = day,
						DayOfWeek = (int)date.DayOfWeek,
						IsWeekend = isWeekend,
						IsHoliday = isHoliday,
						HolidayName = holidayName,
						CurrentShiftCount = dayShifts.Count,
						ShiftedEmployees = dayShifts
							.Select(s => new ShiftedEmployeeDto
							{
								UserId = s.UserId,
								FullName = s.User.FullName,
								// DTO 裡的 ShiftDate 如果是 string，就 ToString，如果是 DateTime 就轉
								//ShiftDate = s.ShiftDate.ToString("yyyy-MM-dd")
								// 修正點：使用 ToDateTime(TimeOnly.MinValue)
								// 這樣會變成例如：2026-02-15 00:00:00
								ShiftDate = s.ShiftDate.ToDateTime(TimeOnly.MinValue)
							})
							.ToList()
					};

					daySchedules.Add(daySchedule);
				}

				// 6️⃣ 構建回傳 DTO
				var result = new MonthlyScheduleDto
				{
					Year = year,
					Month = month,
					Employees = employees
						.Select(e => new EmployeeDto
						{
							UserId = e.UserId,
							FullName = e.FullName,
							Username = e.Username
						})
						.ToList(),
					DaySchedules = daySchedules,
					TotalDays = daysInMonth
				};

				_logger.LogInformation($"成功取得 {year}-{month:D2} 月班表 ({employees.Count} 位員工)");
				return result;
			}
			catch (Exception ex)
			{
				_logger.LogError($"取得月度班表失敗: {ex.Message}");
				throw;
			}
		}

		/// <summary>
		/// 取得月度排行榜
		/// </summary>
		public async Task<List<EmployeeLeaderboardDto>> GetMonthlyLeaderboardAsync(int year, int month)
		{
			try
			{
				_logger.LogInformation($"取得 {year}-{month:D2} 月排行榜");

				// 1️⃣ 取得所有員工及其該月排班天數
				var employees = await _context.Users
					// 修正 CS0019: IsActive == true
					.Where(u => u.Role == "Employee" && u.IsActive == true)
					.Select(u => new
					{
						u.UserId,
						u.FullName,
						ShiftCount = u.ShiftRecords
							// 修正: DateOnly 支援 .Year 和 .Month
							.Where(s => s.ShiftDate.Year == year
									&& s.ShiftDate.Month == month
									&& s.ShiftStatus == "Pending")
							.Count()
					})
					.OrderByDescending(e => e.ShiftCount)
					.ThenBy(e => e.FullName)
					.ToListAsync();

				// 2️⃣ 轉換為排行榜 DTO
				var leaderboard = employees
					.Select((emp, index) => new EmployeeLeaderboardDto
					{
						Rank = index + 1,
						UserId = emp.UserId,
						FullName = emp.FullName,
						ShiftDays = emp.ShiftCount,
						IsCompliant = emp.ShiftCount >= MIN_SHIFTS_PER_MONTH
								   && emp.ShiftCount <= MAX_SHIFTS_PER_MONTH,
						CompliancePercentage = emp.ShiftCount >= MIN_SHIFTS_PER_MONTH
							? (emp.ShiftCount >= MAX_SHIFTS_PER_MONTH
								? 100m
								: Math.Round((decimal)emp.ShiftCount / MAX_SHIFTS_PER_MONTH * 100, 2))
							: Math.Round((decimal)emp.ShiftCount / MIN_SHIFTS_PER_MONTH * 100, 2)
					})
					.ToList();

				_logger.LogInformation($"成功取得 {year}-{month:D2} 月排行榜 ({leaderboard.Count} 位)");
				return leaderboard;
			}
			catch (Exception ex)
			{
				_logger.LogError($"取得月度排行榜失敗: {ex.Message}");
				throw;
			}
		}

		/// <summary>
		/// 取得年度統計
		/// </summary>
		public async Task<List<EmployeeYearlyStatsDto>> GetYearlyStatsAsync(int year)
		{
			try
			{
				_logger.LogInformation($"取得 {year} 年統計");

				// 1️⃣ 取得所有員工及其該年排班記錄
				var employees = await _context.Users
					// 修正 CS0019: IsActive == true
					.Where(u => u.Role == "Employee" && u.IsActive == true)
					.Include(u => u.ShiftRecords)
					.OrderBy(u => u.FullName)
					.ToListAsync();

				var stats = new List<EmployeeYearlyStatsDto>();

				// 2️⃣ 計算每位員工的統計
				foreach (var emp in employees)
				{
					// 該年的排班記錄
					var yearlyShifts = emp.ShiftRecords
						.Where(s => s.ShiftDate.Year == year && s.ShiftStatus == "Pending")
						.ToList();

					// 月度細節
					var monthlyBreakdown = new Dictionary<int, int>();
					for (int m = 1; m <= 12; m++)
					{
						var monthCount = yearlyShifts
							.Where(s => s.ShiftDate.Month == m)
							.Count();
						monthlyBreakdown[m] = monthCount;
					}

					var totalYearly = yearlyShifts.Count;
					var averageMonthly = totalYearly > 0
						? Math.Round((decimal)totalYearly / 12, 2)
						: 0m;

					// 計算平均符合率
					var compliantMonths = monthlyBreakdown.Values
						.Where(count => count >= MIN_SHIFTS_PER_MONTH
									 && count <= MAX_SHIFTS_PER_MONTH)
						.Count();
					var averageCompliance = (decimal)compliantMonths / 12 * 100;

					stats.Add(new EmployeeYearlyStatsDto
					{
						UserId = emp.UserId,
						FullName = emp.FullName,
						TotalYearlyShifts = totalYearly,
						AverageMonthlyShifts = averageMonthly,
						MonthlyBreakdown = monthlyBreakdown,
						AverageCompliancePercentage = Math.Round(averageCompliance, 2)
					});
				}

				_logger.LogInformation($"成功取得 {year} 年統計 ({stats.Count} 位員工)");
				return stats;
			}
			catch (Exception ex)
			{
				_logger.LogError($"取得年度統計失敗: {ex.Message}");
				throw;
			}
		}

		/// <summary>
		/// 取得單位員工該月排班天數
		/// </summary>
		public async Task<int> GetEmployeeMonthlyShiftCountAsync(int userId, int year, int month)
		{
			// 修正 CS1061: 確保已引用 Microsoft.EntityFrameworkCore
			return await _context.ShiftRecords
				.Where(s => s.UserId == userId
						&& s.ShiftDate.Year == year
						&& s.ShiftDate.Month == month
						&& s.ShiftStatus == "Pending")
				.CountAsync();
		}

		/// <summary>
		/// 取得單位員工該年排班天數
		/// </summary>
		public async Task<int> GetEmployeeYearlyShiftCountAsync(int userId, int year)
		{
			return await _context.ShiftRecords
				.Where(s => s.UserId == userId
						&& s.ShiftDate.Year == year
						&& s.ShiftStatus == "Pending")
				.CountAsync();
		}

		/// <summary>
		/// 重新計算統計快取 (背景工作或定期調用)
		/// </summary>
		public async Task<bool> RecalculateStatisticsAsync(int year, int month)
		{
			try
			{
				_logger.LogInformation($"開始重新計算 {year}-{month:D2} 月統計");

				// 1️⃣ 取得所有員工
				var employees = await _context.Users
					// 修正 CS0019: IsActive == true
					.Where(u => u.Role == "Employee" && u.IsActive == true)
					.ToListAsync();

				// 2️⃣ 為每位員工計算並更新統計
				foreach (var emp in employees)
				{
					var monthlyCount = await GetEmployeeMonthlyShiftCountAsync(
						emp.UserId, year, month);

					var yearlyCount = await GetEmployeeYearlyShiftCountAsync(
						emp.UserId, year);

					// 尋找或建立統計記錄
					var monthStat = await _context.ShiftStatistics
						.FirstOrDefaultAsync(s => s.UserId == emp.UserId
											&& s.StatYear == year
											&& s.StatMonth == month);

					// 修正 CS0246: 類別名稱應為 ShiftStatistic (單數)
					if (monthStat == null)
					{
						monthStat = new ShiftStatistic
						{
							UserId = emp.UserId,
							StatYear = year,
							StatMonth = month,
							TotalShiftDays = monthlyCount,
							LastCalculatedAt = DateTime.UtcNow
						};
						_context.ShiftStatistics.Add(monthStat);
					}
					else
					{
						monthStat.TotalShiftDays = monthlyCount;
						monthStat.LastCalculatedAt = DateTime.UtcNow;
					}

					// 年度統計
					var yearStat = await _context.ShiftStatistics
						.FirstOrDefaultAsync(s => s.UserId == emp.UserId
											&& s.StatYear == year
											&& s.StatMonth == null);

					if (yearStat == null)
					{
						yearStat = new ShiftStatistic
						{
							UserId = emp.UserId,
							StatYear = year,
							StatMonth = null,
							TotalShiftDays = yearlyCount,
							LastCalculatedAt = DateTime.UtcNow
						};
						_context.ShiftStatistics.Add(yearStat);
					}
					else
					{
						yearStat.TotalShiftDays = yearlyCount;
						yearStat.LastCalculatedAt = DateTime.UtcNow;
					}
				}

				// 3️⃣ 保存所有變更
				await _context.SaveChangesAsync();

				_logger.LogInformation(
					$"成功重新計算 {year}-{month:D2} 月統計 ({employees.Count} 位員工)");
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError($"重新計算統計失敗: {ex.Message}");
				return false;
			}
		}
	}
}