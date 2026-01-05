using Microsoft.EntityFrameworkCore;
using ShiftManagementSystem.Models;
using ShiftManagementSystem.Models.DTOs;
using System.Text.Json;

namespace ShiftManagementSystem.Services
{
	

	public class HolidayService : IHolidayService
	{
		private readonly ScheduleDBContext _context;
		private readonly HttpClient _httpClient;
		private readonly ILogger<HolidayService> _logger;

		public HolidayService(
			ScheduleDBContext context,
			HttpClient httpClient,
			ILogger<HolidayService> logger)
		{
			_context = context;
			_httpClient = httpClient;
			_logger = logger;
		}
		/// <summary>
		/// 檢查是否為假日 (包括週六日)
		/// </summary>
		public async Task<bool> IsHolidayAsync(DateTime date)
		{
			var targetDate = DateOnly.FromDateTime(date);

			// 1. 先查快取資料庫 (包含國定假日與補班資訊)
			var cache = await _context.HolidayCaches.FirstOrDefaultAsync(h => h.HolidayDate == targetDate);
			if (cache != null)
			{
				return cache.IsOfficialHoliday ?? false;
			}

			// 2. 如果資料庫沒資料（例如還沒初始化），才保險起見判斷六日
			return (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday);
		}

		/// <summary>
		/// 取得月份所有假日
		/// </summary>
		public async Task<List<HolidayCache>> GetMonthHolidaysAsync(int year, int month)
		{
			// 確保該年假日已被快取
			await InitializeHolidaysCacheAsync(year);

			var holidays = await _context.HolidayCaches
				.Where(h => h.CacheYear == year && h.HolidayDate.Month == month)
				.ToListAsync();

			return holidays;
		}

		/// <summary>
		/// 初始化年份快取 (重點!)
		/// </summary>
		public async Task InitializeHolidaysCacheAsync(int year)
		{
			var existing = await _context.HolidayCaches
				.Where(h => h.CacheYear == year)
				.CountAsync();

			if (existing > 0) return;

			try
			{
				_logger.LogInformation($"開始從新 API 取得 {year} 年資料...");

				// 修改處：呼叫新的抓取方法
				var holidays = await FetchFromTaiwanCalendarApiAsync(year);

				if (holidays.Any())
				{
					_context.HolidayCaches.AddRange(holidays);
					await _context.SaveChangesAsync();
				}
			}
			catch (Exception ex)
			{
				_logger.LogError($"取得假日失敗: {ex.Message}");
			}
		}

		/// <summary>
		/// 呼叫TaiwanCalendarApi
		/// </summary>
		private async Task<List<HolidayCache>> FetchFromTaiwanCalendarApiAsync(int year)
		{
			// 1. 確保網址正確
			var apiUrl = $"https://allen0099.github.io/taiwan-calendar/{year}/all.json";

			try
			{
				_logger.LogInformation($"正在發起 API 請求: {apiUrl}");

				// 2. 必須設定 User-Agent，否則 GitHub 可能回傳 403 或 404
				using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
				request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) dotnet-client/1.0");

				var response = await _httpClient.SendAsync(request);

				if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
				{
					_logger.LogWarning($"{year} 年的假日 API 尚未準備好 (404)。");
					return new List<HolidayCache>();
				}

				response.EnsureSuccessStatusCode();
				var jsonContent = await response.Content.ReadAsStringAsync();

				// 3. 解析新的 JSON 格式
				var root = JsonSerializer.Deserialize<TaiwanCalendarRoot>(jsonContent, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

				var holidayCaches = new List<HolidayCache>();

				if (root?.months != null)
				{
					foreach (var month in root.months)
					{
						// 確保 month.holidays 不為空
						if (month.holidays == null) continue;

						foreach (var h in month.holidays)
						{
							// 核心判斷：只抓 API 標註為休假 (isHoliday: true) 的日子
							if (h.isHoliday)
							{
								// 處理格式 "20260101"
								if (DateOnly.TryParseExact(h.date, "yyyyMMdd", out var parsedDate))
								{
									holidayCaches.Add(new HolidayCache
									{
										HolidayDate = parsedDate,
										HolidayName = string.IsNullOrEmpty(h.name) ? "假日" : h.name,
										// 判斷分類
										HolidayCategory = h.isSpecialHoliday ? "National" : "Weekend",
										Description = h.description,
										IsOfficialHoliday = true,
										CacheYear = year,
										LastUpdatedFromApi = DateTime.Now
									});
								}
							}
						}
					}
				}
				return holidayCaches;
			}
			catch (Exception ex)
			{
				_logger.LogError($"抓取 {year} 假日發生錯誤: {ex.Message}");
				return new List<HolidayCache>();
			}
		}

		public async Task<string> GetHolidayNameAsync(DateTime date)
		{
			if (date.DayOfWeek == DayOfWeek.Saturday)
				return "星期六";
			if (date.DayOfWeek == DayOfWeek.Sunday)
				return "星期日";

			// 修正：轉換為 DateOnly 進行查詢
			var targetDate = DateOnly.FromDateTime(date);
			var holiday = await _context.HolidayCaches
				//.FirstOrDefaultAsync(h => h.HolidayDate == date.Date);
				.FirstOrDefaultAsync(h => h.HolidayDate == targetDate);

			return holiday?.HolidayName ?? "";
		}
	}
}
