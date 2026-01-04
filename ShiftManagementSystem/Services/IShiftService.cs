using ShiftManagementSystem.Models;

namespace ShiftManagementSystem.Services
{
	public interface IShiftService
	{
		/// <summary>
		/// 新增排班記錄
		/// </summary>
		Task<(bool success, string message)> AddShiftAsync(int userId, DateTime shiftDate);

		/// <summary>
		/// 刪除排班記錄
		/// </summary>
		Task<(bool success, string message)> RemoveShiftAsync(int userId, DateTime shiftDate);

		/// <summary>
		/// 驗證排班是否合法
		/// </summary>
		Task<(bool valid, string reason)> ValidateShiftAsync(int userId, DateTime shiftDate);

		/// <summary>
		/// 取得用戶該月已排班天數
		/// </summary>
		Task<int> GetUserMonthlyShiftCountAsync(int userId, int year, int month);

		/// <summary>
		/// 取得該日已排班人數
		/// </summary>
		Task<int> GetDailyShiftCountAsync(DateTime date);

		/// <summary>
		/// 查出 User 實體
		/// </summary>
		/// <param name="username"></param>
		/// <returns></returns>
		Task<int?> GetUserIdByUsernameAsync(string username);

		Task<(bool success, string message)> SaveMonthlyShiftsAsync(int userId, int year, int month, List<DateTime> selectedDates);

		// 新增：取得員工當月已排班的具體紀錄
		Task<List<ShiftRecord>> GetUserMonthlyShiftsAsync(int userId, int year, int month);
	}
}
