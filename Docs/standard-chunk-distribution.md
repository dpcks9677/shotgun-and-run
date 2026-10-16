# Standard 스포너 청크 배분

## 설정 위치

- 관리 컴포넌트: DungeonStageScene의 `SpawnerLayer/EnemyPlacement/Connectors` → `EnemyChunkDistributor`.
- 비율·후보 설정: `Assets/07.Datas/Enemy/StandardChunkDistribution.asset`.
- 랜덤 후보: `Assets/07.Datas/Enemy/ChunkPresets`의 C01~C30.
- Zones의 Z1~Z10은 관리 컴포넌트의 하위가 아니므로 배분 대상에서 제외됩니다. 기존 구성은 고정 프리셋 참조로 보존합니다.

## 동작

1. 씬 시작 시 관리 컴포넌트의 Awake에서 활성화되고 enabled인 하위 Standard 스포너를 수집합니다.
2. SO의 쉬움·보통·어려움·매우 어려움 비율 합계를 검사합니다. 각 값은 0~100, 합계는 100이어야 합니다.
3. 실제 대상 수와 비율로 난이도별 개수를 계산합니다. 소수 부분이 큰 순서로 남은 수를 배분하고 동률은 쉬움→보통→어려움→매우 어려움 순서로 처리합니다.
4. 해당 난이도의 후보 중 하나를 동일 확률로 뽑고, 전체 배정 목록을 섞어 각 스포너에 전달합니다. 같은 프리셋의 중복 선택을 허용합니다.
5. Standard 스포너는 Start에서 결정된 프리셋으로 Enemy를 생성합니다. 같은 런의 재생성에서도 같은 프리셋을 사용합니다.

현재 대상 40개와 기본 비율 50·30·15·5의 결과는 20·12·6·2개입니다. 대상을 70개로 늘리면 35·21·11·3개가 됩니다. 계산 대상은 Enemy 마리 수가 아니라 **스포너 수**입니다.

후보 누락·빈 청크·잘못된 비율 등 설정 오류가 있으면 배분을 중단하고 해당 관리자 아래의 스포너도 생성하지 않습니다. 다른 난이도로 임의 대체하지 않습니다.

## Inspector에서 확인

SO Inspector의 `Easy Percent`, `Normal Percent`, `Hard Percent`, `Very Hard Percent`를 수정합니다. 하단의 비율 합계와 오류 표시를 확인합니다.

Connectors의 관리 컴포넌트 Inspector에는 대상 수와 현재 지정된 청크의 난이도별 수가 표시됩니다. Edit Mode에서 **청크 배분 미리보기**를 누르면 현재 설정으로 실제 참조를 배정합니다. 저장하면 그 미리보기 결과가 씬에 남습니다. Play Mode에서는 시작할 때 다시 배분하며 실행 중 미리보기 버튼은 비활성화됩니다.

## 새 스포너 추가

편집 상태에서 Connectors 아래에 EnemyStandardSpawner 프리팹을 배치하면 다음 미리보기 또는 던전 시작 때 자동 포함됩니다. 위치·생성 범위·ScoutRange는 배치할 방에 맞게 확인해야 합니다. 관리 범위 밖의 스포너에는 고정 Chunk Preset을 직접 지정합니다.

런타임에 뒤늦게 생성한 스포너를 현재 배분에 자동 편입하는 기능은 포함하지 않습니다. 이번 작업에서는 빈 방에 스포너를 추가하지 않았습니다.

## 기존 구성 보존

직접 EnemyEntry를 입력하던 Standard 스포너는 Chunk Preset 참조 방식으로 전환했습니다. C01~C30에 없는 기존 수비대·테스트 구성을 `Assets/07.Datas/Enemy/FixedChunks`에 보존했으며, 이 폴더는 랜덤 후보 목록에 포함하지 않습니다. Sandbox 씬과 기본 프리팹의 기존 타입·개수도 보존했습니다.

기존 스포너 이름의 N2·N1S1 같은 표기는 이전 배치를 나타내므로, 현재 배정된 청크는 Inspector의 Chunk Preset으로 확인합니다.

## 검증

Unity 컴파일과 Edit Mode 테스트 9개 통과. 40개·70개 배분, 정수 반올림, 비율 오류, 후보 누락, 비활성/disabled 제외, Zones 제외를 검증했습니다. DungeonStageScene의 실제 40개 참조와 Z1~Z10 고정 구성도 확인했습니다. 실제 몬스터 전투와 재생성 연출의 Play Mode 검증은 별도로 필요합니다.
