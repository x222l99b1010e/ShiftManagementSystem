using Microsoft.EntityFrameworkCore;
using ShiftManagementSystem.Models;
using System.Text.Json;

namespace ShiftManagementSystem.Services
{
	public class ShiftService : IShiftService
	{
		private readonly ScheduleDBContext _context;
		private readonly IHolidayService _holidayService;
		private readonly ILogger<ShiftService> _logger;

		private const int MIN_SHIFTS_PER_MONTH = 6;
		private const int MAX_SHIFTS_PER_MONTH = 15;
		private const int MAX_EMPLOYEES_PER_DAY = 2;

		public ShiftService(
			ScheduleDBContext context,
			IHolidayService holidayService,
			ILogger<ShiftService> logger)
		{
			_context = context;
			_holidayService = holidayService;
			_logger = logger;
		}

		public async Task<(bool success, string message)> AddShiftAsync(int userId, DateTime shiftDate)
		{
			// 驗證
			var validation = await ValidateShiftAsync(userId, shiftDate);
			if (!validation.valid)
			{
				return (false, validation.reason);
			}

			try
			{
				var shift = new ShiftRecord
				{
					UserId = userId,
					//ShiftDate = shiftDate.Date,
					// 修正點：使用 DateOnly.FromDateTime
					ShiftDate = DateOnly.FromDateTime(shiftDate),
					ShiftStatus = "Pending",
					CreatedAt = DateTime.Now,
					UpdatedAt = DateTime.Now
				};

				_context.ShiftRecords.Add(shift);
				await _context.SaveChangesAsync();

				_logger.LogInformation($"用戶 {userId} 成功排班於 {shiftDate:yyyy-MM-dd}");
				return (true, "排班成功");
			}
			catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE") == true)
			{
				return (false, "該員工已在此日期排班");
			}
			catch (Exception ex)
			{
				_logger.LogError($"排班失敗: {ex.Message}");
				return (false, "系統錯誤");
			}
		}

		public async Task<(bool success, string message)> RemoveShiftAsync(int userId, DateTime shiftDate)
		{
			// 修正第 67 行附近
			var targetDate = DateOnly.FromDateTime(shiftDate); // 先轉換
			var shift = await _context.ShiftRecords
				.FirstOrDefaultAsync(s => s.UserId == userId && s.ShiftDate == targetDate);

			if (shift == null)
			{
				return (false, "未找到排班記錄");
			}

			_context.ShiftRecords.Remove(shift);
			await _context.SaveChangesAsync();

			_logger.LogInformation($"用戶 {userId} 成功刪除 {shiftDate:yyyy-MM-dd} 的排班");
			return (true, "取消排班成功");
		}

		public async Task<(bool valid, string reason)> ValidateShiftAsync(int userId, DateTime shiftDate)
		{
			// 1. 檢查是否為假日 (包括週六日)
			if (await _holidayService.IsHolidayAsync(shiftDate))
			{
				var holidayName = await _holidayService.GetHolidayNameAsync(shiftDate);
				return (false, $"無法排班: {holidayName}");
			}

			// 2. 檢查是否只開放下個月 (天條)
			var now = DateTime.Today;
			var nextMonth = now.AddMonths(1);
			if (shiftDate.Year != nextMonth.Year || shiftDate.Month != nextMonth.Month)
			{
				return (false, $"只開放 {nextMonth:yyyy年MM月} 的排班");
			}

			// 3. 檢查該月已排班天數
			var monthlyCount = await GetUserMonthlyShiftCountAsync(userId, shiftDate.Year, shiftDate.Month);
			if (monthlyCount >= MAX_SHIFTS_PER_MONTH)
			{
				return (false, $"已達月度上限 {MAX_SHIFTS_PER_MONTH} 天");
			}

			// 4. 檢查該日人數是否已滿
			var dailyCount = await GetDailyShiftCountAsync(shiftDate);
			if (dailyCount >= MAX_EMPLOYEES_PER_DAY)
			{
				return (false, "該日期已滿額 (2人)");
			}

			// 5. 檢查是否已排過該日
			// 修正第 114 行附近
			var targetDate = DateOnly.FromDateTime(shiftDate);
			var exists = await _context.ShiftRecords
				.AnyAsync(s => s.UserId == userId && s.ShiftDate == targetDate);
			if (exists)
			{
				return (false, "您已在此日期排班");
			}

			return (true, "驗證成功");
		}

