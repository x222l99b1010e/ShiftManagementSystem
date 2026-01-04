using System.Security.Claims;

namespace ShiftManagementSystem.Extensions
{
	public static class ClaimsPrincipalExtensions
	{
		/// <summary>
		/// 從 Claims 中取得目前登入使用者的資料庫 UserId
		/// </summary>
		public static int GetUserId(this ClaimsPrincipal user)
		{
			if (user == null) return 0;
			// 尋找我們在登入時塞進去的 "DbUserId" 標籤
			var claim = user.FindFirst("DbUserId");

			if (claim != null && int.TryParse(claim.Value, out int userId))
			{
				return userId;
			}

			return 0; // 若找不到或解析失敗回傳 0
		}

		/// <summary>
		/// 取得目前使用者的全名 (選配)
		/// </summary>
		public static string GetFullName(this ClaimsPrincipal user)
		{
			return user.FindFirst("FullName")?.Value ?? string.Empty;
		}
	}
}
