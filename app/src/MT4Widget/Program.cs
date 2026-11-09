using System;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		Widget.Startup("main");
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
		try
		{
			if (args.Length >= 2 && args[0] == "--render")
			{
				Widget.RenderPreview(args[1]);
			}
			else
			{
				Widget.Startup("run");
				Application.Run((Form)(object)new Widget());
				Widget.Startup("exit");
			}
		}
		catch (Exception ex)
		{
			Widget.Log(ex);
			throw;
		}
	}
}
