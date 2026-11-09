public class Cfg
{
	public int X { get; set; } = int.MinValue;


	public int Y { get; set; } = int.MinValue;


	public int W { get; set; }

	public int H { get; set; }

	public bool Compact { get; set; }

	public bool Pin { get; set; } = true;


	public double Opacity { get; set; } = 0.96;


	public string JsonPath { get; set; } = "";


	public int V { get; set; }

	public int Tab { get; set; }

	public bool CalHigh { get; set; }

	public string ChartTf { get; set; } = "M15";


	public int HistMode { get; set; }
}
