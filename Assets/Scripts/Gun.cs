using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;    
using System.Collections;
using UnityEngine;

// 총을 구현
public class Gun : MonoBehaviour
{
    // 총의 상태를 표현하는 데 사용할 타입을 선언
    public enum State
    {
        Ready, // 발사 준비됨
        Empty, // 탄알집이 빔
        Reloading // 재장전 중
    }

    public State state { get; private set; } // 현재 총의 상태

    [Header("Bullet Hole")]
    public DecalProjector bulletHolePrefab;       // using UnityEngine.Rendering.Universal; 필요
    public int bulletHoleCount = 30;              // 링 버퍼 크기 (아티스트 손잡이)
    private DecalProjector[] bulletHoles;
    private int bulletHoleIndex;
    
    public Transform fireTransform; // 탄알이 발사될 위치

    public ParticleSystem muzzleFlashEffect; // 총구 화염 효과
    public ParticleSystem shellEjectEffect; // 탄피 배출 효과

    private LineRenderer bulletLineRenderer; // 탄알 궤적을 그리기 위한 렌더러

    private AudioSource gunAudioPlayer; // 총 소리 재생기

    public GunData gunData; // 총의 현재 데이터

    private float fireDistance = 50f; // 사정거리

    public int ammoRemain = 100; // 남은 전체 탄알
    public int magAmmo; // 현재 탄알집에 남아 있는 탄알

    private float lastFireTime; // 총을 마지막으로 발사한 시점

    private CinemachineImpulseSource impulseSource; // 총 발사 시 카메라 흔들림을 위한 소스

    public Light muzzleLight; // 총구 화염 효과를 위한 라이트

