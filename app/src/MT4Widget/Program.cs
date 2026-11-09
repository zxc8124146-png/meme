using System;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		Application.SetHighDpiMode((HighDpiMode)3);
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);
		Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
		{
			Widget.Log(e.Exception);
		};
		AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
		{
			Widget.Log(e.ExceptionObject as Exception);
		};
		if (args.Length >= 2 && args[0] == "--render")
		{
			Widget.RenderPreview(args[1]);
		}
		else
		{
			Application.Run((Form)(object)new Widget());
		}
	}
}
