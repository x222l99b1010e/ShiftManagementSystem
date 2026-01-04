using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore; // 引用
using ShiftManagementSystem.Models;   // 引用 Models
using ShiftManagementSystem.Services;

namespace ShiftManagementSystem
{
	public class Program
	{
		// 1: 改為 async Task
		public static async Task Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			// 2: 必須註冊 DbContext (請確保連線字串名稱與 appsettings.json 一致)
			var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
			builder.Services.AddDbContext<ScheduleDBContext>(options =>
				options.UseSqlServer(connectionString));

			// Add services to the container.
			builder.Services.AddControllersWithViews();

			//builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
			//	.AddNegotiate();
			builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
				.AddCookie(options =>
				{
					options.LoginPath = "/Account/Login"; // 登入頁面路徑
					options.AccessDeniedPath = "/Account/AccessDenied";
					options.ExpireTimeSpan = TimeSpan.FromHours(1); // 登入效期
				});
			// 2. 註冊 PasswordHasher 供 Controller 使用
			builder.Services.AddScoped<IPasswordHasher<string>, PasswordHasher<string>>();

			// 1. 註冊 HttpClient
			builder.Services.AddHttpClient();

			// 2. 註冊服務
			builder.Services.AddScoped<IHolidayService, HolidayService>();
			builder.Services.AddScoped<IShiftService, ShiftService>();
			// 註冊 StatisticsService
			builder.Services.AddScoped<IStatisticsService, StatisticsService>();
			// 註冊轉換器，讓系統把 DB Role 變成 Claims
			//builder.Services.AddScoped<IClaimsTransformation, ClaimsTransformer>();

			builder.Services.AddAuthorization(options =>
			{
				options.FallbackPolicy = options.DefaultPolicy;
			});
			builder.Services.AddRazorPages();

			var app = builder.Build();

			// Configure the HTTP request pipeline.
			if (!app.Environment.IsDevelopment())
			{
				app.UseExceptionHandler("/Home/Error");
				app.UseHsts();
			}

			app.UseHttpsRedirection();
			app.UseStaticFiles();
			app.UseRouting();

			// 必須加這一行才能啟用 Authentication 中介軟體
			app.UseAuthentication();// 必須在 Authorization 之前

			app.UseAuthorization();


			app.MapStaticAssets();
			app.MapControllerRoute(
				name: "default",
				pattern: "{controller=Home}/{action=Index}/{id?}")
				.WithStaticAssets();

			// 3: 初始化假日快取 (現在可以使用 await 了)
			// 在 app.Run() 之前
			using (var scope = app.Services.CreateScope())
			{
				var holidayService = scope.ServiceProvider.GetRequiredService<IHolidayService>();
				try
				{
					// 建議同時初始化今年與明年，確保換年時不會出錯
					int currentYear = DateTime.Now.Year; // 2026
					await holidayService.InitializeHolidaysCacheAsync(currentYear);

					// 只有在 12 月時才嘗試預抓隔年，且 API 若回傳 404 (我們在 Service 已處理) 也不會崩潰
					if (DateTime.Now.Month == 12)
					{
						await holidayService.InitializeHolidaysCacheAsync(currentYear + 1);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"啟動時初始化假日失敗: {ex.Message}");
				}
			}

			// 4: 改為 RunAsync
			await app.RunAsync();
		}
	}
}