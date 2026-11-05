# ErikaMechanic 연결 및 검증

## 구현 범위

ErikaMechanic은 Hero.CurrentState를 공유한다. 자체 상태 enum은 없다.
Hero.EnterState/ExitState가 전용 기믹을 호출하고, ErikaMechanic.Update가
쿨타임과 상태별 처리를 갱신한다. 상태 변경은 Hero.ChangeState에 위임한다.
씬·프리팹에 컴포넌트를 부착하거나 애니메이션/데이터 에셋을 수정하지 않았다.

코드로 연결한 기능:
- 평꾹 HitFrame에서 투사체 생성 성공 시 스택 1 증가. 최대 4, 여진 1회 소비 1.
- InActive인 오프필드 Erika의 스택/10초 쿨타임/MagneticTarget/연출 상태 검사.
- 카메라 쪽 좌우 부채꼴의 등장 위치, 지면·경사·공간·화면·가림 검사.
- 본체 표시, 무적, 모션 시작, 타격 프레임 방출, 비용 소비, 종료 및 중단 정리.
- 일반 교대 인수에서 위치·모션 유지, 추가 행동/일반 교대 차단, 패링 교대 허용.
- 패링·빠른 지원·콤보 스킬·사망 강제 진입 시 본체 상태 전환으로 기존 여진 정리.
- 첫 방출 전 대상 상실은 비용 없이 종료. 방출 후 대상 상실은 마지막 조준점으로 계속 발사.
- 투사체 생성/직선 이동/수명/지형 및 적 충돌/공통 피해 계산/명중 자원 반환.
- 사망 시 자원 제거, 부활 시 Awake의 기본 자원 복원.

피해는 CombatManager.ProcessHit를 사용한다. 원거리 판정은 기존의 비어 있던
ProjectileLauncher/ProjectileController를 구현하여 공유한다. 이미 방출된 투사체는
본체 교대·모션 중단과 무관하게 수명 또는 충돌까지 유지한다.

## 부착을 진행할 때의 대상

대상 프리팹: Assets/03_Prefabs/Hero/Erika/Hero_Erika.prefab
대상 GameObject: Hero_Erika (Hero, Animator가 있는 본체)
추가 Component: ErikaMechanic. 현재 부착은 보류 상태다.

### ErikaMechanic 필드

| 필드 | 연결/의미 |
|---|---|
| Max Stacks / Stack Cost / Aftershock Interval | 4 / 1 / 10초. 조정 가능 |
| Initial Stacks / Initial Cooldown | 최초 시작과 부활의 기본 자원. 현재 코드 기본값은 0 / 0 |
| Projectile Launcher | 여진 발사용 ProjectileLauncher 컴포넌트 |
| Aftershock State Name | 여진 Animator 상태 전체 경로. 코드 기본값 Base Layer.Aftershock |
| Aftershock Sequence > Hits | 발사 프레임 수만큼 HitData 등록. Hit Detection Type은 ProjectileCollision |
| Viewing Camera | 화면을 출력하는 실제 Camera. 미할당 시 MainCamera 사용 |
| Spawn Range | 최소/최대 거리, 좌우 각도, 검사 횟수/재시도 간격 |
| Ground Mask | 지면 |
| Blocking Mask | 벽·적·Hero 등 공간 차단. 지면 제외, 비트리거 몸체 포함 |
| Occlusion Mask | 카메라 시야를 가리는 벽·지형 등 |

MotionSequenceData는 기존 데이터 형식을 재사용한다. 여진은 투사체 방식이므로
그 형식의 근접 범위/전진 오프셋/자력 이동 계수는 사용하지 않는다.
등장 거리 3~5m, 각도 45~80도, 최대 12회, 재시도 0.25초는 조정용 초기값이다.
캐릭터 크기·발 위치·허용 경사는 실제 CharacterController 설정을 사용한다.
최대 스택·소비량 외 피해/그로기/자원 반환 계수와 여진 모션 선택은 미확정이므로
기존 공격 수치를 임의로 복사하지 않았다. Hits에 의도한 값을 입력해야 한다.

### Animator와 Animation Event

현재 Erika_AnimCon.controller에는 Aftershock 상태가 없다.
Base Layer에 여진 상태를 만들고 반복하지 않는 여진 클립을 연결해야 한다.
기존 후보 클립은 Assets/05_Animations/HeroAnimations/Erika/Aftershock에 있다.
어떤 후보를 사용할지는 이번 코드 변경에서 결정하지 않았다.

