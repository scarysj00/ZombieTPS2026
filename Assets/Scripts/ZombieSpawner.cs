using UnityEngine.Rendering; 
using UnityEngine.AI;
using System.Collections.Generic;
using UnityEngine;

// 좀비 게임 오브젝트를 주기적으로 생성
public class ZombieSpawner : MonoBehaviour 
{
    [Header("Mood")]
    public Volume nightVolume;                    // Night Volume 할당
    public int fullNightWave = 8;                 // 이 웨이브에서 weight 1 (아티스트 손잡이)
    public float moodBlendSpeed = 0.5f;           // 초당 weight 변화량
    
    public Zombie zombiePrefab; // 생성할 좀비 원본 프리팹

    public ZombieData[] zombieDatas; // 사용할 좀비 셋업 데이터들
    public Transform[] spawnPoints; // 좀비 AI를 소환할 위치들

    private List<Zombie> zombies = new List<Zombie>(); // 생성된 좀비들을 담는 리스트
    private int wave; // 현재 웨이브

    private void Update() 
    {
        // 게임 오버 상태일때는 생성하지 않음
        if (GameManager.instance != null && GameManager.instance.isGameover)
        {
            return;
        }

        // 좀비를 모두 물리친 경우 다음 스폰 실행
        if (zombies.Count <= 0)
        {
            SpawnWave();
        }

        // UI 갱신
        UpdateUI();

        float targetWeight = Mathf.Clamp01((float)wave / fullNightWave);
        if (nightVolume != null) nightVolume.weight = Mathf.MoveTowards(nightVolume.weight, targetWeight, moodBlendSpeed * Time.deltaTime);
    }

    // 웨이브 정보를 UI로 표시s
    private void UpdateUI()
    {
        // 현재 웨이브와 남은 적 수 표시
        UIManager.instance.UpdateWaveText(wave, zombies.Count);
    }

    // 현재 웨이브에 맞춰 좀비들을 생성
    private void SpawnWave() 
    {
        wave++; // 웨이브 1 증가

        // 현재 웨이브 * 1.5에 반올림한 개수만큼 좀비 생성
        int spawnCount = Mathf.RoundToInt(wave * 1.5f); 
        
        // spawnCount 만큼 좀비 생성
        for (int i = 0; i < spawnCount; i++)
        {
            // 좀비 생성
            CreateZombie();
        }
    }

    // 좀비를 생성하고 생성한 좀비에게 추적할 대상을 할당
    private void CreateZombie() 
    {
        ZombieData zombieData = zombieDatas[Random.Range(0, zombieDatas.Length)];

    // NavMesh 위에 있는 스폰 지점을 고른다 (최대 지점 수만큼 다시 시도)
        Vector3 spawnPos = Vector3.zero;
        bool found = false;
        for (int i = 0; i < spawnPoints.Length; i++) 
        {
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit hit, 2f, NavMesh.AllAreas)) 
            {
                spawnPos = hit.position;
                found = true;
                break;
            }
            Debug.LogWarning($"{spawnPoint.name} 근처 2m 안에 NavMesh 가 없다", spawnPoint);
        }
        if (!found) return;             // 쓸 수 있는 지점이 하나도 없으면 생성하지 않는다

        Zombie zombie = Instantiate(zombiePrefab, spawnPos, Quaternion.identity);
        zombie.Setup(zombieData);
        zombies.Add(zombie);

        zombie.onDeath += () => zombies.Remove(zombie);
        zombie.onDeath += () => Destroy(zombie.gameObject, 10f);
        zombie.onDeath += () => GameManager.instance.AddScore(100);
    }
}