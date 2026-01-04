using ShiftManagementSystem.Models;

namespace ShiftManagementSystem.Services
{
	public interface IHolidayService
	{
		/// <summary>
		/// 檢查指定日期是否為假日
		/// </summary>
		Task<bool> IsHolidayAsync(DateTime date);

		/// <summary>
		/// 取得指定月份的所有假日
		/// </summary>
		Task<List<HolidayCache>> GetMonthHolidaysAsync(int year, int month);

		/// <summary>
		/// 初始化年份假日快取 (系統啟動時呼叫)
		/// </summary>
		Task InitializeHolidaysCacheAsync(int year);

		/// <summary>
		/// 獲取假日名稱
		/// </summary>
		Task<string> GetHolidayNameAsync(DateTime date);
	}
}
