using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using ShiftManagementSystem.Models;
using System.Security.Claims;

namespace ShiftManagementSystem.Services
{
	public class ClaimsTransformer : IClaimsTransformation
	{
		private readonly IServiceProvider _serviceProvider;

		public ClaimsTransformer(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
		}

		public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
		{
			// 1. 如果已經有 DbUserId，表示已經轉換過，直接回傳
			if (principal.HasClaim(c => c.Type == "DbUserId")) return principal;

			var identity = (ClaimsIdentity)principal.Identity;
			var username = identity.Name?.Split('\\').Last();
			if (username == null) return principal;

			using (var scope = _serviceProvider.CreateScope())
			{
				var context = scope.ServiceProvider.GetRequiredService<ScheduleDBContext>();

				// 2. 優化：使用 Select 只抓 UserId 和 Role，不抓整個 User 實體
				var user = await context.Users
					.AsNoTracking() // 唯讀查詢，提升效 theming
					.Where(u => u.Username == username && u.IsActive == true)
					.Select(u => new { u.UserId, u.Role })
					.FirstOrDefaultAsync();

				// ClaimsTransformer.cs 建議檢查處
				if (user != null)
				{
					// 確保這裡的 user.Role 字串與 [Authorize(Roles = "Manager")] 完全一致 (注意大小寫)
					var roleClaim = new Claim(ClaimTypes.Role, user.Role);
					identity.AddClaim(roleClaim);
					identity.AddClaim(new Claim("DbUserId", user.UserId.ToString()));
				}
			}

			return principal;
		}
	}
}
