using System.Collections;
using UnityEngine;
using UnityEngine.AI; // AI, 내비게이션 시스템 관련 코드 가져오기

// 좀비 AI 구현
public class Zombie : LivingEntity
{
    private MaterialPropertyBlock mpb;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Hit Flash")]
    public Color hitFlashColor = Color.white;     // 아티스트 손잡이
    public float hitFlashIntensity = 2f;          // HDR 배수
    public float hitFlashDuration = 0.08f;

    private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
    [Header("Dissolve")]
    public float dissolveDelay = 3f;              // 시체가 누워 있는 시간
    public float dissolveDuration = 2f;

    public LayerMask whatIsTarget; // 추적 대상 레이어

    private LivingEntity targetEntity; // 추적 대상
    private NavMeshAgent navMeshAgent; // 경로 계산 AI 에이전트

    public ParticleSystem hitEffect; // 피격 시 재생할 파티클 효과
    public AudioClip deathSound; // 사망 시 재생할 소리
    public AudioClip hitSound; // 피격 시 재생할 소리

    private Animator zombieAnimator; // 애니메이터 컴포넌트
    private AudioSource zombieAudioPlayer; // 오디오 소스 컴포넌트
    private Renderer zombieRenderer; // 렌더러 컴포넌트

    public float damage = 20f; // 공격력
    public float timeBetAttack = 0.5f; // 공격 간격
    private float lastAttackTime; // 마지막 공격 시점

    // 추적할 대상이 존재하는지 알려주는 프로퍼티
    private bool hasTarget {
        get
        {
            // 추적할 대상이 존재하고, 대상이 사망하지 않았다면 true
            if (targetEntity != null && !targetEntity.dead)
            {
                return true;
            }

            // 그렇지 않다면 false
            return false;
        }
    }

    private void Awake() 
    {
        // 게임 오브젝트로부터 사용할 컴포넌트 가져오기
        navMeshAgent = GetComponent<NavMeshAgent>();
        zombieAnimator = GetComponent<Animator>();
        zombieAudioPlayer = GetComponent<AudioSource>();

        // 렌더러 컴포넌트는 자식 게임 오브젝트에 있으므로
        // GetComponentInChildren() 메서드 사용
        zombieRenderer = GetComponentInChildren<Renderer>();
        // MaterialPropertyBlock 생성
        mpb = new MaterialPropertyBlock();
        // 한 번만 만들어 계속 재사용 (매번 new 하면 쓰레기 메모리가 쌓임)
    }

    // 좀비 AI의 초기 스펙을 결정하는 셋업 메서드
    public void Setup(ZombieData zombieData) 
    {
        // 체력 설정
        startingHealth = zombieData.health;
        health = zombieData.health;
        // 공격력 설정
        damage = zombieData.damage;
        // 내비메시 에이전트의 이동 속도 설정
        navMeshAgent.speed = zombieData.speed;
        // 렌더러가 사용 중인 머티리얼의 컬러를 변경, 외형색이 변경
        zombieRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(BaseColorId, zombieData.skinColor);
        // 디졸브 초기화 (풀링 대비 습관)
        mpb.SetFloat(DissolveId, 0f);
        // 주의 : Zombie 에 OnEnable() 을 새로 만들어 넣지 않는다.
        //        LivingEntity 의 OnEnable(체력 · dead 초기화)을 가려 부모 초기화가 실행되지 않는다
        zombieRenderer.SetPropertyBlock(mpb);    
    }

    private void Start() {
        // 게임 오브젝트 활성화와 동시에 AI의 추적 루틴 시작
        StartCoroutine(UpdatePath());
    }

    private void Update() {
        // 추적 대상의 존재 여부에 따라 다른 애니메이션 재생
        zombieAnimator.SetBool("HasTarget", hasTarget);
    }

