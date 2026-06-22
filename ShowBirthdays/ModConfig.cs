namespace ShowBirthdays
{
	class ModConfig
	{
		public int cycleDuration = 120;
		public string cycleType = "Always";
		public bool showIcon = false;
		internal static string[] cycleTypes = new string[] { "Always", "Hover", "Click" };
	}
}
