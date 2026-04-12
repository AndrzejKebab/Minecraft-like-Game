using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FastNoise2.Editor.Ipc
{
#if FN2_USER_SIGNED
	[InitializeOnLoad]
	internal static class NodeEditorSession
	{
		const int POLL_BUFFER_SIZE = 65536;
		const string SESSION_KEY_PID = "FN2_NodeEditor_PID";
		const string SESSION_KEY_GLOBAL_ID = "FN2_EditingGlobalObjectId";
		const string SESSION_KEY_PROPERTY_PATH = "FN2_EditingPropertyPath";
		const string ENCODED_GRAPH_PROPERTY_PATH = "encodedGraph";

		static IntPtr s_Ctx;
		static bool s_Polling;
		static byte[] s_PollBuffer;
		static string s_NodeEditorPath;

		// Editing target — recoverable from SessionState after domain reload
		static string s_ActiveGlobalId;
		static string s_ActivePropertyPath;
		static SerializedObject s_ActiveSerializedObject;

		public static string ActiveGlobalId => s_ActiveGlobalId;
		public static string ActivePropertyPath => s_ActivePropertyPath;

		static NodeEditorSession()
		{
			EditorApplication.quitting += OnQuitting;
			AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;

			TryReconnect();
		}

		static void TryReconnect()
		{
			int pid = SessionState.GetInt(SESSION_KEY_PID, -1);
			if (pid <= 0)
				return;

			if (!IsProcessAlive(pid))
			{
				ClearSessionState();
				return;
			}

			// Recover editing target
			s_ActiveGlobalId = SessionState.GetString(SESSION_KEY_GLOBAL_ID, "");
			s_ActivePropertyPath = SessionState.GetString(SESSION_KEY_PROPERTY_PATH, "");

			if (!string.IsNullOrEmpty(s_ActiveGlobalId))
				RecoverSerializedObject();

			if (!Setup())
				return;

			StartPolling();
			Debug.Log($"[FN2 IPC] Reconnected to NodeEditor (PID {pid}) after domain reload");
		}

		static void RecoverSerializedObject()
		{
			if (string.IsNullOrEmpty(s_ActiveGlobalId))
				return;

			if (!GlobalObjectId.TryParse(s_ActiveGlobalId, out var globalId))
			{
				Debug.LogWarning($"[FN2 IPC] Failed to parse GlobalObjectId: {s_ActiveGlobalId}");
				return;
			}

			var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
			if (obj == null)
			{
				Debug.LogWarning("[FN2 IPC] Could not recover target object from GlobalObjectId");
				return;
			}

			s_ActiveSerializedObject = new SerializedObject(obj);
		}

		static string ResolveNodeEditorPath()
		{
			if (s_NodeEditorPath != null)
				return s_NodeEditorPath;

			var pkgInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
				typeof(NodeEditorSession).Assembly
			);
			if (pkgInfo == null)
			{
				Debug.LogError("[FN2 IPC] Cannot resolve package path for NodeEditorIpc.");
				return null;
			}

			string basePath = pkgInfo.resolvedPath;

#if UNITY_EDITOR_WIN
			string relPath = "Plugins/windows/bin/NodeEditor.exe";
#elif UNITY_EDITOR_OSX
			string relPath = "Plugins/macos/bin/NodeEditor";
#else
			string relPath = "Plugins/linux/bin/NodeEditor";
#endif
			string fullPath = Path.Combine(basePath, relPath);
			if (!File.Exists(fullPath))
			{
				Debug.LogError($"[FN2 IPC] NodeEditor binary not found at: {fullPath}");
				return null;
			}

			s_NodeEditorPath = fullPath;
			return fullPath;
		}

		static void EnsureExecutable(string path)
		{
#if UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
			var psi = new ProcessStartInfo("chmod", $"+x \"{path}\"")
			{
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			try
			{
				Process.Start(psi)?.WaitForExit(2000);
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[FN2 IPC] chmod failed: {e.Message}");
			}
#endif
		}

		static bool Setup()
		{
			if (s_Ctx != IntPtr.Zero)
				return true;

			string editorPath = ResolveNodeEditorPath();
			if (editorPath == null)
				return false;

			EnsureExecutable(editorPath);

			s_Ctx = NodeEditorIpc.fnEditorIpcSetup(0);
			if (s_Ctx == IntPtr.Zero)
			{
				Debug.LogError("[FN2 IPC] fnEditorIpcSetup returned null context");
				return false;
			}

			s_PollBuffer = new byte[POLL_BUFFER_SIZE];
			return true;
		}

		public static void EditGraph(string encodedGraph, UnityEngine.Object targetObject, string propertyPath)
		{
			KillNodeEditor();
			ReleaseContext();

			// Store editing target
			s_ActiveGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(targetObject).ToString();
			s_ActivePropertyPath = propertyPath;
			s_ActiveSerializedObject = new SerializedObject(targetObject);

			SessionState.SetString(SESSION_KEY_GLOBAL_ID, s_ActiveGlobalId);
			SessionState.SetString(SESSION_KEY_PROPERTY_PATH, s_ActivePropertyPath);

			if (!Setup())
				return;

			StartNodeEditor(encodedGraph);
			StartPolling();
		}

		static void StartNodeEditor(string encodedGraph)
		{
			string path = ResolveNodeEditorPath();
			if (path == null)
				return;

			string binDir = Path.GetDirectoryName(path)!;
			string libDir = Path.GetFullPath(Path.Combine(binDir, "..", "lib"));

			var psi = new ProcessStartInfo(path)
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

			try
			{
				var proc = Process.Start(psi);
				if (proc == null)
				{
					Debug.LogError("[FN2 IPC] Process.Start returned null");
					return;
				}

				int launchedPid = proc.Id;
				SessionState.SetInt(SESSION_KEY_PID, launchedPid);

				proc.EnableRaisingEvents = true;
				proc.Exited += (_, _) =>
				{
					int code = proc.ExitCode;

					if (code != 0 && code != 137)
					{
						string stderr = "";
						try { stderr = proc.StandardError.ReadToEnd(); }
						catch { /* stream already closed */ }
						Debug.LogError($"[FN2 IPC] NodeEditor crashed (code {code}): {stderr}");
					}

					// Defer to main thread — SessionState and Unity API can't be called from thread pool
					EditorApplication.delayCall += () =>
					{
						// Only clear state if we're still the current session —
						// a new EditGraph() call may have already replaced us.
						if (SessionState.GetInt(SESSION_KEY_PID, -1) == launchedPid)
						{
							ClearSessionState();
							UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
						}
					};

					proc.Dispose();
				};
			}
			catch (Exception e)
			{
				Debug.LogError($"[FN2 IPC] Failed to start NodeEditor: {e.Message}");
			}
		}

		static void KillNodeEditor()
		{
			int pid = SessionState.GetInt(SESSION_KEY_PID, -1);
			if (pid <= 0)
				return;

			try
			{
				var p = Process.GetProcessById(pid);
				if (!p.HasExited)
					p.Kill();
				p.Dispose();
			}
			catch { /* already dead */ }

			SessionState.EraseInt(SESSION_KEY_PID);
		}

		static bool IsProcessAlive(int pid)
		{
			try
			{
				var p = Process.GetProcessById(pid);
				bool alive = !p.HasExited;
				p.Dispose();
				return alive;
			}
			catch
			{
				return false;
			}
		}

		static void StartPolling()
		{
			if (s_Polling)
				return;
			s_Polling = true;
			EditorApplication.update += PollUpdate;
		}

		static void StopPolling()
		{
			if (!s_Polling)
				return;
			s_Polling = false;
			EditorApplication.update -= PollUpdate;
		}

		static void PollUpdate()
		{
			if (s_Ctx == IntPtr.Zero)
				return;

			// Detect dead NodeEditor and clean up on the main thread
			int pid = SessionState.GetInt(SESSION_KEY_PID, -1);
			if (pid > 0 && !IsProcessAlive(pid))
			{
				ClearSessionState();
				ReleaseContext();
				UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
				return;
			}

			int msgType = NodeEditorIpc.fnEditorIpcPollMessage(s_Ctx, s_PollBuffer, s_PollBuffer.Length);

			if (msgType <= 0)
				return;

			int len = Array.IndexOf(s_PollBuffer, (byte)0);
			if (len < 0)
				len = s_PollBuffer.Length;

			if (len > 0)
			{
				string message = Encoding.UTF8.GetString(s_PollBuffer, 0, len);
				ApplyGraphChange(message);
			}
		}

		static void ApplyGraphChange(string encodedGraph)
		{
			// Try to recover stale SerializedObject
			if (s_ActiveSerializedObject == null || s_ActiveSerializedObject.targetObject == null)
			{
				RecoverSerializedObject();
				if (s_ActiveSerializedObject == null)
					return;
			}

			s_ActiveSerializedObject.Update();

			var prop = s_ActiveSerializedObject.FindProperty(s_ActivePropertyPath);
			if (prop == null) return;

			var encodedProp = prop.FindPropertyRelative(ENCODED_GRAPH_PROPERTY_PATH);
			if (encodedProp == null) return;

			encodedProp.stringValue = encodedGraph;
			s_ActiveSerializedObject.ApplyModifiedProperties();

			EditorUtility.SetDirty(s_ActiveSerializedObject.targetObject);
			UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
		}

		static void ReleaseContext()
		{
			StopPolling();

			if (s_Ctx == IntPtr.Zero)
				return;
			IntPtr ctx = s_Ctx;
			s_Ctx = IntPtr.Zero;

			try { NodeEditorIpc.fnEditorIpcRelease(ctx); }
			catch (Exception e) { Debug.LogWarning($"[FN2 IPC] Release failed: {e.Message}"); }
		}

		static void ClearSessionState()
		{
			s_ActiveGlobalId = null;
			s_ActivePropertyPath = null;
			s_ActiveSerializedObject = null;

			SessionState.EraseInt(SESSION_KEY_PID);
			SessionState.EraseString(SESSION_KEY_GLOBAL_ID);
			SessionState.EraseString(SESSION_KEY_PROPERTY_PATH);
		}

		static void OnBeforeReload()
		{
			StopPolling();
			s_Ctx = IntPtr.Zero;
			s_ActiveSerializedObject = null;
			// PID, GlobalId, PropertyPath stay in SessionState for TryReconnect
		}

		static void OnQuitting()
		{
			ReleaseContext();

			int pid = SessionState.GetInt(SESSION_KEY_PID, -1);
			if (pid > 0)
			{
				try
				{
					var p = Process.GetProcessById(pid);
					if (!p.HasExited)
						p.Kill();
					p.Dispose();
				}
				catch { /* already dead */ }
			}

			ClearSessionState();
		}
	}
#endif
}