    // 주기적으로 추적할 대상의 위치를 찾아 경로 갱신
    private IEnumerator UpdatePath() 
    {
        // 살아 있는 동안 무한 루프
        // NavMesh 에서 벗어났으면 이동 명령을 내리지 않고, 가장 가까운 NavMesh 위로 다시 붙인다
         // 살아 있는 동안 무한 루프
        while (!dead)
        {
            if (!navMeshAgent.isOnNavMesh)
            {
                // NavMesh 에서 벗어났으면 이동 명령을 내리지 않고, 가장 가까운 NavMesh 위로 다시 붙인다
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    navMeshAgent.Warp(hit.position);
                }
                else
                {
                    Debug.LogWarning($"{name} : NavMesh 밖 {transform.position}", this);
                }
            }
            else if (hasTarget)
            {
                navMeshAgent.isStopped = false;
                navMeshAgent.SetDestination(targetEntity.transform.position);
            }
            else
            {
                navMeshAgent.isStopped = true;
                Collider[] colliders = Physics.OverlapSphere(transform.position, 20f, whatIsTarget);
                for (int i = 0; i < colliders.Length; i++)
                {
                    LivingEntity livingEntity = colliders[i].GetComponent<LivingEntity>();
                    if (livingEntity != null && !livingEntity.dead)
                    {
                        targetEntity = livingEntity;
                        break;
                    }
                }
            }
            yield return new WaitForSeconds(0.25f);
        }
    }


    // 데미지를 입었을 때 실행할 처리
    public override void OnDamage(float damage, Vector3 hitPoint, Vector3 hitNormal) 
    {
        // 아직 사망하지 않은 경우에만 피격 효과 재생
        if (!dead)
        {
            // 공격받은 지점과 방향으로 파티클 효과 재생
            hitEffect.transform.position = hitPoint;
            hitEffect.transform.rotation = Quaternion.LookRotation(hitNormal);
            hitEffect.Play();
            // 피격 소리 재생
            zombieAudioPlayer.PlayOneShot(hitSound);
        }

        // LivingEntity의 OnDamage()를 실행하여 데미지 적용
        base.OnDamage(damage, hitPoint, hitNormal);
        if (!dead) StartCoroutine(HitFlash());
    }

    private IEnumerator HitFlash() 
    {
        zombieRenderer.GetPropertyBlock(mpb);    // 기존 값(skinColor) 유지
        mpb.SetColor(EmissionColorId, hitFlashColor * hitFlashIntensity);
        zombieRenderer.SetPropertyBlock(mpb);

        yield return new WaitForSeconds(hitFlashDuration);

        zombieRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(EmissionColorId, Color.black);
        zombieRenderer.SetPropertyBlock(mpb);
    }

    // 사망 처리
    public override void Die() 
    {
        // LivingEntity의 Die()를 실행하여 기본 사망 처리 실행
        base.Die();
        StartCoroutine(DissolveRoutine());   
        // ← 맨 아래에서 여기로 옮긴다 (아래 줄에서 오류가 나도 디졸브는 시작된다)

        // 다른 AI를 방해하지 않도록 자신의 모든 콜라이더를 비활성화
        // Collider[] zombieColliders = GetComponent<Collider>();
        Collider[] zombieColliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < zombieColliders.Length; i++)
        {
            //zombieAudioPlayer[i].enabled = false;
            zombieColliders[i].enabled = false;       // ← 이렇게 바꿔야 함
        }
        // AI 추적을 중지하고 내비메시 컴포넌트 비활성화
        if (navMeshAgent.isOnNavMesh) navMeshAgent.isStopped = true;
        navMeshAgent.enabled = false;
        // 사망 애니메이션 재생
        zombieAnimator.SetTrigger("Die");
        // 사망 효과음 재생
        zombieAudioPlayer.PlayOneShot(deathSound);
    }

    private IEnumerator DissolveRoutine() 
    {
        yield return new WaitForSeconds(dissolveDelay);

        float t = 0f;
        while (t < dissolveDuration) 
        {
            t += Time.deltaTime;
            zombieRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(DissolveId, t / dissolveDuration);
            zombieRenderer.SetPropertyBlock(mpb);
            yield return null;
        }
    }

    private void OnTriggerStay(Collider other) 
    {
        // 자신이 사망하지 않았으며,
        // 최근 공격 시점에서 timeBetAttack 이상 시간이 지났다면 공격 가능
        if (!dead && Time.time >= lastAttackTime + timeBetAttack)
        {
            // 충돌한 상대방 게임 오브젝트로부터 LivingEntity 컴포넌트 가져오기
            LivingEntity attackTarget = other.GetComponent<LivingEntity>();
            // 상대방의 LivingEntity가 자신의 추적 대상이라면 공격 실행
            if (attackTarget != null && attackTarget == targetEntity)
            {
                // 최근 공격 시간 갱신
                lastAttackTime = Time.time;
                // 상대방의 피격 위치와 피격 방향을 근사값으로 계산
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 hitNormal = transform.position - other.transform.position;
                // 공격 실행
                attackTarget.OnDamage(damage, hitPoint, hitNormal);
            }
        }
    }
}