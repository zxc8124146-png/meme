using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

/// <summary>
/// Account-scoped daily realized ledger.
/// Realized net is closed-trade profit + swap + commission.
/// Balance/credit/pending orders are not earnings. They only prove how far
/// MT4 account history is loaded. Days outside that loaded span stay absent
/// so a short terminal history is not stored as zero.
/// Existing equity and balance cells are kept. This store never writes an
/// intraday equity curve for a day the widget did not sample.
/// </summary>
public sealed class EarningsStore
{
	public const string LegacyFileName = "history.csv";

	public const string AdoptedMarkerName = "legacy-history-adopted.txt";

	public const string LegacyBackupName = "history.legacy.original.csv.bak";

	public const string Header = "date,closingEquity,balance,realizedPL";

	public EarningsStore(string root)
	{
		if (string.IsNullOrWhiteSpace(root))
		{
			throw new ArgumentException("root required", nameof(root));
		}
		Root = root;
	}

	public string Root { get; }

	public string LegacyPath => Path.Combine(Root, LegacyFileName);

	public string AdoptedMarkerPath => Path.Combine(Root, AdoptedMarkerName);

	public static bool IsSafeAccount(string account)
	{
		if (string.IsNullOrEmpty(account) || account.Length > 20)
		{
			return false;
		}
		for (int i = 0; i < account.Length; i++)
		{
			if (account[i] < '0' || account[i] > '9')
			{
				return false;
			}
		}
		return true;
	}

	public string AccountPath(string account)
	{
		if (!IsSafeAccount(account))
		{
			throw new ArgumentException("account must be digits", nameof(account));
		}
		return Path.Combine(Root, "history_" + account + ".csv");
	}

