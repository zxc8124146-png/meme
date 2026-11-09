using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

public class EarningsBackfillTests
{
	private static string RepoFile(string relative)
	{
		DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null)
		{
			string candidate = Path.Combine(dir.FullName, relative);
			if (File.Exists(candidate))
			{
				return candidate;
			}
			dir = dir.Parent;
		}
		throw new FileNotFoundException(relative);
	}

	private static string TempRoot()
	{
		string path = Path.Combine(Path.GetTempPath(), "mt4-backfill-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}

	private static HistoryExport SampleExport()
	{
		return EarningsStore.ReadExport(RepoFile("app/src/MT4Widget.Tests/Fixtures/synthetic-export.csv"));
	}

	[Fact]
	public void FeesSwapAndDeposits_UseClosedMarketNetOnly()
	{
		HistoryExport export = SampleExport();
		var nets = EarningsStore.SumMarketNets(export.Deals);
		Assert.Equal("8.00", EarningsStore.FormatMoney(nets["2026.11.01"]));
		Assert.Equal("-5.00", EarningsStore.FormatMoney(nets["2026.11.03"]));
		Assert.Equal("1.80", EarningsStore.FormatMoney(nets["2026.11.05"]));
		Assert.False(nets.ContainsKey("2026.11.04"));
		Assert.Contains(export.Deals, d => d.Symbol == "XAUUSD");
		Assert.Contains(export.Deals, d => d.Symbol == "EURUSD");
	}

	[Fact]
	public void StartupBackfill_FillsGapsAndKnownZeros_WithoutInventingEquity()
	{
		string root = TempRoot();
		try
		{
			EarningsStore store = new EarningsStore(root);
			BackfillResult result = store.Apply("90000001", SampleExport());
			Assert.True(result.Wrote);
			Assert.Equal(5, result.Inserted);
			var rows = store.Load(store.AccountPath("90000001"));
			Assert.Equal(new[] { "2026.11.01", "2026.11.02", "2026.11.03", "2026.11.04", "2026.11.05" }, rows.Keys.ToArray());
			Assert.Equal("0.00", EarningsStore.FormatMoney(rows["2026.11.02"][2]));
			Assert.Equal("0.00", EarningsStore.FormatMoney(rows["2026.11.04"][2]));
			Assert.True(double.IsNaN(rows["2026.11.02"][0]));
			Assert.True(double.IsNaN(rows["2026.11.02"][1]));
			Assert.DoesNotContain(Directory.GetFiles(root), f => Path.GetFileName(f).StartsWith("equity_", StringComparison.Ordinal));
			string text = File.ReadAllText(store.AccountPath("90000001"));
			Assert.Contains("2026.11.02,,,0.00", text.Replace("\r", ""));
			Assert.DoesNotContain("2026.10.31", text);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void RepeatedStartup_IsIdempotent()
	{
		string root = TempRoot();
		try
		{
			EarningsStore store = new EarningsStore(root);
			HistoryExport export = SampleExport();
			store.Apply("90000001", export);
			string before = File.ReadAllText(store.AccountPath("90000001"));
			int backups = Directory.Exists(Path.Combine(root, "backups")) ? Directory.GetFiles(Path.Combine(root, "backups")).Length : 0;
			BackfillResult again = store.Apply("90000001", export);
			Assert.False(again.Wrote);
			Assert.Equal(0, again.Inserted);
			Assert.Equal(before, File.ReadAllText(store.AccountPath("90000001")));
			int backupsAfter = Directory.GetFiles(Path.Combine(root, "backups")).Length;
			Assert.Equal(backups, backupsAfter);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void ExistingLiveRows_ArePreservedAndBackedUp()
	{
		string root = TempRoot();
		try
		{
			File.Copy(RepoFile("app/src/MT4Widget.Tests/Fixtures/synthetic-legacy-history.csv"), Path.Combine(root, "history.csv"));
			EarningsStore store = new EarningsStore(root);
			BackfillResult result = store.Apply("90000001", SampleExport());
			Assert.True(result.Wrote);
			string legacy = File.ReadAllText(Path.Combine(root, "history.csv"));
			Assert.Equal(File.ReadAllText(RepoFile("app/src/MT4Widget.Tests/Fixtures/synthetic-legacy-history.csv")), legacy);
			Assert.True(File.Exists(Path.Combine(root, EarningsStore.LegacyBackupName)));
			var rows = store.Load(store.AccountPath("90000001"));
			Assert.Equal("111.00", EarningsStore.FormatMoney(rows["2026.11.03"][0]));
			Assert.Equal("100.00", EarningsStore.FormatMoney(rows["2026.11.03"][1]));
			Assert.Equal("1.00", EarningsStore.FormatMoney(rows["2026.11.03"][2]));
			Assert.Equal("4.50", EarningsStore.FormatMoney(rows["2026.10.15"][2]));
			Assert.True(rows.ContainsKey("2026.11.02"));
			Assert.True(Directory.GetFiles(Path.Combine(root, "backups")).Length >= 1);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void AccountChange_DoesNotMixLedgers()
	{
		string root = TempRoot();
		try
		{
			File.WriteAllText(Path.Combine(root, "history.csv"), "date,closingEquity,balance,realizedPL\n2026.11.03,111.00,100.00,1.00\n");
			EarningsStore store = new EarningsStore(root);
			store.Apply("90000001", SampleExport());
			HistoryExport other = SampleExport();
			other.Account = "90000002";
			other.Deals.Clear();
			other.Deals.Add(new HistoryDeal('B', "2026.11.02", 3, 0, 0, "XAUUSD"));
			BackfillResult second = store.Apply("90000002", other);
			Assert.Equal(5, second.Inserted);
			var a = store.Load(store.AccountPath("90000001"));
			var b = store.Load(store.AccountPath("90000002"));
			Assert.True(a.ContainsKey("2026.11.03"));
			Assert.Equal("1.00", EarningsStore.FormatMoney(a["2026.11.03"][2]));
			Assert.False(b.ContainsKey("2026.10.15"));
			Assert.Equal("3.00", EarningsStore.FormatMoney(b["2026.11.02"][2]));
			Assert.Equal("90000001", File.ReadAllText(store.AdoptedMarkerPath).Trim());
			BackfillResult mismatch = store.ApplyExportFile("90000001", WriteExport(root, other));
			Assert.True(mismatch.AccountMismatch);
			Assert.False(mismatch.Wrote);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void IncompleteHistory_DoesNotMaterializeZeros()
	{
		string root = TempRoot();
		try
		{
			EarningsStore store = new EarningsStore(root);
			HistoryExport empty = new HistoryExport
			{
				Account = "90000001",
				Covered = false,
				Loaded = 0
			};
			BackfillResult result = store.Apply("90000001", empty);
			Assert.False(result.Wrote);
			Assert.False(result.Covered);
			Assert.Contains("不是 0", result.Note);
			Assert.Contains("全部歷史", result.Note);
			Assert.False(File.Exists(store.AccountPath("90000001")));
			string bad = Path.Combine(root, "bad.csv");
			File.WriteAllText(bad, "nope\naccount,90000001\ncovered,1\nB,2026.11.01,5,0,0,XAUUSD\n");
			BackfillResult unread = store.ApplyExportFile("90000001", bad);
			Assert.False(unread.Wrote);
			Assert.False(File.Exists(store.AccountPath("90000001")));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void BackfillOriginRows_Refresh_LiveRows_DoNot()
	{
		string root = TempRoot();
		try
		{
			EarningsStore store = new EarningsStore(root);
			store.Apply("90000001", SampleExport());
			store.UpsertLiveDay("90000001", "2026.11.05", 250.5, 240.25, 1.8);
			HistoryExport changed = SampleExport();
			changed.Deals.RemoveAll(d => d.BrokerDate == "2026.11.05");
			changed.Deals.Add(new HistoryDeal('B', "2026.11.05", 9, -1, -0.5, "XAUUSD"));
			changed.Deals.Add(new HistoryDeal('S', "2026.11.02", 4, -0.25, -0.25, "EURUSD"));
			BackfillResult result = store.Apply("90000001", changed);
			Assert.True(result.Wrote);
			Assert.Equal(0, result.Inserted);
			var rows = store.Load(store.AccountPath("90000001"));
			Assert.Equal("250.50", EarningsStore.FormatMoney(rows["2026.11.05"][0]));
			Assert.Equal("1.80", EarningsStore.FormatMoney(rows["2026.11.05"][2]));
			Assert.Equal("3.50", EarningsStore.FormatMoney(rows["2026.11.02"][2]));
			Assert.True(double.IsNaN(rows["2026.11.02"][0]));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Recorder_StaysReadOnly_AndExplainsHistoryLimit()
	{
		string source = File.ReadAllText(RepoFile("app/MT4Recorder.mq4"));
		Assert.DoesNotContain(Regex.Matches(source, @"\bOrder(Send|Modify|Delete|Close|CloseBy)\b").Cast<Match>(), m => true);
		Assert.Contains("全部歷史", source);
		Assert.Contains("OrderProfit() + OrderSwap() + OrderCommission()", source);
		Assert.Contains("OP_BALANCE", source);
		Assert.Contains("mt4realized.csv", source);
		Assert.DoesNotContain("password", source, StringComparison.OrdinalIgnoreCase);
	}

	private static string WriteExport(string root, HistoryExport export)
	{
		string path = Path.Combine(root, "other-export.csv");
		using (StreamWriter writer = new StreamWriter(path))
		{
			writer.WriteLine("v1");
			writer.WriteLine("account," + export.Account);
			writer.WriteLine("from," + export.From);
			writer.WriteLine("to," + export.To);
			writer.WriteLine("loaded," + export.Loaded.ToString(CultureInfo.InvariantCulture));
			writer.WriteLine("covered," + (export.Covered ? "1" : "0"));
			foreach (HistoryDeal deal in export.Deals)
			{
				writer.WriteLine(string.Join(",", deal.Kind.ToString(), deal.BrokerDate, EarningsStore.FormatMoney(deal.Profit), EarningsStore.FormatMoney(deal.Swap), EarningsStore.FormatMoney(deal.Commission), deal.Symbol));
			}
		}
		return path;
	}
}
