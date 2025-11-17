using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ProceduralMeshDestruction : MonoBehaviour, IDestructible
{
    [Header("Destruction Settings")]
    [Min(1)]
    public int CutCascades = 1;

    [Tooltip("Fuerza de explosión aplicada a cada pedazo (Impulse).")]
    public float ExplodeForce = 0f;

    private bool edgeSet = false;
    private Vector3 edgeVertex = Vector3.zero;
    private Vector2 edgeUV = Vector2.zero;
    private Plane edgePlane = new Plane();

    /// <summary>
    /// Llama esto desde fuera cuando quieras destruir el mesh.
    /// </summary>
    public void TriggerDestruction()
    {
        TriggerDestruction(transform.position);
    }

    /// <summary>
    /// Igual que TriggerDestruction(), pero permitiendo definir el origen de la explosión.
    /// </summary>
    public void TriggerDestruction(Vector3 explosionOrigin)
    {
        DestroyMesh(explosionOrigin);
    }

    private void DestroyMesh(Vector3 explosionOrigin)
    {
        var filter = GetComponent<MeshFilter>();
        var originalMesh = filter.mesh;
        originalMesh.RecalculateBounds();

        var parts = new List<PartMesh>();
        var subParts = new List<PartMesh>();

        var mainPart = new PartMesh
        {
            UV = originalMesh.uv,
            Vertices = originalMesh.vertices,
            Normals = originalMesh.normals,
            Triangles = new int[originalMesh.subMeshCount][],
            Bounds = originalMesh.bounds
        };

        for (int i = 0; i < originalMesh.subMeshCount; i++)
            mainPart.Triangles[i] = originalMesh.GetTriangles(i);

        parts.Add(mainPart);

        for (var c = 0; c < CutCascades; c++)
        {
            for (var i = 0; i < parts.Count; i++)
            {
                var bounds = parts[i].Bounds;
                bounds.Expand(0.5f);

                var plane = new Plane(
                    Random.onUnitSphere,
                    new Vector3(
                        Random.Range(bounds.min.x, bounds.max.x),
                        Random.Range(bounds.min.y, bounds.max.y),
                        Random.Range(bounds.min.z, bounds.max.z)
                    )
                );

                subParts.Add(GenerateMesh(parts[i], plane, true));
                subParts.Add(GenerateMesh(parts[i], plane, false));
            }

            parts = new List<PartMesh>(subParts);
            subParts.Clear();
        }

        // Crear los gameobjects finales
        for (var i = 0; i < parts.Count; i++)
        {
            parts[i].MakeGameobject(this);

            if (ExplodeForce > 0f)
            {
                var rb = parts[i].GameObject.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 dir = (parts[i].Bounds.center - explosionOrigin).normalized;
                    if (dir == Vector3.zero) dir = Random.onUnitSphere;
                    rb.AddForce(dir * ExplodeForce, ForceMode.Impulse);
                }
            }
        }

        // Destruir el objeto original
        Destroy(gameObject);
    }

    private PartMesh GenerateMesh(PartMesh original, Plane plane, bool left)
    {
        var partMesh = new PartMesh();
        var ray1 = new Ray();
        var ray2 = new Ray();

        for (var i = 0; i < original.Triangles.Length; i++)
        {
            var triangles = original.Triangles[i];
            if (triangles == null || triangles.Length == 0)
                continue;

            edgeSet = false;

            for (var j = 0; j < triangles.Length; j += 3)
            {
                var sideA = plane.GetSide(original.Vertices[triangles[j]]) == left;
                var sideB = plane.GetSide(original.Vertices[triangles[j + 1]]) == left;
                var sideC = plane.GetSide(original.Vertices[triangles[j + 2]]) == left;

                var sideCount = (sideA ? 1 : 0) +
                                (sideB ? 1 : 0) +
                                (sideC ? 1 : 0);

                if (sideCount == 0)
                    continue;

                if (sideCount == 3)
                {
                    partMesh.AddTriangle(
                        i,
                        original.Vertices[triangles[j]],
                        original.Vertices[triangles[j + 1]],
                        original.Vertices[triangles[j + 2]],
                        original.Normals[triangles[j]],
                        original.Normals[triangles[j + 1]],
                        original.Normals[triangles[j + 2]],
                        original.UV[triangles[j]],
                        original.UV[triangles[j + 1]],
                        original.UV[triangles[j + 2]]
                    );
                    continue;
                }

                // Corte
                var singleIndex = sideB == sideC ? 0 : sideA == sideC ? 1 : 2;

                ray1.origin = original.Vertices[triangles[j + singleIndex]];
                var dir1 = original.Vertices[triangles[j + ((singleIndex + 1) % 3)]] -
                           original.Vertices[triangles[j + singleIndex]];
                ray1.direction = dir1;
                plane.Raycast(ray1, out var enter1);
                var lerp1 = enter1 / dir1.magnitude;

                ray2.origin = original.Vertices[triangles[j + singleIndex]];
                var dir2 = original.Vertices[triangles[j + ((singleIndex + 2) % 3)]] -
                           original.Vertices[triangles[j + singleIndex]];
                ray2.direction = dir2;
                plane.Raycast(ray2, out var enter2);
                var lerp2 = enter2 / dir2.magnitude;

                // First vertex = anchor
                AddEdge(
                    i,
                    partMesh,
                    left ? plane.normal * -1f : plane.normal,
                    ray1.origin + ray1.direction.normalized * enter1,
                    ray2.origin + ray2.direction.normalized * enter2,
                    Vector2.Lerp(
                        original.UV[triangles[j + singleIndex]],
                        original.UV[triangles[j + ((singleIndex + 1) % 3)]],
                        lerp1
                    ),
                    Vector2.Lerp(
                        original.UV[triangles[j + singleIndex]],
                        original.UV[triangles[j + ((singleIndex + 2) % 3)]],
                        lerp2
                    )
                );

                if (sideCount == 1)
                {
                    partMesh.AddTriangle(
                        i,
                        original.Vertices[triangles[j + singleIndex]],
                        ray1.origin + ray1.direction.normalized * enter1,
                        ray2.origin + ray2.direction.normalized * enter2,
                        original.Normals[triangles[j + singleIndex]],
                        Vector3.Lerp(
                            original.Normals[triangles[j + singleIndex]],
                            original.Normals[triangles[j + ((singleIndex + 1) % 3)]],
                            lerp1
                        ),
                        Vector3.Lerp(
                            original.Normals[triangles[j + singleIndex]],
                            original.Normals[triangles[j + ((singleIndex + 2) % 3)]],
                            lerp2
                        ),
                        original.UV[triangles[j + singleIndex]],
                        Vector2.Lerp(
                            original.UV[triangles[j + singleIndex]],
                            original.UV[triangles[j + ((singleIndex + 1) % 3)]],
                            lerp1
                        ),
                        Vector2.Lerp(
                            original.UV[triangles[j + singleIndex]],
                            original.UV[triangles[j + ((singleIndex + 2) % 3)]],
                            lerp2
                        )
                    );

                    continue;
                }

                if (sideCount == 2)
                {
                    partMesh.AddTriangle(
                        i,
                        ray1.origin + ray1.direction.normalized * enter1,
                        original.Vertices[triangles[j + ((singleIndex + 1) % 3)]],
                        original.Vertices[triangles[j + ((singleIndex + 2) % 3)]],
                        Vector3.Lerp(
                            original.Normals[triangles[j + singleIndex]],
                            original.Normals[triangles[j + ((singleIndex + 1) % 3)]],
                            lerp1
                        ),
                        original.Normals[triangles[j + ((singleIndex + 1) % 3)]],
                        original.Normals[triangles[j + ((singleIndex + 2) % 3)]],
                        Vector2.Lerp(
                            original.UV[triangles[j + singleIndex]],
                            original.UV[triangles[j + ((singleIndex + 1) % 3)]],
                            lerp1
                        ),
                        original.UV[triangles[j + ((singleIndex + 1) % 3)]],
                        original.UV[triangles[j + ((singleIndex + 2) % 3)]]
                    );

                    partMesh.AddTriangle(
                        i,
                        ray1.origin + ray1.direction.normalized * enter1,
                        original.Vertices[triangles[j + ((singleIndex + 2) % 3)]],
                        ray2.origin + ray2.direction.normalized * enter2,
                        Vector3.Lerp(
                            original.Normals[triangles[j + singleIndex]],
                            original.Normals[triangles[j + ((singleIndex + 1) % 3)]],
                            lerp1
                        ),
                        original.Normals[triangles[j + ((singleIndex + 2) % 3)]],
                        Vector3.Lerp(
                            original.Normals[triangles[j + singleIndex]],
                            original.Normals[triangles[j + ((singleIndex + 2) % 3)]],
                            lerp2
                        ),
                        Vector2.Lerp(
                            original.UV[triangles[j + singleIndex]],
                            original.UV[triangles[j + ((singleIndex + 1) % 3)]],
                            lerp1
                        ),
                        original.UV[triangles[j + ((singleIndex + 2) % 3)]],
                        Vector2.Lerp(
                            original.UV[triangles[j + singleIndex]],
                            original.UV[triangles[j + ((singleIndex + 2) % 3)]],
                            lerp2
                        )
                    );

                    continue;
                }
            }
        }

        partMesh.FillArrays();

        return partMesh;
    }

    private void AddEdge(
        int subMesh,
        PartMesh partMesh,
        Vector3 normal,
        Vector3 vertex1,
        Vector3 vertex2,
        Vector2 uv1,
        Vector2 uv2)
    {
        if (!edgeSet)
        {
            edgeSet = true;
            edgeVertex = vertex1;
            edgeUV = uv1;
        }
        else
        {
            edgePlane.Set3Points(edgeVertex, vertex1, vertex2);

            partMesh.AddTriangle(
                subMesh,
                edgeVertex,
                edgePlane.GetSide(edgeVertex + normal) ? vertex1 : vertex2,
                edgePlane.GetSide(edgeVertex + normal) ? vertex2 : vertex1,
                normal,
                normal,
                normal,
                edgeUV,
                uv1,
                uv2
            );
        }
    }

    public class PartMesh
    {
        private readonly List<Vector3> _Vertices = new List<Vector3>();
        private readonly List<Vector3> _Normals = new List<Vector3>();
        private readonly List<List<int>> _Triangles = new List<List<int>>();
        private readonly List<Vector2> _UVs = new List<Vector2>();

        public Vector3[] Vertices;
        public Vector3[] Normals;
        public int[][] Triangles;
        public Vector2[] UV;
        public GameObject GameObject;
        public Bounds Bounds = new Bounds();

        private bool boundsInitialized = false;

        public void AddTriangle(
            int submesh,
            Vector3 vert1,
            Vector3 vert2,
            Vector3 vert3,
            Vector3 normal1,
            Vector3 normal2,
            Vector3 normal3,
            Vector2 uv1,
            Vector2 uv2,
            Vector2 uv3)
        {
            if (_Triangles.Count - 1 < submesh)
                _Triangles.Add(new List<int>());

            _Triangles[submesh].Add(_Vertices.Count);
            _Vertices.Add(vert1);

            _Triangles[submesh].Add(_Vertices.Count);
            _Vertices.Add(vert2);

            _Triangles[submesh].Add(_Vertices.Count);
            _Vertices.Add(vert3);

            _Normals.Add(normal1);
            _Normals.Add(normal2);
            _Normals.Add(normal3);

            _UVs.Add(uv1);
            _UVs.Add(uv2);
            _UVs.Add(uv3);

            // Bounds
            if (!boundsInitialized)
            {
                Bounds.min = vert1;
                Bounds.max = vert1;
                boundsInitialized = true;
            }

            Bounds.min = Vector3.Min(Bounds.min, vert1);
            Bounds.min = Vector3.Min(Bounds.min, vert2);
            Bounds.min = Vector3.Min(Bounds.min, vert3);

            Bounds.max = Vector3.Max(Bounds.max, vert1);
            Bounds.max = Vector3.Max(Bounds.max, vert2);
            Bounds.max = Vector3.Max(Bounds.max, vert3);
        }

        public void FillArrays()
        {
            Vertices = _Vertices.ToArray();
            Normals = _Normals.ToArray();
            UV = _UVs.ToArray();

            Triangles = new int[_Triangles.Count][];
            for (var i = 0; i < _Triangles.Count; i++)
                Triangles[i] = _Triangles[i].ToArray();
        }

        public void MakeGameobject(ProceduralMeshDestruction original)
        {
            GameObject = new GameObject(original.name);
            GameObject.transform.position = original.transform.position;
            GameObject.transform.rotation = original.transform.rotation;
            GameObject.transform.localScale = original.transform.localScale;

            var mesh = new Mesh
            {
                name = original.GetComponent<MeshFilter>().mesh.name
            };

            mesh.vertices = Vertices;
            mesh.normals = Normals;
            mesh.uv = UV;

            for (var i = 0; i < Triangles.Length; i++)
            {
                if (Triangles[i] != null && Triangles[i].Length > 0)
                    mesh.SetTriangles(Triangles[i], i, true);
            }

            Bounds = mesh.bounds;

            var renderer = GameObject.AddComponent<MeshRenderer>();
            renderer.materials = original.GetComponent<MeshRenderer>().materials;

            var filter = GameObject.AddComponent<MeshFilter>();
            filter.mesh = mesh;

            var collider = GameObject.AddComponent<MeshCollider>();
            collider.convex = true;

            var rigidbody = GameObject.AddComponent<Rigidbody>();

            // Si quisieras que los pedazos también se puedan volver a romper:
            // var meshDestroy = GameObject.AddComponent<ProceduralMeshDestruction>();
            // meshDestroy.CutCascades = original.CutCascades;
            // meshDestroy.ExplodeForce = original.ExplodeForce;
        }
    }
}
