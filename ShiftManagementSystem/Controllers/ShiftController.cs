using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShiftManagementSystem.Extensions;
using ShiftManagementSystem.Models.DTOs;
using ShiftManagementSystem.Services;

namespace ShiftManagementSystem.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	[Authorize]
	public class ShiftController : ControllerBase
	{
		private readonly IShiftService _shiftService;
		private readonly IHolidayService _holidayService;
		private readonly ILogger<ShiftController> _logger;

		public ShiftController(IShiftService shiftService, IHolidayService holidayService, ILogger<ShiftController> logger)
		{
			_shiftService = shiftService;
			_holidayService = holidayService;
			_logger = logger;
		}

		/// <summary>
		/// 取得該月日曆 + 假日資訊 (前端渲染用)
		/// </summary>
		[HttpGet("calendar/{year}/{month}")]
		public async Task<IActionResult> GetMonthCalendar(int year, int month)
		{
			try
			{
				var holidays = await _holidayService.GetMonthHolidaysAsync(year, month);

				// 將假日資訊包裝成物件陣列，包含名稱
				var holidayInfo = holidays.Select(h => new {
					date = h.HolidayDate.ToString("yyyy-MM-dd"),
					name = h.HolidayName
				}).ToList();

				// 包含週六日
				var daysInMonth = DateTime.DaysInMonth(year, month);
				var weekends = new List<string>();
				for (int day = 1; day <= daysInMonth; day++)
				{
					var date = new DateTime(year, month, day);
					if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
					{
						weekends.Add(date.ToString("yyyy-MM-dd"));
					}
				}

				return Ok(new
				{
					holidays = holidayInfo, // 現在包含日期與名稱
					weekends = weekends,
					totalDays = daysInMonth
				});
			}
			catch (Exception ex)
			{
				_logger.LogError($"取得日曆失敗: {ex.Message}");
				return StatusCode(500, new { message = "取得日曆失敗" });
			}
		}

		// 輔助方法：統一從 Windows 驗證身分換取資料庫 UserId
		//private async Task<int?> GetCurrentUserIdAsync()
		//{
		//	var winName = User.Identity?.Name;
		//	if (string.IsNullOrEmpty(winName)) return null;

		//	return await _shiftService.GetUserIdByUsernameAsync(winName);
		//}

		/// <summary>
		/// 新增排班 (已改用 GetUserId)
		/// </summary>
		[HttpPost("add")]
		public async Task<IActionResult> AddShift([FromBody] AddShiftRequestDTO request)
		{
			if (!DateTime.TryParse(request.ShiftDate, out var shiftDate))
				return BadRequest(new { message = "日期格式錯誤" });

			// 改用擴充方法：直接從 Cookie 拿 ID，不進資料庫
			var currentUserId = User.GetUserId();
			if (currentUserId == 0) return Unauthorized(new { message = "請先登入" });

			// 權限邏輯判定
			int finalTargetId = currentUserId;

			// 如果有傳 TargetUserId 且 登入者是 Manager
			if (request.TargetUserId.HasValue && request.TargetUserId.Value != currentUserId)
			{
				// 判斷角色同樣不需要查資料庫，ASP.NET 已經幫你解析好了
				if (User.IsInRole("Manager"))
					finalTargetId = request.TargetUserId.Value;
				else
					return StatusCode(403, new { message = "權限不足，只有經理可以操作他人班表" }); // 自動回傳 403
			}

			var result = await _shiftService.AddShiftAsync(finalTargetId, shiftDate);

			// 這裡要注意：你原本寫 error = result.message，
			// 前端就要用 e.response.data.error 讀取。
			// 建議統一改成跟以前一樣的 message = result.message
			if (!result.success) return BadRequest(new { message = result.message });

			return Ok(new { message = result.message });
		}

		/// <summary>
		/// 刪除排班 (已改用 GetUserId)
		/// </summary>
		[HttpDelete("remove")]
		public async Task<IActionResult> RemoveShift([FromBody] RemoveShiftRequestDTO request)
		{
			if (!DateTime.TryParse(request.ShiftDate, out var shiftDate))
				return BadRequest(new { message = "日期格式錯誤" });

			var currentUserId = User.GetUserId();
			if (currentUserId == 0) return Unauthorized(new { message = "請先登入" });

			// --- 新增權限判定 (同步 AddShift 的邏輯) ---
			// 權限邏輯判定
			int finalTargetId = currentUserId;

			if (request.TargetUserId.HasValue && request.TargetUserId.Value != currentUserId)
			{
				if (User.IsInRole("Manager"))
				{
					finalTargetId = request.TargetUserId.Value;
				}
				else
				{
					// 不要只用 Forbid()，改用 StatusCode 並帶入 JSON 物件
					return StatusCode(StatusCodes.Status403Forbidden, new
					{
						message = "權限不足，只有經理可以操作他人排班"
					});
				}
			}
			// ---------------------------------------

			var result = await _shiftService.RemoveShiftAsync(finalTargetId, shiftDate);
			if (!result.success) return BadRequest(new { message = result.message }); // 統一用 message

			return Ok(new { message = result.message });
		}

		/// <summary>
		/// 取得用戶當月排班進度（含已排日期細節）(已改用 GetUserId)
		/// </summary>
		[HttpGet("progress/{year}/{month}")]
		public async Task<IActionResult> GetMonthProgress(int year, int month)
		{
			var userId = User.GetUserId();
			if (userId == 0) return Unauthorized(new { message = "請先登入" });

			// 1. 取得該員工當月所有的排班紀錄
			// 呼叫我們剛剛在 Service 寫好的方法
			var records = await _shiftService.GetUserMonthlyShiftsAsync(userId, year, month);

			// 2. 提取日期清單 (yyyy-MM-dd)
			// 將紀錄轉換為字串清單 (yyyy-MM-dd)，方便前端 JS 比對
			var existingDates = records.Select(r => r.ShiftDate.ToString("yyyy-MM-dd")).ToList();
			var count = existingDates.Count;
			
			return Ok(new
			{
				currentShifts = count,
				existingDates = existingDates,// 回傳具體日期，讓前端 Set 已排過的標記
				minRequired = 6,
				maxAllowed = 15,
				isCompliant = count >= 6 && count <= 15
			});
		}

		/// <summary>
		/// 批次儲存 (已優化)
		/// </summary>
		[HttpPost("bulk-save")]
		public async Task<IActionResult> BulkSaveShifts([FromBody] BulkShiftRequest request)
		{
			var userId = User.GetUserId();
			if (userId == 0) return Unauthorized(new { message = "無法辨識使用者身分" });

			var result = await _shiftService.SaveMonthlyShiftsAsync(userId, request.Year, request.Month, request.ShiftDates);

			if (!result.success)
			{
				// 這裡回傳一個明確的 JSON 物件
				
				return BadRequest(new { message = result.message });// 這裡會是「2026-02-02 已滿額」之類的提示
			}
			return Ok(new { message = "整月排班儲存成功" });
		}
	}
}
