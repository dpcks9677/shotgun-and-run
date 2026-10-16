using System.Collections;
using System.Collections.Generic;
using ShotgunAndRun.Combat;
using ShotgunAndRun.Core;
using UnityEngine;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Enemy
{
    /// <summary>
    /// 던전(Run) 안의 적응형 BGM. 앰비언트 · 드럼 · 신스 · 습격 네 스템을 같은 dspTime에 시작해 두고 볼륨만 올리내린다.
    /// 단계는 누적된다: 적 인지(드럼) → 습격 임박(+신스) → 습격 중(+습격 스템).
    /// RunStarted에 런타임으로 생성되며(씬 · 프리팹 수정 없음) 튜토리얼은 RunStarted가 없어 재생되지 않는다.
    /// 던전 BGM 계획서: Docs/dungeon-bgm-plan.md
    /// </summary>
    public class DungeonMusicDirector : MonoBehaviour
    {
        // 스템 · 원샷 에셋 (Resources)
        private static readonly string[] StemPaths =
        {
            "Audio/Dungeon/dungeon_ambient_loop",
            "Audio/Dungeon/dungeon_drums_loop",
            "Audio/Dungeon/dungeon_synth_loop",
            "Audio/Dungeon/dungeon_raid_loop",
        };

        private const string OneShotPrefix = "Audio/Dungeon/Ambience/sfx_";
        private static readonly string[] OneShotNames =
        {
            "drip_01", "drip_02", "drip_03",
            "rustle_01", "rustle_02", "rustle_03",
            "metal_01", "metal_02",
            "stone_01", "stone_02",
            "wind_01", "wind_02",
        };

        private const int Ambient = 0;
        private const int Drums = 1;
        private const int Synth = 2;
        private const int Raid = 3;

        // 적 인지 · 습격 종료 폴링 간격. OverlapBox 1회와 집합 정리라 0.2초면 충분하다.
        private const float PollInterval = 0.2f;

        // 75 BPM의 1박. 레이어를 켤 때 박 경계에 맞춰 시작한다.
        private const float BeatLength = 0.8f;

        // 스템을 켜는 페이드인 시간
        private const float FadeInSeconds = 3f;

        // 드럼은 적이 시야를 벗어난 뒤 5초 두었다가 4초에 걸쳐 뺀다(잠깐 가려졌다 다시 보이는 경우 깜빡임 방지).
        private const float DrumHoldSeconds = 5f;
        private const float DrumFadeOutSeconds = 4f;

        // 신스 · 습격 스템은 습격이 끝난 뒤 2초 두었다가 4초에 걸쳐 뺀다.
        private const float RaidStemHoldSeconds = 2f;
        private const float RaidStemFadeOutSeconds = 4f;

        // RunEnded 시 전체 페이드아웃
        private const float EndFadeSeconds = 2f;

        // 첫 스폰까지 포탈 대기가 있어, 습격 시작 직후 8초는 적이 없어도 종료로 보지 않는다.
        private const float RaidStartGraceSeconds = 8f;

        // 스폰 간격(0.7초) · 포탈 대기 사이의 빈틈을 건너뛰기 위해 마지막 스폰 후 3초 유지되어야 종료로 본다.
        private const float SpawnGapSeconds = 3f;

        // 네 스템 단순 합산 피크가 +2.1 dBFS라 0.55(약 -5.2dB)를 곱해 -3 dBFS 근처로 맞춘다.
        private const float LayerGain = 0.55f;

        // 습격 중 신스는 -6dB로 낮춰 저역 뭉침을 막는다.
        private const float SynthRaidGain = 0.5f;

        // 컷씬 · 일시정지 중 전체 덕킹 배율과 스무딩 시간
        private const float DuckGain = 0.35f;
        private const float DuckSeconds = 0.5f;

        // 앰비언트 원샷 간격(초). 전투 중에는 타이머가 쉰다.
        private const float OneShotMinInterval = 6f;
        private const float OneShotMaxInterval = 18f;

        // PlayScheduled가 늦지 않도록 두는 시작 여유
        private const double ScheduleLeadSeconds = 0.1;

        // 카메라 크기 사각형 바깥 여유(유닛)
        private const float CameraMargin = 1f;

        private readonly AudioClip[] _stems = new AudioClip[StemPaths.Length];
        private readonly AudioClip[] _oneShots = new AudioClip[OneShotNames.Length];
        private readonly AudioSource[] _stemSources = new AudioSource[StemPaths.Length];
        private readonly AudioSource[] _oneShotSources = new AudioSource[2];

        // 레이어별 상태: 켜짐 의도(_on), 페이드 진행(_level 0~1), 박 경계 대기, 끄기 대기
        private readonly bool[] _on = new bool[StemPaths.Length];
        private readonly float[] _level = new float[StemPaths.Length];
        private readonly bool[] _beatPending = new bool[StemPaths.Length];
        private readonly float[] _beatWait = new float[StemPaths.Length];
        private readonly float[] _holdTimer = new float[StemPaths.Length];

        private readonly HashSet<global::Enemy> _raidEnemies = new HashSet<global::Enemy>();
        private RaidTimer _raidTimer;
        private EnemyRaidSpawner _raidSpawner;
        private Camera _camera;

        private bool _loaded;
        private bool _ending;
        private double _startDsp;

        private bool _enemySeen;
        private bool _warning;
        private bool _raidActive;
        private float _raidStartTime;
        private float _lastSpawnTime;

        private float _synthGain = LayerGain;
        private bool _synthLowered;
        private float _duck = 1f;
        private bool _paused;
        private float _pollTimer;

        private float _oneShotTimer;
        private int _lastOneShot = -1;
        private int _oneShotSourceIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                return;
            }

            gameManager.RunStarted -= HandleRunStarted;
            gameManager.RunStarted += HandleRunStarted;

            // 던전 씬에서 바로 시작하면 구독 전에 RunStarted가 지나갔을 수 있어 진행 중인 런이면 바로 만든다.
            if (gameManager.CurrentRun != null && !gameManager.CurrentRun.IsEnded)
            {
                HandleRunStarted(gameManager.CurrentRun);
            }
        }

        private static void HandleRunStarted(RunSession run)
        {
            new GameObject("DungeonMusic").AddComponent<DungeonMusicDirector>();
        }

        private void Awake()
        {
            GameManager.Instance.RunEnded += HandleRunEnded;

            _raidTimer = GameManager.Instance.CurrentRun.RaidTimer;
            _raidTimer.WarningEvent += HandleWarning;
            _raidTimer.RaidEvent += HandleRaid;

            _raidSpawner = FindFirstObjectByType<EnemyRaidSpawner>();
            if (_raidSpawner != null)
            {
                _raidSpawner.EnemySpawned += HandleEnemySpawned;
            }

            _camera = Camera.main;
            _oneShotTimer = Random.Range(OneShotMinInterval, OneShotMaxInterval);
        }

        private IEnumerator Start()
        {
            ResourceRequest[] stemRequests = new ResourceRequest[StemPaths.Length];
            for (int i = 0; i < StemPaths.Length; i++)
            {
                stemRequests[i] = Resources.LoadAsync<AudioClip>(StemPaths[i]);
            }

            ResourceRequest[] oneShotRequests = new ResourceRequest[OneShotNames.Length];
            for (int i = 0; i < OneShotNames.Length; i++)
            {
                oneShotRequests[i] = Resources.LoadAsync<AudioClip>(OneShotPrefix + OneShotNames[i]);
            }

            for (int i = 0; i < stemRequests.Length; i++)
            {
                yield return stemRequests[i];
                _stems[i] = (AudioClip)stemRequests[i].asset;
                if (_stems[i] == null)
                {
                    Debug.LogWarning($"[DungeonMusicDirector] 스템을 불러오지 못했습니다: {StemPaths[i]}");
                }
            }

            for (int i = 0; i < oneShotRequests.Length; i++)
            {
                yield return oneShotRequests[i];
                _oneShots[i] = (AudioClip)oneShotRequests[i].asset;
            }

            AudioManager audioManager = AudioManager.Instance;

            // 쉘터 등 이전 씬의 BGM이 남아 있으면 던전 음악과 겹치므로 끈다 (씬을 떠날 때 정리되지 않은 경우의 안전장치)
            if (audioManager.CurrentBgmClip != null)
            {
                audioManager.StopBgm(FadeInSeconds);
            }

            // 네 스템을 같은 dspTime에 예약해 시작점을 맞춘다. 앰비언트 외에는 볼륨 0으로 계속 돈다.
            _startDsp = AudioSettings.dspTime + ScheduleLeadSeconds;
            for (int i = 0; i < _stemSources.Length; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.clip = _stems[i];
                source.loop = true;
                source.spatialBlend = 0f;
                source.playOnAwake = false;
                source.volume = 0f;
                source.outputAudioMixerGroup = audioManager.BgmGroup;
                source.PlayScheduled(_startDsp);
                _stemSources[i] = source;
            }

            for (int i = 0; i < _oneShotSources.Length; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.spatialBlend = 0f;
                source.playOnAwake = false;
                source.outputAudioMixerGroup = audioManager.AmbienceGroup;
                _oneShotSources[i] = source;
            }

            _on[Ambient] = true;
            _loaded = true;
            Debug.Log("[DungeonMusicDirector] 던전 BGM 시작");
        }

        private void Update()
        {
            if (!_loaded)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;

            _pollTimer += deltaTime;
            if (_pollTimer >= PollInterval)
            {
                _pollTimer = 0f;
                Poll();
            }

            if (!_ending)
            {
                UpdateLayerState(Drums, _enemySeen || _warning || _raidActive, DrumHoldSeconds, deltaTime);
                UpdateLayerState(Synth, _warning || _raidActive, RaidStemHoldSeconds, deltaTime);
                UpdateLayerState(Raid, _raidActive, RaidStemHoldSeconds, deltaTime);
            }

            UpdateVolumes(deltaTime);
            UpdateOneShots(deltaTime);

            if (_ending
                && _level[Ambient] <= 0f && _level[Drums] <= 0f && _level[Synth] <= 0f && _level[Raid] <= 0f)
            {
                Destroy(gameObject);
            }
        }

        // 적 인지 판정과 습격 종료 판정
        private void Poll()
        {
            _enemySeen = false;
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            PlayerComponent player = PlayerComponent.PlayerInstance;
            if (_camera != null && player != null)
            {
                // 카메라는 플레이어를 따라가므로 플레이어 중심 카메라 크기 사각형이 "카메라 안"과 같다.
                float halfHeight = _camera.orthographicSize + CameraMargin;
                float halfWidth = _camera.orthographicSize * _camera.aspect + CameraMargin;
                _enemySeen = Physics2D.OverlapBox(
                    player.transform.position,
                    new Vector2(halfWidth * 2f, halfHeight * 2f),
                    0f,
                    1 << Layers.Enemy) != null;
            }

            _raidEnemies.RemoveWhere(IsGone);

            if (_raidActive
                && Time.time - _raidStartTime >= RaidStartGraceSeconds
                && _raidEnemies.Count == 0
                && Time.time - _lastSpawnTime >= SpawnGapSeconds)
            {
                _raidActive = false;
                Debug.Log("[DungeonMusicDirector] 습격 종료");
            }
        }

        private static bool IsGone(global::Enemy enemy)
        {
            return enemy == null || enemy.EnemyHealth.IsDead;
        }

        // 켤 때는 다음 박 경계까지 기다린 뒤 페이드인, 끌 때는 hold 만큼 기다린 뒤 페이드아웃
        private void UpdateLayerState(int layer, bool want, float holdSeconds, float deltaTime)
        {
            if (want)
            {
                _holdTimer[layer] = 0f;
                if (_on[layer])
                {
                    return;
                }

                // 페이드아웃 중이던 레이어는 박 경계를 기다리지 않고 바로 이어서 올린다(기다리는 동안 음량이 더 내려가는 것을 막는다).
                if (_level[layer] > 0f)
                {
                    _on[layer] = true;
                    return;
                }

                if (!_beatPending[layer])
                {
                    double sinceStart = AudioSettings.dspTime - _startDsp;
                    _beatWait[layer] = BeatLength - (float)(sinceStart % BeatLength);
                    _beatPending[layer] = true;
                }

                _beatWait[layer] -= deltaTime;
                if (_beatWait[layer] <= 0f)
                {
                    _beatPending[layer] = false;
                    _on[layer] = true;
                    Debug.Log($"[DungeonMusicDirector] 레이어 켜짐: {StemPaths[layer]}");
                }

                return;
            }

            _beatPending[layer] = false;
            if (!_on[layer])
            {
                return;
            }

            _holdTimer[layer] += deltaTime;
            if (_holdTimer[layer] >= holdSeconds)
            {
                _on[layer] = false;
                Debug.Log($"[DungeonMusicDirector] 레이어 꺼짐: {StemPaths[layer]}");
            }
        }

        private void UpdateVolumes(float deltaTime)
        {
            RunSession run = GameManager.Instance.CurrentRun;
            _paused = run != null && run.IsPaused;
            _duck = Mathf.MoveTowards(_duck, _paused ? DuckGain : 1f, (1f - DuckGain) / DuckSeconds * deltaTime);

            // 신스는 습격 중 목표 볼륨이 낮아지며 같은 페이드 시간으로 따라간다.
            // 습격이 끝나도 신스가 완전히 꺼질 때까지는 낮춘 채로 두어, 페이드아웃 중에 커지지 않게 한다.
            if (_raidActive)
            {
                _synthLowered = true;
            }
            else if (_level[Synth] <= 0f)
            {
                _synthLowered = false;
            }

            float synthTarget = _synthLowered ? LayerGain * SynthRaidGain : LayerGain;
            _synthGain = Mathf.MoveTowards(_synthGain, synthTarget, LayerGain / FadeInSeconds * deltaTime);

            for (int i = 0; i < _stemSources.Length; i++)
            {
                bool on = _on[i] && !_ending;
                float fadeOutSeconds = _ending ? EndFadeSeconds : (i == Drums ? DrumFadeOutSeconds : RaidStemFadeOutSeconds);
                float seconds = on ? FadeInSeconds : fadeOutSeconds;
                _level[i] = Mathf.MoveTowards(_level[i], on ? 1f : 0f, deltaTime / seconds);

                float gain = i == Synth ? _synthGain : LayerGain;
                _stemSources[i].volume = Mathf.SmoothStep(0f, 1f, _level[i]) * gain * _duck;
            }
        }

        // 전투 중에는 타이머를 멈추고, 끝나면 남은 간격을 이어서 센다.
        private void UpdateOneShots(float deltaTime)
        {
            // 컷씬 · 일시정지 중에도 쉰다(원샷은 SFX 그룹이라 BGM 덕킹이 걸리지 않는다).
            if (_ending || _paused || _enemySeen || _warning || _raidActive)
            {
                return;
            }

            _oneShotTimer -= deltaTime;
            if (_oneShotTimer > 0f)
            {
                return;
            }

            _oneShotTimer = Random.Range(OneShotMinInterval, OneShotMaxInterval);

            // 직전과 같은 클립은 제외
            int index;
            if (_lastOneShot < 0)
            {
                index = Random.Range(0, _oneShots.Length);
            }
            else
            {
                index = Random.Range(0, _oneShots.Length - 1);
                if (index >= _lastOneShot)
                {
                    index++;
                }
            }

            _lastOneShot = index;

            AudioSource source = _oneShotSources[_oneShotSourceIndex];
            _oneShotSourceIndex = (_oneShotSourceIndex + 1) % _oneShotSources.Length;
            source.pitch = Random.Range(0.92f, 1.08f);
            source.panStereo = Random.Range(-0.6f, 0.6f);
            source.PlayOneShot(_oneShots[index], Random.Range(0.6f, 1f));
        }

        private void HandleWarning()
        {
            _warning = true;
            Debug.Log("[DungeonMusicDirector] 습격 임박");
        }

        // 다음 습격이 겹쳐 도착해도 시작 시각만 갱신하고 이어서 유지한다.
        private void HandleRaid()
        {
            _warning = false;
            _raidActive = true;
            _raidStartTime = Time.time;
            Debug.Log("[DungeonMusicDirector] 습격 시작");
        }

        private void HandleEnemySpawned(global::Enemy enemy)
        {
            _raidEnemies.Add(enemy);
            enemy.EnemyHealth.DeathEvent += () => _raidEnemies.Remove(enemy);
            _lastSpawnTime = Time.time;
        }

        private void HandleRunEnded(RunResult result)
        {
            if (!_loaded)
            {
                Destroy(gameObject);
                return;
            }

            _ending = true;
            Debug.Log("[DungeonMusicDirector] 런 종료, BGM 페이드아웃");
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RunEnded -= HandleRunEnded;
            }

            if (_raidTimer != null)
            {
                _raidTimer.WarningEvent -= HandleWarning;
                _raidTimer.RaidEvent -= HandleRaid;
            }

            if (_raidSpawner != null)
            {
                _raidSpawner.EnemySpawned -= HandleEnemySpawned;
            }
        }
    }
}
