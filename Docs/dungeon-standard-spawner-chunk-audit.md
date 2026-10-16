# DungeonStageScene Standard 스포너 청크 확인

이 문서는 청크 배분 기능 적용 전의 확인 기록입니다. 현재 설정은 [standard-chunk-distribution.md](standard-chunk-distribution.md)를 참고합니다.

확인 기준: 저장된 씬 YAML과 프리팹 기본값 및 씬 오버라이드. Unity 실행 상태는 확인하지 않았습니다.

대상: `Assets/01.Scenes/DungeonStageScene.unity`. Standard 스포너 50개 모두 SO 프리셋 참조 없이 직접 입력된 Chunk를 사용합니다. 일치 프리셋은 구성 비교 결과이며 실제 연결 상태가 아닙니다.

| 스포너 | 현재 구성 | 총수 | 구성 일치 프리셋 | 씬 행 |
|---|---|---|---|---|
| Garrison_Z1_P1 | N x 6 + S x 2 | 8 | 일치 없음 | 804 |
| Garrison_Z10_P2 | N x 4 + T x 1 + Sn x 1 + Th x 1 | 7 | 일치 없음 | 757943 |
| Garrison_Z2_P2 | N x 4 + T x 1 + Sn x 1 + Th x 1 | 7 | 일치 없음 | 756854 |
| Garrison_Z3_P2 | N x 4 + T x 1 + Sn x 1 + Th x 1 | 7 | 일치 없음 | 3980 |
| Garrison_Z4_P1 | N x 6 + S x 2 | 8 | 일치 없음 | 760714 |
| Garrison_Z5_P3 | EliteSn x 1 + N x 4 + Th x 1 | 6 | 일치 없음 | 755796 |
| Garrison_Z6_P2 | N x 4 + T x 1 + Sn x 1 + Th x 1 | 7 | 일치 없음 | 760792 |
| Garrison_Z7_P1 | N x 6 + S x 2 | 8 | 일치 없음 | 753719 |
| Garrison_Z8_P3 | EliteSn x 1 + N x 4 + Th x 1 | 6 | 일치 없음 | 756635 |
| Garrison_Z9_P1 | N x 6 + S x 2 | 8 | 일치 없음 | 1542 |
| Patrol_H1_N2 | N x 2 | 2 | 일치 없음 | 756112 |
| Patrol_H10-E_N1S1 | N x 1 + S x 1 | 2 | 일치 없음 | 7802 |
| Patrol_H10-W_N2 | N x 2 | 2 | 일치 없음 | 1780 |
| Patrol_H11_N2S1 | N x 2 + S x 1 | 3 | 일치 없음 | 21358 |
| Patrol_H3-M_N2 | N x 2 | 2 | 일치 없음 | 1973 |
| Patrol_H3-N_N1S1 | N x 1 + S x 1 | 2 | 일치 없음 | 2298 |
| Patrol_H3-S_N1S1 | N x 1 + S x 1 | 2 | 일치 없음 | 416198 |
| Patrol_H4_N2 | N x 2 | 2 | 일치 없음 | 2051 |
| Patrol_H5-P_N2S1 | N x 2 + S x 1 | 3 | 일치 없음 | 7152 |
| Patrol_H5-Q_N1S1 | N x 1 + S x 1 | 2 | 일치 없음 | 21117 |
| Patrol_H5-R_N3 | N x 3 | 3 | C01_기본 무리 | 155 |
| Patrol_H6-Center_N2S1 | N x 2 + S x 1 | 3 | 일치 없음 | 757783 |
| Patrol_H6-M_N3 | N x 3 | 3 | C01_기본 무리 | 7581 |
| Patrol_H6-N_N1S1 | N x 1 + S x 1 | 2 | 일치 없음 | 755634 |
| Patrol_H6-O_N2 | N x 2 | 2 | 일치 없음 | 761077 |
| Patrol_H6c_N2 | N x 2 | 2 | 일치 없음 | 753563 |
| Patrol_H7-E_N3 | N x 3 | 3 | C01_기본 무리 | 1264 |
| Patrol_H7-Mid_N1S1 | N x 1 + S x 1 | 2 | 일치 없음 | 882 |
| Patrol_H7-W_N2 | N x 2 | 2 | 일치 없음 | 1620 |
| Patrol_H8-Island_N2 | N x 2 | 2 | 일치 없음 | 758033 |
| Patrol_H8-N_N1S1 | N x 1 + S x 1 | 2 | 일치 없음 | 1698 |
| Patrol_H9-E_N3 | N x 3 | 3 | C01_기본 무리 | 7234 |
| Patrol_H9-W_N2S1 | N x 2 + S x 1 | 3 | 일치 없음 | 756350 |
| Patrol_HA_N2S1 | N x 2 + S x 1 | 3 | 일치 없음 | 1107 |
| Patrol_HB-M_N1S1 | N x 1 + S x 1 | 2 | 일치 없음 | 756190 |
| Patrol_HB-N_N2 | N x 2 | 2 | 일치 없음 | 726 |
| Patrol_HB-S_N2 | N x 2 | 2 | 일치 없음 | 753641 |
| Patrol_HC-E_N2 | N x 2 | 2 | 일치 없음 | 758910 |
| Patrol_HC-W_N1S1 | N x 1 + S x 1 | 2 | 일치 없음 | 21276 |
| Patrol_HE_N1S1 | N x 1 + S x 1 | 2 | 일치 없음 | 3836 |
| Patrol_HF-Bridge_N2 | N x 2 | 2 | 일치 없음 | 760636 |
| Patrol_HF-L_N3 | N x 3 | 3 | C01_기본 무리 | 756272 |
| Patrol_HF-R-High_N3 | N x 3 | 3 | C01_기본 무리 | 756034 |
| Patrol_HF-R-Low_N2S1 | N x 2 + S x 1 | 3 | 일치 없음 | 4070 |
| Patrol_HG-N_N2 | N x 2 | 2 | 일치 없음 | 6832 |
| Patrol_HG-S_N1S1 | N x 1 + S x 1 | 2 | 일치 없음 | 6910 |
| Patrol_R7-Bay1_N1S1 | N x 1 + S x 1 | 2 | 일치 없음 | 21440 |
| Patrol_R7-Bay2_N2 | N x 2 | 2 | 일치 없음 | 757865 |
| Patrol_R7-Bay3_N1S1 | N x 1 + S x 1 | 2 | 일치 없음 | 379 |
| Patrol_R7-Bay4_N2 | N x 2 | 2 | 일치 없음 | 2220 |