- 각 발사 프레임: HeroAnimationEventHandler.OnAftershockHitFrame, Int = 0, 1, 2 ...
- 종료 시점: HeroAnimationEventHandler.OnAftershockEnd
- 두 이벤트 모두 AnimationEvent를 받으며 현재 여진 상태의 이벤트만 처리한다.
- 종료 이벤트가 없어도 실제 여진 Animator 상태의 normalizedTime >= 1이면 종료한다.
- 여진 상태는 자동 퇴장 전이를 두지 않는다. 코드에서 Idle 또는 InActive로 종료한다.
- 여진 도중 일반 인수 시 Animator 재시작 없음. 별도 이동/무적 종료 이벤트는 넣지 않는다.

평꾹은 기존 OnChargeAttackHitFrame 경로를 사용한다. Erika CombatData의
Charge Attack 해당 Hits를 ProjectileCollision으로 설정하고 HeroHitChecker의
Projectile Launcher를 연결해야 실제 발사와 스택 증가가 일어난다.

### ProjectileLauncher와 투사체 프리팹

Hero_Erika의 ProjectileLauncher에 Projectile Prefab, Launch Anchor, Launch Speed,
Life Time을 설정한다. 여진/평꾹이 같은 발사 설정을 사용하면 동일 컴포넌트를 공유한다.

투사체 프리팹은 ProjectileController와 표시용 메시/이펙트를 가진다.
ProjectileController.Collision Mask에 적과 지형 레이어를 지정한다.
이동 중 SphereCast와 시작점 OverlapSphere를 사용하므로 Rigidbody/센서는 필수가 아니다.
센서를 사용하면 ProjectileCollisionSensor를 Collision Sensor에 연결한다.
물리 Rigidbody를 함께 두는 경우 별도의 중력/이동이 중복되지 않게 kinematic으로 설정한다.
공격자와 아군은 제외하고 최초 적 또는 지형 충돌에서 투사체를 제거한다.
유도/관통/곡사/풀링은 별도 요구가 없으므로 직선 단발 구현이다.

## 교대 실행 경로

- HeroSwapSystem.CanBeSwappedIn은 Aftershock를 교대 후보로 허용한다.
- 일반 인수는 PartySwapManager.ExecuteSwap의 위치/표시/상태 재진입을 건너뛴다.
- Hero.CanReceiveInput과 CanRequestSwap은 Aftershock에서 거짓이다.
- CanRequestParry는 기존 패링 조건을 유지한다.
- 특수 진입은 기존 상태 전환이 ErikaMechanic.ExitState를 호출해 정리한다.
- 인수 후 패링으로 다른 Hero에게 넘어가도 Erika의 여진은 유지된다.
- 정상 종료는 PartyStateManager.CurrentHero를 조회하여 퇴장 여부를 결정한다.

## 확인할 플레이 항목

1. 평꾹 헛발사로 스택 증가, 다수 명중에도 추가 증가 없음, 최대 4.
2. 여진 첫 방출에서만 1 소비/10초 시작. 대상 상실 전후 비용과 남은 발사 확인.
3. 여진 인수 시 위치/모션/카메라 전환, 모션 종료 전 공격·이동·일반 교대 차단.
4. 패링 진입 취소와 인수 후 패링 퇴장 지속, 빠른지원/콤보/사망 강제 진입 정리.
5. 온필드 쿨타임 진행, 연출 중 새 여진 차단, 시전 중 중복 시작 없음.
6. 사망/부활 자원 초기화, 이전 클립 이벤트가 새 행동에 영향을 주지 않음.
7. 벽·적 겹침/절벽/화면 밖 후보 검사, 후보 실패 시 비용 없이 재시도.
8. 투사체 적/지형 충돌과 공격자·아군 제외, 피해·그로기·자원 반환 확인.

컴파일은 Unity 6000.3.13f1의 기존 Assembly-CSharp 응답 파일과 Roslyn으로 확인했다.
출력은 임시 폴더에 생성하여 Unity의 실제 어셈블리를 덮어쓰지 않았다.
플레이 검증은 컴포넌트·Animator·프리팹·데이터 연결 이후에 필요하다.
