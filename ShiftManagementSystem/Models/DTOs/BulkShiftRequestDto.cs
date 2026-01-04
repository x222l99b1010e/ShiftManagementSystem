namespace ShiftManagementSystem.Models.DTOs
{
	public class BulkShiftRequest
	{
		public int Year { get; set; }
		public int Month { get; set; }
		public List<DateTime> ShiftDates { get; set; }
	}
}
