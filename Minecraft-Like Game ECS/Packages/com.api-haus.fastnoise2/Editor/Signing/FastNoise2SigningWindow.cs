#if !FN2_USER_SIGNED
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FastNoise2.Editor.Signing
{
	public class FastNoise2SigningWindow : EditorWindow
	{
		[MenuItem("Window/FastNoise2")]
		public static void ShowWindow()
		{
			var window = GetWindow<FastNoise2SigningWindow>();
			window.titleContent = new GUIContent("FastNoise2");
			window.minSize = new Vector2(400, 260);
		}

		[MenuItem("Window/FastNoise2", true)]
		static bool ShowWindowValidate() => Application.platform == RuntimePlatform.OSXEditor;

		[InitializeOnLoadMethod]
		static void AutoPopup()
		{
			if (Application.platform == RuntimePlatform.OSXEditor)
				EditorApplication.delayCall += () => ShowWindow();
			else
				EditorApplication.delayCall += () => LibrarySigner.AddSigningDefine();
		}

		public void CreateGUI()
		{
			var root = rootVisualElement;
			root.style.paddingTop = 12;
			root.style.paddingBottom = 12;
			root.style.paddingLeft = 12;
			root.style.paddingRight = 12;

			var header = new Label("[FastNoise2]");
			header.style.fontSize = 24;
			header.style.color = new Color(0.3f, 0.5f, 1f);
			header.style.unityFontStyleAndWeight = FontStyle.Bold;
			header.style.marginBottom = 12;
			root.Add(header);

			var disclaimerBox = new VisualElement();
			disclaimerBox.style.backgroundColor = new Color(0f, 0f, 0f, 0.15f);
			disclaimerBox.style.borderTopWidth = 1;
			disclaimerBox.style.borderBottomWidth = 1;
			disclaimerBox.style.borderLeftWidth = 1;
			disclaimerBox.style.borderRightWidth = 1;
			disclaimerBox.style.borderTopColor = new Color(1f, 1f, 1f, 0.1f);
			disclaimerBox.style.borderBottomColor = new Color(1f, 1f, 1f, 0.1f);
			disclaimerBox.style.borderLeftColor = new Color(1f, 1f, 1f, 0.1f);
			disclaimerBox.style.borderRightColor = new Color(1f, 1f, 1f, 0.1f);
			disclaimerBox.style.borderTopLeftRadius = 4;
			disclaimerBox.style.borderTopRightRadius = 4;
			disclaimerBox.style.borderBottomLeftRadius = 4;
			disclaimerBox.style.borderBottomRightRadius = 4;
			disclaimerBox.style.paddingTop = 8;
			disclaimerBox.style.paddingBottom = 8;
			disclaimerBox.style.paddingLeft = 10;
			disclaimerBox.style.paddingRight = 10;
			disclaimerBox.style.marginBottom = 12;

			var paths = LibrarySigner.GetLibraryPaths();
			var sb = new System.Text.StringBuilder();
			sb.AppendLine("FastNoise2 ships unsigned native libraries. macOS Gatekeeper blocks them until quarantine attributes are cleared.");
			sb.AppendLine();
			sb.AppendLine("Clicking 'Sign Libraries' will run:");
			sb.AppendLine();
			foreach (string path in paths)
			{
				string fullPath = LibrarySigner.GetFullLibraryPath(path);
				sb.AppendLine($"  chmod +x '{fullPath}'");
				sb.AppendLine($"  xattr -dr com.apple.quarantine '{fullPath}'");
			}
			sb.AppendLine();
			sb.AppendLine("On Linux and Windows, this is handled automatically.");

			var disclaimer = new Label(sb.ToString());
			disclaimer.style.whiteSpace = WhiteSpace.Normal;
			disclaimerBox.Add(disclaimer);
			root.Add(disclaimerBox);

			var signButton = new Button(() =>
			{
				LibrarySigner.SignLibraries();
				LibrarySigner.AddSigningDefine();
			})
			{
				text = "Sign Libraries"
			};
			root.Add(signButton);
		}
	}
}
#endif
