using UnityEngine;

public class MeshDataExtractor : MonoBehaviour
{
	public Mesh mesh;

	[ContextMenu("Extract Mesh Data")]
	void Extract()
	{
		if (mesh != null)
		{
			PrintMeshData(mesh);
		}
		else
		{
			Debug.LogError("No MeshFilter found on the GameObject.");
		}
	}

	void PrintMeshData(Mesh mesh)
	{
		string MeshData = "";
		Vector3[] vertices = mesh.vertices;
		Vector3[] normals = mesh.normals;
		int[] triangles = mesh.triangles;

		MeshData += "Vertices:\n";
		foreach (var vertex in vertices)
		{
			MeshData += vertex + "\n";
		}

		MeshData += "Normals:\n";
		foreach (var normal in normals)
		{
			MeshData += normal + "\n";
		}

		MeshData += "Triangles:\n";
		for (int i = 0; i < triangles.Length; i += 3)
		{
			MeshData += triangles[i] + ", " + triangles[i + 1] + ", " + triangles[i + 2] + "\n";
		}

		//create file on desktop
		System.IO.File.WriteAllText(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop) + "/MeshData.txt", MeshData);
	}
}