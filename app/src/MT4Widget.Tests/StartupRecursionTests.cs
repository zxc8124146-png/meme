using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

public class StartupRecursionTests
{
	private static readonly string[] SelfCalls =
	{
		"this.CreateParams",
		"this.OnLoad(",
		"this.WndProc(",
		"this.OnKeyDown(",
		"this.OnResize(",
		"this.OnResizeEnd(",
		"this.OnMove(",
		"this.OnDpiChanged(",
		"this.OnFormClosing(",
		"this.OnMouseMove(",
		"this.OnMouseLeave(",
		"this.OnMouseDown(",
		"this.OnMouseUp("
	};

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

	[Fact]
	public void RecoveredOverrides_CallBaseInsteadOfThemselves()
	{
		string widget = File.ReadAllText(RepoFile("app/src/MT4Widget/Widget.cs"));
		string history = File.ReadAllText(RepoFile("app/src/MT4Widget/HistoryForm.cs"));
		string source = widget + "\n" + history;
		foreach (string call in SelfCalls)
		{
			Assert.DoesNotContain(call, source);
		}
		Assert.Contains("base.CreateParams", widget);
		Assert.Contains("base.OnLoad(", widget);
		Assert.Contains("base.WndProc(", widget);
		Assert.Contains("base.OnMouseUp(", history);
		Assert.Contains("base.OnMouseMove(", history);
	}

	[Fact]
	public void CompiledWidget_HasNoDirectSelfCalls()
	{
		string dll = RepoFile("app/src/MT4Widget/bin/Release/net8.0-windows/win-x64/MT4Widget.dll");
		var failures = new List<string>();
		using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(dll);
		Assert.Equal("1.1.1.0", assembly.Name.Version.ToString());
		foreach (TypeDefinition type in assembly.MainModule.Types)
		{
			if (type.Name != "Widget" && type.Name != "HistoryForm")
			{
				continue;
			}
			foreach (MethodDefinition method in type.Methods)
			{
				if (!method.HasBody)
				{
					continue;
				}
				foreach (Instruction instruction in method.Body.Instructions)
				{
					if (instruction.OpCode.Code != Code.Call && instruction.OpCode.Code != Code.Callvirt)
					{
						continue;
					}
					if (instruction.Operand is MethodReference target
						&& target.Name == method.Name
						&& target.DeclaringType != null
						&& target.DeclaringType.Name == type.Name)
					{
						failures.Add(type.Name + "::" + method.Name + " calls itself at IL " + instruction.Offset.ToString());
					}
				}
			}
		}
		Assert.True(failures.Count == 0, string.Join("; ", failures));
	}
}
