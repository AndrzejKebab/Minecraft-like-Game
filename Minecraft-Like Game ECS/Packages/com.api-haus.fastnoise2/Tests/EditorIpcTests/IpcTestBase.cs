using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace FastNoise2.Tests
{
#if FN2_USER_SIGNED
	using Editor.Ipc;

	public abstract class IpcTestBase
	{
		protected const string DEFAULT_GRAPH = "Bg@AMhCBA==";
		protected const int POLL_BUFFER_SIZE = 65536;

		protected IntPtr Ctx { get; private set; }
		protected string NodeEditorPath { get; private set; }

		byte[] m_PollBuffer;

		[OneTimeSetUp]
		public void BaseOneTimeSetUp()
		{
			Ctx = NodeEditorIpc.fnEditorIpcSetup(0);
			NodeEditorPath = ResolveNodeEditorPath();
			m_PollBuffer = new byte[POLL_BUFFER_SIZE];
		}

		[OneTimeTearDown]
		public void BaseOneTimeTearDown()
		{
			KillNodeEditors();

			if (Ctx != IntPtr.Zero)
			{
				NodeEditorIpc.fnEditorIpcRelease(Ctx);
				Ctx = IntPtr.Zero;
			}
		}

		protected string ResolveNodeEditorPath()
		{
			var pkgInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
				typeof(NodeEditorIpc).Assembly
			);
			if (pkgInfo == null) return null;

#if UNITY_EDITOR_WIN
			string relPath = "Plugins/windows/bin/NodeEditor.exe";
#elif UNITY_EDITOR_OSX
			string relPath = "Plugins/macos/bin/NodeEditor";
#else
			string relPath = "Plugins/linux/bin/NodeEditor";
#endif
			string fullPath = Path.Combine(pkgInfo.resolvedPath, relPath);
			return File.Exists(fullPath) ? fullPath : null;
		}

		/// <summary>
		/// Launches NodeEditor via Process.Start. Returns the Process (caller should NOT dispose
		/// immediately — keep the reference to track liveness). Returns null on failure.
		/// </summary>
		protected Process LaunchNodeEditor(string encodedGraph = null)
		{
			Assert.That(NodeEditorPath, Is.Not.Null, "NodeEditor binary not found");

			string binDir = Path.GetDirectoryName(NodeEditorPath)!;
			string libDir = Path.GetFullPath(Path.Combine(binDir, "..", "lib"));

			var psi = new ProcessStartInfo(NodeEditorPath)
			{
				UseShellExecute = false,
				CreateNoWindow = false,
				WorkingDirectory = binDir,
				RedirectStandardError = true,
			};

			psi.ArgumentList.Add("--detached");
			if (!string.IsNullOrEmpty(encodedGraph))
			{
				psi.ArgumentList.Add("--import-ent");
				psi.ArgumentList.Add(encodedGraph);
			}

#if UNITY_EDITOR_LINUX
			string existing = psi.Environment.ContainsKey("LD_LIBRARY_PATH")
				? psi.Environment["LD_LIBRARY_PATH"] : "";
			psi.Environment["LD_LIBRARY_PATH"] = string.IsNullOrEmpty(existing)
				? libDir : $"{libDir}:{existing}";
#elif UNITY_EDITOR_OSX
			string existing = psi.Environment.ContainsKey("DYLD_LIBRARY_PATH")
				? psi.Environment["DYLD_LIBRARY_PATH"] : "";
			psi.Environment["DYLD_LIBRARY_PATH"] = string.IsNullOrEmpty(existing)
				? libDir : $"{libDir}:{existing}";
#endif

			var proc = Process.Start(psi);
			return proc;
		}

		/// <summary>
		/// Waits up to timeoutMs for a NodeEditor process to appear.
		/// </summary>
		protected static bool WaitForNodeEditor(int timeoutMs = 5000)
		{
			var sw = Stopwatch.StartNew();
			while (sw.ElapsedMilliseconds < timeoutMs)
			{
				var procs = Process.GetProcessesByName("NodeEditor");
				bool alive = procs.Length > 0;
				foreach (var p in procs) p.Dispose();
				if (alive) return true;
				System.Threading.Thread.Sleep(100);
			}
			return false;
		}

		/// <summary>
		/// Polls for a message. Returns the message string, or null if no message.
		/// </summary>
		protected string PollMessage()
		{
			int msgType = NodeEditorIpc.fnEditorIpcPollMessage(Ctx, m_PollBuffer, m_PollBuffer.Length);
			if (msgType <= 0) return null;

			int len = Array.IndexOf(m_PollBuffer, (byte)0);
			if (len < 0) len = m_PollBuffer.Length;
			return len > 0 ? Encoding.UTF8.GetString(m_PollBuffer, 0, len) : null;
		}

		protected static void KillNodeEditors()
		{
			try
			{
				foreach (var p in Process.GetProcessesByName("NodeEditor"))
				{
					try { p.Kill(); }
					catch { /* already exited */ }
					finally { p.Dispose(); }
				}
			}
			catch { /* no processes found */ }
		}
	}
#endif
}
