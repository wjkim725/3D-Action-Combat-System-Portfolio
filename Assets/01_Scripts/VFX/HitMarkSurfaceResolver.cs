using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HitMark 표현 방식과 별개로 현재 스킨 메시의 표면 위치와 법선을 구한다.
/// </summary>
public sealed class HitMarkSurfaceResolver : IDisposable
{
    private Mesh _bakedMesh;
    private readonly List<SkinnedMeshRenderer> _renderers = new();
    private readonly List<Vector3> _vertices = new();
    private readonly List<int> _triangles = new();

    #region [표면 탐색 영역]

    /// <summary>
    /// 피격점을 지나는 진행선에서 가장 먼저 만나는 스킨 메시 표면을 반환한다.
    /// 정적 메시 또는 교차점이 없는 대상은 false를 반환하며 기존 피격점을 유지한다.
    /// </summary>
    public bool TryGetSurface(
        Transform target,
        Vector3 impactPoint,
        Vector3 impactDirection,
        out Vector3 surfacePoint,
        out Vector3 surfaceNormal)
    {
        surfacePoint = impactPoint;
        surfaceNormal = Vector3.zero;
        if (target == null || impactDirection.sqrMagnitude < 0.0001f) return false;

        Vector3 direction = impactDirection.normalized;
        target.GetComponentsInChildren(false, _renderers);

        // Collider 피격점이 메시 안에 있어도 입사면을 찾도록 메시 경계 밖에서 시작한다.
        float startDistance = 0.01f;
        foreach (SkinnedMeshRenderer renderer in _renderers)
        {
            if (!renderer.enabled || renderer.sharedMesh == null) continue;
            Bounds bounds = renderer.bounds;
            startDistance = Mathf.Max(startDistance,
                Vector3.Distance(impactPoint, bounds.center) + bounds.extents.magnitude + 0.01f);
        }

        Ray worldRay = new Ray(impactPoint - direction * startDistance, direction);
        float closestDistance = float.PositiveInfinity;
        bool found = false;

        foreach (SkinnedMeshRenderer renderer in _renderers)
        {
            if (!renderer.enabled || renderer.sharedMesh == null) continue;
            if (!renderer.bounds.IntersectRay(worldRay)) continue;

            if (_bakedMesh == null)
            {
                _bakedMesh = new Mesh { name = "HitMark Surface Buffer" };
                _bakedMesh.MarkDynamic();
            }

            // Renderer의 스케일을 보정한 로컬 메시를 받아 아래 월드 변환과 크기를 맞춘다.
            renderer.BakeMesh(_bakedMesh, true);
            _bakedMesh.GetVertices(_vertices);
            Matrix4x4 localToWorld = renderer.transform.localToWorldMatrix;
            Matrix4x4 worldToLocal = renderer.transform.worldToLocalMatrix;
            Vector3 localOrigin = worldToLocal.MultiplyPoint3x4(worldRay.origin);
            Vector3 localDirection = worldToLocal.MultiplyVector(direction);

            // 로컬 방향을 정규화하지 않아 교차 거리의 단위를 월드 거리로 유지한다.
            for (int subMesh = 0; subMesh < _bakedMesh.subMeshCount; subMesh++)
            {
                if (_bakedMesh.GetTopology(subMesh) != MeshTopology.Triangles) continue;
                _bakedMesh.GetTriangles(_triangles, subMesh);

                for (int i = 0; i < _triangles.Count; i += 3)
                {
                    Vector3 a = _vertices[_triangles[i]];
                    Vector3 b = _vertices[_triangles[i + 1]];
                    Vector3 c = _vertices[_triangles[i + 2]];
                    if (!TryIntersectTriangle(localOrigin, localDirection, a, b, c,
                        out float distance) || distance >= closestDistance) continue;

                    Vector3 normal = Vector3.Cross(
                        localToWorld.MultiplyVector(b - a),
                        localToWorld.MultiplyVector(c - a)).normalized;
                    if (Vector3.Dot(normal, direction) > 0f) normal = -normal;

                    closestDistance = distance;
                    surfacePoint = worldRay.GetPoint(distance);
                    surfaceNormal = normal;
                    found = true;
                }
            }
        }

        // 대상 파괴 후에도 Renderer 참조가 남지 않도록 작업 목록을 비운다.
        _renderers.Clear();
        return found;
    }

    /// <summary>
    /// 양면 삼각형과 진행선의 교차 여부 및 전방 교차 거리를 구한다.
    /// </summary>
    private bool TryIntersectTriangle(
        Vector3 origin, Vector3 direction,
        Vector3 a, Vector3 b, Vector3 c,
        out float distance)
    {
        distance = 0f;
        Vector3 edge1 = b - a;
        Vector3 edge2 = c - a;
        Vector3 cross = Vector3.Cross(direction, edge2);
        float determinant = Vector3.Dot(edge1, cross);
        float tolerance = 0.000001f * edge1.magnitude * edge2.magnitude * direction.magnitude;
        if (Mathf.Abs(determinant) <= tolerance) return false;

        float inverse = 1f / determinant;
        Vector3 offset = origin - a;
        float u = Vector3.Dot(offset, cross) * inverse;
        if (u < 0f || u > 1f) return false;

        Vector3 q = Vector3.Cross(offset, edge1);
        float v = Vector3.Dot(direction, q) * inverse;
        if (v < 0f || u + v > 1f) return false;

        distance = Vector3.Dot(edge2, q) * inverse;
        return distance >= 0f;
    }

    #endregion

    #region [정리 영역]

    /// <summary>
    /// 소유자가 종료될 때 계산용 메시와 작업 목록을 정리한다.
    /// </summary>
    public void Dispose()
    {
        if (_bakedMesh != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(_bakedMesh);
            else UnityEngine.Object.DestroyImmediate(_bakedMesh);
            _bakedMesh = null;
        }

        _renderers.Clear();
        _vertices.Clear();
        _triangles.Clear();
    }

    #endregion
}
