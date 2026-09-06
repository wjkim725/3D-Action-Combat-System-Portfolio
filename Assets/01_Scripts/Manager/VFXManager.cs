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


    #region [WarningSign 영역]

    public void PlayWarningSign(Enemy enemy, DefenseWindowType defenseType)
    {
        if (enemy == null || _warningSignController == null) return;

        _warningSignController.Play(enemy.transform, defenseType);
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
    /// 타격 대상 표면에 HitMark 생성 및 재생
    /// </summary>
    public void PlayHitMark(
        HitMarkDataSO data,
        Transform target,
        Vector3 impactPoint,
        Vector3 surfaceNormal,
        Vector3 slashDirection)
    {
        if (data == null || target == null || _hitMarkPrefab == null) return;

        HitMarkController hitMark = Instantiate(
            _hitMarkPrefab,
            impactPoint,
            Quaternion.identity,
            target
        );

        hitMark.Play(
            data,
            impactPoint,
            surfaceNormal,
            slashDirection
        );
    }

    #endregion
}