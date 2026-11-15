using UnityEngine;

namespace SpaceRat.Environment
{
    /// <summary>
    /// Greybox starfield: a point-cloud mesh on a large sphere that follows the
    /// active camera so stars read as infinitely far away. Generated at runtime.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Starfield : MonoBehaviour
    {
        [SerializeField] int starCount = 4000;
        [SerializeField] float radius = 9000f;
        [SerializeField] int seed = 1337;

        Mesh _mesh;

        void OnEnable()
        {
            var rng = new System.Random(seed);
            var verts = new Vector3[starCount];
            var colors = new Color[starCount];
            var idx = new int[starCount];
            for (int i = 0; i < starCount; i++)
            {
                // Uniform direction on a sphere.
                float z = (float)rng.NextDouble() * 2f - 1f;
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt(1f - z * z);
                verts[i] = new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), z) * radius;
                float b = Mathf.Lerp(0.25f, 1f, Mathf.Pow((float)rng.NextDouble(), 3f));
                float tint = (float)rng.NextDouble();
                colors[i] = new Color(b * Mathf.Lerp(0.8f, 1f, tint), b * 0.9f, b * Mathf.Lerp(1f, 0.8f, tint), 1f);
                idx[i] = i;
            }
            _mesh = new Mesh { name = "Starfield" };
            _mesh.vertices = verts;
            _mesh.colors = colors;
            _mesh.SetIndices(idx, MeshTopology.Points, 0);
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2.2f);
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        void OnDisable()
        {
            if (_mesh) Destroy(_mesh);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam) transform.position = cam.transform.position;
        }
    }
}
