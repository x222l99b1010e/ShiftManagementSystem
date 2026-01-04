namespace ShiftManagementSystem.Models.DTOs
{
	/// <summary>
	/// 每日班表信息
	/// </summary>
	public class DayScheduleDto
	{
		public DateTime Date { get; set; }
		public int DayOfMonth { get; set; }
		public int DayOfWeek { get; set; }  // 0=日, 1=一, ..., 6=六
		public bool IsWeekend { get; set; }
		public bool IsHoliday { get; set; }
		public string HolidayName { get; set; }
		public List<ShiftedEmployeeDto> ShiftedEmployees { get; set; } = new();
		public int CurrentShiftCount { get; set; }  // 該日已排班人數
	}
}
