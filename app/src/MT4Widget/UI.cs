using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;

internal static class UI
{
	public static readonly Color Gold = Color.FromArgb(0, 255, 136);

	public static readonly Color Au = Color.FromArgb(255, 204, 51);

	public static readonly Color AuHi = Color.FromArgb(255, 236, 150);

	public static readonly Color Cyan = Color.FromArgb(0, 230, 255);

	public static readonly Color GoldHi = Color.FromArgb(170, 255, 200);

	public static readonly Color GoldDim = Color.FromArgb(40, 170, 90);

	public static readonly Color Text2 = Color.FromArgb(110, 160, 125);

	public static readonly Color Green = Color.FromArgb(57, 255, 20);

	public static readonly Color Red = Color.FromArgb(255, 77, 100);

	public static readonly Color Bg1 = Color.FromArgb(2, 10, 5);

	public static readonly Color Bg2 = Color.FromArgb(0, 0, 0);

	private const string FontName = "Microsoft JhengHei UI";

	private static readonly Dictionary<(float, bool), Font> fonts = new Dictionary<(float, bool), Font>();

	private static readonly Dictionary<(float, bool), Font> mfonts = new Dictionary<(float, bool), Font>();

	public static Font F(float px, bool bold = false)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected O, but got Unknown
		if (!fonts.TryGetValue((px, bold), out var value))
		{
			try
			{
				value = new Font("Microsoft JhengHei UI", px, (FontStyle)(bold ? 1 : 0), (GraphicsUnit)2);
			}
			catch
			{
				value = new Font(FontFamily.GenericSansSerif, px, (FontStyle)(bold ? 1 : 0), (GraphicsUnit)2);
			}
			fonts[(px, bold)] = value;
		}
		return value;
	}

	public static Font M(float px, bool bold = false)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected O, but got Unknown
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Expected O, but got Unknown
		if (!mfonts.TryGetValue((px, bold), out var value))
		{
			string[] array = new string[3] { "Cascadia Mono", "Consolas", "Courier New" };
			foreach (string text in array)
			{
				try
				{
					value = new Font(text, px, (FontStyle)(bold ? 1 : 0), (GraphicsUnit)2);
					if (value.Name == text)
					{
						break;
					}
					value.Dispose();
					value = null;
					continue;
				}
				catch
				{
					value = null;
					continue;
				}
			}
			if (value == null)
			{
				value = new Font(FontFamily.GenericMonospace, px, (FontStyle)(bold ? 1 : 0), (GraphicsUnit)2);
			}
			mfonts[(px, bold)] = value;
		}
		return value;
	}

	public static void Scan(Graphics g, float W, float H)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Expected O, but got Unknown
		Pen val = new Pen(Color.FromArgb(38, 0, 0, 0), 1f);
		try
		{
			for (float num = 0f; num < H; num += 3f)
			{
				g.DrawLine(val, 0f, num, W, num);
			}
			PathGradientBrush val2 = new PathGradientBrush(new PointF[4]
			{
				new PointF(0f, 0f),
				new PointF(W, 0f),
				new PointF(W, H),
				new PointF(0f, H)
			});
			val2.CenterColor = Color.FromArgb(0, 0, 0, 0);
			val2.SurroundColors = new Color[1] { Color.FromArgb(110, 0, 0, 0) };
			PathGradientBrush val3 = val2;
			try
			{
				g.FillRectangle((Brush)(object)val3, 0f, 0f, W, H);
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public static GraphicsPath RR(RectangleF r, float rad)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Expected O, but got Unknown
		GraphicsPath val = new GraphicsPath();
		float num = Math.Max(1f, Math.Min(rad * 2f, Math.Min(r.Width, r.Height)));
		val.AddArc(r.X, r.Y, num, num, 180f, 90f);
		val.AddArc(r.Right - num, r.Y, num, num, 270f, 90f);
		val.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
		val.AddArc(r.X, r.Bottom - num, num, num, 90f, 90f);
		val.CloseFigure();
		return val;
	}

	public static void Card(Graphics g, RectangleF r, Color accent, float glow = 0.35f)
	{
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Expected O, but got Unknown
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Expected O, but got Unknown
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Expected O, but got Unknown
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ec: Expected O, but got Unknown
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Expected O, but got Unknown
		if (r.Width < 4f || r.Height < 4f)
		{
			return;
		}
		for (int num = 4; num >= 1; num--)
		{
			GraphicsPath val = RR(RectangleF.Inflate(r, (float)num * 1.5f, (float)num * 1.5f), 4f + (float)num * 1.5f);
			try
			{
				Pen val2 = new Pen(Color.FromArgb((int)(14f * glow * (float)(5 - num)), accent), 1.5f);
				try
				{
					g.DrawPath(val2, val);
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
		GraphicsPath val3 = RR(r, 4f);
		try
		{
			LinearGradientBrush val4 = new LinearGradientBrush(new PointF(r.X, r.Y - 1f), new PointF(r.X, r.Bottom + 1f), Color.FromArgb(200, 4, 22, 10), Color.FromArgb(215, 0, 6, 2));
			try
			{
				g.FillPath((Brush)(object)val4, val3);
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
			RectangleF r2 = new RectangleF(r.X + 1f, r.Y + 1f, r.Width - 2f, Math.Max(2f, r.Height * 0.45f));
			GraphicsPath val5 = RR(r2, 3f);
			try
			{
				LinearGradientBrush val6 = new LinearGradientBrush(new PointF(0f, r2.Y - 1f), new PointF(0f, r2.Bottom + 1f), Color.FromArgb(14, 0, 255, 136), Color.FromArgb(0, 0, 255, 136));
				try
				{
					g.FillPath((Brush)(object)val6, val5);
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
			Pen val7 = new Pen(Color.FromArgb(110, accent), 1f);
			try
			{
				g.DrawPath(val7, val3);
			}
			finally
			{
				((IDisposable)val7)?.Dispose();
			}
			SolidBrush val8 = new SolidBrush(accent);
			try
			{
				g.FillRectangle((Brush)(object)val8, r.X, r.Y, 8f, 2f);
				g.FillRectangle((Brush)(object)val8, r.X, r.Y, 2f, 8f);
				g.FillRectangle((Brush)(object)val8, r.Right - 8f, r.Bottom - 2f, 8f, 2f);
				g.FillRectangle((Brush)(object)val8, r.Right - 2f, r.Bottom - 8f, 2f, 8f);
			}
			finally
			{
				((IDisposable)val8)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
		}
	}

	public static void Txt(Graphics g, string s, Font f, Color c, float x, float y, bool right = false, float w = 0f)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected O, but got Unknown
		SolidBrush val = new SolidBrush(c);
		try
		{
			if (right)
			{
				g.DrawString(s, f, (Brush)(object)val, x + w - g.MeasureString(s, f).Width, y);
			}
			else
			{
				g.DrawString(s, f, (Brush)(object)val, x, y);
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public static void Glow(Graphics g, string s, Font f, Color c, float x, float y, int strength = 50, bool right = false, float w = 0f)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected O, but got Unknown
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Expected O, but got Unknown
		if (right)
		{
			x = x + w - g.MeasureString(s, f).Width;
		}
		SolidBrush val = new SolidBrush(Color.FromArgb(Math.Clamp(strength / 3, 0, 255), c));
		try
		{
			(float, float)[] array = new(float, float)[4]
			{
				(-1.5f, 0f),
				(1.5f, 0f),
				(0f, -1.5f),
				(0f, 1.5f)
			};
			for (int i = 0; i < array.Length; i++)
			{
				var (num, num2) = array[i];
				g.DrawString(s, f, (Brush)(object)val, x + num, y + num2);
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		SolidBrush val2 = new SolidBrush(c);
		try
		{
			g.DrawString(s, f, (Brush)(object)val2, x, y);
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	public static string Money(double v)
	{
		return v.ToString("#,##0.00", CultureInfo.InvariantCulture);
	}

	public static string Signed(double v)
	{
		return ((v >= 0.0) ? "+" : "−") + Money(Math.Abs(v));
	}

	public static Color PL(double v)
	{
		if (!(v >= 0.0))
		{
			return Red;
		}
		return Green;
	}

	public static Color Lerp(Color a, Color b, float t)
	{
		t = Math.Clamp(t, 0f, 1f);
		return Color.FromArgb((int)((float)(int)a.A + (float)(b.A - a.A) * t), (int)((float)(int)a.R + (float)(b.R - a.R) * t), (int)((float)(int)a.G + (float)(b.G - a.G) * t), (int)((float)(int)a.B + (float)(b.B - a.B) * t));
	}

	public static void Spark(Graphics g, RectangleF r, IList<double> vals, Color c, bool fill = true)
	{
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Expected O, but got Unknown
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Expected O, but got Unknown
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Expected O, but got Unknown
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Expected O, but got Unknown
		//IL_020d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Expected O, but got Unknown
		if (vals.Count < 2 || r.Width < 4f || r.Height < 4f)
		{
			return;
		}
		double num = vals.Min();
		double num2 = vals.Max();
		if (num2 - num < 1E-09)
		{
			num2 += 0.5;
			num -= 0.5;
		}
		PointF[] array = new PointF[vals.Count];
		for (int i = 0; i < vals.Count; i++)
		{
			array[i] = new PointF(r.X + r.Width * (float)i / (float)(vals.Count - 1), (float)((double)r.Bottom - (vals[i] - num) / (num2 - num) * (double)r.Height));
		}
		if (fill)
		{
			GraphicsPath val = new GraphicsPath();
			try
			{
				val.AddLines(array);
				val.AddLine(array[^1], new PointF(r.Right, r.Bottom + 2f));
				val.AddLine(new PointF(r.Right, r.Bottom + 2f), new PointF(r.X, r.Bottom + 2f));
				val.CloseFigure();
				LinearGradientBrush val2 = new LinearGradientBrush(new PointF(0f, r.Y - 1f), new PointF(0f, r.Bottom + 3f), Color.FromArgb(90, c), Color.FromArgb(0, c));
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
		Pen val3 = new Pen(Color.FromArgb(50, c), 5f)
		{
			LineJoin = (LineJoin)2
		};
		try
		{
			g.DrawLines(val3, array);
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
		}
		Pen val4 = new Pen(c, 1.8f)
		{
			LineJoin = (LineJoin)2
		};
		try
		{
			g.DrawLines(val4, array);
		}
		finally
		{
			((IDisposable)val4)?.Dispose();
		}
		SolidBrush val5 = new SolidBrush(c);
		try
		{
			g.FillEllipse((Brush)(object)val5, array[^1].X - 3f, array[^1].Y - 3f, 6f, 6f);
		}
		finally
		{
			((IDisposable)val5)?.Dispose();
		}
	}
}
