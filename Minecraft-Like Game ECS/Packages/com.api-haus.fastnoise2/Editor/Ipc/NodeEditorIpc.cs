using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("FastNoise2.EditorIpcTests")]

namespace FastNoise2.Editor.Ipc
{
#if FN2_USER_SIGNED
	internal static class NodeEditorIpc
	{
		const string LIB = "NodeEditorIpc";

		/// <summary>
		/// Initializes IPC shared memory region. Returns context handle (non-null on success).
		/// </summary>
		[DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr fnEditorIpcSetup(int sockType);

		/// <summary>
		/// Releases IPC context and unmaps shared memory.
		/// </summary>
		[DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
		public static extern void fnEditorIpcRelease(IntPtr ctx);

		/// <summary>
		/// Sets the path to the NodeEditor binary (global, not per-context).
		/// </summary>
		[DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
		public static extern void fnEditorIpcSetNodeEditorPath(
			[MarshalAs(UnmanagedType.LPStr)] string path
		);

		/// <summary>
		/// Forks and executes the NodeEditor process with --detached --import-ent.
		/// </summary>
		[DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
		public static extern int fnEditorIpcStartNodeEditor(
			[MarshalAs(UnmanagedType.LPStr)] string encodedTree,
			int sockType,
			int flags
		);

		/// <summary>
		/// Sends a selected node's encoded tree to the editor via shared memory.
		/// </summary>
		[DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
		public static extern void fnEditorIpcSendSelectedNode(
			IntPtr ctx,
			[MarshalAs(UnmanagedType.LPStr)] string nodeData
		);

		/// <summary>
		/// Sends an import request (encoded graph) to the editor via shared memory.
		/// </summary>
		[DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
		public static extern void fnEditorIpcSendImportRequest(
			IntPtr ctx,
			[MarshalAs(UnmanagedType.LPStr)] string importData
		);

		/// <summary>
		/// Polls for messages from the editor.
		/// Returns: 0 = no message, 1-2 = message type (copied to buffer), -1 = buffer error, -2 = null context.
		/// </summary>
		[DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
		public static extern int fnEditorIpcPollMessage(
			IntPtr ctx,
			byte[] outBuffer,
			int bufferSize
		);
	}
#endif
}
