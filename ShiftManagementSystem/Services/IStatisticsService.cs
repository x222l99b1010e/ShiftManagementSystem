using ShiftManagementSystem.Models.DTOs;
namespace ShiftManagementSystem.Services
{
	public interface IStatisticsService
	{
		/// <summary>
		/// 取得指定月份的完整班表 (老闆一覽視圖)
		/// </summary>
		/// <param name="year">年份</param>
		/// <param name="month">月份</param>
		/// <returns>月度班表DTO</returns>
		Task<MonthlyScheduleDto> GetMonthlyScheduleAsync(int year, int month);

		/// <summary>
		/// 取得指定月份員工排班排行榜
		/// </summary>
		Task<List<EmployeeLeaderboardDto>> GetMonthlyLeaderboardAsync(int year, int month);

		/// <summary>
		/// 取得指定年份員工排班統計
		/// </summary>
		Task<List<EmployeeYearlyStatsDto>> GetYearlyStatsAsync(int year);

		/// <summary>
		/// 取得單位員工該月的排班天數
		/// </summary>
		Task<int> GetEmployeeMonthlyShiftCountAsync(int userId, int year, int month);

		/// <summary>
		/// 取得單位員工該年的排班天數
		/// </summary>
		Task<int> GetEmployeeYearlyShiftCountAsync(int userId, int year);

		/// <summary>
		/// 重新計算並更新統計快取 (背景工作調用)
		/// </summary>
		Task<bool> RecalculateStatisticsAsync(int year, int month);
	}
}
