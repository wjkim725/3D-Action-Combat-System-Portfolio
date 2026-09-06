using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SwordTrailController : MonoBehaviour
{
    private struct TrailSample
    {
        public Vector3 rootPosition;
        public Vector3 tipPosition;
        public float createdTime;

        public TrailSample(Vector3 root, Vector3 tip, float time)
        {
            rootPosition = root;
            tipPosition = tip;
            createdTime = time;
        }
    }

    [Header("Trail Points")]
    [SerializeField] private Transform _trailRoot;
    [SerializeField] private Transform _trailTip;

    [Header("Trail Settings")]
    [SerializeField] private float _trailDuration = 0.15f;
    [SerializeField] private float _minSampleDistance = 0.02f;

    private readonly List<TrailSample> _samples = new();
    private readonly List<Vector3> _vertices = new();
    private readonly List<int> _triangles = new();
    private readonly List<Vector2> _uvs = new();
    private readonly List<Color> _colors = new();

    private Mesh _mesh;
    private MeshRenderer _meshRenderer;
    private bool _isRecording;

    private void Awake()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();

        _mesh = new Mesh
        {
            name = $"{gameObject.name}_RibbonMesh"
        };

        _mesh.MarkDynamic();
        meshFilter.sharedMesh = _mesh;
        _meshRenderer.enabled = false;
    }

    private void LateUpdate()
    {
        if (_trailRoot == null || _trailTip == null) return;

        if (_isRecording)
        {
            RecordSample();
        }

        RemoveExpiredSamples();
        RebuildMesh();
    }

    public void Play()
    {
        ClearTrail();
        _isRecording = true;

        RecordSample();
    }

    public void Stop()
    {
        _isRecording = false;
    }

    public void StopImmediate()
    {
        _isRecording = false;
        ClearTrail();
    }

    private void RecordSample()
    {
        Vector3 rootPosition = _trailRoot.position;
        Vector3 tipPosition = _trailTip.position;

        if (_samples.Count > 0)
        {
            TrailSample lastSample = _samples[^1];

            float rootDistance = (rootPosition - lastSample.rootPosition).sqrMagnitude;
            float tipDistance = (tipPosition - lastSample.tipPosition).sqrMagnitude;
            float minDistanceSqr = _minSampleDistance * _minSampleDistance;

            if (rootDistance < minDistanceSqr && tipDistance < minDistanceSqr) return;
        }

        _samples.Add(new TrailSample(rootPosition, tipPosition, Time.time));
    }

    private void RemoveExpiredSamples()
    {
        float expireTime = Time.time - _trailDuration;

        while (_samples.Count > 0 && _samples[0].createdTime < expireTime)
        {
            _samples.RemoveAt(0);
        }
    }

    private void RebuildMesh()
    {
        _mesh.Clear();

        if (_samples.Count < 2)
        {
            _meshRenderer.enabled = false;
            return;
        }

        _vertices.Clear();
        _triangles.Clear();
        _uvs.Clear();
        _colors.Clear();

        int sampleCount = _samples.Count;

        for (int i = 0; i < sampleCount; i++)
        {
            TrailSample sample = _samples[i];

            // 월드 좌표 기록값을 현재 SwordTrail 로컬 좌표로 변환
            _vertices.Add(transform.InverseTransformPoint(sample.rootPosition));
            _vertices.Add(transform.InverseTransformPoint(sample.tipPosition));

            float normalizedPosition = i / (float)(sampleCount - 1);

            _uvs.Add(new Vector2(normalizedPosition, 0f));
            _uvs.Add(new Vector2(normalizedPosition, 1f));

            // 오래된 Trail부터 Alpha 감소
            Color vertexColor = new Color(1f, 1f, 1f, normalizedPosition);
            _colors.Add(vertexColor);
            _colors.Add(vertexColor);
        }

        for (int i = 0; i < sampleCount - 1; i++)
        {
            int currentRoot = i * 2;
            int currentTip = currentRoot + 1;
            int nextRoot = currentRoot + 2;
            int nextTip = currentRoot + 3;

            _triangles.Add(currentRoot);
            _triangles.Add(nextRoot);
            _triangles.Add(currentTip);

            _triangles.Add(currentTip);
            _triangles.Add(nextRoot);
            _triangles.Add(nextTip);
        }

        _mesh.SetVertices(_vertices);
        _mesh.SetTriangles(_triangles, 0);
        _mesh.SetUVs(0, _uvs);
        _mesh.SetColors(_colors);
        _mesh.RecalculateBounds();

        _meshRenderer.enabled = true;
    }

    private void ClearTrail()
    {
        _samples.Clear();

        if (_mesh != null)
        {
            _mesh.Clear();
        }

        if (_meshRenderer != null)
        {
            _meshRenderer.enabled = false;
        }
    }

    private void OnDisable()
    {
        _isRecording = false;
        ClearTrail();
    }

    private void OnDestroy()
    {
        if (_mesh != null)
        {
            Destroy(_mesh);
        }
    }
}