using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private Enemy _enemy;
    private Animator _animator;
    public CharacterController CharacterController {  get; private set; }

    private float _walkSpeed = 2f;      // Orbit 상태일때 걷는 속도
    private float _runSpeed = 5f;       // Chasing 상태일때 뛰는 속도
    private float _movementRotationSpeed = 10f;
    private float _gravity = 9.81f;
    private float _orbitRadius = 3.6f;

    private Transform _targetTransform;
    private float _verticalVelocity = 0f;
    private int _orbitDirection = 1;    // 1 : 시계방향, -1 : 반시계방향 (상태 진입 시 랜덤 결정)

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _animator = GetComponent<Animator>();
        CharacterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        // 중력은 바닥에 닿아있지 않다면 매 프레임 기본적으로 적용
        ApplyGravity();

        if (_targetTransform == null) return;

        switch (_enemy.CurrentState)
        {
            case EnemyState.Chase:
                ChaseAndRun();
                break;
            case EnemyState.Orbit:
                OrbitAndWalk();
                break;
        }
    }

    private void OnAnimatorMove()
    {
        if (_enemy.CurrentState != EnemyState.Attack) return;

        transform.position += _animator.deltaPosition;
    }

    public void StartMoving(Transform target)
    {
        _targetTransform = target;
    }

    public void StartOrbiting(Transform target)
    {
        _targetTransform = target;
        // 상태 진입 시 회전 방향 랜덤 결정
        _orbitDirection = Random.Range(0, 2) == 0 ? 1 : -1;
    }

    public void StopMoving()
    {
        _targetTransform = null;
        CharacterController.Move(Vector3.zero);
        _enemy.Animation.SetMoveSpeed(0);
    }

    private void MoveForward(Vector3 direction, float speed)
    {
        direction.y = 0f;
        direction.Normalize();

        CharacterController.Move(direction * speed * Time.deltaTime);
        _enemy.Animation.SetMoveSpeed(speed);

        RotateToTarget(_targetTransform.position);
    }

    private void ChaseAndRun()
    {
        Vector3 dirToTarget = (_targetTransform.position - transform.position);
        MoveForward(dirToTarget, _runSpeed);
    }

    private void OrbitAndWalk()
    {
        Vector3 dirToTarget = (_targetTransform.position - transform.position);
        float distance = dirToTarget.magnitude;

        Vector3 orbitDir = Vector3.Cross(dirToTarget.normalized, Vector3.up) * _orbitDirection;

        // AI의 진입 사거리가 4m 라면, Orbit을 도는 목표 반지름(_orbitRadius)은 그보다 약간 안쪽인 4m 부근이거나 
        // 물러서는 판정선(distance < _orbitRadius - 0.5f)이 AI 추격 시작 조건인 4m를 절대 넘지 않게 조절해야 합니다.

        Vector3 correctionDir = Vector3.zero;

        if (distance > _orbitRadius + 0.3f)
            correctionDir = dirToTarget.normalized; // 너무 멀면 다가감
        else if (distance < _orbitRadius - 0.3f)
            correctionDir = -dirToTarget.normalized; // 너무 가까우면 물러남 (물러나도 3.9m 이므로 AI 추격선인 4.0m를 안 넘음!)

        Vector3 finalDir = (orbitDir + correctionDir * 0.5f).normalized;
        MoveForward(finalDir, _walkSpeed);
    }

    public void RotateToTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position);
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _movementRotationSpeed * Time.deltaTime);
        }
    }

    private void ApplyGravity()
    {
        if (CharacterController.isGrounded)
        {
            _verticalVelocity = -0.5f; // 바닥에 안정적으로 붙어있도록 살짝 음수 유지
        }
        else
        {
            _verticalVelocity -= _gravity * Time.deltaTime; // 공중이라면 중력 가속도 적용
        }
    }

}
