using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FastNoise2.Tests
{
#if FN2_USER_SIGNED
	using Authoring.NoiseGraph;
	using Editor.Ipc;

	public class NodeEditorIpcTests : IpcTestBase
	{
		#region IPC Lifecycle

		[Test, Order(0)]
		public void Setup_ReturnsValidContext()
		{
			Assert.That(Ctx, Is.Not.EqualTo(IntPtr.Zero));
		}

		#endregion

		#region NodeEditor Binary

		[Test]
		public void NodeEditorBinary_Exists()
		{
			Assert.That(NodeEditorPath, Is.Not.Null, "NodeEditor binary not found at expected package path");
			Assert.That(File.Exists(NodeEditorPath), Is.True);
		}

		#endregion

		#region Message Passing (no editor running)

		[Test, Order(1)]
		public void PollMessage_OnFreshContext_ReturnsNull()
		{
			while (PollMessage() != null) { }

			Assert.That(PollMessage(), Is.Null);
		}

		[Test]
		public void SendImport_WithValidContext_DoesNotThrow()
		{
			Assert.DoesNotThrow(() => NodeEditorIpc.fnEditorIpcSendImportRequest(Ctx, DEFAULT_GRAPH));
		}

		[Test]
		public void SendSelected_WithValidContext_DoesNotThrow()
		{
			Assert.DoesNotThrow(() => NodeEditorIpc.fnEditorIpcSendSelectedNode(Ctx, DEFAULT_GRAPH));
		}

		#endregion

		#region Session State

		[Test]
		public void ActiveGlobalId_IsNullByDefault()
		{
			Assert.That(NodeEditorSession.ActiveGlobalId, Is.Null);
		}

		#endregion

		#region E2E Process Lifecycle

		[Test]
		public void LaunchNodeEditor_ProcessStarts()
		{
			KillNodeEditors();

			var proc = LaunchNodeEditor(DEFAULT_GRAPH);
			Assert.That(proc, Is.Not.Null, "Process.Start returned null");

			try
			{
				bool started = WaitForNodeEditor(5000);

				if (!started && proc.HasExited)
				{
					string stderr = proc.StandardError.ReadToEnd();
					Assert.Fail($"NodeEditor exited immediately with code {proc.ExitCode}. stderr: {stderr}");
				}

				Assert.That(started, Is.True, "NodeEditor process did not appear within 5s");
			}
			finally
			{
				KillNodeEditors();
			}
		}

		[Test]
		public void LaunchNodeEditor_KillAndRelaunch()
		{
			KillNodeEditors();

			var proc1 = LaunchNodeEditor(DEFAULT_GRAPH);
			Assert.That(proc1, Is.Not.Null);
			Assert.That(WaitForNodeEditor(5000), Is.True, "First launch: process not found");

			KillNodeEditors();
			System.Threading.Thread.Sleep(500);

			var procs = Process.GetProcessesByName("NodeEditor");
			bool stillAlive = procs.Length > 0;
			foreach (var p in procs) p.Dispose();
			Assert.That(stillAlive, Is.False, "NodeEditor should be dead after kill");

			var proc2 = LaunchNodeEditor(DEFAULT_GRAPH);
			Assert.That(proc2, Is.Not.Null);
			Assert.That(WaitForNodeEditor(5000), Is.True, "Second launch: process not found after kill+relaunch");

			KillNodeEditors();
		}

		#endregion

		#region E2E Session Switching

		[Test]
		public void EditGraph_SwitchBetweenObjects_SessionTracksCorrectTarget()
		{
			KillNodeEditors();

			const string pathA = "Assets/_fn2_test_a.asset";
			const string pathB = "Assets/_fn2_test_b.asset";

			var assetA = ScriptableObject.CreateInstance<FastNoiseGraphAsset>();
			var assetB = ScriptableObject.CreateInstance<FastNoiseGraphAsset>();

			try
			{
				// Save as actual assets so GlobalObjectId is valid
				AssetDatabase.CreateAsset(assetA, pathA);
				AssetDatabase.CreateAsset(assetB, pathB);
				AssetDatabase.SaveAssets();

				string propertyPath = "savedGraph";

				var globalIdA = GlobalObjectId.GetGlobalObjectIdSlow(assetA).ToString();
				var globalIdB = GlobalObjectId.GetGlobalObjectIdSlow(assetB).ToString();

				Assert.That(globalIdA, Is.Not.EqualTo(globalIdB),
					"Two distinct assets should have different GlobalObjectIds");

				// Edit object A
				NodeEditorSession.EditGraph(DEFAULT_GRAPH, assetA, propertyPath);

				Assert.That(NodeEditorSession.ActiveGlobalId, Is.EqualTo(globalIdA),
					"Session should track object A after EditGraph(A)");
				Assert.That(NodeEditorSession.ActivePropertyPath, Is.EqualTo(propertyPath));

				Assert.That(WaitForNodeEditor(5000), Is.True,
					"NodeEditor should be running after EditGraph(A)");

				// Switch to object B
				NodeEditorSession.EditGraph(DEFAULT_GRAPH, assetB, propertyPath);

				// Give the old process time to exit and fire its Exited handler
				System.Threading.Thread.Sleep(1000);

				Assert.That(NodeEditorSession.ActiveGlobalId, Is.EqualTo(globalIdB),
					"Session should track object B after EditGraph(B) — must NOT be cleared by old process Exited handler");
				Assert.That(NodeEditorSession.ActivePropertyPath, Is.EqualTo(propertyPath));

				Assert.That(WaitForNodeEditor(5000), Is.True,
					"NodeEditor should be running after EditGraph(B)");
			}
			finally
			{
				KillNodeEditors();
				AssetDatabase.DeleteAsset(pathA);
				AssetDatabase.DeleteAsset(pathB);
			}
		}

		#endregion
	}
#endif
}
