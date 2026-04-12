using UnityEditor;
using UnityEngine;
using FastNoise2.Authoring.NoiseGraph;

namespace FastNoise2.Editor
{
	[CustomPropertyDrawer(typeof(FastNoiseGraph))]
	public class FastNoiseGraphPropertyDrawer : PropertyDrawer
	{
		const int BUTTON_WIDTH = 40;
		const int EDIT_BUTTON_WIDTH = 40;
		const int PADDING = 4;
		const string ENCODED_GRAPH_PROPERTY_PATH = "encodedGraph";

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);
			position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

			int indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			var encodedProp = property.FindPropertyRelative(ENCODED_GRAPH_PROPERTY_PATH);

#if FN2_USER_SIGNED
			// Check if this property is the active IPC editing target
			bool isActive = false;
			string sessionGlobalId = Ipc.NodeEditorSession.ActiveGlobalId;
			if (!string.IsNullOrEmpty(sessionGlobalId)
				&& Ipc.NodeEditorSession.ActivePropertyPath == property.propertyPath)
			{
				var targetGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(
					property.serializedObject.targetObject
				).ToString();
				isActive = targetGlobalId == sessionGlobalId;
			}

			// Layout: [encoded field] [Edit] [Copy] [Paste]
			float buttonsWidth = EDIT_BUTTON_WIDTH + BUTTON_WIDTH * 2 + PADDING * 3;
			Rect fieldRect = new(position.x, position.y, position.width - buttonsWidth, position.height);
			Rect editRect = new(fieldRect.xMax + PADDING, position.y, EDIT_BUTTON_WIDTH, position.height);
			Rect copyRect = new(editRect.xMax + PADDING, position.y, BUTTON_WIDTH, position.height);
			Rect pasteRect = new(copyRect.xMax + PADDING, position.y, BUTTON_WIDTH, position.height);

			EditorGUI.PropertyField(fieldRect, encodedProp, GUIContent.none);

			var prevBg = GUI.backgroundColor;
			if (isActive)
				GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);

			if (GUI.Button(editRect, "Edit"))
			{
				Ipc.NodeEditorSession.EditGraph(
					encodedProp.stringValue,
					property.serializedObject.targetObject,
					property.propertyPath
				);
			}

			GUI.backgroundColor = prevBg;
#else
			// No IPC without signing — fallback layout without Edit button
			float buttonsWidth = BUTTON_WIDTH * 2 + PADDING * 2;
			Rect fieldRect = new(position.x, position.y, position.width - buttonsWidth, position.height);
			Rect copyRect = new(fieldRect.xMax + PADDING, position.y, BUTTON_WIDTH, position.height);
			Rect pasteRect = new(copyRect.xMax + PADDING, position.y, BUTTON_WIDTH, position.height);

			EditorGUI.PropertyField(fieldRect, encodedProp, GUIContent.none);
#endif

			if (GUI.Button(copyRect, "Copy"))
				EditorGUIUtility.systemCopyBuffer = encodedProp.stringValue;

			if (GUI.Button(pasteRect, "Paste"))
			{
				string clipboard = EditorGUIUtility.systemCopyBuffer;
				if (!string.IsNullOrEmpty(clipboard))
				{
					encodedProp.stringValue = clipboard;
					property.serializedObject.ApplyModifiedProperties();
				}
			}

			EditorGUI.indentLevel = indent;
			EditorGUI.EndProperty();
		}
	}
}
