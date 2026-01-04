namespace ShiftManagementSystem.Models.DTOs
{
	/// <summary>
	/// 月度班表DTO (老闆查看)
	/// </summary>
	public class MonthlyScheduleDto
	{
		public int Year { get; set; }
		public int Month { get; set; }
		public List<EmployeeDto> Employees { get; set; } = new();
		public List<DayScheduleDto> DaySchedules { get; set; } = new();
		public int TotalDays { get; set; }
	}
}
