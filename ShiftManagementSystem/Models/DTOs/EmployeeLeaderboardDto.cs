namespace ShiftManagementSystem.Models.DTOs
{
	/// <summary>
	/// 員工排行榜DTO (月度排名)
	/// </summary>
	public class EmployeeLeaderboardDto
	{
		public int Rank { get; set; }
		public int UserId { get; set; }
		public string FullName { get; set; }
		public int ShiftDays { get; set; }
		public bool IsCompliant { get; set; }  // 是否符合 6-15 天規範
		public decimal CompliancePercentage { get; set; }  // 進度百分比
	}
}
