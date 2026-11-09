using System.Collections.Generic;

internal class Snap
{
	public string Account = "";

	public string Server = "";

	public string Currency = "";

	public string GoldSym = "";

	public string ServerTime = "";

	public double Balance;

	public double Equity;

	public double Margin;

	public double FreeMargin;

	public double TotalLots;

	public double Floating;

	public double TodayPL;

	public double Realized;

	public double Bid;

	public double Ask;

	public int GoldDigits = 2;

	public Dictionary<string, Dictionary<string, double>> Tech = new Dictionary<string, Dictionary<string, double>>();

	public List<(string sym, string type, double lots, double open, double cur, double profit)> Pos = new List<(string, string, double, double, double, double)>();

	public Dictionary<string, List<double[]>> Bars = new Dictionary<string, List<double[]>>();

	public Dictionary<string, double> Daily = new Dictionary<string, double>();

	public List<(string name, string sym, string bid, double chg)> Markets = new List<(string, string, string, double)>();

	public List<(long t, double profit, string sym, string type, double lots)> ClosedWeek = new List<(long, double, string, string, double)>();

	public List<(string sym, string type, double lots, double profit, string time)> ClosedToday = new List<(string, string, double, double, string)>();

	public string RealizedFile = "";

	public string RealizedAccount = "";

	public string RealizedStamp = "";

	public int RealizedLoaded;
}
