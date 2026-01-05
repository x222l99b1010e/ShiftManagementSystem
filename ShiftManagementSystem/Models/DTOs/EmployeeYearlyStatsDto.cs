namespace ShiftManagementSystem.Models.DTOs
{
	/// <summary>
	/// 員工年度統計DTO
	/// </summary>
	public class EmployeeYearlyStatsDto
	{
		public int UserId { get; set; }
		public string FullName { get; set; }
		public int TotalYearlyShifts { get; set; }
		public decimal AverageMonthlyShifts { get; set; }
		public Dictionary<int, int> MonthlyBreakdown { get; set; } = new();  // 月份 -> 天數
		public decimal AverageCompliancePercentage { get; set; }
	}
}
