using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FastNoise2.Editor.Signing
{
	public static class LibrarySigner
	{
		const string DEFINE = "FN2_USER_SIGNED";

		static readonly NamedBuildTarget[] s_Targets =
		{
			NamedBuildTarget.Standalone,
			NamedBuildTarget.Android,
			NamedBuildTarget.iOS,
			NamedBuildTarget.Server,
		};

		public static bool IsSigningDefineSet
		{
			get
			{
#if FN2_USER_SIGNED
				return true;
#else
				return false;
#endif
			}
		}

		public static void SignLibraries()
		{
			var paths = GetLibraryPaths();
			if (paths.Count == 0)
			{
				Debug.Log("[FastNoise2] No libraries to sign on this platform.");
				return;
			}

			foreach (string path in paths)
			{
				string fullPath = GetFullLibraryPath(path);

				if (!System.IO.File.Exists(fullPath) && !System.IO.Directory.Exists(fullPath))
				{
					Debug.LogWarning($"[FastNoise2] Path not found, skipping: {fullPath}");
					continue;
				}

				switch (Application.platform)
				{
					case RuntimePlatform.OSXEditor:
						RunShell($"chmod +x '{fullPath}' && xattr -dr com.apple.quarantine '{fullPath}'");
						break;
					case RuntimePlatform.LinuxEditor:
						RunShell($"chmod +x '{fullPath}'");
						break;
				}
			}

			Debug.Log("[FastNoise2] Library signing complete.");
		}

		public static void AddSigningDefine()
		{
			foreach (var target in s_Targets)
			{
				try
				{
					string defines = PlayerSettings.GetScriptingDefineSymbols(target);
					if (!ContainsDefine(defines, DEFINE))
					{
						defines = string.IsNullOrEmpty(defines) ? DEFINE : defines + ";" + DEFINE;
						PlayerSettings.SetScriptingDefineSymbols(target, defines);
					}
				}
				catch (System.Exception)
				{
					// Target not installed, skip
				}
			}
		}

		public static void RemoveSigningDefine()
		{
			foreach (var target in s_Targets)
			{
				try
				{
					string defines = PlayerSettings.GetScriptingDefineSymbols(target);
					if (ContainsDefine(defines, DEFINE))
					{
						defines = RemoveDefine(defines, DEFINE);
						PlayerSettings.SetScriptingDefineSymbols(target, defines);
					}
				}
				catch (System.Exception)
				{
					// Target not installed, skip
				}
			}
		}

		public static List<string> GetLibraryPaths()
		{
			var paths = new List<string>();
			switch (Application.platform)
			{
				case RuntimePlatform.OSXEditor:
					paths.Add("Plugins/macos/lib/libFastNoise.dylib");
					break;
				case RuntimePlatform.LinuxEditor:
					paths.Add("Plugins/linux/lib/libFastNoise.so");
					break;
				// Windows: no signing needed
			}
			return paths;
		}

		public static string GetFullLibraryPath(string relativePath) =>
			System.IO.Path.GetFullPath(System.IO.Path.Combine(GetPackageRoot(), relativePath));

		static string GetPackageRoot() =>
			UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(LibrarySigner).Assembly).resolvedPath;

		static void RunShell(string command)
		{
			var psi = new ProcessStartInfo("/bin/sh", $"-c \"{command}\"")
			{
				UseShellExecute = false,
				RedirectStandardError = true,
				CreateNoWindow = true,
			};

			using Process p = Process.Start(psi);
			string stderr = p.StandardError.ReadToEnd();
			p.WaitForExit();

			if (p.ExitCode != 0)
				Debug.LogWarning($"[FastNoise2] Shell command exited {p.ExitCode}: {stderr}");
		}

		static bool ContainsDefine(string defines, string define)
		{
			if (string.IsNullOrEmpty(defines)) return false;
			foreach (string d in defines.Split(';'))
			{
				if (d.Trim() == define) return true;
			}
			return false;
		}

		static string RemoveDefine(string defines, string define)
		{
			var parts = new List<string>();
			foreach (string d in defines.Split(';'))
			{
				string trimmed = d.Trim();
				if (trimmed != define && trimmed.Length > 0)
					parts.Add(trimmed);
			}
			return string.Join(";", parts);
		}
	}
}