    private void Awake()
    {
        // 사용할 컴포넌트의 참조 가져오기
        gunAudioPlayer = GetComponent<AudioSource>();
        bulletLineRenderer = GetComponent<LineRenderer>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
        // 사용할 점을 두 개로 변경
        bulletLineRenderer.positionCount = 2;
        // 라인 렌더러를 비활성화
        bulletLineRenderer.enabled = false;
        if (muzzleLight == null) muzzleLight = GetComponentInChildren<Light>();

        bulletHoles = new DecalProjector[bulletHoleCount];
        for (int i = 0; i < bulletHoleCount; i++) 
        {
            bulletHoles[i] = Instantiate(bulletHolePrefab);
            bulletHoles[i].gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        // 전체 예비 탄알 양을 초기화
        ammoRemain = gunData.startAmmoRemain;
        // 현재 탄창을 가득 채우기
        magAmmo = gunData.magCapacity;

        // 총의 현재 상태를 발사 준비된 상태로 변경
        state = State.Ready;
        // 마지막 발사 시점 초기화
        lastFireTime = 0;
    }

    // 발사 시도
    public void Fire()
    {
        // 현재 상태가 발사 가능한 상태
        // && 마지막 총 발사 시점에서 gunData.timeBetFire 이상 시간이 지남
        if (state == State.Ready && Time.time >= lastFireTime + gunData.timeBetFire)
        {
            // 마지막 총 발사 시점 갱신
            lastFireTime = Time.time;
            // 실제 발사 처리 실행
            Shot();
        }
    }

    // 실제 발사 처리
    private void Shot()
    {
        impulseSource.GenerateImpulse(); // 총 발사 시 카메라 흔들림 발생. Default Velocity로 발생

        // 레이캐스트에 의한 충돌 정보를 저장하는 컨테이너
        RaycastHit hit;
        // 탄알이 맞은 곳을 저장할 변수
        Vector3 hitPosition = Vector3.zero;

        // 레이캐스트 (시작지점, 방향, 충돌 정보 컨테이너, 사정거리)
        if (Physics.Raycast(fireTransform.position, fireTransform.forward, out hit, fireDistance))
        {
            // 레이가 어떤 물체와 충돌한 경우

            // 충돌한 상대방으로부터 IDamageable 컴포넌트를 가져오기 시도
            IDamageable target = hit.collider.GetComponent<IDamageable>();
            // 상대방으로부터 IDamageable 컴포넌트를 가져오는데 성공했다면
            if (target != null)
            {
                // 상대방이 IDamageable 컴포넌트를 가지고 있다면 데미지 전달
                target.OnDamage(gunData.damage, hit.point, hit.normal);
            }

            // 레이가 충돌한 위치 저장
            hitPosition = hit.point;
            if (target != null) 
            {
                target.OnDamage(gunData.damage, hit.point, hit.normal);
            } 
            else 
            {
                PlaceBulletHole(hit.point, hit.normal);
            }
        }
        else
        {
            // 레이가 다른 물체와 충돌하지 않았다면
            // 탄알이 최대 사정거리까지 날아갔을 때의 위치를 충돌 위치로 사용
            hitPosition = fireTransform.position + fireTransform.forward * fireDistance;
        }
        // 발사 이펙트 재생 시작
        StartCoroutine(ShotEffect(hitPosition));

        // 남은 탄알 수를 -1
        magAmmo--;
        if (magAmmo <= 0)
        {
            // 탄알이 다 떨어졌다면 총의 상태를 Empty 상태로 변경
            state = State.Empty;
        }
    }

    // 발사 이펙트와 소리를 재생하고 탄알 궤적을 그림
    private IEnumerator ShotEffect(Vector3 hitPosition) 
    {
        // 총구 화염 효과 재생
        muzzleFlashEffect.Play();
        if (muzzleLight != null) muzzleLight.enabled = true;    // 라이트가 없어도 나머지 연출은 계속
        // 탄피 배출 효과 재생
        shellEjectEffect.Play();
        
        gunAudioPlayer.pitch = Random.Range(0.94f, 1.06f);
        gunAudioPlayer.PlayOneShot(gunData.shotClip);
        
        // 총격 소리 재생
        gunAudioPlayer.PlayOneShot(gunData.shotClip);

        // 선의 시작점은 총구의 위치
        bulletLineRenderer.SetPosition(0, fireTransform.position);
        // 선의 끝점은 입력으로 들어온 충돌 위치
        bulletLineRenderer.SetPosition(1, hitPosition);
        // 라인 렌더러를 활성화하여 탄알 궤적을 그림
        bulletLineRenderer.enabled = true;

        // 0.03초 동안 잠시 처리를 대기
        yield return new WaitForSeconds(0.03f);
        // 총구 라이트를 비활성화
        if (muzzleLight != null) muzzleLight.enabled = false;
        // 라인 렌더러를 비활성화하여 탄알 궤적을 지움
        bulletLineRenderer.enabled = false;
    }

    private void PlaceBulletHole(Vector3 point, Vector3 normal) 
    {
        DecalProjector hole = bulletHoles[bulletHoleIndex];
        bulletHoleIndex = (bulletHoleIndex + 1) % bulletHoleCount;     // 가장 오래된 것부터 재사용

        hole.transform.SetPositionAndRotation(point, Quaternion.LookRotation(-normal, Random.insideUnitSphere)); // 투영 방향은 표면 안쪽, 회전은 랜덤
        hole.gameObject.SetActive(true);
    }
    public bool Reload()
    {
        if (state == State.Reloading || magAmmo >= gunData.magCapacity)
        {
            // 이미 재장전 중이거나 남은 탄알이 없거나
            // 탄창에 탄알이 이미 가득한 경우 재장전할 수 없음
            return false;
        }
        // 재장전 처리 시작
        StartCoroutine(ReloadRoutine());
        return true;
    }

    // 실제 재장전 처리를 진행
    private IEnumerator ReloadRoutine() {
        // 현재 상태를 재장전 중 상태로 전환
        state = State.Reloading;
        // 재장전 소리 재생
        gunAudioPlayer.PlayOneShot(gunData.reloadClip);

        // 재장전 소요 시간 만큼 처리 쉬기
        yield return new WaitForSeconds(gunData.reloadTime);

        // 탄창이 채울 탄알 수 계산
        int ammoToFill = gunData.magCapacity - magAmmo;

        // 탄창에 채워야 할 탄알 수가 남은 탄알 수보다 많다면
        if (ammoRemain < ammoToFill)
        {
            // 채워야 할 탄알 수를 남은 탄알 수에 맞춰 줄임
            ammoToFill = ammoRemain;
        }
        // 탄창을 채움
        magAmmo += ammoToFill;
        // 남은 탄알 수에서 탄창에 채운만큼 탄알을 뺌
        ammoRemain -= ammoToFill;

        // 총의 현재 상태를 발사 준비된 상태로 변경
        state = State.Ready;
    }
}