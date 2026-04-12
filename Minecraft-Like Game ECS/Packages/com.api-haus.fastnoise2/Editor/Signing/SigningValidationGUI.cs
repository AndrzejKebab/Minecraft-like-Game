using UnityEditor;
using UnityEngine;

namespace FastNoise2.Editor.Signing
{
	public static class SigningValidationGUI
	{
		/// <summary>
		/// Returns true if signed (proceed with normal GUI).
		/// Returns false if unsigned (caller should return early).
		/// </summary>
		public static bool DrawValidation()
		{
#if FN2_USER_SIGNED
			return true;
#else
			EditorGUILayout.HelpBox(
				"FastNoise2 native libraries are not signed. Native functionality is disabled.",
				MessageType.Warning);
			if (GUILayout.Button("Sign Libraries"))
				FastNoise2SigningWindow.ShowWindow();
			return false;
#endif
		}
	}
}