	public static string SafeExportName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return null;
		}
		name = name.Trim();
		if (name.Length > 80 || name.IndexOfAny(new char[3] { '/', '\\', ':' }) >= 0)
		{
			return null;
		}
		if (!name.Equals(Path.GetFileName(name), StringComparison.Ordinal))
		{
			return null;
		}
		return name;
	}

	public static bool TryParseBrokerDate(string text, out DateTime date)
	{
		return DateTime.TryParseExact((text ?? "").Trim(), new string[2] { "yyyy.MM.dd", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
	}

	public static string FormatBrokerDate(DateTime date)
	{
		return date.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
	}

	public static string FormatMoney(double value)
	{
		return value.ToString("0.00", CultureInfo.InvariantCulture);
	}

	public void EnsureAccount(string account)
	{
		string path = AccountPath(account);
		if (File.Exists(path))
		{
			return;
		}
		Directory.CreateDirectory(Root);
		if (File.Exists(LegacyPath))
		{
			string adopted = File.Exists(AdoptedMarkerPath) ? File.ReadAllText(AdoptedMarkerPath).Trim() : "";
			if (adopted.Length == 0 || adopted == account)
			{
				string legacyBackup = Path.Combine(Root, LegacyBackupName);
				if (!File.Exists(legacyBackup))
				{
					File.Copy(LegacyPath, legacyBackup);
				}
				File.Copy(LegacyPath, path);
				if (adopted.Length == 0)
				{
					File.WriteAllText(AdoptedMarkerPath, account);
				}
				return;
			}
		}
		File.WriteAllText(path, Header + "\n");
	}

	public SortedDictionary<string, double[]> Load(string path)
	{
		SortedDictionary<string, double[]> rows = new SortedDictionary<string, double[]>(StringComparer.Ordinal);
		if (!File.Exists(path))
		{
			return rows;
		}
		foreach (string raw in File.ReadAllLines(path))
		{
			string line = raw.Trim();
			if (line.Length == 0 || line.StartsWith("date", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string[] parts = line.Split(',');
			if (parts.Length < 4 || !TryParseBrokerDate(parts[0], out DateTime _))
			{
				continue;
			}
			if (!TryCell(parts[3], out double realized) || double.IsNaN(realized))
			{
				continue;
			}
			rows[FormatBrokerDate(TryParseBrokerDate(parts[0], out DateTime d) ? d : default)] = new double[3]
			{
				TryCell(parts[1], out double equity) ? equity : double.NaN,
				TryCell(parts[2], out double balance) ? balance : double.NaN,
				Math.Round(realized, 2, MidpointRounding.AwayFromZero)
			};
		}
		return rows;
	}

	public void Save(string path, IEnumerable<KeyValuePair<string, double[]>> rows)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Root);
		StringBuilder builder = new StringBuilder(Header).Append('\n');
		foreach (KeyValuePair<string, double[]> row in rows)
		{
			double[] value = row.Value;
			builder.Append(row.Key).Append(',').Append(Cell(value[0])).Append(',').Append(Cell(value[1])).Append(',').Append(FormatMoney(value[2])).Append('\n');
		}
		string tmp = path + ".tmp";
		File.WriteAllText(tmp, builder.ToString());
		File.Move(tmp, path, overwrite: true);
	}

	public void UpsertLiveDay(string account, string brokerDate, double equity, double balance, double realized)
	{
		if (!TryParseBrokerDate(brokerDate, out DateTime date))
		{
			return;
		}
		EnsureAccount(account);
		string path = AccountPath(account);
		SortedDictionary<string, double[]> rows = Load(path);
		rows[FormatBrokerDate(date)] = new double[3]
		{
			equity,
			balance,
			Math.Round(realized, 2, MidpointRounding.AwayFromZero)
		};
		Save(path, rows);
	}

	public BackfillResult ApplyExportFile(string account, string exportPath)
	{
		BackfillResult result = new BackfillResult();
		if (!IsSafeAccount(account))
		{
			result.Note = BuildNote(result);
			return result;
		}
		if (string.IsNullOrWhiteSpace(exportPath) || !File.Exists(exportPath))
		{
			result.Note = BuildNote(result);
			return result;
		}
		HistoryExport export = ReadExport(exportPath);
		result.ExportSeen = true;
		result.Loaded = export.Loaded;
		if (!IsSafeAccount(export.Account))
		{
			result.Covered = false;
			result.Note = BuildNote(result);
			return result;
		}
		if (!string.Equals(export.Account, account, StringComparison.Ordinal))
		{
			result.AccountMismatch = true;
			result.Note = BuildNote(result);
			return result;
		}
		return Apply(account, export);
	}

	public BackfillResult Apply(string account, HistoryExport export)
	{
		BackfillResult result = new BackfillResult
		{
			ExportSeen = true,
			Loaded = export?.Loaded ?? 0,
			Covered = export?.Covered ?? false,
			From = export?.From ?? "",
			To = export?.To ?? ""
		};
		if (!IsSafeAccount(account) || export == null || !string.Equals(export.Account, account, StringComparison.Ordinal))
		{
			result.AccountMismatch = export != null && !string.Equals(export.Account, account, StringComparison.Ordinal);
			result.Covered = false;
			result.Note = BuildNote(result);
			return result;
		}
		if (!export.Covered || !TryParseBrokerDate(export.From, out DateTime from) || !TryParseBrokerDate(export.To, out DateTime to) || to < from)
		{
			result.Covered = false;
			result.Note = BuildNote(result);
			return result;
		}
		int span = (int)(to - from).TotalDays;
		if (span > 20000)
		{
			result.Covered = false;
			result.Note = BuildNote(result);
			return result;
		}
		Dictionary<string, double> nets = SumMarketNets(export.Deals);
		EnsureAccount(account);
		string path = AccountPath(account);
		SortedDictionary<string, double[]> rows = Load(path);
		SortedDictionary<string, double[]> merged = new SortedDictionary<string, double[]>(rows, StringComparer.Ordinal);
		int inserted = 0;
		for (int i = 0; i <= span; i++)
		{
			string key = FormatBrokerDate(from.AddDays(i));
			double net = Math.Round(nets.TryGetValue(key, out double found) ? found : 0.0, 2, MidpointRounding.AwayFromZero);
			if (!merged.TryGetValue(key, out double[] existing))
			{
				merged[key] = new double[3] { double.NaN, double.NaN, net };
				inserted++;
				continue;
			}
			bool backfilled = double.IsNaN(existing[0]) && double.IsNaN(existing[1]);
			if (backfilled && FormatMoney(existing[2]) != FormatMoney(net))
			{
				merged[key] = new double[3] { double.NaN, double.NaN, net };
			}
		}
		result.From = FormatBrokerDate(from);
		result.To = FormatBrokerDate(to);
		result.Covered = true;
		result.Inserted = inserted;
		if (!Same(rows, merged))
		{
			Backup(path, account);
			Save(path, merged);
			result.Wrote = true;
		}
		result.Note = BuildNote(result);
		return result;
	}

	public static Dictionary<string, double> SumMarketNets(IEnumerable<HistoryDeal> deals)
	{
		Dictionary<string, double> map = new Dictionary<string, double>(StringComparer.Ordinal);
		if (deals == null)
		{
			return map;
		}
		foreach (HistoryDeal deal in deals)
		{
			if (deal.Kind != 'B' && deal.Kind != 'S')
			{
				continue;
			}
			if (!TryParseBrokerDate(deal.BrokerDate, out DateTime date))
			{
				continue;
			}
			string key = FormatBrokerDate(date);
			map.TryGetValue(key, out double current);
			map[key] = current + deal.Profit + deal.Swap + deal.Commission;
		}
		return map;
	}

	public const string LimitText = "已實現淨利＝平倉盈虧＋庫存費＋手續費，不含入金與出金。MT4 只匯出「帳戶歷史」目前載入的區間；區間開始之前沒有列資料，那些空白不是 0 元。若缺更早日期，請在 MT4 帳戶歷史按右鍵選「全部歷史」，等載入完成後重新開啟本程式。程式沒開過的日子不會回推淨值曲線。";

	public static string BuildNote(BackfillResult result)
	{
		if (result == null || !result.ExportSeen)
		{
			return "尚未讀到 EA 歷史匯出。缺日不會記成 0。" + LimitText;
		}
		if (result.AccountMismatch)
		{
			return "歷史匯出的帳號與目前連線帳號不同，沒有寫入。" + LimitText;
		}
		if (!result.Covered)
		{
			return "這次帳戶歷史是空的或無法涵蓋連續區間，所以沒有把缺日記成 0。" + LimitText;
		}
		return "載入區間 " + result.From + "–" + result.To + "（" + result.Loaded.ToString(CultureInfo.InvariantCulture) + " 筆歷史，補上 " + result.Inserted.ToString(CultureInfo.InvariantCulture) + " 日）。" + LimitText;
	}

	private void Backup(string path, string account)
	{
		if (!File.Exists(path))
		{
			return;
		}
		string dir = Path.Combine(Root, "backups");
		Directory.CreateDirectory(dir);
		string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
		string dest = Path.Combine(dir, "history_" + account + ".csv.bak-" + stamp);
		int n = 0;
		while (File.Exists(dest))
		{
			n++;
			dest = Path.Combine(dir, "history_" + account + ".csv.bak-" + stamp + "-" + n.ToString(CultureInfo.InvariantCulture));
		}
		File.Copy(path, dest);
	}

	private static bool Same(SortedDictionary<string, double[]> left, SortedDictionary<string, double[]> right)
	{
		if (left.Count != right.Count)
		{
			return false;
		}
		foreach (KeyValuePair<string, double[]> item in left)
		{
			if (!right.TryGetValue(item.Key, out double[] other))
			{
				return false;
			}
			for (int i = 0; i < 3; i++)
			{
				bool nan = double.IsNaN(item.Value[i]);
				if (nan != double.IsNaN(other[i]))
				{
					return false;
				}
				if (!nan && FormatMoney(item.Value[i]) != FormatMoney(other[i]))
				{
					return false;
				}
			}
		}
		return true;
	}

	private static string Cell(double value)
	{
		if (double.IsNaN(value))
		{
			return "";
		}
		return FormatMoney(value);
	}

	private static bool TryCell(string text, out double value)
	{
		text = (text ?? "").Trim();
		if (text.Length == 0)
		{
			value = double.NaN;
			return false;
		}
		return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
	}

	public static HistoryExport ReadExport(string path)
	{
		HistoryExport export = new HistoryExport();
		string[] lines = File.ReadAllLines(path);
		if (lines.Length == 0 || lines[0].Trim() != "v1")
		{
			export.Covered = false;
			return export;
		}
		int i = 1;
		for (; i < lines.Length; i++)
		{
			string line = lines[i].Trim();
			if (line.Length == 0)
			{
				continue;
			}
			int comma = line.IndexOf(',');
			if (comma <= 0)
			{
				break;
			}
			string key = line.Substring(0, comma);
			string value = line.Substring(comma + 1).Trim();
			if (key == "account")
			{
				export.Account = value;
			}
			else if (key == "from")
			{
				export.From = value;
			}
			else if (key == "to")
			{
				export.To = value;
			}
			else if (key == "loaded" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int loaded))
			{
				export.Loaded = loaded;
			}
			else if (key == "covered")
			{
				export.Covered = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
			}
			else if (key.Length == 1)
			{
				break;
			}
		}
		for (; i < lines.Length; i++)
		{
			string line = lines[i].Trim();
			if (line.Length == 0 || line[0] == '#')
			{
				continue;
			}
			string[] parts = line.Split(',');
			if (parts.Length < 5 || parts[0].Length != 1)
			{
				continue;
			}
			if (!TryCell(parts[2], out double profit) || !TryCell(parts[3], out double swap) || !TryCell(parts[4], out double commission))
			{
				continue;
			}
			export.Deals.Add(new HistoryDeal(parts[0][0], parts[1], profit, swap, commission, parts.Length > 5 ? parts[5] : ""));
		}
		return export;
	}
}

public sealed class HistoryExport
{
	public string Account = "";

	public string From = "";

	public string To = "";

	public int Loaded;

	public bool Covered;

	public List<HistoryDeal> Deals = new List<HistoryDeal>();
}

public readonly struct HistoryDeal
{
	public HistoryDeal(char kind, string brokerDate, double profit, double swap, double commission, string symbol)
	{
		Kind = kind;
		BrokerDate = brokerDate ?? "";
		Profit = profit;
		Swap = swap;
		Commission = commission;
		Symbol = symbol ?? "";
	}

	public char Kind { get; }

	public string BrokerDate { get; }

	public double Profit { get; }

	public double Swap { get; }

	public double Commission { get; }

	public string Symbol { get; }
}

public sealed class BackfillResult
{
	public bool ExportSeen;

	public bool Covered;

	public bool AccountMismatch;

	public bool Wrote;

	public int Inserted;

	public int Loaded;

	public string From = "";

	public string To = "";

	public string Note = "";
}
