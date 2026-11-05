using System.Collections.Generic;
using UnityEngine;

public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [Header("Warning Sign")]
    [SerializeField] private WarningSignController _warningSignController;

    [Header("Pooling VFX Object Root")]
    [SerializeField] private Transform _worldVFXRoot;

    [Header("Hit Impact")]
    [SerializeField] private HitImpactController _hitImpactPrefab;
    [SerializeField] private Transform _hitImpactPoolRoot;
    [SerializeField, Min(0)] private int _initialHitImpactPoolSize = 10;
    private readonly Queue<HitImpactController> _hitImpactPool = new();

    [Header("Hit Mark")]
    [SerializeField] private HitMarkController _hitMarkPrefab;
    [SerializeField, Min(0)] private int _initialHitMarkPoolSize = 10;
    private readonly Queue<HitMarkController> _hitMarkPool = new();
    private readonly HitMarkSurfaceResolver _hitMarkSurfaceResolver = new();


    private void Awake()
    {
        // 싱글톤 예외 처리 및 인스턴스 지정
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeHitImpactPool();
    }

    private void Start()
    {
        InitializeHitMarkPool();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        _hitMarkPool.Clear();
        _hitMarkSurfaceResolver.Dispose();
    }


    #region [WarningSign 영역]

    public void PlayWarningSign(Enemy enemy, DefenseWindowType defenseType)
    {
        if (enemy == null || _warningSignController == null) return;

        Transform warningSignPos = enemy.VFX.GetWarningSignTarget();

        _warningSignController.Play(warningSignPos, defenseType);
    }

    #endregion


    #region [Hit Impact 영역]

    /// <summary>
    /// Hit Impact Pool Queue 초기화 메서드
    /// </summary>
    private void InitializeHitImpactPool()
    {
        if (_hitImpactPrefab == null)
        {
            Debug.LogError("VFXManager: HitImpact 프리팹이 지정되지 않았습니다.");
            return;
        }

        for (int i = 0; i < _initialHitImpactPoolSize; i++)
        {
            _hitImpactPool.Enqueue(CreateHitImpactInstance());
        }
    }

    /// <summary>
    /// HitImpact 프리팹 인스턴스 생성 메서드
    /// </summary>
    private HitImpactController CreateHitImpactInstance()
    {
        Transform parent = _hitImpactPoolRoot != null
            ? _hitImpactPoolRoot
            : _worldVFXRoot;

        HitImpactController newImpact = Instantiate(_hitImpactPrefab, parent);

        // 재생 종료 시 VFXManager의 Pool 반환 메서드 호출 등록
        newImpact.Initialize(ReturnHitImpact);
        newImpact.gameObject.SetActive(false);

        return newImpact;
    }

    /// <summary>
    /// 공격자의 HitImpact VFX 데이터를 사용하여 타격 이펙트 재생
    /// </summary>
    public void PlayHitImpact(
        IAttacker attacker,
        Vector3 impactPoint,
        Vector3 impactDirection)
    {
        if (_hitImpactPrefab == null)
        {
            return;
        }

        if (!TryGetHitImpactVFXData(
            attacker,
            out HitImpactVFXDataSO hitImpactData))
        {
            return;
        }

        HitImpactController impact =
            _hitImpactPool.Count > 0
                ? _hitImpactPool.Dequeue()
                : CreateHitImpactInstance();

        impact.gameObject.SetActive(true);

        impact.Play(
            hitImpactData,
            impactPoint,
            impactDirection
        );

        // Test Code
        //Instantiate(_test, impactPoint, Quaternion.identity);
    }

    /// <summary>
    /// HitImpact Queue 반환 메서드
    /// </summary>
    private void ReturnHitImpact(HitImpactController impact)
    {
        if (impact == null) return;
        if (_hitImpactPool.Contains(impact)) return;

        impact.gameObject.SetActive(false);

        Transform parent = _hitImpactPoolRoot != null
            ? _hitImpactPoolRoot : _worldVFXRoot;

        impact.transform.SetParent(parent);
        _hitImpactPool.Enqueue(impact);
    }

    private bool TryGetHitImpactVFXData(IAttacker attacker, out HitImpactVFXDataSO hitImpactData)
    {
        hitImpactData = attacker switch
        {
            Hero hero when hero.VFX != null
                => hero.VFX.HitImpactData,

            Enemy enemy when enemy.VFX != null
                => enemy.VFX.HitImpactData,

            _ => null
        };

        return hitImpactData != null;
    }

    #endregion


    #region [Hit Mark 영역]

    /// <summary>
    /// 첫 타격 이전에 데칼 객체와 전용 머티리얼을 준비함.
    /// </summary>
    private void InitializeHitMarkPool()
    {
        if (_hitMarkPrefab == null) return;
        for (int i = 0; i < _initialHitMarkPoolSize; i++)
            _hitMarkPool.Enqueue(CreateHitMarkInstance());
    }

    /// <summary>
    /// 데칼의 활성화 경로를 거친 인스턴스를 비활성 대기 상태로 생성함.
    /// </summary>
    private HitMarkController CreateHitMarkInstance()
    {
        Transform root = _worldVFXRoot != null ? _worldVFXRoot : transform;
        HitMarkController hitMark = Instantiate(_hitMarkPrefab, root);
        hitMark.gameObject.SetActive(true);
        hitMark.Initialize(ReturnHitMark);
        hitMark.ResetForPool();
        return hitMark;
    }

    /// <summary>
    /// 사용이 끝난 데칼을 초기화하고 대기열에 한 번 반환함.
    /// </summary>
    private void ReturnHitMark(HitMarkController hitMark)
    {
        if (hitMark == null || _hitMarkPool.Contains(hitMark)) return;
        hitMark.ResetForPool();
        Transform root = _worldVFXRoot != null ? _worldVFXRoot : transform;
        hitMark.transform.SetParent(root, false);
        hitMark.transform.localPosition = Vector3.zero;
        hitMark.transform.localRotation = Quaternion.identity;
        hitMark.transform.localScale = _hitMarkPrefab.transform.localScale;
        _hitMarkPool.Enqueue(hitMark);
    }


    /// <summary>
    /// 타격 대상 표면에 HitMark 생성 및 재생
    /// </summary>
    public void PlayHitMark(
        HitMarkDataSO data,
        Transform target,
        Vector3 impactPoint,
        Vector3 impactDirection,
        Vector3 surfaceNormal,
        Vector3 slashDirection)
    {
        if (data == null || target == null) return;

        switch (data.hitMarkType)
        {
            case HitMarkType.Decal:
                PlayDecalHitMark(data, target, impactPoint, impactDirection, surfaceNormal, slashDirection);
                break;

            case HitMarkType.Arrow:
                PlayArrowHitMark(data, target, impactPoint, impactDirection);
                break;
        }
    }

    /// <summary>
    /// 메시 표면 위치와 법선을 보정한 뒤 데칼 잔흔을 재생한다.
    /// </summary>
    private void PlayDecalHitMark(
        HitMarkDataSO data,
        Transform target,
        Vector3 impactPoint,
        Vector3 impactDirection,
        Vector3 surfaceNormal,
        Vector3 slashDirection)
    {
        if (_hitMarkPrefab == null) return;

        // 표면을 찾지 못하면 판정 단계에서 전달된 위치와 법선을 그대로 사용한다.
        if (_hitMarkSurfaceResolver.TryGetSurface(
            target, impactPoint, impactDirection,
            out Vector3 surfacePoint, out Vector3 meshNormal))
        {
            impactPoint = surfacePoint;
            surfaceNormal = meshNormal;
        }

        HitMarkController hitMark = null;
        while (_hitMarkPool.Count > 0 && hitMark == null)
            hitMark = _hitMarkPool.Dequeue();
        if (hitMark == null) hitMark = CreateHitMarkInstance();

        if (!hitMark.Play(data, target, impactPoint, surfaceNormal, slashDirection,
            _hitMarkPrefab.transform.localScale))
            ReturnHitMark(hitMark);
    }

    /// <summary>
    /// 진행 방향으로 화살을 박아 몸통 본에 부착하고 유지 시간이 지나면 제거한다.
    /// </summary>
    private void PlayArrowHitMark(
        HitMarkDataSO data,
        Transform target,
        Vector3 impactPoint,
        Vector3 impactDirection)
    {
        if (data.arrowPrefab == null)
        {
            Debug.LogWarning($"[{data.name}] 화살 HitMark 프리팹이 지정되지 않았습니다.", data);
            return;
        }

        if (impactDirection.sqrMagnitude < 0.0001f) return;

        Vector3 direction = impactDirection.normalized;
        // 데칼과 공통으로 사용하는 표면 탐색 결과에서 화살은 위치만 보정한다.
        if (_hitMarkSurfaceResolver.TryGetSurface(
            target, impactPoint, direction, out Vector3 surfacePoint, out _))
        {
            impactPoint = surfacePoint;
        }

        Vector3 position = impactPoint + direction * Mathf.Max(0f, data.penetrationDepth);

        // 수직으로 떨어지는 화살도 회전 기준축이 진행 방향과 겹치지 않게 한다.
        Vector3 upDirection = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.999f
            ? Vector3.forward : Vector3.up;

        GameObject arrow = Instantiate(
            data.arrowPrefab,
            position,
            Quaternion.LookRotation(direction, upDirection)
        );

        // 부착 순간의 월드 위치·회전·크기를 유지하고 이후 몸통 애니메이션을 따라간다.
        Transform attachTarget = GetArrowAttachTarget(target);
        arrow.transform.SetParent(attachTarget, true);
        Destroy(arrow, Mathf.Max(0.01f, data.duration));
    }

    /// <summary>
    /// Humanoid의 상체 본을 우선 선택하고 사용할 본이 없으면 적 루트를 반환한다.
    /// </summary>
    private Transform GetArrowAttachTarget(Transform target)
    {
        Animator animator = target.GetComponent<Animator>();
        if (animator == null)
        {
            animator = target.GetComponentInChildren<Animator>();
        }

        // 유효한 Humanoid Avatar가 있어야 GetBoneTransform을 호출할 수 있다.
        if (animator == null || animator.avatar == null ||
            !animator.avatar.isValid || !animator.isHuman)
        {
            return target;
        }

        Transform bone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (bone != null) return bone;

        bone = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (bone != null) return bone;

        bone = animator.GetBoneTransform(HumanBodyBones.Spine);
        return bone != null ? bone : target;
    }

    #endregion
}
