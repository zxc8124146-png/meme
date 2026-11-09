using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

internal class Widget : Form
{
	private record struct Cell(string T, Color C, bool Mono = true, bool Bold = false, bool Right = false, bool Glow = false, Color? Bg = null);

	private static readonly string AppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MT4Widget");

	private static readonly string CfgPath = Path.Combine(AppDir, "settings.json");

	private static readonly string DefaultJson = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MetaQuotes", "Terminal", "Common", "Files", "mt4dash.json");

	private Cfg cfg = new Cfg();

	private Snap snap;

	private bool fileOk;

	private bool stale = true;

	private bool found;

	private string jsonPath;

	private readonly Dictionary<string, double> cur = new Dictionary<string, double>();

	private readonly Dictionary<string, double> tgt = new Dictionary<string, double>();

	private readonly List<(DateTime t, double v)> ticks = new List<(DateTime, double)>();

	private readonly List<(DateTime t, double v)> eqs = new List<(DateTime, double)>();

	private DateTime lastEqSample = DateTime.MinValue;

	private double lastBid;

	private float flash;

	private Color flashCol = UI.Green;

	private float pulse;

	private float[] rain = new float[0];

	private float[] rainSpd = new float[0];

	private readonly Random rng = new Random();

	private int blinkT;

	private float glitchX;

	private Point hover = new Point(-1, -1);

	private bool draggingSlider;

	private readonly Dictionary<string, RectangleF> btns = new Dictionary<string, RectangleF>();

	private RectangleF sliderR;

	private readonly Timer frame = new Timer
	{
		Interval = 40
	};

	private readonly Timer data = new Timer
	{
		Interval = 2000
	};

	private readonly ContextMenuStrip menu = new ContextMenuStrip();

	private readonly Timer calTimer = new Timer
	{
		Interval = 1800000
	};

	private readonly Icon appIcon;

	private bool preview;

	private bool fullScreen;

	private SortedDictionary<string, double[]> histCache;

	private DateTime histAt;

	private string histNote = EarningsStore.BuildNote(null);

	private string appliedStamp = "";

	private string appliedAccount = "";

	private const string Glyphs = "01アイウエオカキクケコサシスセソ$#%&*+=<>XAU金ﾊﾐﾋｰｳｼﾅﾓﾆｻﾜﾂｵﾘ";

	private SortedDictionary<string, double[]> previewHist;

