using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

internal class HistoryForm : Form
{
	private readonly SortedDictionary<string, double[]> all;

	private List<(string key, double eq, double pl)> rows = new List<(string, double, double)>();

	private int mode;

	private int scroll;

	private readonly RectangleF[] tabs = new RectangleF[3];

	private readonly string note;

	private readonly Timer blink = new Timer
	{
		Interval = 500
	};

	private bool on;

	private float K => (float)this.DeviceDpi / 96f;

	public HistoryForm(SortedDictionary<string, double[]> data, int startMode = 0, string note = null)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Expected O, but got Unknown
		all = data;
		this.note = note ?? "";
		mode = Math.Clamp(startMode, 0, 2);
		this.Text = "收益紀錄 :: PNL.LOG";
		this.AutoScaleMode = (AutoScaleMode)0;
		this.BackColor = UI.Bg2;
		this.StartPosition = (FormStartPosition)4;
		this.Size = new Size((int)(420f * K), (int)(540f * K));
		this.MinimumSize = new Size((int)(340f * K), (int)(380f * K));
		this.DoubleBuffered = true;
		this.SetStyle((ControlStyles)139282, true);
		Build();
		blink.Tick += delegate
		{
			on = !on;
			this.Invalidate();
		};
		this.FormClosed += (FormClosedEventHandler)delegate
		{
			((Component)(object)blink).Dispose();
		};
		blink.Start();
	}

	private static double LastKnownEq(IEnumerable<(string k, DateTime? d, double eq, double pl)> rows)
	{
		double eq = double.NaN;
		foreach (var row in rows)
		{
			if (!double.IsNaN(row.eq))
			{
				eq = row.eq;
			}
		}
		return eq;
	}

	private static DateTime? Dt(string k)
	{
		if (!DateTime.TryParseExact(k, new string[2] { "yyyy.MM.dd", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			return null;
		}
		return result;
	}

	public static List<(string key, double eq, double pl)> Agg(SortedDictionary<string, double[]> all, int mode)
	{
		List<(string, DateTime?, double, double)> source = all.Select((KeyValuePair<string, double[]> kv) => (k: kv.Key, d: Dt(kv.Key), eq: kv.Value[0], pl: kv.Value[2])).ToList();
		IEnumerable<(string, double, double)> source2 = (IEnumerable<(string, double, double)>)((mode != 0) ? ((IEnumerable)(from gr in source.GroupBy<(string, DateTime?, double, double), string>(delegate((string k, DateTime? d, double eq, double pl) x)
			{
				if (!x.d.HasValue)
				{
					return x.k;
				}
				DateTime value = x.d.Value;
				return (mode == 2) ? value.ToString("yyyy.MM", CultureInfo.InvariantCulture) : (value.AddDays(-((int)(value.DayOfWeek + 6) % 7)).ToString("yyyy.MM.dd", CultureInfo.InvariantCulture) + " 週");
			})
			select (Key: gr.Key, eq: LastKnownEq(gr), gr.Sum(((string k, DateTime? d, double eq, double pl) x) => double.IsNaN(x.pl) ? 0.0 : x.pl)))) : ((IEnumerable)source.Select<(string, DateTime?, double, double), (string, double, double)>(((string k, DateTime? d, double eq, double pl) x) => (k: x.k, eq: x.eq, pl: x.pl))));
		List<(string, double, double)> list = source2.ToList();
		int num = ((mode == 0) ? 30 : 12);
		return list.Skip(Math.Max(0, list.Count - num)).ToList();
	}

	private void Build()
	{
		rows = Agg(all, mode);
		scroll = 0;
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		scroll = Math.Clamp(scroll - Math.Sign(e.Delta), 0, Math.Max(0, rows.Count - 1));
		this.Invalidate();
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		PointF pt = new PointF((float)e.X / K, (float)e.Y / K);
		for (int i = 0; i < 3; i++)
		{
			if (tabs[i].Contains(pt) && mode != i)
			{
				mode = i;
				Build();
				this.Invalidate();
			}
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		PointF p = new PointF((float)e.X / K, (float)e.Y / K);
		this.Cursor = (tabs.Any((RectangleF t) => t.Contains(p)) ? Cursors.Hand : Cursors.Default);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		try
		{
			float k = K;
			e.Graphics.ScaleTransform(k, k);
			Draw(e.Graphics, (float)this.ClientSize.Width / k, (float)this.ClientSize.Height / k);
		}
		catch (Exception e2)
		{
			Widget.Log(e2);
		}
	}

	public void Draw(Graphics g, float W, float H)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Expected O, but got Unknown
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Expected O, but got Unknown
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Expected O, but got Unknown
		//IL_0585: Unknown result type (might be due to invalid IL or missing references)
		//IL_058a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0593: Expected O, but got Unknown
		//IL_068e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0695: Expected O, but got Unknown
		//IL_06be: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c5: Expected O, but got Unknown
		g.SmoothingMode = (SmoothingMode)4;
		g.TextRenderingHint = (TextRenderingHint)3;
		LinearGradientBrush val = new LinearGradientBrush(new PointF(0f, -1f), new PointF(0f, H + 1f), UI.Bg1, UI.Bg2);
		try
		{
			g.FillRectangle((Brush)(object)val, 0f, 0f, W, H);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		float num = 12f;
		float width = W - num * 2f;
		string value = ((mode == 0) ? "天" : ((mode == 1) ? "週" : "月"));
		UI.Glow(g, "> PNL.LOG --" + ((mode == 0) ? "daily" : ((mode == 1) ? "weekly" : "monthly")) + (on ? "_" : " "), UI.M(12f, bold: true), UI.Gold, num, num, 60);
		string[] array = new string[3] { "日", "週", "月" };
		float num2 = W - num - 102f;
		for (int i = 0; i < 3; i++)
		{
			tabs[i] = new RectangleF(num2 + (float)(i * 34), num - 3f, 32f, 22f);
			GraphicsPath val2 = UI.RR(tabs[i], 3f);
			try
			{
				SolidBrush val3 = new SolidBrush(Color.FromArgb((i == mode) ? 200 : 25, UI.Gold));
				try
				{
					g.FillPath((Brush)(object)val3, val2);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
				Pen val4 = new Pen(Color.FromArgb(140, UI.Gold));
				try
				{
					g.DrawPath(val4, val2);
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
				Font val5 = UI.F(12f, bold: true);
				SizeF sizeF = g.MeasureString(array[i], val5);
				UI.Txt(g, array[i], val5, (i == mode) ? Color.Black : UI.Gold, tabs[i].X + (32f - sizeF.Width) / 2f, tabs[i].Y + (22f - sizeF.Height) / 2f);
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		double v = rows.Sum(((string key, double eq, double pl) r) => r.pl);
		int value2 = rows.Count(((string key, double eq, double pl) r) => r.pl > 0.0);
		RectangleF r2 = new RectangleF(num, num + 30f, width, 64f);
		UI.Card(g, r2, UI.PL(v), 0.4f);
		UI.Txt(g, $"[ 近 {rows.Count} {value}合計已實現 ]", UI.F(11f), UI.Text2, r2.X + 10f, r2.Y + 8f);
		UI.Glow(g, UI.Signed(v), UI.M(24f, bold: true), UI.PL(v), r2.X + 10f, r2.Y + 28f, 70);
		UI.Txt(g, (rows.Count > 0) ? $"獲利 {value2}/{rows.Count} {value}" : "尚無紀錄", UI.F(10.5f, bold: true), UI.GoldDim, r2.X, r2.Y + 10f, right: true, r2.Width - 12f);
		RectangleF r3 = new RectangleF(num, r2.Bottom + 10f, width, 150f);
		UI.Card(g, r3, UI.Gold, 0.25f);
		UI.Txt(g, "> 損益柱狀圖", UI.M(10.5f), UI.Text2, r3.X + 10f, r3.Y + 6f);
		if (rows.Count > 0)
		{
			RectangleF rectangleF = new RectangleF(r3.X + 10f, r3.Y + 26f, r3.Width - 20f, r3.Height - 36f);
			double num3 = Math.Max(1.0, rows.Max(((string key, double eq, double pl) r) => Math.Abs(r.pl)));
			float num4 = rectangleF.Y + rectangleF.Height / 2f;
			Pen val6 = new Pen(Color.FromArgb(70, UI.Gold), 1f)
			{
				DashStyle = (DashStyle)1
			};
			try
			{
				g.DrawLine(val6, rectangleF.X, num4, rectangleF.Right, num4);
			}
			finally
			{
				((IDisposable)val6)?.Dispose();
			}
			float num5 = rectangleF.Width / (float)Math.Max(rows.Count, (mode == 0) ? 30 : 12);
			for (int j = 0; j < rows.Count; j++)
			{
				double item = rows[j].pl;
				float num6 = (float)(Math.Abs(item) / num3 * (double)(rectangleF.Height / 2f - 2f));
				RectangleF rectangleF2 = new RectangleF(rectangleF.X + (float)j * num5 + num5 * 0.15f, (item >= 0.0) ? (num4 - num6) : num4, num5 * 0.7f, Math.Max(1f, num6));
				Color color = ((item >= 0.0) ? UI.Green : UI.Red);
				SolidBrush val7 = new SolidBrush(Color.FromArgb(40, color));
				try
				{
					g.FillRectangle((Brush)(object)val7, RectangleF.Inflate(rectangleF2, 2f, 2f));
				}
				finally
				{
					((IDisposable)val7)?.Dispose();
				}
				SolidBrush val8 = new SolidBrush(color);
				try
				{
					g.FillRectangle((Brush)(object)val8, rectangleF2);
				}
				finally
				{
					((IDisposable)val8)?.Dispose();
				}
			}
		}
		float noteH = string.IsNullOrEmpty(note) ? 0f : 78f;
		RectangleF rectangleF3 = new RectangleF(num, r3.Bottom + 10f, width, Math.Max(40f, H - r3.Bottom - 22f - noteH));
		UI.Card(g, rectangleF3, UI.Gold, 0.2f);
		float num7 = rectangleF3.Y + 8f;
		UI.Txt(g, (mode == 0) ? "日期" : ((mode == 1) ? "週 (起始日)" : "月份"), UI.F(10f, bold: true), UI.GoldDim, rectangleF3.X + 12f, num7);
		UI.Txt(g, "期末淨值", UI.F(10f, bold: true), UI.GoldDim, rectangleF3.X + rectangleF3.Width * 0.42f, num7);
		UI.Txt(g, "已實現", UI.F(10f, bold: true), UI.GoldDim, rectangleF3.X, num7, right: true, rectangleF3.Width - 12f);
		num7 += 22f;
		g.SetClip(rectangleF3);
		foreach (var item2 in Enumerable.Reverse(rows).Skip(scroll))
		{
			if (num7 + 22f > rectangleF3.Bottom)
			{
				break;
			}
			UI.Txt(g, item2.key, UI.M(11f), UI.GoldHi, rectangleF3.X + 12f, num7);
			UI.Txt(g, double.IsNaN(item2.eq) ? "—" : UI.Money(item2.eq), UI.M(11f), UI.Au, rectangleF3.X + rectangleF3.Width * 0.42f, num7);
			UI.Txt(g, UI.Signed(item2.pl), UI.M(11f, bold: true), UI.PL(item2.pl), rectangleF3.X, num7, right: true, rectangleF3.Width - 12f);
			num7 += 22f;
		}
		g.ResetClip();
		if (!string.IsNullOrEmpty(note))
		{
			SolidBrush noteBrush = new SolidBrush(UI.Text2);
			try
			{
				g.DrawString(note, UI.F(8f), (Brush)(object)noteBrush, new RectangleF(num, H - 74f, width, 70f));
			}
			finally
			{
				((IDisposable)noteBrush)?.Dispose();
			}
		}
		UI.Scan(g, W, H);
	}
}