		public async Task<int> GetUserMonthlyShiftCountAsync(int userId, int year, int month)
		{
			// 建立該月第一天與下個月第一天的 DateOnly 物件
			var firstDayOfMonth = new DateOnly(year, month, 1);
			var firstDayOfNextMonth = firstDayOfMonth.AddMonths(1);

			return await _context.ShiftRecords
				.Where(s => s.UserId == userId
					&& s.ShiftDate >= firstDayOfMonth
					&& s.ShiftDate < firstDayOfNextMonth
					&& s.ShiftStatus == "Pending")
				.CountAsync();
		}

		// 修正第 136 行附近
		public async Task<int> GetDailyShiftCountAsync(DateTime date)
		{
			var targetDate = DateOnly.FromDateTime(date);
			return await _context.ShiftRecords
				.Where(s => s.ShiftDate == targetDate && s.ShiftStatus == "Pending")
				.CountAsync();
		}

		public async Task<int?> GetUserIdByUsernameAsync(string username)
		{
			// Windows 帳號通常是 "DOMAIN\Account"，我們只要後面的 Account
			var accountName = username.Split('\\').Last();

			var user = await _context.Users
				.FirstOrDefaultAsync(u => u.Username == accountName);

			return user?.UserId;
		}

		public async Task<(bool success, string message)> SaveMonthlyShiftsAsync(int userId, int year, int month, List<DateTime> selectedDates)
		{
			// 1. 基本規則檢查 (天條：6-15天)
			if (selectedDates.Count < 6 || selectedDates.Count > 15)
				return (false, "每月排班必須在 6 至 15 天之間");

			// 2. 檢查是否為下個月 (面試規格需求)
			var nextMonth = DateTime.Today.AddMonths(1);
			if (year != nextMonth.Year || month != nextMonth.Month)
				return (false, $"目前僅開放 {nextMonth:yyyy/MM} 的排班");

			using var transaction = await _context.Database.BeginTransactionAsync();
			try
			{
				// 3. 先清除該員工該月份既有的排班 (批次更新的標準做法：先清後加)
				var firstDay = new DateOnly(year, month, 1);
				var lastDay = firstDay.AddMonths(1);
				var oldRecords = await _context.ShiftRecords
					.Where(s => s.UserId == userId && s.ShiftDate >= firstDay && s.ShiftDate < lastDay)
					.ToListAsync();
				_context.ShiftRecords.RemoveRange(oldRecords);
				await _context.SaveChangesAsync();

				// 4. 逐一驗證每一天
				foreach (var dt in selectedDates)
				{
					// A. 檢查假日 (天條：不可排假日)
					if (await _holidayService.IsHolidayAsync(dt))
						return (false, $"{dt:MM/dd} 是假日/週末，不可排班");

					// B. 檢查每日上限 (天條：每天最多2位)
					var dailyCount = await _context.ShiftRecords.CountAsync(s => s.ShiftDate == DateOnly.FromDateTime(dt));
					if (dailyCount >= 2)
						return (false, $"{dt:MM/dd} 班次已滿 (限2人)");

					// C. 加入新記錄
					_context.ShiftRecords.Add(new ShiftRecord
					{
						UserId = userId,
						ShiftDate = DateOnly.FromDateTime(dt),
						ShiftStatus = "Pending",
						CreatedAt = DateTime.Now
					});
				}

				await _context.SaveChangesAsync();
				await transaction.CommitAsync(); // 只有全部成功才寫入資料庫
				return (true, "排班成功");
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				_logger.LogError($"批次排班失敗: {ex.Message}");
				return (false, "系統錯誤，請稍後再試");
			}
		}

		// 請將此方法放入你的 ShiftService 類別內
		public async Task<List<ShiftRecord>> GetUserMonthlyShiftsAsync(int userId, int year, int month)
		{
			// 建立該月範圍
			var firstDayOfMonth = new DateOnly(year, month, 1);
			var firstDayOfNextMonth = firstDayOfMonth.AddMonths(1);

			// 抓取該用戶當月所有 Pending 的排班紀錄
			return await _context.ShiftRecords
				.Where(s => s.UserId == userId
					&& s.ShiftDate >= firstDayOfMonth
					&& s.ShiftDate < firstDayOfNextMonth
					&& s.ShiftStatus == "Pending")
				.OrderBy(s => s.ShiftDate) // 排序讓前端好處理
				.ToListAsync();
		}
	}
}
