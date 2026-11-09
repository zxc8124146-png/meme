using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms;

internal static class Econ
{
	private const string Url = "https://nfs.faireconomy.media/ff_calendar_thisweek.json";

	private static readonly string CachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MT4Widget", "ff_calendar_cache.json");

	public static List<EconEvent> Events = new List<EconEvent>();

	public static DateTime Updated = DateTime.MinValue;

	public static bool LastError;

	private static bool busy;

	private static readonly HttpClient http = CreateHttp();

	private static readonly (string key, string zh)[] Map = new(string, string)[55]
	{
		("ADP Weekly", "ADP週就業"),
		("ADP Non-Farm", "ADP小非農"),
		("Non-Farm Employment Change", "非農就業人數"),
		("Unemployment Rate", "失業率"),
		("Average Hourly Earnings", "平均時薪"),
		("Unemployment Claims", "初請失業金"),
		("Continuing Claims", "續請失業金"),
		("Core CPI", "核心CPI"),
		("CPI", "CPI 消費者物價"),
		("Core PPI", "核心PPI"),
		("PPI", "PPI 生產者物價"),
		("Core PCE", "核心PCE物價"),
		("PCE Price", "PCE物價"),
		("Federal Funds Rate", "FOMC利率決議"),
		("FOMC Statement", "FOMC聲明"),
		("FOMC Press Conference", "FOMC記者會"),
		("FOMC Meeting Minutes", "FOMC會議紀要"),
		("Fed Chair", "聯準會主席談話"),
		("Advance GDP Price", "GDP物價指數初值"),
		("Advance GDP", "GDP初值"),
		("Prelim GDP", "GDP修正值"),
		("Final GDP", "GDP終值"),
		("GDP", "GDP"),
		("ISM Manufacturing PMI", "ISM製造業PMI"),
		("ISM Services PMI", "ISM服務業PMI"),
		("Flash Manufacturing PMI", "製造業PMI初值"),
		("Flash Services PMI", "服務業PMI初值"),
		("Chicago PMI", "芝加哥PMI"),
		("PMI", "PMI"),
		("Core Retail Sales", "核心零售銷售"),
		("Retail Sales", "零售銷售"),
		("Core Durable Goods", "核心耐久財訂單"),
		("Durable Goods", "耐久財訂單"),
		("JOLTS", "JOLTS職缺"),
		("CB Consumer Confidence", "諮商會消費者信心"),
		("UoM Consumer Sentiment", "密大消費者信心"),
		("UoM Inflation Expectations", "密大通膨預期"),
		("Employment Cost Index", "就業成本指數"),
		("Personal Spending", "個人支出"),
		("Personal Income", "個人收入"),
		("Pending Home Sales", "成屋待售"),
		("Existing Home Sales", "成屋銷售"),
		("New Home Sales", "新屋銷售"),
		("Building Permits", "營建許可"),
		("Housing Starts", "新屋開工"),
		("Crude Oil Inventories", "EIA原油庫存"),
		("Natural Gas Storage", "天然氣庫存"),
		("Trade Balance", "貿易收支"),
		("Industrial Production", "工業生產"),
		("Empire State", "紐約州製造業"),
		("Philly Fed", "費城聯儲製造業"),
		("Treasury Currency Report", "財政部匯率報告"),
		("Bond Auction", "公債標售"),
		("Bank Holiday", "銀行休假"),
		("Speaks", "官員談話")
	};

	private static HttpClient CreateHttp()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromSeconds(20.0);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 MT4Widget/1.0");
		return httpClient;
	}

	public static string Zh(string t)
	{
		(string, string)[] map = Map;
		for (int i = 0; i < map.Length; i++)
		{
			var (value, text) = map[i];
			if (t.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return text + (t.Contains("m/m") ? " 月率" : (t.Contains("y/y") ? " 年率" : (t.Contains("q/q") ? " 季率" : "")));
			}
		}
		return t;
	}

	private static List<EconEvent> Parse(string json)
	{
		List<EconEvent> list = new List<EconEvent>();
		using JsonDocument jsonDocument = JsonDocument.Parse(json);
		if (jsonDocument.RootElement.ValueKind != JsonValueKind.Array)
		{
			throw new FormatException("not array");
		}
		foreach (JsonElement item in jsonDocument.RootElement.EnumerateArray())
		{
			JsonElement e = item;
			if (!(Get("country") != "USD") && DateTimeOffset.TryParse(Get("date"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
			{
				string text = Get("impact");
				EconEvent econEvent = new EconEvent
				{
					T = result,
					Title = Get("title"),
					Forecast = Get("forecast"),
					Previous = Get("previous"),
					Actual = Get("actual"),
					Stars = ((text == "High") ? 3 : ((!(text == "Medium")) ? 1 : 2)),
					AllDay = (result.Hour == 0 && result.Minute == 0 && text == "Holiday")
				};
				econEvent.Zh = Zh(econEvent.Title);
				list.Add(econEvent);
			}
			string Get(string k)
			{
				if (!e.TryGetProperty(k, out var value) || value.ValueKind != JsonValueKind.String)
				{
					return "";
				}
				return value.GetString() ?? "";
			}
		}
		return list;
	}

	public static void LoadCache()
	{
		try
		{
			if (File.Exists(CachePath))
			{
				Events = Parse(File.ReadAllText(CachePath));
				Updated = File.GetLastWriteTime(CachePath);
			}
		}
		catch (Exception e)
		{
			Widget.Log(e);
		}
	}

	public static async void Refresh(Control ui)
	{
		if (busy)
		{
			return;
		}
		busy = true;
		List<EconEvent> ev;
		try
		{
			string text = await http.GetStringAsync("https://nfs.faireconomy.media/ff_calendar_thisweek.json").ConfigureAwait(continueOnCapturedContext: false);
			ev = Parse(text);
			if (ev.Count == 0)
			{
				throw new FormatException("no USD events");
			}
			Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
			string text2 = CachePath + ".tmp";
			File.WriteAllText(text2, text);
			File.Move(text2, CachePath, overwrite: true);
			if (ui.IsHandleCreated && !ui.IsDisposed)
			{
				ui.BeginInvoke((Action)Apply);
				return;
			}
			Events = ev;
			Updated = DateTime.Now;
			LastError = false;
		}
		catch (Exception e)
		{
			Widget.Log(e);
			LastError = true;
		}
		finally
		{
			busy = false;
		}
		void Apply()
		{
			Events = ev;
			Updated = DateTime.Now;
			LastError = false;
			ui.Invalidate();
		}
	}
}
