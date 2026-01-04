using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShiftManagementSystem.Services;

namespace ShiftManagementSystem.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	[Authorize(Roles = "Manager")]  // 只有老闆可以存取
	//[Authorize]
	public class StatisticsController : ControllerBase
	{
		private readonly IStatisticsService _statisticsService;
		private readonly ILogger<StatisticsController> _logger;

		public StatisticsController(
			IStatisticsService statisticsService,
			ILogger<StatisticsController> logger)
		{
			_statisticsService = statisticsService;
			_logger = logger;
		}

		/// <summary>
		/// 取得月度班表 (老闆一覽無遺視圖)
		/// GET /api/statistics/monthly-schedule/2026/2
		/// </summary>
		[HttpGet("monthly-schedule/{year}/{month}")]
		public async Task<IActionResult> GetMonthlySchedule(int year, int month)
		{
			try
			{
				if (month < 1 || month > 12)
				{
					return BadRequest(new { error = "月份必須在 1-12 之間" });
				}

				var schedule = await _statisticsService
					.GetMonthlyScheduleAsync(year, month);

				return Ok(new
				{
					success = true,
					data = schedule,
					message = $"成功取得 {year}年{month}月班表"
				});
			}
			catch (Exception ex)
			{
				_logger.LogError($"取得月度班表失敗: {ex.Message}");
				return StatusCode(500, new
				{
					success = false,
					error = "取得班表失敗"
				});
			}
		}

		/// <summary>
		/// 取得月度排行榜
		/// GET /api/statistics/leaderboard/2026/2
		/// </summary>
		[HttpGet("leaderboard/{year}/{month}")]
		public async Task<IActionResult> GetMonthlyLeaderboard(int year, int month)
		{
			try
			{
				if (month < 1 || month > 12)
				{
					return BadRequest(new { error = "月份必須在 1-12 之間" });
				}

				var leaderboard = await _statisticsService
					.GetMonthlyLeaderboardAsync(year, month);

				return Ok(new
				{
					success = true,
					data = leaderboard,
					message = $"成功取得 {year}年{month}月排行榜"
				});
			}
			catch (Exception ex)
			{
				_logger.LogError($"取得排行榜失敗: {ex.Message}");
				return StatusCode(500, new
				{
					success = false,
					error = "取得排行榜失敗"
				});
			}
		}

		/// <summary>
		/// 取得年度統計
		/// GET /api/statistics/yearly-stats/2026
		/// </summary>
		[HttpGet("yearly-stats/{year}")]
		public async Task<IActionResult> GetYearlyStats(int year)
		{
			try
			{
				var stats = await _statisticsService.GetYearlyStatsAsync(year);

				return Ok(new
				{
					success = true,
					data = stats,
					message = $"成功取得 {year} 年統計資料"
				});
			}
			catch (Exception ex)
			{
				_logger.LogError($"取得年度統計失敗: {ex.Message}");
				return StatusCode(500, new
				{
					success = false,
					error = "取得統計失敗"
				});
			}
		}

		/// <summary>
		/// 重新計算統計 (管理員用)
		/// POST /api/statistics/recalculate/2026/2
		/// </summary>
		[HttpPost("recalculate/{year}/{month}")]
		[Authorize(Roles = "Manager")]
		public async Task<IActionResult> RecalculateStatistics(int year, int month)
		{
			try
			{
				if (month < 1 || month > 12)
				{
					return BadRequest(new { error = "月份必須在 1-12 之間" });
				}

				var result = await _statisticsService
					.RecalculateStatisticsAsync(year, month);

				if (result)
				{
					return Ok(new
					{
						success = true,
						message = $"已重新計算 {year}年{month}月統計"
					});
				}
				else
				{
					return StatusCode(500, new
					{
						success = false,
						error = "重新計算統計失敗"
					});
				}
			}
			catch (Exception ex)
			{
				_logger.LogError($"重新計算統計失敗: {ex.Message}");
				return StatusCode(500, new
				{
					success = false,
					error = ex.Message
				});
			}
		}

		/// <summary>
		/// 取得單一員工該月統計
		/// GET /api/statistics/employee/1/monthly/2026/2
		/// </summary>
		[HttpGet("employee/{userId}/monthly/{year}/{month}")]
		public async Task<IActionResult> GetEmployeeMonthlyStats(int userId, int year, int month)
		{
			try
			{
				var count = await _statisticsService
					.GetEmployeeMonthlyShiftCountAsync(userId, year, month);

				return Ok(new
				{
					success = true,
					data = new
					{
						UserId = userId,
						Year = year,
						Month = month,
						ShiftDays = count,
						IsCompliant = count >= 6 && count <= 15,
						MinRequired = 6,
						MaxAllowed = 15
					}
				});
			}
			catch (Exception ex)
			{
				_logger.LogError($"取得員工月度統計失敗: {ex.Message}");
				return StatusCode(500, new
				{
					success = false,
					error = "取得統計失敗"
				});
			}
		}

		/// <summary>
		/// 取得單一員工該年統計
		/// GET /api/statistics/employee/1/yearly/2026
		/// </summary>
		[HttpGet("employee/{userId}/yearly/{year}")]
		public async Task<IActionResult> GetEmployeeYearlyStats(int userId, int year)
		{
			try
			{
				var count = await _statisticsService
					.GetEmployeeYearlyShiftCountAsync(userId, year);

				return Ok(new
				{
					success = true,
					data = new
					{
						UserId = userId,
						Year = year,
						TotalShiftDays = count,
						AverageMonthlyShifts = Math.Round((decimal)count / 12, 2)
					}
				});
			}
			catch (Exception ex)
			{
				_logger.LogError($"取得員工年度統計失敗: {ex.Message}");
				return StatusCode(500, new
				{
					success = false,
					error = "取得統計失敗"
				});
			}
		}
	}
}