	private static readonly (string name, TimeZoneInfo tz, TimeSpan open, TimeSpan close)[] Sessions = new(string, TimeZoneInfo, TimeSpan, TimeSpan)[4]
	{
		("雪梨", Tz("AUS Eastern Standard Time", "Australia/Sydney", 10.0), new TimeSpan(7, 0, 0), new TimeSpan(16, 0, 0)),
		("東京", Tz("Tokyo Standard Time", "Asia/Tokyo", 9.0), new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0)),
		("倫敦", Tz("GMT Standard Time", "Europe/London", 0.0), new TimeSpan(8, 0, 0), new TimeSpan(16, 30, 0)),
		("紐約", Tz("Eastern Standard Time", "America/New_York", -5.0), new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0))
	};

	private float K => (float)this.DeviceDpi / 96f;

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = this.CreateParams;
			createParams.ClassStyle |= 0x20000;
			createParams.Style |= 0x20000;
			return createParams;
		}
	}

	private bool Blink => blinkT / 15 % 2 == 0;

	public static void Log(Exception e)
	{
		try
		{
			Directory.CreateDirectory(AppDir);
			File.AppendAllText(Path.Combine(AppDir, "error.log"), DateTime.Now.ToString() + " " + e?.ToString() + "\n");
		}
		catch
		{
		}
	}

	public Widget(bool previewMode = false)
	{
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Expected O, but got Unknown
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Expected O, but got Unknown
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Expected O, but got Unknown
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Expected O, but got Unknown
		//IL_02c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d0: Expected O, but got Unknown
		//IL_0325: Unknown result type (might be due to invalid IL or missing references)
		//IL_032f: Expected O, but got Unknown
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Expected O, but got Unknown
		preview = previewMode;
		try
		{
			Directory.CreateDirectory(AppDir);
		}
		catch
		{
		}
		LoadCfg();
		jsonPath = (string.IsNullOrWhiteSpace(cfg.JsonPath) ? DefaultJson : cfg.JsonPath);
		try
		{
			using Stream stream = typeof(Widget).Assembly.GetManifestResourceStream("app.ico");
			if (stream != null)
			{
				appIcon = new Icon(stream);
			}
		}
		catch
		{
		}
		if (appIcon != null)
		{
			this.Icon = appIcon;
		}
		this.Text = "MT4 黃金儀表板";
		this.AutoScaleMode = (AutoScaleMode)0;
		this.FormBorderStyle = (FormBorderStyle)0;
		this.MaximizeBox = false;
		this.BackColor = UI.Bg2;
		this.ShowInTaskbar = true;
		this.DoubleBuffered = true;
		this.SetStyle((ControlStyles)139282, true);
		this.StartPosition = (FormStartPosition)0;
		this.TopMost = cfg.Pin;
		this.Opacity = Math.Clamp(cfg.Opacity, 0.3, 1.0);
		((ToolStrip)menu).Items.Add("精簡模式 / 完整模式", (Image)null, (EventHandler)delegate
		{
			ToggleCompact();
		});
		((ToolStrip)menu).Items.Add("置頂 開/關", (Image)null, (EventHandler)delegate
		{
			this.TopMost = !this.TopMost;
			cfg.Pin = this.TopMost;
			SaveCfg();
			this.Invalidate();
		});
		((ToolStrip)menu).Items.Add("收益紀錄 (30天)", (Image)null, (EventHandler)delegate
		{
			ShowHistory();
		});
		((ToolStrip)menu).Items.Add("選擇 mt4dash.json…", (Image)null, (EventHandler)delegate
		{
			ChoosePath();
		});
		((ToolStrip)menu).Items.Add("開啟資料夾", (Image)null, (EventHandler)delegate
		{
			try
			{
				Process.Start("explorer.exe", AppDir);
			}
			catch
			{
			}
		});
		((ToolStrip)menu).Items.Add((ToolStripItem)new ToolStripSeparator());
		((ToolStrip)menu).Items.Add("結束", (Image)null, (EventHandler)delegate
		{
			this.Close();
		});
		this.ContextMenuStrip = menu;
		this.KeyPreview = true;
		((ToolStrip)menu).Items.Insert(0, (ToolStripItem)new ToolStripMenuItem("最大化 / 還原 (F11 全螢幕)", (Image)null, (EventHandler)delegate
		{
			ToggleMax();
		}));
		LoadEquityToday();
		Econ.LoadCache();
		if (!preview)
		{
			calTimer.Tick += delegate
			{
				Econ.Refresh((Control)(object)this);
			};
			calTimer.Start();
			Econ.Refresh((Control)(object)this);
		}
		if (!preview)
		{
			frame.Tick += delegate
			{
				Animate();
			};
			data.Tick += delegate
			{
				ReadData();
			};
			frame.Start();
			data.Start();
		}
	}

	protected override void OnLoad(EventArgs e)
	{
		this.OnLoad(e);
		ApplyModeSize(initial: true);
		ReadData();
	}

	private void LoadCfg()
	{
		try
		{
			if (File.Exists(CfgPath))
			{
				cfg = JsonSerializer.Deserialize<Cfg>(File.ReadAllText(CfgPath)) ?? new Cfg();
			}
		}
		catch (Exception e)
		{
			Log(e);
			cfg = new Cfg();
		}
	}

	private void SaveCfg()
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		if (preview)
		{
			return;
		}
		try
		{
			if ((int)this.WindowState == 0)
			{
				cfg.X = this.Left;
				cfg.Y = this.Top;
				if (!cfg.Compact)
				{
					cfg.W = (int)((float)this.Width / K);
					cfg.H = (int)((float)this.Height / K);
				}
			}
			cfg.Opacity = this.Opacity;
			cfg.Pin = this.TopMost;
			cfg.JsonPath = ((jsonPath == DefaultJson) ? "" : jsonPath);
			string text = CfgPath + ".tmp";
			File.WriteAllText(text, JsonSerializer.Serialize(cfg));
			File.Move(text, CfgPath, overwrite: true);
		}
		catch (Exception e)
		{
			Log(e);
		}
	}

	private void ApplyModeSize(bool initial)
	{
		float k = K;
		if (cfg.Compact)
		{
			this.MinimumSize = new Size((int)(300f * k), (int)(46f * k));
			this.Size = new Size((int)(340f * k), (int)(46f * k));
		}
		else
		{
			this.MinimumSize = new Size((int)(360f * k), (int)(560f * k));
			if (cfg.V < 3)
			{
				cfg.V = 3;
				cfg.H = 0;
				cfg.W = 0;
			}
			int num = ((cfg.W >= 360) ? cfg.W : 420);
			int num2 = ((cfg.H >= 560) ? cfg.H : 720);
			this.Size = new Size((int)((float)num * k), (int)((float)num2 * k));
		}
		if (initial)
		{
			Screen primaryScreen = Screen.PrimaryScreen;
			Rectangle rectangle = ((primaryScreen != null) ? primaryScreen.WorkingArea : new Rectangle(0, 0, 1280, 720));
			Point p = new Point(cfg.X, cfg.Y);
			bool flag = cfg.X != int.MinValue && Screen.AllScreens.Any((Screen s) => s.WorkingArea.IntersectsWith(new Rectangle(p, new Size(80, 40))));
			this.Location = (flag ? p : new Point(rectangle.Right - this.Width - 24, rectangle.Top + 24));
		}
		UpdateRegion();
	}

	private void ToggleMax(bool full = false)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Invalid comparison between Unknown and I4
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Invalid comparison between Unknown and I4
		if (cfg.Compact)
		{
			ToggleCompact();
		}
		if ((int)this.WindowState == 2 && (!full || fullScreen))
		{
			this.WindowState = (FormWindowState)0;
			fullScreen = false;
		}
		else
		{
			Screen val = Screen.FromControl((Control)(object)this);
			Rectangle rectangle = (full ? val.Bounds : val.WorkingArea);
			if ((int)this.WindowState == 2)
			{
				this.WindowState = (FormWindowState)0;
			}
			this.MaximizedBounds = new Rectangle(rectangle.X - val.Bounds.X, rectangle.Y - val.Bounds.Y, rectangle.Width, rectangle.Height);
			fullScreen = full;
			this.WindowState = (FormWindowState)2;
		}
		UpdateRegion();
		this.Invalidate();
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Invalid comparison between Unknown and I4
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Invalid comparison between Unknown and I4
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Invalid comparison between Unknown and I4
		this.OnKeyDown(e);
		if ((int)e.KeyCode == 122)
		{
			ToggleMax(full: true);
			e.Handled = true;
		}
		else if ((int)e.KeyCode == 27 && (int)this.WindowState == 2)
		{
			ToggleMax();
			e.Handled = true;
		}
	}

	private void ToggleCompact()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		if ((int)this.WindowState == 2)
		{
			this.WindowState = (FormWindowState)0;
			fullScreen = false;
		}
		SaveCfg();
		cfg.Compact = !cfg.Compact;
		ApplyModeSize(initial: false);
		SaveCfg();
		this.Invalidate();
	}

	protected override void OnResize(EventArgs e)
	{
		this.OnResize(e);
		UpdateRegion();
		this.Invalidate();
	}

	protected override void OnResizeEnd(EventArgs e)
	{
		this.OnResizeEnd(e);
		SaveCfg();
	}

	protected override void OnMove(EventArgs e)
	{
		this.OnMove(e);
	}

	protected override void OnDpiChanged(DpiChangedEventArgs e)
	{
		this.OnDpiChanged(e);
		UpdateRegion();
		this.Invalidate();
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		SaveCfg();
		this.OnFormClosing(e);
	}

	private void UpdateRegion()
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Invalid comparison between Unknown and I4
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Expected O, but got Unknown
		if (this.Width <= 0 || this.Height <= 0)
		{
			return;
		}
		Region region = this.Region;
		if ((int)this.WindowState == 2)
		{
			this.Region = null;
		}
		else
		{
			GraphicsPath val = UI.RR(new RectangleF(0f, 0f, this.Width, this.Height), 14f * K);
			try
			{
				this.Region = new Region(val);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		if (region != null)
		{
			region.Dispose();
		}
	}

	protected override void WndProc(ref Message m)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Invalid comparison between Unknown and I4
		if (m.Msg == 132)
		{
			this.WndProc(ref m);
			long num = ((IntPtr)(nint)m.LParam).ToInt64();
			Point point = this.PointToClient(new Point((short)(num & 0xFFFF), (short)((num >> 16) & 0xFFFF)));
			float k = K;
			int num2 = (int)(6f * k);
			if (!cfg.Compact && (int)this.WindowState != 2)
			{
				bool flag = point.X < num2;
				bool flag2 = point.X >= this.ClientSize.Width - num2;
				bool flag3 = point.Y < num2;
				bool flag4 = point.Y >= this.ClientSize.Height - num2;
				if (flag3 && flag)
				{
					m.Result = (IntPtr)13;
					return;
				}
				if (flag3 && flag2)
				{
					m.Result = (IntPtr)14;
					return;
				}
				if (flag4 && flag)
				{
					m.Result = (IntPtr)16;
					return;
				}
				if (flag4 && flag2)
				{
					m.Result = (IntPtr)17;
					return;
				}
				if (flag)
				{
					m.Result = (IntPtr)10;
					return;
				}
				if (flag2)
				{
					m.Result = (IntPtr)11;
					return;
				}
				if (flag3)
				{
					m.Result = (IntPtr)12;
					return;
				}
				if (flag4)
				{
					m.Result = (IntPtr)15;
					return;
				}
			}
			PointF lpnt = new PointF((float)point.X / k, (float)point.Y / k);
			if (!btns.Values.Any((RectangleF r2) => r2.Contains(lpnt)) && !RectangleF.Inflate(sliderR, 4f, 6f).Contains(lpnt) && (cfg.Compact || lpnt.Y < 58f))
			{
				m.Result = (IntPtr)2;
			}
			else
			{
				m.Result = (IntPtr)1;
			}
		}
		else if (m.Msg == 163)
		{
			if (cfg.Compact)
			{
				ToggleCompact();
			}
			else
			{
				ToggleMax();
			}
		}
		else if (m.Msg == 165)
		{
			((ToolStripDropDown)menu).Show(Cursor.Position);
		}
		else if (m.Msg == 562)
		{
			this.WndProc(ref m);
			SaveCfg();
		}
		else
		{
			this.WndProc(ref m);
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		this.OnMouseMove(e);
		hover = e.Location;
		if (draggingSlider)
		{
			SetOpacityFrom((float)e.X / K);
		}
		this.Cursor = ((btns.Values.Any((RectangleF r) => r.Contains(new PointF((float)e.X / K, (float)e.Y / K))) || sliderR.Contains(new PointF((float)e.X / K, (float)e.Y / K))) ? Cursors.Hand : Cursors.Default);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		this.OnMouseLeave(e);
		hover = new Point(-1, -1);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		this.OnMouseDown(e);
		if ((int)e.Button == 1048576)
		{
			PointF pt = new PointF((float)e.X / K, (float)e.Y / K);
			if (RectangleF.Inflate(sliderR, 4f, 6f).Contains(pt))
			{
				draggingSlider = true;
				this.Capture = true;
				SetOpacityFrom(pt.X);
			}
		}
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Invalid comparison between Unknown and I4
		this.OnMouseUp(e);
		if (draggingSlider)
		{
			draggingSlider = false;
			this.Capture = false;
			SaveCfg();
		}
		else
		{
			if ((int)e.Button != 1048576)
			{
				return;
			}
			PointF pt = new PointF((float)e.X / K, (float)e.Y / K);
			foreach (KeyValuePair<string, RectangleF> item in btns.ToList())
			{
				if (item.Value.Contains(pt))
				{
					switch (item.Key)
					{
					case "close":
						this.Close();
						break;
					case "min":
						this.WindowState = (FormWindowState)1;
						break;
					case "compact":
						ToggleCompact();
						break;
					case "max":
						ToggleMax();
						break;
					case "tfM15":
						cfg.ChartTf = "M15";
						SaveCfg();
						break;
					case "tfH1":
						cfg.ChartTf = "H1";
						SaveCfg();
						break;
					case "tfH4":
						cfg.ChartTf = "H4";
						SaveCfg();
						break;
					case "pin":
						this.TopMost = !this.TopMost;
						SaveCfg();
						break;
					case "hist":
						ShowHistory();
						break;
					case "menu":
						((ToolStripDropDown)menu).Show((Control)(object)this, e.Location);
						break;
					case "tab1":
					case "tab0":
					case "tab2":
						cfg.Tab = item.Key[3] - 48;
						SaveCfg();
						break;
					case "h0":
					case "h1":
					case "h2":
						cfg.HistMode = item.Key[1] - 48;
						SaveCfg();
						break;
					case "hstrip":
						ShowHistory();
						break;
					case "cal_all":
						cfg.CalHigh = false;
						SaveCfg();
						break;
					case "cal_high":
						cfg.CalHigh = true;
						SaveCfg();
						break;
					}
					this.Invalidate();
					break;
				}
			}
		}
	}

	private void SetOpacityFrom(float x)
	{
		if (!(sliderR.Width <= 0f))
		{
			double num = Math.Clamp((x - sliderR.X) / sliderR.Width, 0f, 1f);
			this.Opacity = 0.3 + 0.7 * num;
			this.Invalidate();
		}
	}

	private void ChoosePath()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Invalid comparison between Unknown and I4
		OpenFileDialog val = new OpenFileDialog
		{
			Filter = "mt4dash.json|mt4dash.json|JSON (*.json)|*.json",
			Title = "選擇 EA 輸出的 mt4dash.json"
		};
		try
		{
			try
			{
				string directoryName = Path.GetDirectoryName(jsonPath);
				if (Directory.Exists(directoryName))
				{
					((FileDialog)val).InitialDirectory = directoryName;
				}
			}
			catch
			{
			}
			if ((int)((CommonDialog)val).ShowDialog((IWin32Window)(object)this) == 1)
			{
				jsonPath = ((FileDialog)val).FileName;
				SaveCfg();
				ReadData();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private static double N(JsonElement e, string k)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(k, out var value) || value.ValueKind != JsonValueKind.Number)
		{
			return 0.0;
		}
		return value.GetDouble();
	}

	private static string S(JsonElement e, string k)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(k, out var value))
		{
			return "";
		}
		if (value.ValueKind != JsonValueKind.String)
		{
			if (value.ValueKind != JsonValueKind.Null)
			{
				return value.ToString();
			}
			return "";
		}
		return value.GetString() ?? "";
	}

	private static Snap Parse(string txt)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(txt);
		JsonElement rootElement = jsonDocument.RootElement;
		Snap snap = new Snap
		{
			Account = S(rootElement, "account"),
			Server = S(rootElement, "server"),
			Currency = S(rootElement, "currency"),
			ServerTime = S(rootElement, "serverTime"),
			Balance = N(rootElement, "balance"),
			Equity = N(rootElement, "equity"),
			Margin = N(rootElement, "margin"),
			FreeMargin = N(rootElement, "freeMargin"),
			TotalLots = N(rootElement, "totalLots"),
			Floating = N(rootElement, "floatingPL"),
			TodayPL = N(rootElement, "todayPL"),
			Realized = N(rootElement, "todayRealized")
		};
		if (rootElement.TryGetProperty("gold", out var value) && value.ValueKind == JsonValueKind.Object)
		{
			snap.GoldSym = S(value, "symbol");
			snap.Bid = N(value, "bid");
			snap.Ask = N(value, "ask");
			string text = S(value, "bid");
			int num = text.IndexOf('.');
			snap.GoldDigits = ((num >= 0) ? Math.Clamp(text.Length - num - 1, 0, 5) : 0);
		}
		if (rootElement.TryGetProperty("tech", out var value2) && value2.ValueKind == JsonValueKind.Object)
		{
			foreach (JsonProperty item in value2.EnumerateObject())
			{
				if (item.Value.ValueKind != JsonValueKind.Object)
				{
					continue;
				}
				Dictionary<string, double> dictionary = new Dictionary<string, double>();
				foreach (JsonProperty item2 in item.Value.EnumerateObject())
				{
					if (item2.Value.ValueKind == JsonValueKind.Number)
					{
						dictionary[item2.Name] = item2.Value.GetDouble();
					}
				}
				snap.Tech[item.Name] = dictionary;
			}
		}
		if (rootElement.TryGetProperty("positions", out var value3) && value3.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement item3 in value3.EnumerateArray())
			{
				snap.Pos.Add((S(item3, "symbol"), S(item3, "type"), N(item3, "lots"), N(item3, "openPrice"), N(item3, "currentPrice"), N(item3, "profit")));
			}
		}
		if (rootElement.TryGetProperty("bars", out var value4) && value4.ValueKind == JsonValueKind.Object)
		{
			foreach (JsonProperty item4 in value4.EnumerateObject())
			{
				if (item4.Value.ValueKind != JsonValueKind.Array)
				{
					continue;
				}
				List<double[]> list = new List<double[]>();
				foreach (JsonElement item5 in item4.Value.EnumerateArray())
				{
					if (item5.ValueKind != JsonValueKind.Array || item5.GetArrayLength() < 5)
					{
						continue;
					}
					double[] array = new double[7];
					int num2 = 0;
					foreach (JsonElement item6 in item5.EnumerateArray())
					{
						if (num2 < 7 && item6.ValueKind == JsonValueKind.Number)
						{
							array[num2] = item6.GetDouble();
						}
						num2++;
					}
					list.Add(array);
				}
				snap.Bars[item4.Name] = list;
			}
		}
		if (rootElement.TryGetProperty("daily", out var value5) && value5.ValueKind == JsonValueKind.Object)
		{
			foreach (JsonProperty item7 in value5.EnumerateObject())
			{
				if (item7.Value.ValueKind == JsonValueKind.Number)
				{
					snap.Daily[item7.Name] = item7.Value.GetDouble();
				}
			}
		}
		if (rootElement.TryGetProperty("markets", out var value6) && value6.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement item8 in value6.EnumerateArray())
			{
				snap.Markets.Add((S(item8, "name"), S(item8, "symbol"), S(item8, "bid"), N(item8, "chg")));
			}
		}
		if (rootElement.TryGetProperty("closedWeek", out var value7) && value7.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement item9 in value7.EnumerateArray())
			{
				if (item9.ValueKind == JsonValueKind.Array && item9.GetArrayLength() >= 5)
				{
					JsonElement[] array2 = item9.EnumerateArray().ToArray();
					if (array2[0].ValueKind == JsonValueKind.Number && array2[1].ValueKind == JsonValueKind.Number)
					{
						snap.ClosedWeek.Add(((long)array2[0].GetDouble(), array2[1].GetDouble(), (array2[2].ValueKind == JsonValueKind.String) ? array2[2].GetString() : "", (array2[3].ValueKind == JsonValueKind.String) ? array2[3].GetString() : "", (array2[4].ValueKind == JsonValueKind.Number) ? array2[4].GetDouble() : 0.0));
					}
				}
			}
		}
		if (rootElement.TryGetProperty("closedToday", out var value8) && value8.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement item10 in value8.EnumerateArray())
			{
				snap.ClosedToday.Add((S(item10, "symbol"), S(item10, "type"), N(item10, "lots"), N(item10, "profit"), S(item10, "closeTime")));
			}
		}
		if (rootElement.TryGetProperty("realizedExport", out var value9) && value9.ValueKind == JsonValueKind.Object)
		{
			snap.RealizedFile = S(value9, "file");
			snap.RealizedAccount = S(value9, "account");
			snap.RealizedStamp = S(value9, "stamp");
			snap.RealizedLoaded = (int)N(value9, "loaded");
		}
		return snap;
	}

	private void ReadData()
	{
		try
		{
			found = File.Exists(jsonPath);
			if (!found)
			{
				fileOk = false;
				stale = true;
				this.Invalidate();
				return;
			}
			string text;
			using (FileStream stream = new FileStream(jsonPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
			{
				using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8);
				text = streamReader.ReadToEnd();
			}
			if (!string.IsNullOrWhiteSpace(text))
			{
				Snap s = Parse(text);
				stale = (DateTime.UtcNow - File.GetLastWriteTimeUtc(jsonPath)).TotalSeconds > 10.0;
				fileOk = true;
				Ingest(s, DateTime.Now);
			}
		}
		catch (IOException)
		{
		}
		catch (Exception e)
		{
			Log(e);
		}
	}

	private void Ingest(Snap s, DateTime now)
	{
		bool first = snap == null;
		snap = s;
		T("eq", s.Equity);
		T("bal", s.Balance);
		T("pl", s.TodayPL);
		T("lots", s.TotalLots);
		T("bid", s.Bid);
		T("ml", (s.Margin > 0.0) ? (s.Equity / s.Margin * 100.0) : 0.0);
		T("fl", s.Floating);
		if (s.Bid > 0.0)
		{
			if (lastBid > 0.0 && Math.Abs(s.Bid - lastBid) > 1E-09)
			{
				flash = 1f;
				flashCol = ((s.Bid > lastBid) ? UI.Green : UI.Red);
			}
			lastBid = s.Bid;
			ticks.Add((now, s.Bid));
			ticks.RemoveAll(((DateTime t, double v) t) => (now - t.t).TotalMinutes > 5.0);
		}
		if (!stale && s.Equity > 0.0)
		{
			if (eqs.Count > 0)
			{
				List<(DateTime t, double v)> list = eqs;
				if (list[list.Count - 1].t.Date != now.Date)
				{
					eqs.Clear();
				}
			}
			if ((now - lastEqSample).TotalSeconds >= 5.0)
			{
				lastEqSample = now;
				eqs.Add((now, s.Equity));
				if (!preview)
				{
					try
					{
						File.AppendAllText(EqFile(now), now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "," + s.Equity.ToString("0.00", CultureInfo.InvariantCulture) + "\n");
					}
					catch
					{
					}
				}
			}
			if (!preview)
			{
				RecordDay(s);
			}
			if (!preview && (now - histAt).TotalSeconds > 20.0)
			{
				histCache = AccountHist();
				histAt = now;
			}
		}
		if (!preview)
		{
			TryBackfill(s);
		}
		this.Invalidate();
		void T(string k, double v)
		{
			tgt[k] = v;
			if (first || !cur.ContainsKey(k))
			{
				cur[k] = v;
			}
		}
	}

	private static string EqFile(DateTime d)
	{
		return Path.Combine(AppDir, "equity_" + d.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".csv");
	}

	private void LoadEquityToday()
	{
		try
		{
			string path = EqFile(DateTime.Now);
			if (!File.Exists(path))
			{
				return;
			}
			DateTime today = DateTime.Today;
			string[] array = File.ReadAllLines(path);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(',');
				if (array2.Length >= 2 && TimeSpan.TryParse(array2[0], CultureInfo.InvariantCulture, out var result) && double.TryParse(array2[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
				{
					eqs.Add((today + result, result2));
				}
			}
		}
		catch (Exception e)
		{
			Log(e);
		}
	}

	private void RecordDay(Snap s)
	{
		try
		{
			if (s == null || !EarningsStore.IsSafeAccount(s.Account))
			{
				return;
			}
			string key = ((s.ServerTime.Length >= 10) ? s.ServerTime.Substring(0, 10) : DateTime.Now.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture));
			new EarningsStore(AppDir).UpsertLiveDay(s.Account, key, s.Equity, s.Balance, s.Realized);
		}
		catch (Exception e)
		{
			Log(e);
		}
	}

	private SortedDictionary<string, double[]> AccountHist()
	{
		try
		{
			if (snap == null || !EarningsStore.IsSafeAccount(snap.Account))
			{
				return new SortedDictionary<string, double[]>(StringComparer.Ordinal);
			}
			EarningsStore earningsStore = new EarningsStore(AppDir);
			return earningsStore.Load(earningsStore.AccountPath(snap.Account));
		}
		catch (Exception e)
		{
			Log(e);
			return new SortedDictionary<string, double[]>(StringComparer.Ordinal);
		}
	}

	private void TryBackfill(Snap s)
	{
		try
		{
			if (s == null || !EarningsStore.IsSafeAccount(s.Account))
			{
				return;
			}
			if (string.IsNullOrWhiteSpace(s.RealizedFile))
			{
				histNote = EarningsStore.BuildNote(null);
				return;
			}
			if (s.RealizedStamp == appliedStamp && s.Account == appliedAccount)
			{
				return;
			}
			string exportName = EarningsStore.SafeExportName(s.RealizedFile);
			string directory = Path.GetDirectoryName(jsonPath);
			if (exportName == null || string.IsNullOrEmpty(directory))
			{
				return;
			}
			string exportPath = Path.Combine(directory, exportName);
			if (!File.Exists(exportPath))
			{
				histNote = "EA 已宣告歷史檔，但檔案尚未寫入。缺日不會記成 0。" + EarningsStore.LimitText;
				return;
			}
			BackfillResult backfillResult = new EarningsStore(AppDir).ApplyExportFile(s.Account, exportPath);
			appliedStamp = s.RealizedStamp;
			appliedAccount = s.Account;
			histNote = backfillResult.Note;
			histCache = AccountHist();
			histAt = DateTime.Now;
		}
		catch (Exception e)
		{
			Log(e);
		}
	}

	private void Animate()
	{
		bool flag = false;
		foreach (string item in tgt.Keys.ToList())
		{
			double value;
			double num = (cur.TryGetValue(item, out value) ? value : tgt[item]);
			double num2 = tgt[item];
			double num3 = num + (num2 - num) * 0.16;
			if (Math.Abs(num2 - num3) < Math.Max(0.004, Math.Abs(num2) * 1E-07))
			{
				num3 = num2;
			}
			else
			{
				flag = true;
			}
			cur[item] = num3;
		}
		pulse = (pulse + 0.06f) % ((float)Math.PI * 2f);
		blinkT++;
		for (int i = 0; i < rain.Length; i++)
		{
			rain[i] += rainSpd[i];
			if (rain[i] * 14f > (float)this.Height / K + 200f)
			{
				rain[i] = -rng.Next(5, 40);
				rainSpd[i] = 0.15f + (float)rng.NextDouble() * 0.35f;
			}
		}
		glitchX = ((flash > 0.45f) ? ((float)(rng.NextDouble() * 8.0 - 4.0)) : 0f);
		if (flash > 0f)
		{
			flash = Math.Max(0f, flash - 0.035f);
			flag = true;
		}
		if (!flag)
		{
			_ = stale;
		}
		this.Invalidate();
	}

	private double V(string k)
	{
		if (!cur.TryGetValue(k, out var value))
		{
			return 0.0;
		}
		return value;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		try
		{
			Graphics graphics = e.Graphics;
			float k = K;
			graphics.ScaleTransform(k, k);
			Draw(graphics, (float)this.ClientSize.Width / k, (float)this.ClientSize.Height / k);
		}
		catch (Exception e2)
		{
			Log(e2);
		}
	}

	private void Draw(Graphics g, float W, float H)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Expected O, but got Unknown
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Expected O, but got Unknown
		g.SmoothingMode = (SmoothingMode)4;
		g.TextRenderingHint = (TextRenderingHint)3;
		g.PixelOffsetMode = (PixelOffsetMode)2;
		btns.Clear();
		sliderR = RectangleF.Empty;
		LinearGradientBrush val = new LinearGradientBrush(new PointF(0f, -1f), new PointF(0f, H + 1f), UI.Bg1, UI.Bg2);
		try
		{
			g.FillRectangle((Brush)(object)val, 0f, 0f, W, H);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		DrawRain(g, W, H);
		if (cfg.Compact)
		{
			DrawCompact(g, W, H);
		}
		else if (W >= 1000f)
		{
			DrawWide(g, W, H);
		}
		else
		{
			DrawFull(g, W, H);
		}
		UI.Scan(g, W, H);
		GraphicsPath val2 = UI.RR(new RectangleF(0.75f, 0.75f, W - 1.5f, H - 1.5f), 14f);
		try
		{
			LinearGradientBrush val3 = new LinearGradientBrush(new PointF(0f, 0f), new PointF(W, H), Color.FromArgb(220, UI.Gold), Color.FromArgb(60, UI.Gold));
			try
			{
				Pen val4 = new Pen((Brush)(object)val3, 1.5f);
				try
				{
					g.DrawPath(val4, val2);
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private void DrawRain(Graphics g, float W, float H)
	{
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Expected O, but got Unknown
		int num = Math.Max(1, (int)(W / 14f));
		if (rain.Length != num)
		{
			rain = new float[num];
			rainSpd = new float[num];
			for (int i = 0; i < num; i++)
			{
				rain[i] = rng.Next(-40, (int)(H / 14f));
				rainSpd[i] = 0.15f + (float)rng.NextDouble() * 0.35f;
			}
		}
		Font val = UI.M(12f);
		for (int j = 0; j < num; j++)
		{
			int num2 = (int)rain[j];
			for (int k = 0; k < 14; k++)
			{
				int num3 = num2 - k;
				if (num3 < 0)
				{
					continue;
				}
				float num4 = num3 * 14;
				if (!(num4 > H))
				{
					int alpha = ((k == 0) ? 45 : ((int)(22f * (1f - (float)k / 14f))));
					char c = "01アイウエオカキクケコサシスセソ$#%&*+=<>XAU金ﾊﾐﾋｰｳｼﾅﾓﾆｻﾜﾂｵﾘ"[(j * 7 + num3 * 13 + ((k == 0) ? (blinkT / 3) : 0)) % "01アイウエオカキクケコサシスセソ$#%&*+=<>XAU金ﾊﾐﾋｰｳｼﾅﾓﾆｻﾜﾂｵﾘ".Length];
					SolidBrush val2 = new SolidBrush(Color.FromArgb(alpha, (k == 0) ? UI.GoldHi : UI.Gold));
					try
					{
						g.DrawString(c.ToString(), val, (Brush)(object)val2, (float)(j * 14), num4);
					}
					finally
					{
						((IDisposable)val2)?.Dispose();
					}
				}
			}
		}
	}

	private (Color c, string t) Status()
	{
		if (!found)
		{
			return (c: UI.Red, t: "NO_FILE 找不到資料檔");
		}
		if (!fileOk)
		{
			return (c: UI.Au, t: "LOADING 讀取中");
		}
		if (stale)
		{
			return (c: UI.Red, t: "OFFLINE EA未執行");
		}
		return (c: UI.Gold, t: "CONNECTED 已連線");
	}

	private void Dot(Graphics g, float x, float y, Color c)
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Expected O, but got Unknown
		float num = (float)(Math.Sin(pulse) * 0.5 + 0.5);
		float num2 = 4f + num * 5f;
		SolidBrush val = new SolidBrush(Color.FromArgb((int)(90f * (1f - num)) + 20, c));
		try
		{
			g.FillEllipse((Brush)(object)val, x - num2, y - num2, num2 * 2f, num2 * 2f);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		SolidBrush val2 = new SolidBrush(c);
		try
		{
			g.FillEllipse((Brush)(object)val2, x - 4f, y - 4f, 8f, 8f);
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private void IconBtn(Graphics g, string key, string glyph, float x, float y, bool active = false)
	{
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Expected O, but got Unknown
		RectangleF rectangleF = new RectangleF(x, y, 24f, 24f);
		btns[key] = rectangleF;
		bool flag = rectangleF.Contains(new PointF((float)hover.X / K, (float)hover.Y / K));
		if (flag || active)
		{
			GraphicsPath val = UI.RR(rectangleF, 7f);
			try
			{
				SolidBrush val2 = new SolidBrush(Color.FromArgb(flag ? 50 : 28, (key == "close") ? UI.Red : UI.Gold));
				try
				{
					g.FillPath((Brush)(object)val2, val);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		Font val3 = UI.F(13f, bold: true);
		SizeF sizeF = g.MeasureString(glyph, val3);
		UI.Txt(g, glyph, val3, (active || flag) ? UI.GoldHi : UI.GoldDim, rectangleF.X + (24f - sizeF.Width) / 2f, rectangleF.Y + (24f - sizeF.Height) / 2f);
	}

	private void Diamond(Graphics g, float cx, float cy, float s)
	{
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Expected O, but got Unknown
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Expected O, but got Unknown
		PointF[] array = new PointF[4]
		{
			new PointF(cx, cy - s),
			new PointF(cx + s, cy),
			new PointF(cx, cy + s),
			new PointF(cx - s, cy)
		};
		SolidBrush val = new SolidBrush(Color.FromArgb(50, UI.Au));
		try
		{
			g.FillEllipse((Brush)(object)val, cx - s * 1.6f, cy - s * 1.6f, s * 3.2f, s * 3.2f);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		LinearGradientBrush val2 = new LinearGradientBrush(new PointF(cx, cy - s - 1f), new PointF(cx, cy + s + 1f), UI.AuHi, Color.FromArgb(200, 140, 0));
		try
		{
			g.FillPolygon((Brush)(object)val2, array);
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private void GlitchText(Graphics g, string t, Font f, Color c, float x, float y)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Expected O, but got Unknown
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Expected O, but got Unknown
		if (flash > 0.25f)
		{
			float num = 2f + flash * 4f;
			SolidBrush val = new SolidBrush(Color.FromArgb((int)(150f * flash), 255, 0, 80));
			try
			{
				SolidBrush val2 = new SolidBrush(Color.FromArgb((int)(150f * flash), 0, 230, 255));
				try
				{
					g.DrawString(t, f, (Brush)(object)val, x - num + glitchX, y);
					g.DrawString(t, f, (Brush)(object)val2, x + num - glitchX, y);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		UI.Glow(g, t, f, c, x + glitchX * 0.3f, y, (int)(90f + 140f * flash));
		if (!(flash > 0.5f))
		{
			return;
		}
		SizeF sizeF = g.MeasureString(t, f);
		SolidBrush val3 = new SolidBrush(Color.FromArgb(120, c));
		try
		{
			for (int i = 0; i < 3; i++)
			{
				g.FillRectangle((Brush)(object)val3, x + (float)rng.NextDouble() * sizeF.Width * 0.7f, y + (float)rng.NextDouble() * sizeF.Height, 10f + (float)rng.NextDouble() * 40f, 2f);
			}
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
		}
	}

	private string GoldText()
	{
		if (snap != null && !(snap.Bid <= 0.0))
		{
			return V("bid").ToString("F" + snap.GoldDigits, CultureInfo.InvariantCulture);
		}
		return "—";
	}

	private void DrawCompact(Graphics g, float W, float H)
	{
		Diamond(g, 18f, H / 2f, 7f);
		(Color, string) tuple = Status();
		Color c = UI.Lerp(UI.AuHi, flashCol, flash);
		GlitchText(g, GoldText(), UI.M(20f, bold: true), c, 32f, H / 2f - 13f);
		UI.Txt(g, snap?.GoldSym ?? "XAUUSD", UI.F(9f), UI.Text2, 34f, H / 2f + 9f - 1f);
		double v = V("pl");
		UI.Txt(g, "當日", UI.F(10f), UI.Text2, 150f, H / 2f - 7f);
		UI.Glow(g, (snap == null) ? "—" : UI.Signed(v), UI.M(15f, bold: true), UI.PL(v), 180f, H / 2f - 9f);
		Dot(g, W - 66f, H / 2f, tuple.Item1);
		IconBtn(g, "compact", "⤢", W - 56f, H / 2f - 12f);
		IconBtn(g, "close", "✕", W - 30f, H / 2f - 12f);
	}

	private float Table(Graphics g, float x, float y, float w, float[] cols, string[] heads, List<Cell[]> rows, float rh = 17f, float maxBottom = float.MaxValue)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Expected O, but got Unknown
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Expected O, but got Unknown
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Expected O, but got Unknown
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Expected O, but got Unknown
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Expected O, but got Unknown
		float tot = cols.Sum();
		float[] array = cols.Select((float c) => c / tot * w).ToArray();
		Pen val = new Pen(Color.FromArgb(70, UI.Gold), 1f);
		try
		{
			SolidBrush val2 = new SolidBrush(Color.FromArgb(215, UI.Gold));
			try
			{
				g.FillRectangle((Brush)(object)val2, x, y, w, rh);
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
			float num = x;
			for (int i = 0; i < cols.Length; i++)
			{
				Font f = UI.F(9.5f, bold: true);
				DrawCellText(g, heads[i], f, Color.Black, num, y, array[i], rh, i > 0 && rows.Count > 0 && rows[0][i].Right);
				num += array[i];
			}
			float num2 = y + rh;
			PointF pt = new PointF((float)hover.X / K, (float)hover.Y / K);
			for (int j = 0; j < rows.Count; j++)
			{
				if (num2 + rh > maxBottom)
				{
					break;
				}
				RectangleF rectangleF = new RectangleF(x, num2, w, rh);
				bool flag = rectangleF.Contains(pt);
				SolidBrush val3 = new SolidBrush(flag ? Color.FromArgb(55, UI.Gold) : Color.FromArgb((j % 2 == 0) ? 14 : 30, 0, 255, 136));
				try
				{
					g.FillRectangle((Brush)(object)val3, rectangleF);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
				num = x;
				for (int k = 0; k < cols.Length && k < rows[j].Length; k++)
				{
					Cell cell = rows[j][k];
					Color? bg = cell.Bg;
					if (bg.HasValue)
					{
						Color valueOrDefault = bg.GetValueOrDefault();
						SolidBrush val4 = new SolidBrush(valueOrDefault);
						try
						{
							g.FillRectangle((Brush)(object)val4, num + 1f, num2 + 1f, array[k] - 2f, rh - 2f);
						}
						finally
						{
							((IDisposable)val4)?.Dispose();
						}
					}
					Font f2 = (cell.Mono ? UI.M(10.5f, cell.Bold) : UI.F(10.5f, cell.Bold));
					if (cell.Glow)
					{
						SolidBrush val5 = new SolidBrush(Color.FromArgb(60, cell.C));
						try
						{
							DrawCellText(g, cell.T, f2, cell.C, num, num2, array[k], rh, cell.Right, (Brush)(object)val5);
						}
						finally
						{
							((IDisposable)val5)?.Dispose();
						}
					}
					DrawCellText(g, cell.T, f2, flag ? Color.White : cell.C, num, num2, array[k], rh, cell.Right);
					num += array[k];
				}
				g.DrawLine(val, x, num2 + rh, x + w, num2 + rh);
				num2 += rh;
			}
			num = x;
			for (int l = 0; l <= cols.Length; l++)
			{
				g.DrawLine(val, num, y, num, num2);
				if (l < cols.Length)
				{
					num += array[l];
				}
			}
			g.DrawRectangle(val, x, y, w, num2 - y);
			return num2;
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private static void DrawCellText(Graphics g, string t, Font f, Color c, float x, float y, float w, float h, bool right, Brush glow = null)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Expected O, but got Unknown
		StringFormat val = new StringFormat((StringFormatFlags)4096)
		{
			Trimming = (StringTrimming)3,
			LineAlignment = (StringAlignment)1,
			Alignment = (StringAlignment)(right ? 2 : 0)
		};
		try
		{
			RectangleF rectangleF = new RectangleF(x + 3f, y, w - 6f, h);
			if (glow != null)
			{
				g.DrawString(t, f, glow, new RectangleF(rectangleF.X - 1f, rectangleF.Y, rectangleF.Width, rectangleF.Height), val);
				g.DrawString(t, f, glow, new RectangleF(rectangleF.X + 1f, rectangleF.Y, rectangleF.Width, rectangleF.Height), val);
				return;
			}
			SolidBrush val2 = new SolidBrush(c);
			try
			{
				g.DrawString(t, f, (Brush)(object)val2, rectangleF, val);
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private float Title(Graphics g, string t, float x, float y, float w, string right = null, Color? rc = null)
	{
		UI.Glow(g, t, UI.M(10.5f, bold: true), UI.Gold, x, y);
		if (right != null)
		{
			UI.Txt(g, right, UI.M(10f, bold: true), rc ?? UI.Text2, x, y + 1f, right: true, w);
		}
		return y + 17f;
	}

	private (double today, double week, double month) Realized()
	{
		double item = snap?.Realized ?? 0.0;
		double valueOrDefault = (snap?.ClosedWeek?.Sum<(long, double, string, string, double)>(((long t, double profit, string sym, string type, double lots) c) => c.profit)).GetValueOrDefault();
		double item2 = 0.0;
		SortedDictionary<string, double[]> sortedDictionary = histCache ?? (preview ? previewHist : null);
		string ym = ((snap != null && snap.ServerTime.Length >= 7) ? snap.ServerTime.Substring(0, 7) : DateTime.Now.ToString("yyyy.MM", CultureInfo.InvariantCulture));
		if (sortedDictionary != null)
		{
			item2 = sortedDictionary.Where((KeyValuePair<string, double[]> kv) => kv.Key.StartsWith(ym, StringComparison.Ordinal)).Sum((KeyValuePair<string, double[]> kv) => double.IsNaN(kv.Value[2]) ? 0.0 : kv.Value[2]);
		}
		return (today: item, week: valueOrDefault, month: item2);
	}

	private void BigPL(Graphics g, RectangleF r, string title, double v, bool has, string sub, float vsz)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected O, but got Unknown
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Expected O, but got Unknown
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Expected O, but got Unknown
		Color color = (has ? UI.PL(v) : UI.Text2);
		SolidBrush val = new SolidBrush(Color.FromArgb(has ? 38 : 15, color));
		try
		{
			g.FillRectangle((Brush)(object)val, r);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		Pen val2 = new Pen(Color.FromArgb(200, color), 1.5f);
		try
		{
			g.DrawRectangle(val2, r.X, r.Y, r.Width, r.Height);
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		SolidBrush val3 = new SolidBrush(color);
		try
		{
			g.FillRectangle((Brush)(object)val3, r.X, r.Y, 4f, r.Height);
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
		}
		UI.Txt(g, title, UI.F(10.5f, bold: true), color, r.X + 9f, r.Y + 3f);
		Font f = UI.M(vsz, bold: true);
		SolidBrush val4 = new SolidBrush(Color.FromArgb(70, color));
		try
		{
			DrawCellText(g, has ? UI.Signed(v) : "—", f, color, r.X + 6f, r.Y + 16f, r.Width - 10f, r.Height - 16f - (float)((sub != null) ? 13 : 0), right: false, (Brush)(object)val4);
		}
		finally
		{
			((IDisposable)val4)?.Dispose();
		}
		DrawCellText(g, has ? UI.Signed(v) : "—", f, color, r.X + 6f, r.Y + 16f, r.Width - 10f, r.Height - 16f - (float)((sub != null) ? 13 : 0), right: false);
		if (sub != null)
		{
			DrawCellText(g, sub, UI.M(9.5f), UI.Text2, r.X + 6f, r.Bottom - 15f, r.Width - 10f, 14f, right: false);
		}
	}

	private void DrawFull(Graphics g, float W, float H)
	{
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Invalid comparison between Unknown and I4
		//IL_0894: Unknown result type (might be due to invalid IL or missing references)
		//IL_089b: Expected O, but got Unknown
		//IL_08cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_08d6: Expected O, but got Unknown
		//IL_0998: Unknown result type (might be due to invalid IL or missing references)
		//IL_099f: Expected O, but got Unknown
		//IL_128f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1296: Expected O, but got Unknown
		//IL_12d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_12d9: Expected O, but got Unknown
		//IL_170f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1714: Unknown result type (might be due to invalid IL or missing references)
		//IL_171d: Expected O, but got Unknown
		//IL_1763: Unknown result type (might be due to invalid IL or missing references)
		//IL_176a: Expected O, but got Unknown
		//IL_169d: Unknown result type (might be due to invalid IL or missing references)
		//IL_16a4: Expected O, but got Unknown
		//IL_1c0d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1c14: Expected O, but got Unknown
		//IL_2621: Unknown result type (might be due to invalid IL or missing references)
		//IL_2628: Expected O, but got Unknown
		//IL_251d: Unknown result type (might be due to invalid IL or missing references)
		//IL_2524: Expected O, but got Unknown
		//IL_264c: Unknown result type (might be due to invalid IL or missing references)
		//IL_2653: Expected O, but got Unknown
		//IL_2764: Unknown result type (might be due to invalid IL or missing references)
		//IL_276b: Expected O, but got Unknown
		float num = 10f;
		float num2 = W - num * 2f;
		_ = K;
		(Color, string) tuple = Status();
		Diamond(g, 19f, 17f, 7f);
		string s = "> ACCT_" + ((snap == null) ? "????????" : snap.Account) + " :: " + tuple.Item2.Split(' ')[0] + (Blink ? "_" : " ");
		UI.Glow(g, s, UI.M(11.5f, bold: true), tuple.Item1, 32f, 8f, 70);
		UI.Txt(g, (snap == null) ? "// 等待 EA 資料…" : ("// " + snap.Server + " · " + snap.Currency), UI.M(9.5f), UI.Text2, 33f, 25f);
		float num3 = W - num - 22f;
		(string, string)[] array = new(string, string)[7]
		{
			("close", "✕"),
			("max", ((int)this.WindowState == 2) ? "❐" : "□"),
			("min", "–"),
			("menu", "⋯"),
			("hist", "▤"),
			("pin", "\ud83d\udccc"),
			("compact", "⤡")
		};
		for (int i = 0; i < array.Length; i++)
		{
			var (text, glyph) = array[i];
			IconBtn(g, text, glyph, num3, 6f, text == "pin" && this.TopMost);
			num3 -= 24f;
		}
		Dot(g, W - num - 5f, 33f, tuple.Item1);
		UI.Txt(g, tuple.Item2.Contains(' ') ? tuple.Item2.Substring(tuple.Item2.IndexOf(' ') + 1) : tuple.Item2, UI.F(9.5f, bold: true), tuple.Item1, W - num - 130f, 27f, right: true, 115f);
		float y = 42f;
		RectangleF r2 = new RectangleF(num, y, num2, 62f);
		UI.Card(g, r2, (flash > 0f) ? UI.Lerp(UI.Au, flashCol, flash) : UI.Au, 0.35f + flash);
		UI.Txt(g, "> " + (string.IsNullOrEmpty(snap?.GoldSym) ? "XAUUSD" : snap.GoldSym), UI.M(10f, bold: true), UI.Au, r2.X + 8f, r2.Y + 4f);
		GlitchText(g, GoldText(), UI.M(28f, bold: true), UI.Lerp(UI.AuHi, flashCol, flash), r2.X + 6f, r2.Y + 18f);
		if (snap != null && snap.Ask > 0.0)
		{
			UI.Txt(g, "B " + snap.Bid.ToString("F" + snap.GoldDigits, CultureInfo.InvariantCulture) + " A " + snap.Ask.ToString("F" + snap.GoldDigits, CultureInfo.InvariantCulture), UI.M(9f), UI.Text2, r2.X + 90f, r2.Y + 5f);
		}
		RectangleF r3 = new RectangleF(r2.X + r2.Width * 0.52f, r2.Y + 20f, r2.Width * 0.48f - 8f, 36f);
		if (ticks.Count >= 2)
		{
			List<(DateTime t, double v)> list = ticks;
			double num4 = list[list.Count - 1].v - ticks[0].v;
			UI.Txt(g, ((num4 >= 0.0) ? "▲" : "▼") + Math.Abs(num4).ToString("F" + (snap?.GoldDigits ?? 2), CultureInfo.InvariantCulture) + " 5m", UI.M(9.5f, bold: true), UI.PL(num4), r3.X, r2.Y + 4f, right: true, r3.Width);
			UI.Spark(g, r3, ticks.Select(((DateTime t, double v) t) => t.v).ToList(), (num4 >= 0.0) ? UI.Au : UI.Red);
		}
		y = r2.Bottom + 6f;
		bool flag = snap != null;
		double v = V("eq");
		double v2 = V("pl");
		double num5 = V("lots");
		double num6 = V("ml");
		double v3 = V("fl");
		double num7 = (snap?.Equity ?? 0.0) - (snap?.TodayPL ?? 0.0);
		double num8 = ((num7 > 0.0) ? ((snap?.TodayPL ?? 0.0) / num7 * 100.0) : 0.0);
		(string, string, Color)[] array2 = new(string, string, Color)[5]
		{
			("總金額", flag ? UI.Money(v) : "—", UI.GoldHi),
			("當日營利 " + (flag ? (((num8 >= 0.0) ? "+" : "") + num8.ToString("0.00", CultureInfo.InvariantCulture) + "%") : ""), flag ? UI.Signed(v2) : "—", flag ? UI.PL(v2) : UI.Gold),
			("浮動", flag ? UI.Signed(v3) : "—", flag ? UI.PL(v3) : UI.Gold),
			("口數", flag ? num5.ToString("0.00", CultureInfo.InvariantCulture) : "—", UI.GoldHi),
			("保證金比例", (flag && snap.Margin > 0.0) ? (num6.ToString("#,##0", CultureInfo.InvariantCulture) + "%") : "—", (!flag || snap.Margin <= 0.0) ? UI.GoldHi : ((num6 < 150.0) ? UI.Red : ((num6 < 400.0) ? UI.Au : UI.Green)))
		};
		float[] array3 = new float[5] { 1.25f, 1.25f, 1.05f, 0.7f, 0.95f };
		float num9 = array3.Sum();
		float num10 = num;
		Pen val = new Pen(Color.FromArgb(80, UI.Gold));
		try
		{
			for (int j = 0; j < array2.Length; j++)
			{
				float num11 = array3[j] / num9 * num2;
				RectangleF rectangleF = new RectangleF(num10, y, num11, 38f);
				SolidBrush val2 = new SolidBrush(Color.FromArgb(150, 0, 14, 6));
				try
				{
					g.FillRectangle((Brush)(object)val2, rectangleF);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
				g.DrawRectangle(val, rectangleF.X, rectangleF.Y, rectangleF.Width, rectangleF.Height);
				DrawCellText(g, array2[j].Item1, UI.F(9f), UI.Text2, rectangleF.X, rectangleF.Y + 1f, rectangleF.Width, 14f, right: false);
				Font f = UI.M((array2[j].Item2.Length > 9) ? 11.5f : 13f, bold: true);
				SolidBrush val3 = new SolidBrush(Color.FromArgb(55, array2[j].Item3));
				try
				{
					DrawCellText(g, array2[j].Item2, f, array2[j].Item3, rectangleF.X, rectangleF.Y + 15f, rectangleF.Width, 22f, right: false, (Brush)(object)val3);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
				DrawCellText(g, array2[j].Item2, f, array2[j].Item3, rectangleF.X, rectangleF.Y + 15f, rectangleF.Width, 22f, right: false);
				num10 += num11;
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		y += 42f;
		(double, double, double) tuple3 = Realized();
		float num12 = (num2 - 6f) / 2f;
		BigPL(g, new RectangleF(num, y, num12, 52f), "平倉收益 · 今日", tuple3.Item1, flag, "7天 " + UI.Signed(tuple3.Item2) + " · 本月 " + UI.Signed(tuple3.Item3), 17f);
		BigPL(g, new RectangleF(num + num12 + 6f, y, num12, 52f), $"浮動盈虧 · {snap?.Pos.Count ?? 0}筆", flag ? snap.Floating : 0.0, flag, $"口數 {snap?.TotalLots ?? 0.0:0.00}", 17f);
		y += 58f;
		Dictionary<string, Dictionary<string, double>> dictionary = snap?.Tech;
		string[] array4 = new string[3] { "M15", "H1", "H4" };
		int[] array5 = new int[3] { 1, 2, 3 };
		double num13 = 0.0;
		List<Cell[]> list2 = new List<Cell[]>();
		bool flag2 = dictionary != null && dictionary.Count > 0;
		if (flag2)
		{
			for (int k = 0; k < 3; k++)
			{
				if (dictionary.TryGetValue(array4[k], out var value))
				{
					int item = ScoreTf(array4[k], value).score;
					num13 += (double)(item * array5[k]);
					(string t, Color c) tuple4 = Sig(item, 2.0);
					string item2 = tuple4.t;
					Color item3 = tuple4.c;
					double valueOrDefault = value.GetValueOrDefault("rsi");
					double valueOrDefault2 = value.GetValueOrDefault("macd");
					double valueOrDefault3 = value.GetValueOrDefault("macdSignal");
					double valueOrDefault4 = value.GetValueOrDefault("close");
					double valueOrDefault5 = value.GetValueOrDefault("ema20");
					double valueOrDefault6 = value.GetValueOrDefault("ema50");
					double valueOrDefault7 = value.GetValueOrDefault("ema200");
					string text2 = ((valueOrDefault4 > valueOrDefault5 && valueOrDefault5 > valueOrDefault6 && valueOrDefault6 > valueOrDefault7) ? "多頭排列" : ((valueOrDefault4 < valueOrDefault5 && valueOrDefault5 < valueOrDefault6 && valueOrDefault6 < valueOrDefault7) ? "空頭排列" : ((valueOrDefault4 > valueOrDefault7) ? "EMA200上" : "EMA200下")));
					Color c = ((text2 == "多頭排列" || text2 == "EMA200上") ? UI.Green : UI.Red);
					list2.Add(new Cell[6]
					{
						new Cell(array4[k], UI.GoldHi, Mono: true, Bold: true),
						new Cell(item2, item3, Mono: false, Bold: true, Right: false, Glow: true, Color.FromArgb(45, item3)),
						new Cell(((item > 0) ? "+" : "") + item, item3, Mono: true, Bold: true, Right: true, Glow: true),
						new Cell(valueOrDefault.ToString("0", CultureInfo.InvariantCulture) + ((valueOrDefault >= 70.0) ? "↑" : ((valueOrDefault <= 30.0) ? "↓" : "")), (valueOrDefault >= 70.0) ? UI.Red : ((valueOrDefault <= 30.0) ? UI.Green : UI.GoldHi), Mono: true, Bold: false, Right: true),
						new Cell((valueOrDefault2 > valueOrDefault3) ? "金叉▲" : "死叉▼", (valueOrDefault2 > valueOrDefault3) ? UI.Green : UI.Red, Mono: false, Bold: true),
						new Cell(text2, c, Mono: false)
					});
				}
			}
		}
		double num14 = num13 / 36.0;
		(string t, Color c) tuple5 = Sig(num14, 0.15);
		string item4 = tuple5.t;
		Color item5 = tuple5.c;
		int value2 = (int)Math.Round(Math.Min(1.0, Math.Abs(num14)) * 100.0);
		y = Title(g, "> MARKET.SCAN", num, y, num2, flag2 ? $"綜合 {item4} {value2}%" : "等待 EA 指標…", flag2 ? item5 : UI.Text2);
		if (!flag2)
		{
			list2.Add(new Cell[6]
			{
				new Cell("—", UI.Text2),
				new Cell("—", UI.Text2),
				new Cell("—", UI.Text2),
				new Cell("—", UI.Text2),
				new Cell("—", UI.Text2),
				new Cell("請更新 EA", UI.Text2, Mono: false)
			});
		}
		y = Table(g, num, y, num2, new float[6] { 0.6f, 0.7f, 0.55f, 0.6f, 0.85f, 1.1f }, new string[6] { "週期", "訊號", "分數", "RSI", "MACD", "趨勢" }, list2) + 4f;
		float num15 = num2 * 0.6f;
		if (flag2)
		{
			UI.Txt(g, "綜合建議", UI.F(9.5f), UI.Text2, num, y);
			UI.Glow(g, item4, UI.F(15f, bold: true), item5, num + 52f, y - 3f, 80);
			UI.Glow(g, value2 + "%", UI.M(13f, bold: true), item5, num + 92f, y - 1f, 60);
			UI.Txt(g, "信心", UI.F(9f), UI.Text2, num + 92f + g.MeasureString(value2 + "%", UI.M(13f, bold: true)).Width, y + 2f);
		}
		double value3 = 0.0;
		double value4 = 0.0;
		if (flag2 && dictionary.TryGetValue("H1", out var value5))
		{
			value5.TryGetValue("buyVol", out value3);
			value5.TryGetValue("sellVol", out value4);
		}
		double num16 = ((value3 + value4 > 0.0) ? (value3 / (value3 + value4)) : 0.5);
		UI.Txt(g, "買賣量 H1·20", UI.F(9f), UI.Text2, num, y + 20f);
		RectangleF rectangleF2 = new RectangleF(num, y + 35f, num15 - 8f, 6f);
		SolidBrush val4 = new SolidBrush(UI.Green);
		try
		{
			g.FillRectangle((Brush)(object)val4, rectangleF2.X, rectangleF2.Y, (float)((double)rectangleF2.Width * num16), rectangleF2.Height);
		}
		finally
		{
			((IDisposable)val4)?.Dispose();
		}
		SolidBrush val5 = new SolidBrush(UI.Red);
		try
		{
			g.FillRectangle((Brush)(object)val5, rectangleF2.X + (float)((double)rectangleF2.Width * num16), rectangleF2.Y, (float)((double)rectangleF2.Width * (1.0 - num16)), rectangleF2.Height);
		}
		finally
		{
			((IDisposable)val5)?.Dispose();
		}
		UI.Txt(g, $"買{num16 * 100.0:0}%", UI.M(9.5f, bold: true), UI.Green, rectangleF2.X + 70f, y + 20f);
		UI.Txt(g, $"賣{(1.0 - num16) * 100.0:0}%", UI.M(9.5f, bold: true), UI.Red, rectangleF2.X, y + 20f, right: true, rectangleF2.Width);
		double num17 = 50.0;
		int num18 = 0;
		double num19 = 0.0;
		if (flag2)
		{
			string[] array6 = array4;
			foreach (string key in array6)
			{
				if (dictionary.TryGetValue(key, out var value6))
				{
					double valueOrDefault8 = value6.GetValueOrDefault("rsi", 50.0);
					double valueOrDefault9 = value6.GetValueOrDefault("bbPos", 0.5);
					double valueOrDefault10 = value6.GetValueOrDefault("atr", 0.0);
					double num20 = ((valueOrDefault10 > 0.0) ? Math.Clamp((value6.GetValueOrDefault("macd") - value6.GetValueOrDefault("macdSignal")) / valueOrDefault10 * 2.0, -1.0, 1.0) : 0.0);
					num19 += 0.45 * valueOrDefault8 + 0.3 * Math.Clamp(valueOrDefault9, 0.0, 1.0) * 100.0 + 0.25 * (50.0 + num20 * 50.0);
					num18++;
				}
			}
		}
		if (num18 > 0)
		{
			num17 = Math.Clamp(num19 / (double)num18, 0.0, 100.0);
		}
		string text3 = ((num17 >= 75.0) ? "極度貪婪" : ((num17 >= 58.0) ? "貪婪" : ((num17 > 42.0) ? "中性" : ((num17 > 25.0) ? "恐懼" : "極度恐懼"))));
		PointF pointF = new PointF(num + num15 + (num2 - num15) * 0.32f, y + 40f);
		float num21 = 30f;
		RectangleF rectangleF3 = new RectangleF(pointF.X - num21, pointF.Y - num21, num21 * 2f, num21 * 2f);
		for (int l = 0; l < 18; l++)
		{
			float num22 = (float)l / 18f;
			Color baseColor = ((num22 < 0.5f) ? UI.Lerp(UI.Red, UI.Au, num22 * 2f) : UI.Lerp(UI.Au, UI.Green, (num22 - 0.5f) * 2f));
			Pen val6 = new Pen(Color.FromArgb(flag2 ? 230 : 70, baseColor), 5f);
			try
			{
				g.DrawArc(val6, rectangleF3, (float)(180 + l * 10), 10.5f);
			}
			finally
			{
				((IDisposable)val6)?.Dispose();
			}
		}
		if (flag2)
		{
			double num23 = Math.PI * (1.0 - num17 / 100.0);
			Pen val7 = new Pen(Color.White, 2f)
			{
				EndCap = (LineCap)2
			};
			try
			{
				g.DrawLine(val7, pointF, new PointF(pointF.X + (float)Math.Cos(num23) * (num21 - 6f), pointF.Y - (float)Math.Sin(num23) * (num21 - 6f)));
				SolidBrush val8 = new SolidBrush(Color.White);
				try
				{
					g.FillEllipse((Brush)(object)val8, pointF.X - 3f, pointF.Y - 3f, 6f, 6f);
				}
				finally
				{
					((IDisposable)val8)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val7)?.Dispose();
			}
		}
		Color c2 = ((num17 >= 58.0) ? UI.Green : ((num17 <= 42.0) ? UI.Red : UI.Au));
		UI.Glow(g, flag2 ? num17.ToString("0", CultureInfo.InvariantCulture) : "—", UI.M(14f, bold: true), c2, pointF.X + num21 + 6f, y + 6f, 60);
		UI.Txt(g, flag2 ? text3 : "情緒", UI.F(9.5f, bold: true), c2, pointF.X + num21 + 6f, y + 24f);
		UI.Txt(g, "指標推算", UI.F(8.5f), UI.Text2, pointF.X + num21 + 6f, y + 37f);
		y += 50f;
		DateTimeOffset now = DateTimeOffset.Now;
		TimeSpan offset = TimeSpan.FromHours(8.0);
		List<EconEvent> list3 = (from e in Econ.Events
			where !cfg.CalHigh || e.Stars == 3
			where e.T > now.AddHours(-1.0)
			orderby e.T
			select e).ToList();
		int num24 = Math.Max(1, snap?.Pos.Count ?? 0);
		int num25 = Math.Max(1, list3.Count);
		float num26 = H - 24f - y;
		float num27 = 84f;
		float num28 = 48f;
		float num29 = Math.Max(0f, num26 - num27 - num28);
		int num30 = Math.Min(num25, 3);
		int num31 = Math.Min(num24, 2);
		int num32 = (int)(num29 / 17f) - num30 - num31;
		if (num32 < 0)
		{
			num30 = Math.Max(1, num30 + num32 / 2);
			num31 = Math.Max(1, (int)(num29 / 17f) - num30);
			num32 = 0;
		}
		int num33 = Math.Min(num32, num24 - num31);
		num31 += num33;
		num32 -= num33;
		int num34 = Math.Min(num32, num25 - num30);
		num30 += num34;
		num32 -= num34;
		EconEvent econEvent = (from e in Econ.Events
			where e.Stars == 3 && e.T > now
			orderby e.T
			select e).FirstOrDefault();
		string right = ((econEvent == null) ? "本週無★★★" : ("★★★ T-" + (((econEvent.T - now).Days > 0) ? ((econEvent.T - now).Days + "d ") : "") + (econEvent.T - now).ToString("hh\\:mm\\:ss")));
		y = Title(g, "> ECON.CALENDAR :: USD" + ((list3.Count > 0) ? $" [{Math.Min(num30, list3.Count)}/{list3.Count}]" : ""), num, y, num2 - 96f, right, (econEvent == null) ? UI.Text2 : (Blink ? UI.Red : Color.FromArgb(200, UI.Red)));
		string[] array7 = new string[2] { "全部", "★★★" };
		for (int m = 0; m < 2; m++)
		{
			RectangleF rectangleF4 = new RectangleF(W - num - (float)((2 - m) * 44), y - 17f, 42f, 15f);
			btns[(m == 0) ? "cal_all" : "cal_high"] = rectangleF4;
			bool flag3 = cfg.CalHigh == (m == 1);
			SolidBrush val9 = new SolidBrush(Color.FromArgb(flag3 ? 200 : 25, UI.Gold));
			try
			{
				g.FillRectangle((Brush)(object)val9, rectangleF4);
			}
			finally
			{
				((IDisposable)val9)?.Dispose();
			}
			DrawCellText(g, array7[m], UI.F(9f, bold: true), flag3 ? Color.Black : UI.Gold, rectangleF4.X, rectangleF4.Y, rectangleF4.Width, rectangleF4.Height, right: false);
		}
		List<EconEvent> list4 = list3.Take(num30).ToList();
		bool flag4 = num2 >= 470f;
		EconEvent econEvent2 = list4.FirstOrDefault((EconEvent e) => e.T > now);
		List<Cell[]> list5 = new List<Cell[]>();
		foreach (EconEvent item6 in list4)
		{
			bool flag5 = item6.T <= now;
			Color color = (flag5 ? Color.FromArgb(120, UI.Text2) : UI.GoldHi);
			Color c3 = ((item6.Stars == 3) ? UI.Red : ((item6.Stars == 2) ? UI.Au : UI.Text2));
			Color? bg = ((item6 == econEvent2) ? new Color?(Color.FromArgb(Blink ? 70 : 40, UI.Au)) : null);
			DateTimeOffset dateTimeOffset = item6.T.ToOffset(offset);
			list5.Add(new Cell[6]
			{
				new Cell(dateTimeOffset.ToString("MM/dd HH:mm", CultureInfo.InvariantCulture), color, Mono: true, item6 == econEvent2, Right: false, Glow: false, bg),
				new Cell(new string('★', item6.Stars), c3, Mono: false, Bold: false, Right: false, item6.Stars == 3, bg),
				new Cell(item6.Zh, flag5 ? color : Color.White, Mono: false, item6.Stars == 3, Right: false, Glow: false, bg),
				new Cell(Dash(item6.Previous), flag5 ? color : UI.Text2, Mono: true, Bold: false, Right: true, Glow: false, bg),
				new Cell(Dash(item6.Forecast), flag5 ? color : UI.Gold, Mono: true, Bold: true, Right: true, Glow: false, bg),
				new Cell(Dash(item6.Actual), UI.GoldHi, Mono: true, Bold: true, Right: true, Glow: false, bg)
			});
		}
		if (list5.Count == 0)
		{
			list5.Add(new Cell[6]
			{
				new Cell("—", UI.Text2),
				new Cell("", UI.Text2),
				new Cell((Econ.Events.Count == 0) ? "讀取中… (需網路)" : "近期無事件", UI.Text2, Mono: false),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2)
			});
		}
		if (list3.Count > num30)
		{
			_ = list5.Count;
			_ = 0;
		}
		y = (flag4 ? (Table(g, num, y, num2, new float[6] { 1.05f, 0.45f, 2f, 0.65f, 0.65f, 0.65f }, new string[6] { "台灣時間", "★", "事件", "前值", "預期", "公布" }, list5) + 6f) : (Table(g, num, y, num2, new float[5] { 1.05f, 0.45f, 1.7f, 0.65f, 0.65f }, new string[5] { "台灣時間", "★", "事件", "前值", "預期" }, list5) + 6f));
		bool flag6 = num2 >= 470f;
		List<(string, string, double, double, double, double)> list6 = snap?.Pos ?? new List<(string, string, double, double, double, double)>();
		double v4 = list6.Sum<(string, string, double, double, double, double)>(((string sym, string type, double lots, double open, double cur, double profit) p) => p.profit);
		y = Title(g, "> POSITIONS [" + list6.Count + "]", num, y, num2, (list6.Count > 0) ? UI.Signed(v4) : null, UI.PL(v4));
		List<Cell[]> list7 = new List<Cell[]>();
		foreach (var item7 in list6)
		{
			Color color2 = (item7.Item2.StartsWith("BUY", StringComparison.Ordinal) ? UI.Green : UI.Red);
			list7.Add(new Cell[6]
			{
				new Cell(item7.Item1, UI.GoldHi, Mono: true, Bold: true),
				new Cell(item7.Item2, color2, Mono: true, Bold: true, Right: false, Glow: false, Color.FromArgb(45, color2)),
				new Cell(item7.Item3.ToString("0.00", CultureInfo.InvariantCulture), UI.GoldHi, Mono: true, Bold: false, Right: true),
				new Cell(item7.Item4.ToString("0.#####", CultureInfo.InvariantCulture), UI.Text2, Mono: true, Bold: false, Right: true),
				new Cell(item7.Item5.ToString("0.#####", CultureInfo.InvariantCulture), UI.GoldHi, Mono: true, Bold: false, Right: true),
				new Cell(UI.Signed(item7.Item6), UI.PL(item7.Item6), Mono: true, Bold: true, Right: true, Glow: true)
			});
		}
		if (list7.Count == 0)
		{
			list7.Add(Blank((snap == null) ? "—" : "無持倉"));
		}
		if (list7.Count > num31)
		{
			int value7 = list7.Count - num31 + 1;
			list7 = list7.Take(Math.Max(0, num31 - 1)).ToList();
			list7.Add(Blank($"… +{value7}"));
		}
		if (!flag6)
		{
			list7 = list7.Select((Cell[] r) => new Cell[5]
			{
				r[0],
				r[1],
				r[2],
				r[3],
				r[5]
			}).ToList();
		}
		y = (flag6 ? Table(g, num, y, num2, new float[6] { 0.95f, 0.8f, 0.55f, 0.9f, 0.9f, 1f }, new string[6] { "商品", "方向", "口數", "開倉", "現價", "損益" }, list7) : Table(g, num, y, num2, new float[5] { 0.95f, 0.8f, 0.55f, 0.9f, 1f }, new string[5] { "商品", "方向", "口數", "開倉", "損益" }, list7));
		y += 6f;
		float num35 = y;
		List<(string, double, double)> list8 = HistoryForm.Agg(histCache ?? (preview ? previewHist : new SortedDictionary<string, double[]>()), cfg.HistMode);
		double v5 = list8.Sum<(string, double, double)>(((string key, double eq, double pl) r) => r.pl);
		UI.Glow(g, "> PNL", UI.M(10f, bold: true), UI.Gold, num, num35, 40);
		string[] array8 = new string[3] { "日", "週", "月" };
		for (int n = 0; n < 3; n++)
		{
			RectangleF rectangleF5 = new RectangleF(num + 44f + (float)(n * 22), num35, 20f, 15f);
			btns["h" + n] = rectangleF5;
			SolidBrush val10 = new SolidBrush(Color.FromArgb((cfg.HistMode == n) ? 200 : 25, UI.Gold));
			try
			{
				g.FillRectangle((Brush)(object)val10, rectangleF5);
			}
			finally
			{
				((IDisposable)val10)?.Dispose();
			}
			DrawCellText(g, array8[n], UI.F(9f, bold: true), (cfg.HistMode == n) ? Color.Black : UI.Gold, rectangleF5.X - 1f, rectangleF5.Y, rectangleF5.Width + 2f, rectangleF5.Height, right: false);
		}
		UI.Txt(g, "合計 " + UI.Signed(v5), UI.M(10f, bold: true), UI.PL(v5), num, num35, right: true, num2);
		RectangleF rectangleF6 = new RectangleF(num, num35 + 18f, num2, Math.Max(26f, H - 24f - 4f - (num35 + 18f)));
		btns["hstrip"] = rectangleF6;
		SolidBrush val11 = new SolidBrush(Color.FromArgb(120, 0, 14, 6));
		try
		{
			g.FillRectangle((Brush)(object)val11, rectangleF6);
		}
		finally
		{
			((IDisposable)val11)?.Dispose();
		}
		Pen val12 = new Pen(Color.FromArgb(70, UI.Gold));
		try
		{
			g.DrawRectangle(val12, rectangleF6.X, rectangleF6.Y, rectangleF6.Width, rectangleF6.Height);
		}
		finally
		{
			((IDisposable)val12)?.Dispose();
		}
		if (list8.Count > 0)
		{
			double num36 = Math.Max(1.0, list8.Max<(string, double, double)>(((string key, double eq, double pl) r) => Math.Abs(r.pl)));
			float num37 = rectangleF6.Y + rectangleF6.Height / 2f;
			float num38 = rectangleF6.Width / (float)Math.Max(list8.Count, (cfg.HistMode == 0) ? 30 : 12);
			for (int num39 = 0; num39 < list8.Count; num39++)
			{
				float num40 = (float)(Math.Abs(list8[num39].Item3) / num36 * (double)(rectangleF6.Height / 2f - 2f));
				SolidBrush val13 = new SolidBrush((list8[num39].Item3 >= 0.0) ? UI.Green : UI.Red);
				try
				{
					g.FillRectangle((Brush)(object)val13, rectangleF6.X + (float)num39 * num38 + num38 * 0.15f, (list8[num39].Item3 >= 0.0) ? (num37 - num40) : num37, Math.Max(1f, num38 * 0.7f), Math.Max(1f, num40));
				}
				finally
				{
					((IDisposable)val13)?.Dispose();
				}
			}
		}
		else
		{
			DrawCellText(g, "尚無收益紀錄 (點此開啟紀錄視窗)", UI.F(9f), UI.Text2, rectangleF6.X, rectangleF6.Y, rectangleF6.Width, rectangleF6.Height, right: false);
		}
		DrawFooter(g, W, H, num, num2);
		static Cell[] Blank(string t)
		{
			return new Cell[6]
			{
				new Cell(t, UI.Text2, Mono: false),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2)
			};
		}
	}

	private (List<Cell[]> rows, string ost, Color ocol, int conf, double bp, double senti, string sl, bool has) ScanData()
	{
		Dictionary<string, Dictionary<string, double>> dictionary = snap?.Tech;
		bool flag = dictionary != null && dictionary.Count > 0;
		string[] array = new string[3] { "M15", "H1", "H4" };
		int[] array2 = new int[3] { 1, 2, 3 };
		double num = 0.0;
		List<Cell[]> list = new List<Cell[]>();
		if (flag)
		{
			for (int i = 0; i < 3; i++)
			{
				if (dictionary.TryGetValue(array[i], out var value))
				{
					int item = ScoreTf(array[i], value).score;
					num += (double)(item * array2[i]);
					(string t, Color c) tuple = Sig(item, 2.0);
					string item2 = tuple.t;
					Color item3 = tuple.c;
					double valueOrDefault = value.GetValueOrDefault("rsi");
					double valueOrDefault2 = value.GetValueOrDefault("macd");
					double valueOrDefault3 = value.GetValueOrDefault("macdSignal");
					double valueOrDefault4 = value.GetValueOrDefault("close");
					double valueOrDefault5 = value.GetValueOrDefault("ema20");
					double valueOrDefault6 = value.GetValueOrDefault("ema50");
					double valueOrDefault7 = value.GetValueOrDefault("ema200");
					string text = ((valueOrDefault4 > valueOrDefault5 && valueOrDefault5 > valueOrDefault6 && valueOrDefault6 > valueOrDefault7) ? "多頭排列" : ((valueOrDefault4 < valueOrDefault5 && valueOrDefault5 < valueOrDefault6 && valueOrDefault6 < valueOrDefault7) ? "空頭排列" : ((valueOrDefault4 > valueOrDefault7) ? "EMA200上" : "EMA200下")));
					Color c = ((text == "多頭排列" || text == "EMA200上") ? UI.Green : UI.Red);
					list.Add(new Cell[7]
					{
						new Cell(array[i], UI.GoldHi, Mono: true, Bold: true),
						new Cell(item2, item3, Mono: false, Bold: true, Right: false, Glow: true, Color.FromArgb(45, item3)),
						new Cell(((item > 0) ? "+" : "") + item, item3, Mono: true, Bold: true, Right: true, Glow: true),
						new Cell(valueOrDefault.ToString("0", CultureInfo.InvariantCulture) + ((valueOrDefault >= 70.0) ? "↑" : ((valueOrDefault <= 30.0) ? "↓" : "")), (valueOrDefault >= 70.0) ? UI.Red : ((valueOrDefault <= 30.0) ? UI.Green : UI.GoldHi), Mono: true, Bold: false, Right: true),
						new Cell((valueOrDefault2 > valueOrDefault3) ? "金叉▲" : "死叉▼", (valueOrDefault2 > valueOrDefault3) ? UI.Green : UI.Red, Mono: false, Bold: true),
						new Cell(text, c, Mono: false),
						new Cell(value.GetValueOrDefault("atr").ToString("0.00", CultureInfo.InvariantCulture), UI.Text2, Mono: true, Bold: false, Right: true)
					});
				}
			}
		}
		else
		{
			list.Add(new Cell[7]
			{
				new Cell("—", UI.Text2),
				new Cell("—", UI.Text2),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2),
				new Cell("請更新 EA", UI.Text2, Mono: false),
				new Cell("", UI.Text2)
			});
		}
		double num2 = num / 36.0;
		(string t, Color c) tuple2 = Sig(num2, 0.15);
		string item4 = tuple2.t;
		Color item5 = tuple2.c;
		int item6 = (int)Math.Round(Math.Min(1.0, Math.Abs(num2)) * 100.0);
		double value2 = 0.0;
		double value3 = 0.0;
		if (flag && dictionary.TryGetValue("H1", out var value4))
		{
			value4.TryGetValue("buyVol", out value2);
			value4.TryGetValue("sellVol", out value3);
		}
		double item7 = ((value2 + value3 > 0.0) ? (value2 / (value2 + value3)) : 0.5);
		double num3 = 50.0;
		double num4 = 0.0;
		int num5 = 0;
		if (flag)
		{
			string[] array3 = array;
			foreach (string key in array3)
			{
				if (dictionary.TryGetValue(key, out var value5))
				{
					double valueOrDefault8 = value5.GetValueOrDefault("rsi", 50.0);
					double valueOrDefault9 = value5.GetValueOrDefault("bbPos", 0.5);
					double valueOrDefault10 = value5.GetValueOrDefault("atr", 0.0);
					double num6 = ((valueOrDefault10 > 0.0) ? Math.Clamp((value5.GetValueOrDefault("macd") - value5.GetValueOrDefault("macdSignal")) / valueOrDefault10 * 2.0, -1.0, 1.0) : 0.0);
					num4 += 0.45 * valueOrDefault8 + 0.3 * Math.Clamp(valueOrDefault9, 0.0, 1.0) * 100.0 + 0.25 * (50.0 + num6 * 50.0);
					num5++;
				}
			}
		}
		if (num5 > 0)
		{
			num3 = Math.Clamp(num4 / (double)num5, 0.0, 100.0);
		}
		string item8 = ((num3 >= 75.0) ? "極度貪婪" : ((num3 >= 58.0) ? "貪婪" : ((num3 > 42.0) ? "中性" : ((num3 > 25.0) ? "恐懼" : "極度恐懼"))));
		return (rows: list, ost: item4, ocol: item5, conf: item6, bp: item7, senti: num3, sl: item8, has: flag);
	}

	private void Gauge(Graphics g, PointF gc, float rad, double senti, bool has, float thick = 6f)
	{
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Expected O, but got Unknown
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Expected O, but got Unknown
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Expected O, but got Unknown
		RectangleF rectangleF = new RectangleF(gc.X - rad, gc.Y - rad, rad * 2f, rad * 2f);
		for (int i = 0; i < 18; i++)
		{
			float num = (float)i / 18f;
			Color baseColor = ((num < 0.5f) ? UI.Lerp(UI.Red, UI.Au, num * 2f) : UI.Lerp(UI.Au, UI.Green, (num - 0.5f) * 2f));
			Pen val = new Pen(Color.FromArgb(has ? 230 : 70, baseColor), thick);
			try
			{
				g.DrawArc(val, rectangleF, (float)(180 + i * 10), 10.5f);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		if (!has)
		{
			return;
		}
		double num2 = Math.PI * (1.0 - senti / 100.0);
		Pen val2 = new Pen(Color.White, 2f)
		{
			EndCap = (LineCap)2
		};
		try
		{
			g.DrawLine(val2, gc, new PointF(gc.X + (float)Math.Cos(num2) * (rad - thick), gc.Y - (float)Math.Sin(num2) * (rad - thick)));
			SolidBrush val3 = new SolidBrush(Color.White);
			try
			{
				g.FillEllipse((Brush)(object)val3, gc.X - 3f, gc.Y - 3f, 6f, 6f);
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private void Panel(Graphics g, RectangleF r)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Expected O, but got Unknown
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Expected O, but got Unknown
		SolidBrush val = new SolidBrush(Color.FromArgb(150, 0, 10, 4));
		try
		{
			g.FillRectangle((Brush)(object)val, r);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		Pen val2 = new Pen(Color.FromArgb(90, UI.Gold));
		try
		{
			g.DrawRectangle(val2, r.X, r.Y, r.Width, r.Height);
			SolidBrush val3 = new SolidBrush(UI.Gold);
			try
			{
				g.FillRectangle((Brush)(object)val3, r.X, r.Y, 10f, 2f);
				g.FillRectangle((Brush)(object)val3, r.X, r.Y, 2f, 10f);
				g.FillRectangle((Brush)(object)val3, r.Right - 10f, r.Bottom - 2f, 10f, 2f);
				g.FillRectangle((Brush)(object)val3, r.Right - 2f, r.Bottom - 10f, 2f, 10f);
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private static (int n, double win, double avgW, double avgL, double pf, double dd, double sum) Stats(IEnumerable<double> seq)
	{
		List<double> list = seq.ToList();
		if (list.Count == 0)
		{
			return (n: 0, win: 0.0, avgW: 0.0, avgL: 0.0, pf: 0.0, dd: 0.0, sum: 0.0);
		}
		List<double> list2 = list.Where((double x) => x > 0.0).ToList();
		List<double> list3 = list.Where((double x) => x < 0.0).ToList();
		double num = list2.Sum();
		double num2 = 0.0 - list3.Sum();
		double num3 = 0.0;
		double num4 = 0.0;
		double num5 = 0.0;
		foreach (double item in list)
		{
			num4 += item;
			num3 = Math.Max(num3, num4);
			num5 = Math.Max(num5, num3 - num4);
		}
		return (n: list.Count, win: (double)list2.Count / (double)list.Count * 100.0, avgW: (list2.Count > 0) ? (num / (double)list2.Count) : 0.0, avgL: (list3.Count > 0) ? ((0.0 - num2) / (double)list3.Count) : 0.0, pf: (num2 > 0.0) ? (num / num2) : ((double)((num > 0.0) ? 99 : 0)), dd: num5, sum: list.Sum());
	}

	private static TimeZoneInfo Tz(string winId, string ianaId, double fallback)
	{
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(winId);
		}
		catch
		{
		}
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
		}
		catch
		{
		}
		return TimeZoneInfo.CreateCustomTimeZone(winId, TimeSpan.FromHours(fallback), winId, winId);
	}

	private void Chart(Graphics g, RectangleF r)
	{
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_020c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Expected O, but got Unknown
		//IL_051a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0521: Expected O, but got Unknown
		//IL_035e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0365: Expected O, but got Unknown
		//IL_0552: Unknown result type (might be due to invalid IL or missing references)
		//IL_0559: Expected O, but got Unknown
		//IL_03d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dc: Expected O, but got Unknown
		//IL_082e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0835: Expected O, but got Unknown
		//IL_0890: Unknown result type (might be due to invalid IL or missing references)
		//IL_0897: Expected O, but got Unknown
		//IL_06cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_06d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_06d9: Expected O, but got Unknown
		List<double[]> value = null;
		snap?.Bars.TryGetValue(cfg.ChartTf, out value);
		if (value == null || value.Count < 2)
		{
			DrawCellText(g, "等待 EA K線資料… (請更新 MT4Recorder.mq4)", UI.F(11f), UI.Text2, r.X, r.Y, r.Width, r.Height, right: false);
			return;
		}
		float num = 64f;
		float num2 = 16f;
		RectangleF pr = new RectangleF(r.X + 4f, r.Y + 4f, r.Width - num - 8f, r.Height - num2 - 8f);
		int num3 = Math.Min(value.Count, Math.Max(10, (int)(pr.Width / 7f)));
		List<double[]> list = value.Skip(value.Count - num3).ToList();
		double hi = list.Max((double[] b) => Math.Max(b[2], Math.Max((b[5] > 0.0) ? b[5] : b[2], (b[6] > 0.0) ? b[6] : b[2])));
		double lo = list.Min((double[] b) => Math.Min(b[3], Math.Min((b[5] > 0.0) ? b[5] : b[3], (b[6] > 0.0) ? b[6] : b[3])));
		double num4 = snap?.Bid ?? 0.0;
		if (num4 > 0.0)
		{
			hi = Math.Max(hi, num4);
			lo = Math.Min(lo, num4);
		}
		double num5 = (hi - lo) * 0.06 + 1E-09;
		hi += num5;
		lo -= num5;
		int num6 = snap?.GoldDigits ?? 2;
		Pen val = new Pen(Color.FromArgb(28, UI.Gold))
		{
			DashStyle = (DashStyle)2
		};
		try
		{
			for (int i = 0; i <= 6; i++)
			{
				double v2 = lo + (hi - lo) * (double)i / 6.0;
				float num7 = Y(v2);
				g.DrawLine(val, pr.X, num7, pr.Right, num7);
				UI.Txt(g, v2.ToString("F" + num6, CultureInfo.InvariantCulture), UI.M(9.5f), UI.Text2, pr.Right + 4f, num7 - 7f);
			}
			float num8 = pr.Width / (float)num3;
			float num9 = Math.Max(1f, num8 * 0.62f);
			List<PointF> list2 = new List<PointF>();
			List<PointF> list3 = new List<PointF>();
			for (int j = 0; j < list.Count; j++)
			{
				double[] array = list[j];
				float num10 = pr.X + num8 * (float)j + num8 / 2f;
				bool flag = array[4] >= array[1];
				Color color = (flag ? UI.Green : UI.Red);
				Pen val2 = new Pen(color, 1f);
				try
				{
					g.DrawLine(val2, num10, Y(array[2]), num10, Y(array[3]));
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
				float num11 = Y(Math.Max(array[1], array[4]));
				float num12 = Y(Math.Min(array[1], array[4]));
				SolidBrush val3 = new SolidBrush(flag ? Color.FromArgb(200, color) : color);
				try
				{
					g.FillRectangle((Brush)(object)val3, num10 - num9 / 2f, num11, num9, Math.Max(1f, num12 - num11));
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
				if (array[5] > 0.0)
				{
					list2.Add(new PointF(num10, Y(array[5])));
				}
				if (array[6] > 0.0)
				{
					list3.Add(new PointF(num10, Y(array[6])));
				}
				if (j % Math.Max(1, num3 / 6) == 0 && array[0] > 0.0)
				{
					UI.Txt(g, DateTimeOffset.FromUnixTimeSeconds((long)array[0]).ToString((cfg.ChartTf == "M15") ? "HH:mm" : "MM/dd HH", CultureInfo.InvariantCulture), UI.M(9f), UI.Text2, num10 - 14f, pr.Bottom + 3f);
				}
			}
			if (list2.Count > 1)
			{
				Pen val4 = new Pen(UI.Cyan, 1.4f);
				try
				{
					g.DrawLines(val4, list2.ToArray());
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			if (list3.Count > 1)
			{
				Pen val5 = new Pen(UI.Au, 1.4f);
				try
				{
					g.DrawLines(val5, list3.ToArray());
				}
				finally
				{
					((IDisposable)val5)?.Dispose();
				}
			}
			UI.Txt(g, "EMA20", UI.M(9f, bold: true), UI.Cyan, pr.X + 4f, pr.Y + 2f);
			UI.Txt(g, "EMA50", UI.M(9f, bold: true), UI.Au, pr.X + 48f, pr.Y + 2f);
			string text = snap?.GoldSym ?? "";
			foreach (var item in snap?.Pos ?? new List<(string, string, double, double, double, double)>())
			{
				if (!(item.sym != text) && !(item.open < lo) && !(item.open > hi))
				{
					bool flag2 = item.type.StartsWith("BUY", StringComparison.Ordinal);
					Color color2 = (flag2 ? UI.Green : UI.Red);
					float num13 = Y(item.open);
					Pen val6 = new Pen(Color.FromArgb(170, color2), 1f)
					{
						DashStyle = (DashStyle)1
					};
					try
					{
						g.DrawLine(val6, pr.X, num13, pr.Right, num13);
					}
					finally
					{
						((IDisposable)val6)?.Dispose();
					}
					UI.Txt(g, $"{(flag2 ? "BUY" : "SELL")} {item.lots:0.00} @{item.open.ToString("F" + num6, CultureInfo.InvariantCulture)}  {UI.Signed(item.profit)}", UI.M(9f, bold: true), color2, pr.X + 4f, num13 - 13f);
				}
			}
			if (num4 > 0.0)
			{
				float num14 = Y(num4);
				Color color3 = UI.Lerp(UI.AuHi, flashCol, flash);
				Pen val7 = new Pen(color3, 1f);
				try
				{
					g.DrawLine(val7, pr.X, num14, pr.Right, num14);
				}
				finally
				{
					((IDisposable)val7)?.Dispose();
				}
				RectangleF rectangleF = new RectangleF(pr.Right + 1f, num14 - 8f, num, 16f);
				SolidBrush val8 = new SolidBrush(color3);
				try
				{
					g.FillRectangle((Brush)(object)val8, rectangleF);
				}
				finally
				{
					((IDisposable)val8)?.Dispose();
				}
				DrawCellText(g, num4.ToString("F" + num6, CultureInfo.InvariantCulture), UI.M(10f, bold: true), Color.Black, rectangleF.X, rectangleF.Y, rectangleF.Width, rectangleF.Height, right: false);
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		float Y(double v)
		{
			return (float)((double)pr.Bottom - (v - lo) / (hi - lo) * (double)pr.Height);
		}
	}

	private void DrawWide(Graphics g, float W, float H)
	{
		//IL_03a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ad: Invalid comparison between Unknown and I4
		//IL_0888: Unknown result type (might be due to invalid IL or missing references)
		//IL_088f: Expected O, but got Unknown
		//IL_08c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_08cd: Expected O, but got Unknown
		//IL_0971: Unknown result type (might be due to invalid IL or missing references)
		//IL_0978: Expected O, but got Unknown
		//IL_196b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1972: Expected O, but got Unknown
		//IL_1c84: Unknown result type (might be due to invalid IL or missing references)
		//IL_1c8b: Expected O, but got Unknown
		//IL_1ccc: Unknown result type (might be due to invalid IL or missing references)
		//IL_1cd3: Expected O, but got Unknown
		//IL_1f67: Unknown result type (might be due to invalid IL or missing references)
		//IL_1f6c: Unknown result type (might be due to invalid IL or missing references)
		//IL_1f75: Expected O, but got Unknown
		//IL_1f7a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1f81: Expected O, but got Unknown
		//IL_2d15: Unknown result type (might be due to invalid IL or missing references)
		//IL_2d1c: Expected O, but got Unknown
		//IL_3050: Unknown result type (might be due to invalid IL or missing references)
		//IL_3057: Expected O, but got Unknown
		//IL_324f: Unknown result type (might be due to invalid IL or missing references)
		//IL_3254: Unknown result type (might be due to invalid IL or missing references)
		//IL_325d: Expected O, but got Unknown
		//IL_3377: Unknown result type (might be due to invalid IL or missing references)
		//IL_337e: Expected O, but got Unknown
		//IL_33a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_33ae: Expected O, but got Unknown
		float num = 10f;
		float num2 = 10f;
		(Color, string) tuple = Status();
		Diamond(g, 19f, 19f, 8f);
		string s = "> ACCT_" + ((snap == null) ? "????????" : snap.Account) + " :: " + tuple.Item2.Split(' ')[0] + (Blink ? "_" : " ");
		UI.Glow(g, s, UI.M(13f, bold: true), tuple.Item1, 34f, 8f, 70);
		UI.Txt(g, (snap == null) ? "// 等待 EA 資料…" : ("// " + snap.Server + " · " + snap.Currency + " · MT4 TERMINAL HUD"), UI.M(9.5f), UI.Text2, 35f, 26f);
		float num3 = W * 0.36f;
		UI.Txt(g, string.IsNullOrEmpty(snap?.GoldSym) ? "XAUUSD" : snap.GoldSym, UI.M(11f, bold: true), UI.Au, num3, 6f);
		GlitchText(g, GoldText(), UI.M(24f, bold: true), UI.Lerp(UI.AuHi, flashCol, flash), num3 + 70f, 3f);
		if (ticks.Count >= 2)
		{
			List<(DateTime t, double v)> list = ticks;
			double num4 = list[list.Count - 1].v - ticks[0].v;
			UI.Txt(g, ((num4 >= 0.0) ? "▲" : "▼") + Math.Abs(num4).ToString("F" + (snap?.GoldDigits ?? 2), CultureInfo.InvariantCulture) + " 5m", UI.M(10f, bold: true), UI.PL(num4), num3, 22f);
		}
		if (snap != null && snap.Daily.TryGetValue("prevClose", out var value) && value > 0.0 && snap.Bid > 0.0)
		{
			double num5 = (snap.Bid - value) / value * 100.0;
			UI.Txt(g, $"日 {((num5 >= 0.0) ? "+" : "")}{num5:0.00}%", UI.M(10f, bold: true), UI.PL(num5), num3 + 230f, 10f);
		}
		float num6 = W - num - 22f;
		(string, string)[] array = new(string, string)[7]
		{
			("close", "✕"),
			("max", ((int)this.WindowState == 2) ? "❐" : "□"),
			("min", "–"),
			("menu", "⋯"),
			("hist", "▤"),
			("pin", "\ud83d\udccc"),
			("compact", "⤡")
		};
		for (int i = 0; i < array.Length; i++)
		{
			var (text, glyph) = array[i];
			IconBtn(g, text, glyph, num6, 8f, text == "pin" && this.TopMost);
			num6 -= 24f;
		}
		UI.Txt(g, DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " 台北", UI.M(11f, bold: true), UI.GoldHi, num6 - 120f, 12f, right: true, 110f);
		Dot(g, num6 - 136f, 20f, tuple.Item1);
		float num7 = 44f;
		float num8 = H - 24f;
		float num9 = (W - num * 2f - num2 * 2f) * 0.27f;
		float num10 = (W - num * 2f - num2 * 2f) * 0.45f;
		float num11 = W - num * 2f - num2 * 2f - num9 - num10;
		float num12 = num;
		float num13 = num12 + num9 + num2;
		float num14 = num13 + num10 + num2;
		float num15 = num7;
		bool flag = snap != null;
		double v2 = V("eq");
		double v3 = V("bal");
		double v4 = V("pl");
		double num16 = V("lots");
		double num17 = V("ml");
		double v5 = V("fl");
		double num18 = (snap?.Equity ?? 0.0) - (snap?.TodayPL ?? 0.0);
		double num19 = ((num18 > 0.0) ? ((snap?.TodayPL ?? 0.0) / num18 * 100.0) : 0.0);
		(string, string, Color)[] array2 = new(string, string, Color)[6]
		{
			("總金額 " + snap?.Currency, flag ? UI.Money(v2) : "—", UI.GoldHi),
			("餘額", flag ? UI.Money(v3) : "—", UI.GoldHi),
			("當日營利 " + (flag ? (((num19 >= 0.0) ? "+" : "") + num19.ToString("0.00", CultureInfo.InvariantCulture) + "%") : ""), flag ? UI.Signed(v4) : "—", flag ? UI.PL(v4) : UI.Gold),
			("浮動損益", flag ? UI.Signed(v5) : "—", flag ? UI.PL(v5) : UI.Gold),
			("持倉口數", flag ? (num16.ToString("0.00", CultureInfo.InvariantCulture) + $" ({snap.Pos.Count}筆)") : "—", UI.GoldHi),
			("保證金比例", (flag && snap.Margin > 0.0) ? (num17.ToString("#,##0", CultureInfo.InvariantCulture) + "%") : "—", (!flag || snap.Margin <= 0.0) ? UI.GoldHi : ((num17 < 150.0) ? UI.Red : ((num17 < 400.0) ? UI.Au : UI.Green)))
		};
		float num20 = num9 / 2f;
		float num21 = 44f;
		Pen val = new Pen(Color.FromArgb(80, UI.Gold));
		try
		{
			for (int j = 0; j < 6; j++)
			{
				RectangleF rectangleF = new RectangleF(num12 + (float)(j % 2) * num20, num15 + (float)(j / 2) * num21, num20, num21);
				SolidBrush val2 = new SolidBrush(Color.FromArgb(150, 0, 14, 6));
				try
				{
					g.FillRectangle((Brush)(object)val2, rectangleF);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
				g.DrawRectangle(val, rectangleF.X, rectangleF.Y, rectangleF.Width, rectangleF.Height);
				DrawCellText(g, array2[j].Item1, UI.F(9.5f), UI.Text2, rectangleF.X, rectangleF.Y + 1f, rectangleF.Width, 15f, right: false);
				Font f = UI.M(15f, bold: true);
				SolidBrush val3 = new SolidBrush(Color.FromArgb(55, array2[j].Item3));
				try
				{
					DrawCellText(g, array2[j].Item2, f, array2[j].Item3, rectangleF.X, rectangleF.Y + 16f, rectangleF.Width, 26f, right: false, (Brush)(object)val3);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
				DrawCellText(g, array2[j].Item2, f, array2[j].Item3, rectangleF.X, rectangleF.Y + 16f, rectangleF.Width, 26f, right: false);
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		num15 += num21 * 3f + 6f;
		(double, double, double) tuple3 = Realized();
		float num22 = (num9 - 6f) / 2f;
		BigPL(g, new RectangleF(num12, num15, num22, 70f), "平倉收益 · 今日", tuple3.Item1, flag, "7天 " + UI.Signed(tuple3.Item2) + "  月 " + UI.Signed(tuple3.Item3), 22f);
		BigPL(g, new RectangleF(num12 + num22 + 6f, num15, num22, 70f), $"浮動盈虧 · {snap?.Pos.Count ?? 0}筆", flag ? snap.Floating : 0.0, flag, $"口數 {snap?.TotalLots ?? 0.0:0.00}", 22f);
		num15 += 78f;
		float y = num15;
		object right2;
		if (eqs.Count < 2)
		{
			right2 = null;
		}
		else
		{
			List<(DateTime t, double v)> list2 = eqs;
			right2 = UI.Signed(list2[list2.Count - 1].v - eqs[0].v);
		}
		Color? rc;
		if (eqs.Count < 2)
		{
			rc = null;
		}
		else
		{
			List<(DateTime t, double v)> list3 = eqs;
			rc = UI.PL(list3[list3.Count - 1].v - eqs[0].v);
		}
		num15 = Title(g, "> EQUITY.TODAY", num12, y, num9, (string)right2, rc);
		RectangleF rectangleF2 = new RectangleF(num12, num15, num9, 84f);
		Panel(g, rectangleF2);
		if (eqs.Count >= 2)
		{
			RectangleF r2 = RectangleF.Inflate(rectangleF2, -6f, -8f);
			List<double> vals = eqs.Select(((DateTime t, double v) t) => t.v).ToList();
			List<(DateTime t, double v)> list4 = eqs;
			UI.Spark(g, r2, vals, (list4[list4.Count - 1].v >= eqs[0].v) ? UI.Gold : UI.Red);
		}
		else
		{
			DrawCellText(g, "每 5 秒取樣，累積中…", UI.F(10f), UI.Text2, rectangleF2.X, rectangleF2.Y, rectangleF2.Width, rectangleF2.Height, right: false);
		}
		num15 = rectangleF2.Bottom + 8f;
		if (snap != null && snap.ServerTime.Length >= 10)
		{
			snap.ServerTime.Substring(0, 10);
		}
		(int, double, double, double, double, double, double) tuple4 = Stats((snap?.ClosedToday ?? new List<(string, string, double, double, string)>()).Select(((string sym, string type, double lots, double profit, string time) c) => c.profit).Reverse());
		(int, double, double, double, double, double, double) tuple5 = Stats(from c in snap?.ClosedWeek ?? new List<(long, double, string, string, double)>()
			orderby c.t
			select c.profit);
		double num23 = 0.0;
		double num24 = 0.0;
		foreach (var eq in eqs)
		{
			num24 = Math.Max(num24, eq.v);
			num23 = Math.Max(num23, num24 - eq.v);
		}
		List<Cell[]> list5 = new List<Cell[]>();
		list5.Add(new Cell[3]
		{
			new Cell("交易筆數", UI.Text2, Mono: false),
			C(tuple4.Item1.ToString(), UI.GoldHi),
			C(tuple5.Item1.ToString(), UI.GoldHi)
		});
		list5.Add(new Cell[3]
		{
			new Cell("已實現", UI.Text2, Mono: false),
			C(UI.Signed(tuple4.Item7), UI.PL(tuple4.Item7)),
			C(UI.Signed(tuple5.Item7), UI.PL(tuple5.Item7))
		});
		list5.Add(new Cell[3]
		{
			new Cell("勝率", UI.Text2, Mono: false),
			C((tuple4.Item1 > 0) ? (tuple4.Item2.ToString("0.0") + "%") : "—", (tuple4.Item2 >= 50.0) ? UI.Green : UI.Red),
			C((tuple5.Item1 > 0) ? (tuple5.Item2.ToString("0.0") + "%") : "—", (tuple5.Item2 >= 50.0) ? UI.Green : UI.Red)
		});
		list5.Add(new Cell[3]
		{
			new Cell("平均獲利", UI.Text2, Mono: false),
			C(F2(tuple4.Item3), UI.Green),
			C(F2(tuple5.Item3), UI.Green)
		});
		list5.Add(new Cell[3]
		{
			new Cell("平均虧損", UI.Text2, Mono: false),
			C(F2(tuple4.Item4), UI.Red),
			C(F2(tuple5.Item4), UI.Red)
		});
		list5.Add(new Cell[3]
		{
			new Cell("獲利因子", UI.Text2, Mono: false),
			C((tuple4.Item1 > 0) ? tuple4.Item5.ToString("0.00") : "—", (tuple4.Item5 >= 1.0) ? UI.Green : UI.Red),
			C((tuple5.Item1 > 0) ? tuple5.Item5.ToString("0.00") : "—", (tuple5.Item5 >= 1.0) ? UI.Green : UI.Red)
		});
		list5.Add(new Cell[3]
		{
			new Cell("最大回撤", UI.Text2, Mono: false),
			C("-" + F2(Math.Max(tuple4.Item6, num23)), UI.Red),
			C("-" + F2(tuple5.Item6), UI.Red)
		});
		List<Cell[]> rows = list5;
		num15 = Title(g, "> STATS", num12, num15, num9, "回撤:今日含淨值曲線", UI.Text2);
		num15 = Table(g, num12, num15, num9, new float[3] { 1f, 1f, 1f }, new string[3] { "項目", "今日", "近7天" }, rows) + 8f;
		float num25 = (num8 - num15) * 0.55f;
		List<(string, string, double, double, double, double)> list6 = snap?.Pos ?? new List<(string, string, double, double, double, double)>();
		double v6 = list6.Sum<(string, string, double, double, double, double)>(((string sym, string type, double lots, double open, double cur, double profit) p) => p.profit);
		float num26 = Title(g, "> POSITIONS [" + list6.Count + "]", num12, num15, num9, (list6.Count > 0) ? UI.Signed(v6) : null, UI.PL(v6));
		int num27 = Math.Max(1, (int)((num15 + num25 - num26 - 17f) / 17f));
		List<Cell[]> list7 = list6.Select<(string, string, double, double, double, double), Cell[]>(delegate((string sym, string type, double lots, double open, double cur, double profit) p)
		{
			Color color3 = (p.type.StartsWith("BUY", StringComparison.Ordinal) ? UI.Green : UI.Red);
			return new Cell[5]
			{
				new Cell(p.sym, UI.GoldHi, Mono: true, Bold: true),
				new Cell(p.type, color3, Mono: true, Bold: true, Right: false, Glow: false, Color.FromArgb(45, color3)),
				new Cell(p.lots.ToString("0.00", CultureInfo.InvariantCulture), UI.GoldHi, Mono: true, Bold: false, Right: true),
				new Cell(p.open.ToString("0.#####", CultureInfo.InvariantCulture), UI.Text2, Mono: true, Bold: false, Right: true),
				new Cell(UI.Signed(p.profit), UI.PL(p.profit), Mono: true, Bold: true, Right: true, Glow: true)
			};
		}).ToList();
		if (list7.Count == 0)
		{
			list7.Add(new Cell[5]
			{
				new Cell((snap == null) ? "—" : "無持倉", UI.Text2, Mono: false),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2)
			});
		}
		if (list7.Count > num27)
		{
			int value2 = list7.Count - num27 + 1;
			list7 = list7.Take(num27 - 1).ToList();
			list7.Add(new Cell[5]
			{
				new Cell($"… +{value2}", UI.Text2, Mono: false),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2)
			});
		}
		Table(g, num12, num26, num9, new float[5] { 0.95f, 0.75f, 0.55f, 0.95f, 1f }, new string[5] { "商品", "方向", "口數", "開倉", "損益" }, list7);
		num15 += num25 + 6f;
		List<(string, string, double, double, string)> list8 = snap?.ClosedToday ?? new List<(string, string, double, double, string)>();
		float num28 = Title(g, "> CLOSED.TODAY [" + list8.Count + "]", num12, num15, num9, (list8.Count > 0) ? UI.Signed(list8.Sum<(string, string, double, double, string)>(((string sym, string type, double lots, double profit, string time) c) => c.profit)) : null, UI.PL(list8.Sum<(string, string, double, double, string)>(((string sym, string type, double lots, double profit, string time) c) => c.profit)));
		int count = Math.Max(1, (int)((num8 - num28 - 17f) / 17f));
		List<Cell[]> list9 = list8.OrderByDescending<(string, string, double, double, string), string>(((string sym, string type, double lots, double profit, string time) c) => c.time, StringComparer.Ordinal).Take(count).Select<(string, string, double, double, string), Cell[]>(delegate((string sym, string type, double lots, double profit, string time) c)
		{
			bool flag7 = c.type.StartsWith("BUY", StringComparison.Ordinal);
			return new Cell[5]
			{
				new Cell((c.time.Length > 11) ? c.time.Substring(11, Math.Min(5, c.time.Length - 11)) : c.time, UI.Text2),
				new Cell(c.sym, UI.GoldHi, Mono: true, Bold: true),
				new Cell(c.type, flag7 ? UI.Green : UI.Red, Mono: true, Bold: true),
				new Cell(c.lots.ToString("0.00", CultureInfo.InvariantCulture), UI.GoldHi, Mono: true, Bold: false, Right: true),
				new Cell(UI.Signed(c.profit), UI.PL(c.profit), Mono: true, Bold: true, Right: true, Glow: true)
			};
		})
			.ToList();
		if (list9.Count == 0)
		{
			list9.Add(new Cell[5]
			{
				new Cell("—", UI.Text2),
				new Cell("今日無平倉", UI.Text2, Mono: false),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2)
			});
		}
		Table(g, num12, num28, num9, new float[5] { 0.6f, 0.95f, 0.65f, 0.55f, 1f }, new string[5] { "時間", "商品", "方向", "口數", "損益" }, list9, 17f, num8);
		(List<Cell[]>, string, Color, int, double, double, string, bool) tuple6 = ScanData();
		float num29 = 233f;
		float num30 = num8 - num29 - 8f;
		float num31 = Title(g, "> CHART :: " + (string.IsNullOrEmpty(snap?.GoldSym) ? "XAUUSD" : snap.GoldSym) + " " + cfg.ChartTf, num13, num7, num10);
		string[] array3 = new string[3] { "M15", "H1", "H4" };
		for (int l = 0; l < 3; l++)
		{
			RectangleF rectangleF3 = new RectangleF(num13 + num10 - (float)((3 - l) * 40), num7, 38f, 15f);
			btns["tf" + array3[l]] = rectangleF3;
			bool flag2 = cfg.ChartTf == array3[l];
			SolidBrush val4 = new SolidBrush(Color.FromArgb(flag2 ? 200 : 25, UI.Gold));
			try
			{
				g.FillRectangle((Brush)(object)val4, rectangleF3);
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
			DrawCellText(g, array3[l], UI.M(9.5f, bold: true), flag2 ? Color.Black : UI.Gold, rectangleF3.X, rectangleF3.Y, rectangleF3.Width, rectangleF3.Height, right: false);
		}
		RectangleF r3 = new RectangleF(num13, num31 + 2f, num10, num30 - num31 - 2f);
		Panel(g, r3);
		Chart(g, r3);
		float y2 = num30 + 8f;
		float num32 = num10 * 0.56f;
		float y3 = Title(g, "> MARKET.SCAN", num13, y2, num32, tuple6.Rest.Item1 ? $"綜合 {tuple6.Item2} {tuple6.Item4}%" : "等待指標", tuple6.Rest.Item1 ? tuple6.Item3 : UI.Text2);
		y3 = Table(g, num13, y3, num32, new float[7] { 0.6f, 0.7f, 0.5f, 0.55f, 0.8f, 1f, 0.6f }, new string[7] { "週期", "訊號", "分數", "RSI", "MACD", "趨勢", "ATR" }, tuple6.Item1) + 6f;
		UI.Txt(g, "綜合建議", UI.F(10f), UI.Text2, num13, y3 + 4f);
		UI.Glow(g, tuple6.Rest.Item1 ? tuple6.Item2 : "—", UI.F(20f, bold: true), tuple6.Item3, num13 + 56f, y3 - 2f, 90);
		UI.Glow(g, tuple6.Rest.Item1 ? (tuple6.Item4 + "%") : "", UI.M(16f, bold: true), tuple6.Item3, num13 + 108f, y3 + 1f, 60);
		UI.Txt(g, "信心度", UI.F(9f), UI.Text2, num13 + 160f, y3 + 6f);
		float num33 = y3 + 34f;
		UI.Txt(g, "買賣量 H1·20根", UI.F(9.5f), UI.Text2, num13, num33);
		RectangleF rectangleF4 = new RectangleF(num13, num33 + 16f, num32 * 0.55f, 8f);
		SolidBrush val5 = new SolidBrush(UI.Green);
		try
		{
			g.FillRectangle((Brush)(object)val5, rectangleF4.X, rectangleF4.Y, (float)((double)rectangleF4.Width * tuple6.Item5), rectangleF4.Height);
		}
		finally
		{
			((IDisposable)val5)?.Dispose();
		}
		SolidBrush val6 = new SolidBrush(UI.Red);
		try
		{
			g.FillRectangle((Brush)(object)val6, rectangleF4.X + (float)((double)rectangleF4.Width * tuple6.Item5), rectangleF4.Y, (float)((double)rectangleF4.Width * (1.0 - tuple6.Item5)), rectangleF4.Height);
		}
		finally
		{
			((IDisposable)val6)?.Dispose();
		}
		UI.Txt(g, $"買 {tuple6.Item5 * 100.0:0}%", UI.M(10f, bold: true), UI.Green, rectangleF4.X, rectangleF4.Bottom + 2f);
		UI.Txt(g, $"賣 {(1.0 - tuple6.Item5) * 100.0:0}%", UI.M(10f, bold: true), UI.Red, rectangleF4.X, rectangleF4.Bottom + 2f, right: true, rectangleF4.Width);
		PointF gc = new PointF(num13 + num32 * 0.55f + num32 * 0.45f / 2f - 10f, num8 - 22f);
		float num34 = Math.Min(48f, Math.Min(num32 * 0.45f / 2f - 8f, num8 - y3 - 30f));
		Gauge(g, gc, num34, tuple6.Item6, tuple6.Rest.Item1, 7f);
		Color c2 = ((tuple6.Item6 >= 58.0) ? UI.Green : ((tuple6.Item6 <= 42.0) ? UI.Red : UI.Au));
		DrawCellText(g, tuple6.Rest.Item1 ? $"{tuple6.Item6:0} {tuple6.Item7}" : "情緒", UI.M(11f, bold: true), c2, gc.X - 60f, gc.Y + 2f, 120f, 16f, right: false);
		StringFormat val7 = new StringFormat
		{
			Alignment = (StringAlignment)1
		};
		try
		{
			SolidBrush val8 = new SolidBrush(UI.Text2);
			try
			{
				g.DrawString("情緒 · 指標推算", UI.F(9f), (Brush)(object)val8, new RectangleF(gc.X - 60f, gc.Y - num34 - 16f, 120f, 14f), val7);
			}
			finally
			{
				((IDisposable)val8)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val7)?.Dispose();
		}
		float x = num13 + num32 + 10f;
		float w = num10 - num32 - 10f;
		Dictionary<string, double> dd = snap?.Daily ?? new Dictionary<string, double>();
		int dg = snap?.GoldDigits ?? 2;
		double num35 = Dg("high") - Dg("low");
		double num36 = Dg("atrD1");
		Dictionary<string, double> value3;
		double num37 = ((snap?.Tech != null && snap.Tech.TryGetValue("H1", out value3)) ? value3.GetValueOrDefault("atr") : 0.0);
		double bidv = snap?.Bid ?? 0.0;
		list5 = new List<Cell[]>();
		list5.Add(new Cell[4]
		{
			new Cell("開盤", UI.Text2, Mono: false),
			C(P(Dg("open")), UI.GoldHi),
			new Cell("R3", UI.Red, Mono: true, Bold: true),
			C(P(Dg("r3")), PivC(Dg("r3")))
		});
		list5.Add(new Cell[4]
		{
			new Cell("最高", UI.Text2, Mono: false),
			C(P(Dg("high")), UI.Green),
			new Cell("R2", UI.Red, Mono: true, Bold: true),
			C(P(Dg("r2")), PivC(Dg("r2")))
		});
		list5.Add(new Cell[4]
		{
			new Cell("最低", UI.Text2, Mono: false),
			C(P(Dg("low")), UI.Red),
			new Cell("R1", UI.Red, Mono: true, Bold: true),
			C(P(Dg("r1")), PivC(Dg("r1")))
		});
		list5.Add(new Cell[4]
		{
			new Cell("日波幅", UI.Text2, Mono: false),
			C((num35 > 0.0) ? num35.ToString("F" + dg, CultureInfo.InvariantCulture) : "—", UI.GoldHi),
			new Cell("PP", UI.Au, Mono: true, Bold: true),
			C(P(Dg("pp")), UI.Au)
		});
		list5.Add(new Cell[4]
		{
			new Cell("ATR D1", UI.Text2, Mono: false),
			C((num36 > 0.0) ? num36.ToString("F" + dg, CultureInfo.InvariantCulture) : "—", UI.GoldHi),
			new Cell("S1", UI.Green, Mono: true, Bold: true),
			C(P(Dg("s1")), PivC(Dg("s1")))
		});
		list5.Add(new Cell[4]
		{
			new Cell("波幅/ATR", UI.Text2, Mono: false),
			C((num36 > 0.0) ? ((num35 / num36 * 100.0).ToString("0") + "%") : "—", (num36 > 0.0 && num35 / num36 > 1.0) ? UI.Red : UI.GoldHi),
			new Cell("S2", UI.Green, Mono: true, Bold: true),
			C(P(Dg("s2")), PivC(Dg("s2")))
		});
		list5.Add(new Cell[4]
		{
			new Cell("ATR H1", UI.Text2, Mono: false),
			C((num37 > 0.0) ? num37.ToString("F" + dg, CultureInfo.InvariantCulture) : "—", UI.GoldHi),
			new Cell("S3", UI.Green, Mono: true, Bold: true),
			C(P(Dg("s3")), PivC(Dg("s3")))
		});
		List<Cell[]> rows2 = list5;
		float y4 = Title(g, "> RANGE.PIVOT", x, y2, w, "昨日樞軸", UI.Text2);
		Table(g, x, y4, w, new float[4] { 0.85f, 1f, 0.45f, 1f }, new string[4] { "日內", "價位", "樞軸", "價位" }, rows2, 17f, num8);
		num15 = num7;
		DateTimeOffset utcNow = DateTimeOffset.UtcNow;
		TimeSpan tpe = TimeSpan.FromHours(8.0);
		List<Cell[]> list10 = new List<Cell[]>();
		(string, TimeZoneInfo, TimeSpan, TimeSpan)[] sessions = Sessions;
		for (int i = 0; i < sessions.Length; i++)
		{
			(string, TimeZoneInfo, TimeSpan, TimeSpan) tuple7 = sessions[i];
			string item = tuple7.Item1;
			TimeZoneInfo item2 = tuple7.Item2;
			TimeSpan item3 = tuple7.Item3;
			TimeSpan item4 = tuple7.Item4;
			DateTimeOffset dateTimeOffset = TimeZoneInfo.ConvertTime(utcNow, item2);
			DateTimeOffset dateTimeOffset2 = new DateTimeOffset(dateTimeOffset.Date, dateTimeOffset.Offset);
			DateTimeOffset dateTimeOffset3 = dateTimeOffset2 + item3;
			DateTimeOffset dateTimeOffset4 = dateTimeOffset2 + item4;
			bool flag3 = dateTimeOffset.DayOfWeek == DayOfWeek.Saturday || dateTimeOffset.DayOfWeek == DayOfWeek.Sunday;
			bool flag4 = !flag3 && dateTimeOffset >= dateTimeOffset3 && dateTimeOffset < dateTimeOffset4;
			TimeSpan timeSpan = (flag4 ? dateTimeOffset4 : ((dateTimeOffset < dateTimeOffset3 && !flag3) ? dateTimeOffset3 : dateTimeOffset3.AddDays((dateTimeOffset.DayOfWeek == DayOfWeek.Friday) ? 3 : ((dateTimeOffset.DayOfWeek != DayOfWeek.Saturday) ? 1 : 2)))) - dateTimeOffset;
			list10.Add(new Cell[4]
			{
				new Cell(item, UI.GoldHi, Mono: false, Bold: true),
				new Cell(flag4 ? "● 開盤" : "○ 休市", flag4 ? UI.Green : UI.Text2, Mono: false, Bold: true, Right: false, flag4, flag4 ? new Color?(Color.FromArgb(40, UI.Green)) : null),
				new Cell(dateTimeOffset3.ToOffset(tpe).ToString("HH:mm", CultureInfo.InvariantCulture) + "-" + dateTimeOffset4.ToOffset(tpe).ToString("HH:mm", CultureInfo.InvariantCulture), UI.Text2, Mono: true, Bold: false, Right: true),
				new Cell((flag4 ? "收 " : "開 ") + ((timeSpan.Days > 0) ? (timeSpan.Days + "d ") : "") + timeSpan.ToString("hh\\:mm"), flag4 ? UI.Au : UI.Text2, Mono: true, Bold: false, Right: true)
			});
		}
		num15 = Title(g, "> SESSIONS", num14, num15, num11, "台北時間", UI.Text2);
		num15 = Table(g, num14, num15, num11, new float[4] { 0.6f, 0.8f, 1.1f, 1f }, new string[4] { "市場", "狀態", "交易時段", "倒數" }, list10) + 8f;
		List<Cell[]> list11 = (snap?.Markets ?? new List<(string, string, string, double)>()).Select(((string name, string sym, string bid, double chg) m) => new Cell[4]
		{
			new Cell(m.name, UI.GoldHi, Mono: true, Bold: true),
			new Cell(m.sym, UI.Text2),
			new Cell(m.bid, UI.GoldHi, Mono: true, Bold: false, Right: true),
			new Cell(((m.chg >= 0.0) ? "+" : "") + m.chg.ToString("0.00", CultureInfo.InvariantCulture) + "%", UI.PL(m.chg), Mono: true, Bold: true, Right: true, Glow: true, Color.FromArgb(30, UI.PL(m.chg)))
		}).ToList();
		if (list11.Count == 0)
		{
			list11.Add(new Cell[4]
			{
				new Cell("—", UI.Text2),
				new Cell((snap?.Markets == null) ? "" : "券商無相關商品/請更新EA", UI.Text2, Mono: false),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2)
			});
		}
		num15 = Title(g, "> RELATED.MKTS", num14, num15, num11, "較昨收", UI.Text2);
		num15 = Table(g, num14, num15, num11, new float[4] { 0.8f, 0.9f, 1f, 0.8f }, new string[4] { "市場", "代碼", "價格", "漲跌" }, list11) + 8f;
		float num38 = Math.Max(120f, (num8 - num15) * 0.3f);
		float num39 = num8 - num38 - 8f;
		DateTimeOffset now = DateTimeOffset.Now;
		List<EconEvent> source = (from e in Econ.Events
			where !cfg.CalHigh || e.Stars == 3
			where e.T > now.AddHours(-1.0)
			orderby e.T
			select e).ToList();
		EconEvent econEvent = (from e in Econ.Events
			where e.Stars == 3 && e.T > now
			orderby e.T
			select e).FirstOrDefault();
		string right3 = ((econEvent == null) ? "本週無★★★" : ("★★★ T-" + (((econEvent.T - now).Days > 0) ? ((econEvent.T - now).Days + "d ") : "") + (econEvent.T - now).ToString("hh\\:mm\\:ss")));
		float num40 = Title(g, "> ECON.CALENDAR :: USD", num14, num15, num11 - 96f, right3, (econEvent == null) ? UI.Text2 : (Blink ? UI.Red : Color.FromArgb(200, UI.Red)));
		string[] array4 = new string[2] { "全部", "★★★" };
		for (int n = 0; n < 2; n++)
		{
			RectangleF rectangleF5 = new RectangleF(num14 + num11 - (float)((2 - n) * 44), num15, 42f, 15f);
			btns[(n == 0) ? "cal_all" : "cal_high"] = rectangleF5;
			bool flag5 = cfg.CalHigh == (n == 1);
			SolidBrush val9 = new SolidBrush(Color.FromArgb(flag5 ? 200 : 25, UI.Gold));
			try
			{
				g.FillRectangle((Brush)(object)val9, rectangleF5);
			}
			finally
			{
				((IDisposable)val9)?.Dispose();
			}
			DrawCellText(g, array4[n], UI.F(9f, bold: true), flag5 ? Color.Black : UI.Gold, rectangleF5.X, rectangleF5.Y, rectangleF5.Width, rectangleF5.Height, right: false);
		}
		int count2 = Math.Max(1, (int)((num39 - num40 - 17f) / 17f));
		EconEvent next = source.FirstOrDefault((EconEvent e) => e.T > now);
		List<Cell[]> list12 = source.Take(count2).Select(delegate(EconEvent e)
		{
			bool flag6 = e.T <= now;
			Color color2 = (flag6 ? Color.FromArgb(120, UI.Text2) : UI.GoldHi);
			Color c3 = ((e.Stars == 3) ? UI.Red : ((e.Stars == 2) ? UI.Au : UI.Text2));
			Color? bg = ((e == next) ? new Color?(Color.FromArgb(Blink ? 70 : 40, UI.Au)) : null);
			DateTimeOffset dateTimeOffset5 = e.T.ToOffset(tpe);
			return new Cell[6]
			{
				new Cell(dateTimeOffset5.ToString("MM/dd ddd HH:mm", CultureInfo.InvariantCulture), color2, Mono: true, e == next, Right: false, Glow: false, bg),
				new Cell(new string('★', e.Stars), c3, Mono: false, Bold: false, Right: false, e.Stars == 3, bg),
				new Cell(e.Zh, flag6 ? color2 : Color.White, Mono: false, e.Stars == 3, Right: false, Glow: false, bg),
				new Cell(Dash(e.Previous), flag6 ? color2 : UI.Text2, Mono: true, Bold: false, Right: true, Glow: false, bg),
				new Cell(Dash(e.Forecast), flag6 ? color2 : UI.Gold, Mono: true, Bold: true, Right: true, Glow: false, bg),
				new Cell(Dash(e.Actual), UI.GoldHi, Mono: true, Bold: true, Right: true, Glow: false, bg)
			};
		}).ToList();
		if (list12.Count == 0)
		{
			list12.Add(new Cell[6]
			{
				new Cell("—", UI.Text2),
				new Cell("", UI.Text2),
				new Cell((Econ.Events.Count == 0) ? "讀取中… (需網路)" : "近期無事件", UI.Text2, Mono: false),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2),
				new Cell("", UI.Text2)
			});
		}
		Table(g, num14, num40, num11, new float[6] { 1.25f, 0.45f, 1.9f, 0.6f, 0.6f, 0.6f }, new string[6] { "台灣時間", "★", "事件", "前值", "預期", "公布" }, list12, 17f, num39);
		float num41 = num39 + 8f;
		List<(string, double, double)> list13 = HistoryForm.Agg(histCache ?? (preview ? previewHist : new SortedDictionary<string, double[]>()), cfg.HistMode);
		double v7 = list13.Sum<(string, double, double)>(((string key, double eq, double pl) r) => r.pl);
		UI.Glow(g, "> PNL", UI.M(10.5f, bold: true), UI.Gold, num14, num41, 40);
		string[] array5 = new string[3] { "日", "週", "月" };
		for (int num42 = 0; num42 < 3; num42++)
		{
			RectangleF rectangleF6 = new RectangleF(num14 + 50f + (float)(num42 * 24), num41, 22f, 15f);
			btns["h" + num42] = rectangleF6;
			SolidBrush val10 = new SolidBrush(Color.FromArgb((cfg.HistMode == num42) ? 200 : 25, UI.Gold));
			try
			{
				g.FillRectangle((Brush)(object)val10, rectangleF6);
			}
			finally
			{
				((IDisposable)val10)?.Dispose();
			}
			DrawCellText(g, array5[num42], UI.F(9f, bold: true), (cfg.HistMode == num42) ? Color.Black : UI.Gold, rectangleF6.X - 1f, rectangleF6.Y, rectangleF6.Width + 2f, rectangleF6.Height, right: false);
		}
		UI.Txt(g, $"合計 {UI.Signed(v7)} · 勝 {list13.Count<(string, double, double)>(((string key, double eq, double pl) r) => r.pl > 0.0)}/{list13.Count}", UI.M(10f, bold: true), UI.PL(v7), num14, num41, right: true, num11);
		RectangleF rectangleF7 = new RectangleF(num14, num41 + 19f, num11, num8 - num41 - 19f);
		btns["hstrip"] = rectangleF7;
		Panel(g, rectangleF7);
		if (list13.Count > 0)
		{
			RectangleF rectangleF8 = new RectangleF(rectangleF7.X + 6f, rectangleF7.Y + 6f, rectangleF7.Width - 12f, rectangleF7.Height - 22f);
			double num43 = Math.Max(1.0, list13.Max<(string, double, double)>(((string key, double eq, double pl) r) => Math.Abs(r.pl)));
			float num44 = rectangleF8.Y + rectangleF8.Height / 2f;
			Pen val11 = new Pen(Color.FromArgb(70, UI.Gold))
			{
				DashStyle = (DashStyle)1
			};
			try
			{
				g.DrawLine(val11, rectangleF8.X, num44, rectangleF8.Right, num44);
			}
			finally
			{
				((IDisposable)val11)?.Dispose();
			}
			int num45 = Math.Max(list13.Count, (cfg.HistMode == 0) ? 30 : 12);
			float num46 = rectangleF8.Width / (float)num45;
			for (int num47 = 0; num47 < list13.Count; num47++)
			{
				float num48 = (float)(Math.Abs(list13[num47].Item3) / num43 * (double)(rectangleF8.Height / 2f - 2f));
				Color color = ((list13[num47].Item3 >= 0.0) ? UI.Green : UI.Red);
				RectangleF rectangleF9 = new RectangleF(rectangleF8.X + (float)num47 * num46 + num46 * 0.15f, (list13[num47].Item3 >= 0.0) ? (num44 - num48) : num44, Math.Max(1f, num46 * 0.7f), Math.Max(1f, num48));
				SolidBrush val12 = new SolidBrush(Color.FromArgb(45, color));
				try
				{
					g.FillRectangle((Brush)(object)val12, RectangleF.Inflate(rectangleF9, 2f, 2f));
				}
				finally
				{
					((IDisposable)val12)?.Dispose();
				}
				SolidBrush val13 = new SolidBrush(color);
				try
				{
					g.FillRectangle((Brush)(object)val13, rectangleF9);
				}
				finally
				{
					((IDisposable)val13)?.Dispose();
				}
				if (num47 % Math.Max(1, list13.Count / 6) == 0)
				{
					string item5 = list13[num47].Item1;
					UI.Txt(g, (item5.Length >= 10) ? item5.Substring(5, 5) : item5, UI.M(8.5f), UI.Text2, rectangleF9.X - 6f, rectangleF7.Bottom - 15f);
				}
			}
		}
		else
		{
			DrawCellText(g, "尚無收益紀錄", UI.F(10f), UI.Text2, rectangleF7.X, rectangleF7.Y, rectangleF7.Width, rectangleF7.Height, right: false);
		}
		DrawFooter(g, W, H, num, W - num * 2f);
		static Cell C(string t, Color c, bool right = true)
		{
			return new Cell(t, c, Mono: true, Bold: false, right);
		}
		double Dg(string k)
		{
			if (!dd.TryGetValue(k, out var value4))
			{
				return 0.0;
			}
			return value4;
		}
		static string F2(double v)
		{
			return v.ToString("#,##0.00", CultureInfo.InvariantCulture);
		}
		string P(double v)
		{
			if (!(v > 0.0))
			{
				return "—";
			}
			return v.ToString("F" + dg, CultureInfo.InvariantCulture);
		}
		Color PivC(double v)
		{
			if (!(bidv > 0.0) || !(v > 0.0))
			{
				return UI.GoldHi;
			}
			if (!(bidv >= v))
			{
				return UI.Red;
			}
			return UI.Green;
		}
	}

	private void DrawFooter(Graphics g, float W, float H, float pad, float cw)
	{
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Expected O, but got Unknown
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Expected O, but got Unknown
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Expected O, but got Unknown
		float num = H - 22f;
		UI.Txt(g, "α", UI.M(10f, bold: true), UI.Text2, pad, num + 2f);
		sliderR = new RectangleF(pad + 14f, num + 9f, 60f, 3f);
		SolidBrush val = new SolidBrush(Color.FromArgb(60, 255, 255, 255));
		try
		{
			g.FillRectangle((Brush)(object)val, sliderR);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		float num2 = (float)((this.Opacity - 0.3) / 0.7);
		SolidBrush val2 = new SolidBrush(UI.Gold);
		try
		{
			g.FillRectangle((Brush)(object)val2, sliderR.X, sliderR.Y, Math.Max(2f, sliderR.Width * num2), 3f);
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		SolidBrush val3 = new SolidBrush(UI.GoldHi);
		try
		{
			g.FillEllipse((Brush)(object)val3, sliderR.X + sliderR.Width * num2 - 5f, sliderR.Y - 4f, 10f, 10f);
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
		}
		UI.Txt(g, "⚠ 僅供參考，非投資建議", UI.F(9f), UI.Au, pad + 84f, num + 2f);
		UI.Txt(g, (snap == null) ? "" : ("SYNC " + ((snap.ServerTime.Length > 11) ? snap.ServerTime.Substring(11) : snap.ServerTime)), UI.M(9f), UI.Text2, pad, num + 3f, right: true, cw);
	}

	public static (int score, List<string> why) ScoreTf(string tf, Dictionary<string, double> d)
	{
		int num = 0;
		List<string> list = new List<string>();
		double num2 = G("close");
		double num3 = G("ema20");
		double num4 = G("ema50");
		double num5 = G("ema200");
		double num6 = G("macd");
		double num7 = G("macdSignal");
		double num8 = G("rsi");
		if (num2 <= 0.0)
		{
			return (score: 0, why: list);
		}
		num += ((num2 > num3) ? 1 : (-1));
		num += ((num3 > num4) ? 1 : (-1));
		num += ((num4 > num5) ? 1 : (-1));
		if (num2 > num3 && num3 > num4 && num4 > num5)
		{
			list.Add(tf + " 均線多頭排列");
		}
		else if (num2 < num3 && num3 < num4 && num4 < num5)
		{
			list.Add(tf + " 均線空頭排列");
		}
		else
		{
			list.Add(tf + ((num2 > num5) ? " 價在EMA200上" : " 價在EMA200下"));
		}
		num += ((num6 > num7) ? 1 : (-1));
		num += ((num6 > 0.0) ? 1 : (-1));
		list.Add(tf + ((num6 > num7) ? " MACD金叉" : " MACD死叉"));
		if (num8 > 55.0)
		{
			num++;
		}
		else if (num8 < 45.0)
		{
			num--;
		}
		list.Add(tf + $" RSI {num8:0}" + ((num8 >= 70.0) ? "超買" : ((num8 <= 30.0) ? "超賣" : "")));
		return (score: num, why: list);
		double G(string k)
		{
			if (!d.TryGetValue(k, out var value))
			{
				return 0.0;
			}
			return value;
		}
	}

	private static (string t, Color c) Sig(double v, double th)
	{
		if (!(v >= th))
		{
			if (!(v <= 0.0 - th))
			{
				return (t: "觀望", c: UI.Au);
			}
			return (t: "偏空", c: UI.Red);
		}
		return (t: "偏多", c: UI.Green);
	}

	private void DrawScan(Graphics g, float pad, float y, float cw, float ch)
	{
		//IL_03c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c7: Expected O, but got Unknown
		//IL_03d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d7: Expected O, but got Unknown
		//IL_064f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0656: Expected O, but got Unknown
		//IL_04dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e4: Expected O, but got Unknown
		//IL_0692: Unknown result type (might be due to invalid IL or missing references)
		//IL_0699: Expected O, but got Unknown
		//IL_0a3b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a42: Expected O, but got Unknown
		//IL_0a44: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a4b: Expected O, but got Unknown
		//IL_0adc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ae3: Expected O, but got Unknown
		RectangleF r = new RectangleF(pad, y, cw, Math.Max(240f, ch));
		UI.Card(g, r, UI.Gold, 0.3f);
		UI.Txt(g, "> MARKET.SCAN :: " + (string.IsNullOrEmpty(snap?.GoldSym) ? "XAUUSD" : snap.GoldSym), UI.M(11f, bold: true), UI.Gold, r.X + 10f, r.Y + 8f);
		Dictionary<string, Dictionary<string, double>> dictionary = snap?.Tech;
		if (dictionary == null || dictionary.Count == 0)
		{
			UI.Txt(g, "等待 EA 指標資料… (請更新 MT4Recorder.mq4)", UI.F(10.5f), UI.Text2, r.X + 10f, r.Y + 40f);
			return;
		}
		string[] array = new string[3] { "M15", "H1", "H4" };
		int[] array2 = new int[3] { 1, 2, 3 };
		double num = 0.0;
		List<(string, int, List<string>)> list = new List<(string, int, List<string>)>();
		for (int i = 0; i < 3; i++)
		{
			if (dictionary.TryGetValue(array[i], out var value))
			{
				(int score, List<string> why) tuple = ScoreTf(array[i], value);
				int item = tuple.score;
				List<string> item2 = tuple.why;
				num += (double)(item * array2[i]);
				list.Add((array[i], item, item2));
			}
		}
		double num2 = num / 36.0;
		(string t, Color c) tuple2 = Sig(num2, 0.15);
		string item3 = tuple2.t;
		Color item4 = tuple2.c;
		int value2 = (int)Math.Round(Math.Min(1.0, Math.Abs(num2)) * 100.0);
		float num3 = r.Y + 34f;
		UI.Txt(g, "綜合建議", UI.F(11f), UI.Text2, r.X + 10f, num3 + 8f);
		UI.Glow(g, item3, UI.F(26f, bold: true), item4, r.X + 70f, num3, 90);
		UI.Glow(g, $"{value2}%", UI.M(20f, bold: true), item4, r.X + 140f, num3 + 5f, 60);
		UI.Txt(g, "信心度", UI.F(9.5f), UI.Text2, r.X + 144f + g.MeasureString($"{value2}%", UI.M(20f, bold: true)).Width, num3 + 12f);
		num3 += 44f;
		foreach (var item9 in list)
		{
			string tf = item9.Item1;
			int item5 = item9.Item2;
			List<string> item6 = item9.Item3;
			(string t, Color c) tuple3 = Sig(item5, 2.0);
			string item7 = tuple3.t;
			Color item8 = tuple3.c;
			RectangleF r2 = new RectangleF(r.X + 8f, num3, r.Width - 16f, 34f);
			GraphicsPath val = UI.RR(r2, 3f);
			try
			{
				SolidBrush val2 = new SolidBrush(Color.FromArgb(22, item8));
				try
				{
					Pen val3 = new Pen(Color.FromArgb(110, item8));
					try
					{
						g.FillPath((Brush)(object)val2, val);
						g.DrawPath(val3, val);
					}
					finally
					{
						((IDisposable)val3)?.Dispose();
					}
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
			UI.Txt(g, tf, UI.M(11f, bold: true), UI.GoldHi, r2.X + 6f, r2.Y + 3f);
			UI.Txt(g, item7, UI.F(11f, bold: true), item8, r2.X + 6f, r2.Y + 17f);
			UI.Txt(g, ((item5 > 0) ? "+" : "") + item5 + "/6", UI.M(10f, bold: true), item8, r2.X + 44f, r2.Y + 3f);
			SolidBrush val4 = new SolidBrush(UI.Text2);
			try
			{
				g.DrawString(string.Join(" · ", item6.Select((string x) => x.Substring(tf.Length + 1))), UI.F(9.5f), (Brush)(object)val4, new RectangleF(r2.X + 84f, r2.Y + 3f, r2.Width - 90f, 30f));
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
			num3 += 38f;
		}
		num3 += 4f;
		double value3 = 0.0;
		double value4 = 0.0;
		if (dictionary.TryGetValue("H1", out var value5))
		{
			value5.TryGetValue("buyVol", out value3);
			value5.TryGetValue("sellVol", out value4);
		}
		double num4 = ((value3 + value4 > 0.0) ? (value3 / (value3 + value4)) : 0.5);
		UI.Txt(g, "買賣量 (H1 近20根)", UI.F(10f), UI.Text2, r.X + 10f, num3);
		RectangleF rectangleF = new RectangleF(r.X + 10f, num3 + 18f, r.Width - 20f, 10f);
		SolidBrush val5 = new SolidBrush(UI.Green);
		try
		{
			g.FillRectangle((Brush)(object)val5, rectangleF.X, rectangleF.Y, (float)((double)rectangleF.Width * num4), rectangleF.Height);
		}
		finally
		{
			((IDisposable)val5)?.Dispose();
		}
		SolidBrush val6 = new SolidBrush(UI.Red);
		try
		{
			g.FillRectangle((Brush)(object)val6, rectangleF.X + (float)((double)rectangleF.Width * num4), rectangleF.Y, (float)((double)rectangleF.Width * (1.0 - num4)), rectangleF.Height);
		}
		finally
		{
			((IDisposable)val6)?.Dispose();
		}
		UI.Txt(g, $"買 {num4 * 100.0:0}%", UI.M(10f, bold: true), UI.Green, rectangleF.X, rectangleF.Bottom + 2f);
		UI.Txt(g, $"賣 {(1.0 - num4) * 100.0:0}%", UI.M(10f, bold: true), UI.Red, rectangleF.X, rectangleF.Bottom + 2f, right: true, rectangleF.Width);
		num3 += 48f;
		double num5 = 0.0;
		int num6 = 0;
		string[] array3 = array;
		foreach (string key in array3)
		{
			if (dictionary.TryGetValue(key, out var value6))
			{
				double valueOrDefault = value6.GetValueOrDefault("rsi", 50.0);
				double valueOrDefault2 = value6.GetValueOrDefault("bbPos", 0.5);
				double valueOrDefault3 = value6.GetValueOrDefault("atr", 0.0);
				double num7 = ((valueOrDefault3 > 0.0) ? Math.Clamp((value6.GetValueOrDefault("macd") - value6.GetValueOrDefault("macdSignal")) / valueOrDefault3 * 2.0, -1.0, 1.0) : 0.0);
				num5 += 0.45 * valueOrDefault + 0.3 * Math.Clamp(valueOrDefault2, 0.0, 1.0) * 100.0 + 0.25 * (50.0 + num7 * 50.0);
				num6++;
			}
		}
		num5 = ((num6 > 0) ? Math.Clamp(num5 / (double)num6, 0.0, 100.0) : 50.0);
		string value7 = ((num5 >= 75.0) ? "極度貪婪" : ((num5 >= 58.0) ? "貪婪" : ((num5 > 42.0) ? "中性" : ((num5 > 25.0) ? "恐懼" : "極度恐懼"))));
		UI.Txt(g, "情緒指標 (指標推算 RSI/布林/動能)", UI.F(10f), UI.Text2, r.X + 10f, num3);
		RectangleF rectangleF2 = new RectangleF(r.X + 10f, num3 + 24f, r.Width - 20f, 8f);
		LinearGradientBrush val7 = new LinearGradientBrush(new PointF(rectangleF2.X - 1f, 0f), new PointF(rectangleF2.Right + 1f, 0f), UI.Red, UI.Green);
		try
		{
			ColorBlend val8 = new ColorBlend();
			val8.Colors = new Color[3]
			{
				UI.Red,
				UI.Au,
				UI.Green
			};
			val8.Positions = new float[3] { 0f, 0.5f, 1f };
			val7.InterpolationColors = val8;
			g.FillRectangle((Brush)(object)val7, rectangleF2);
		}
		finally
		{
			((IDisposable)val7)?.Dispose();
		}
		float num8 = rectangleF2.X + rectangleF2.Width * (float)(num5 / 100.0);
		SolidBrush val9 = new SolidBrush(Color.White);
		try
		{
			g.FillPolygon((Brush)(object)val9, new PointF[3]
			{
				new PointF(num8 - 5f, rectangleF2.Y - 7f),
				new PointF(num8 + 5f, rectangleF2.Y - 7f),
				new PointF(num8, rectangleF2.Y + 1f)
			});
		}
		finally
		{
			((IDisposable)val9)?.Dispose();
		}
		Color c = ((num5 >= 58.0) ? UI.Green : ((num5 <= 42.0) ? UI.Red : UI.Au));
		UI.Glow(g, $"{num5:0}  {value7}", UI.M(12f, bold: true), c, rectangleF2.X, rectangleF2.Bottom + 4f);
		UI.Txt(g, "恐懼", UI.F(9f), UI.Text2, rectangleF2.X, rectangleF2.Bottom + 22f);
		UI.Txt(g, "貪婪", UI.F(9f), UI.Text2, rectangleF2.X, rectangleF2.Bottom + 22f, right: true, rectangleF2.Width);
		UI.Txt(g, "⚠ 僅供參考，非投資建議", UI.F(10f), UI.Au, r.X + 10f, r.Bottom - 22f);
	}

	private void DrawCal(Graphics g, float pad, float y, float cw, float ch)
	{
		//IL_030b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0312: Expected O, but got Unknown
		//IL_031e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0325: Expected O, but got Unknown
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Expected O, but got Unknown
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Expected O, but got Unknown
		//IL_06aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_06b1: Expected O, but got Unknown
		//IL_06bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c2: Expected O, but got Unknown
		RectangleF r = new RectangleF(pad, y, cw, Math.Max(200f, ch));
		UI.Card(g, r, UI.Gold, 0.3f);
		UI.Txt(g, "> ECON.CALENDAR :: USD", UI.M(11f, bold: true), UI.Gold, r.X + 10f, r.Y + 8f);
		string[] array = new string[2] { "全部", "★★★" };
		for (int i = 0; i < 2; i++)
		{
			RectangleF rectangleF = new RectangleF(r.Right - 10f - (float)((2 - i) * 50), r.Y + 6f, 46f, 20f);
			btns[(i == 0) ? "cal_all" : "cal_high"] = rectangleF;
			bool flag = cfg.CalHigh == (i == 1);
			GraphicsPath val = UI.RR(rectangleF, 3f);
			try
			{
				SolidBrush val2 = new SolidBrush(Color.FromArgb(flag ? 190 : 20, UI.Gold));
				try
				{
					g.FillPath((Brush)(object)val2, val);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
				Pen val3 = new Pen(Color.FromArgb(130, UI.Gold));
				try
				{
					g.DrawPath(val3, val);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
				Font val4 = UI.F(10f, bold: true);
				SizeF sizeF = g.MeasureString(array[i], val4);
				UI.Txt(g, array[i], val4, flag ? Color.Black : UI.Gold, rectangleF.X + (46f - sizeF.Width) / 2f, rectangleF.Y + (20f - sizeF.Height) / 2f);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		DateTimeOffset now = DateTimeOffset.Now;
		TimeSpan tpe = TimeSpan.FromHours(8.0);
		List<EconEvent> list = (from e in Econ.Events
			where !cfg.CalHigh || e.Stars == 3
			where e.T.ToOffset(tpe).Date >= now.ToOffset(tpe).Date
			orderby e.T
			select e).ToList();
		EconEvent econEvent = (from e in Econ.Events
			where e.Stars == 3 && e.T > now
			orderby e.T
			select e).FirstOrDefault();
		EconEvent econEvent2 = list.FirstOrDefault((EconEvent e) => e.T > now);
		float num = r.Y + 32f;
		RectangleF r2 = new RectangleF(r.X + 8f, num, r.Width - 16f, 40f);
		GraphicsPath val5 = UI.RR(r2, 3f);
		try
		{
			SolidBrush val6 = new SolidBrush(Color.FromArgb(28, UI.Red));
			try
			{
				Pen val7 = new Pen(Color.FromArgb(120, UI.Red));
				try
				{
					g.FillPath((Brush)(object)val6, val5);
					g.DrawPath(val7, val5);
				}
				finally
				{
					((IDisposable)val7)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val6)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val5)?.Dispose();
		}
		if (econEvent != null)
		{
			TimeSpan timeSpan = econEvent.T - now;
			string text = ((timeSpan.Days > 0) ? (timeSpan.Days + "天 ") : "") + timeSpan.ToString("hh\\:mm\\:ss");
			UI.Txt(g, "下個★★★ " + econEvent.Zh, UI.F(10.5f, bold: true), UI.GoldHi, r2.X + 8f, r2.Y + 3f);
			UI.Glow(g, "T-" + text, UI.M(14f, bold: true), UI.Red, r2.X + 8f, r2.Y + 19f, Blink ? 80 : 30);
			UI.Txt(g, econEvent.T.ToOffset(tpe).ToString("MM/dd HH:mm", CultureInfo.InvariantCulture) + " 台北", UI.M(10f), UI.Text2, r2.X, r2.Y + 22f, right: true, r2.Width - 8f);
		}
		else
		{
			UI.Txt(g, "本週已無★★★數據", UI.F(10.5f), UI.Text2, r2.X + 8f, r2.Y + 12f);
		}
		num += 48f;
		UI.Txt(g, "時間(台北)", UI.F(9.5f, bold: true), UI.GoldDim, r.X + 10f, num);
		UI.Txt(g, "事件", UI.F(9.5f, bold: true), UI.GoldDim, r.X + 120f, num);
		UI.Txt(g, "前值/預測/公布", UI.F(9.5f, bold: true), UI.GoldDim, r.X, num, right: true, r.Width - 10f);
		num += 18f;
		float num2 = r.Bottom - 20f;
		g.SetClip(new RectangleF(r.X, num, r.Width, num2 - num));
		if (list.Count == 0)
		{
			UI.Txt(g, (Econ.Events.Count == 0) ? "讀取中… (需網路)" : "今天以後沒有事件", UI.F(10.5f), UI.Text2, r.X + 10f, num + 4f);
		}
		foreach (EconEvent item in list)
		{
			if (num + 34f > num2)
			{
				break;
			}
			bool flag2 = item.T <= now;
			bool num3 = item == econEvent2;
			RectangleF r3 = new RectangleF(r.X + 6f, num, r.Width - 12f, 32f);
			if (num3)
			{
				GraphicsPath val8 = UI.RR(r3, 3f);
				try
				{
					SolidBrush val9 = new SolidBrush(Color.FromArgb(Blink ? 40 : 25, UI.Gold));
					try
					{
						Pen val10 = new Pen(UI.Gold, 1.2f);
						try
						{
							g.FillPath((Brush)(object)val9, val8);
							g.DrawPath(val10, val8);
						}
						finally
						{
							((IDisposable)val10)?.Dispose();
						}
					}
					finally
					{
						((IDisposable)val9)?.Dispose();
					}
				}
				finally
				{
					((IDisposable)val8)?.Dispose();
				}
			}
			Color color = (flag2 ? Color.FromArgb(110, UI.Text2) : UI.GoldHi);
			DateTimeOffset dateTimeOffset = item.T.ToOffset(tpe);
			UI.Txt(g, dateTimeOffset.ToString("MM/dd", CultureInfo.InvariantCulture) + " " + (item.AllDay ? "全天" : dateTimeOffset.ToString("HH:mm", CultureInfo.InvariantCulture)), UI.M(10f, bold: true), color, r3.X + 4f, r3.Y + 2f);
			Color c = ((item.Stars == 3) ? UI.Red : ((item.Stars == 2) ? UI.Au : UI.Text2));
			UI.Txt(g, new string('★', item.Stars) + new string('☆', 3 - item.Stars), UI.F(10f), c, r3.X + 4f, r3.Y + 16f);
			UI.Txt(g, item.Zh, UI.F(10.5f, bold: true), flag2 ? color : Color.White, r3.X + 114f, r3.Y + 2f);
			UI.Txt(g, (item.Title.Length > 30) ? item.Title.Substring(0, 30) : item.Title, UI.M(8.5f), Color.FromArgb(120, UI.Text2), r3.X + 114f, r3.Y + 18f);
			string s = $"{Dash(item.Previous)} / {Dash(item.Forecast)} / {Dash(item.Actual)}";
			UI.Txt(g, s, UI.M(9.5f), flag2 ? color : UI.Gold, r3.X, r3.Y + 9f, right: true, r3.Width - 4f);
			num += 34f;
		}
		g.ResetClip();
		UI.Txt(g, "來源 ForexFactory · " + ((Econ.Updated == DateTime.MinValue) ? "未更新" : ("更新 " + Econ.Updated.ToString("HH:mm", CultureInfo.InvariantCulture))) + (Econ.LastError ? " (離線快取)" : ""), UI.F(9f), UI.Text2, r.X + 10f, r.Bottom - 18f);
	}

	private static string Dash(string v)
	{
		if (!string.IsNullOrWhiteSpace(v))
		{
			return v;
		}
		return "—";
	}

	private void ShowHistory()
	{
		HistoryForm historyForm = new HistoryForm(AccountHist(), cfg.HistMode, histNote);
		((Form)historyForm).TopMost = this.TopMost;
		((Form)historyForm).Icon = this.Icon;
		((Form)historyForm).Show((IWin32Window)(object)this);
	}

	public static void RenderPreview(string outDir)
	{
		Directory.CreateDirectory(outDir);
		Widget w = new Widget(previewMode: true);
		w.found = true;
		w.fileOk = true;
		w.stale = false;
		DateTime dateTime = new DateTime(2026, 10, 29, 15, 30, 0);
		Random random = new Random(7);
		double num = 2741.2;
		for (int i = 0; i < 150; i++)
		{
			num += (random.NextDouble() - 0.47) * 0.6;
			w.ticks.Add((dateTime.AddSeconds(-300 + i * 2), num));
		}
		double num2 = 10230.0;
		for (int j = 0; j < 200; j++)
		{
			num2 += (random.NextDouble() - 0.45) * 12.0;
			w.eqs.Add((dateTime.AddMinutes(-400 + j * 2), num2));
		}
		Snap snap = Parse("{\"account\":90000001,\"server\":\"DemoBroker-Synthetic\",\"currency\":\"USD\",\"balance\":10000.00,\"equity\":10412.55,\"margin\":820.40,\"freeMargin\":9592.15,\"totalLots\":0.80,\"floatingPL\":212.55,\"todayPL\":368.20,\"todayRealized\":155.65,\"positions\":[{\"symbol\":\"XAUUSD\",\"type\":\"BUY\",\"lots\":0.30,\"profit\":186.30},{\"symbol\":\"XAUUSD\",\"type\":\"SELL\",\"lots\":0.20,\"profit\":-42.10},{\"symbol\":\"EURUSD\",\"type\":\"BUY\",\"lots\":0.30,\"profit\":68.35}],\"gold\":{\"symbol\":\"XAUUSD\",\"bid\":" + num.ToString("F2", CultureInfo.InvariantCulture) + ",\"ask\":" + (num + 0.25).ToString("F2", CultureInfo.InvariantCulture) + "},\"serverTime\":\"2026.10.29 10:30:02\",\"tech\":{\"M15\":{\"close\":2741.2,\"rsi\":61,\"macd\":0.8,\"macdSignal\":0.5,\"ema20\":2739,\"ema50\":2736,\"ema200\":2720,\"atr\":3.1,\"bbPos\":0.78,\"buyVol\":5200,\"sellVol\":3900},\"H1\":{\"close\":2741.2,\"rsi\":57,\"macd\":2.1,\"macdSignal\":1.4,\"ema20\":2735,\"ema50\":2728,\"ema200\":2705,\"atr\":8.2,\"bbPos\":0.7,\"buyVol\":21000,\"sellVol\":15000},\"H4\":{\"close\":2741.2,\"rsi\":52,\"macd\":-1.0,\"macdSignal\":0.4,\"ema20\":2738,\"ema50\":2742,\"ema200\":2690,\"atr\":19,\"bbPos\":0.55,\"buyVol\":80000,\"sellVol\":76000}}}");
		w.snap = snap;
		w.lastBid = snap.Bid;
		foreach (KeyValuePair<string, double> item in new Dictionary<string, double>
		{
			["eq"] = snap.Equity,
			["bal"] = snap.Balance,
			["pl"] = snap.TodayPL,
			["lots"] = snap.TotalLots,
			["bid"] = snap.Bid,
			["ml"] = snap.Equity / snap.Margin * 100.0,
			["fl"] = snap.Floating
		})
		{
			w.cur[item.Key] = item.Value;
			w.tgt[item.Key] = item.Value;
		}
		w.flash = 0.6f;
		w.flashCol = UI.Green;
		SortedDictionary<string, double[]> sortedDictionary = new SortedDictionary<string, double[]>(StringComparer.Ordinal);
		for (int num3 = 29; num3 >= 0; num3--)
		{
			sortedDictionary[dateTime.AddDays(-num3).ToString("yyyy.MM.dd", CultureInfo.InvariantCulture)] = new double[3]
			{
				10000.0 + (double)num3 * 10.0,
				10000.0,
				Math.Round((random.NextDouble() - 0.4) * 300.0, 2)
			};
		}
		w.previewHist = sortedDictionary;
		Econ.LoadCache();
		Save("full.png", 420, 720, delegate(Graphics g)
		{
			w.Draw(g, 420f, 720f);
		});
		Save("wide.png", 1700, 1000, delegate(Graphics g)
		{
			w.Draw(g, 1700f, 1000f);
		});
		w.cfg.Compact = true;
		Save("compact.png", 340, 46, delegate(Graphics g)
		{
			w.Draw(g, 340f, 46f);
		});
		HistoryForm hf = new HistoryForm(sortedDictionary);
		Save("history.png", 420, 520, delegate(Graphics g)
		{
			hf.Draw(g, 420f, 520f);
		});
		void Save(string name, int W, int H, Action<Graphics> draw)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Expected O, but got Unknown
			Bitmap val = new Bitmap(W, H);
			try
			{
				Graphics val2 = Graphics.FromImage((Image)(object)val);
				try
				{
					draw(val2);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
				((Image)val).Save(Path.Combine(outDir, name), ImageFormat.Png);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
	}
}
