namespace ShiftManagementSystem.Models.DTOs
{
	// 最外層
	public class TaiwanCalendarRoot
	{
		public int year { get; set; }
		public List<MonthData> months { get; set; }
	}

	public class MonthData
	{
		public int month { get; set; }
		public List<TaiwanHolidayDTO> holidays { get; set; }
	}

	public class TaiwanHolidayDTO
	{
		public string date { get; set; }         // 格式: "20260101" (注意: 這邊沒有槓槓)
		public string name { get; set; }         // 節日名稱
		public bool isHoliday { get; set; }      // 是否放假 (核心判斷指標)
		public bool isSpecialHoliday { get; set; }
		public string description { get; set; }
	}
}
