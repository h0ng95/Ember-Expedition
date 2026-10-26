# v0.45 자동 원정 비교

실제 전투 계산과 승리 보상을 사용하는 432회 비교 + 별도 시드 8회. 사람의 승률이나 난도 확정값이 아니다. 같은 시드에 서로 다른 정책을 실행한 사례를 포함하며 독립 표본으로 합산한 승률은 사용하지 않는다.

기본 방어 비교: standard는 시드 45101–45108, 나머지는 45101–45104. 정밀/술식 비교: 모든 편성 45101–45104. final은 최종 진화 중심, H-01/H-05는 해당 결합체 확보를 목표로 하는 고정 정책이다. early는 6전투부터, delayed는 11전투부터, selective는 출전 5명 확보 후 기초 연구 하나, none은 연구 미구매다. 성장 순서는 Balanced.

방어: 방벽 훈련 → 방패술 → 정밀 사격. 정밀: 정밀 사격 → 약점 공략 → 방벽 훈련. 술식: 술식 연구 → 잔불 증폭 → 방벽 훈련. 실제 구매는 골드·선행 조건에 따라 달라진다.

별도 확인: 45201–45204, final/정밀/delayed. 기본 3/4, 서리 1/4 완주. 소환·연구·전투·보상 경계의 JSON 재로드 및 전투 재시작 동일성 3,280조건 통과.

해석: 연구 조기 구매가 모든 빌드에 유리하지 않다. 서리 편성은 이번 정책에서 약하며 정밀 계열에서는 완주가 가능하다. 서리 편성의 초중반 화력과 술식 투자 대비 효율을 다음 균형 조정 대상으로 둔다. 전투 수치·연구 가격은 이번 변경에서 조정하지 않았다.

| 시작 편성 | 정책 | 연구 계열 | 지출 방식 | 완주 / 실행 |
| --- | --- | --- | --- | --- |
| ember | final | arcana | delayed | 1 / 4 |
| ember | H-01 | arcana | delayed | 1 / 4 |
| ember | H-05 | arcana | delayed | 0 / 4 |
| ember | final | arcana | early | 1 / 4 |
| ember | H-01 | arcana | early | 0 / 4 |
| ember | H-05 | arcana | early | 0 / 4 |
| ember | final | defense | none | 2 / 4 |
| ember | final | defense | early | 3 / 4 |
| ember | final | defense | delayed | 3 / 4 |
| ember | final | defense | selective | 2 / 4 |
| ember | H-01 | defense | none | 0 / 4 |
| ember | H-01 | defense | early | 0 / 4 |
| ember | H-01 | defense | delayed | 1 / 4 |
| ember | H-01 | defense | selective | 1 / 4 |
| ember | H-05 | defense | none | 2 / 4 |
| ember | H-05 | defense | early | 0 / 4 |
| ember | H-05 | defense | delayed | 0 / 4 |
| ember | H-05 | defense | selective | 2 / 4 |
| ember | final | precision | delayed | 2 / 4 |
| ember | H-01 | precision | delayed | 2 / 4 |
| ember | H-05 | precision | delayed | 0 / 4 |
| ember | final | precision | early | 2 / 4 |
| ember | H-01 | precision | early | 1 / 4 |
| ember | H-05 | precision | early | 0 / 4 |
| frost | final | arcana | delayed | 0 / 4 |
| frost | H-01 | arcana | delayed | 0 / 4 |
| frost | H-05 | arcana | delayed | 0 / 4 |
| frost | final | arcana | early | 0 / 4 |
| frost | H-01 | arcana | early | 0 / 4 |
| frost | H-05 | arcana | early | 0 / 4 |
| frost | final | defense | none | 0 / 4 |
| frost | final | defense | early | 0 / 4 |
| frost | final | defense | delayed | 0 / 4 |
| frost | final | defense | selective | 0 / 4 |
| frost | H-01 | defense | none | 0 / 4 |
| frost | H-01 | defense | early | 0 / 4 |
| frost | H-01 | defense | delayed | 0 / 4 |
| frost | H-01 | defense | selective | 0 / 4 |
| frost | H-05 | defense | none | 1 / 4 |
| frost | H-05 | defense | early | 0 / 4 |
| frost | H-05 | defense | delayed | 0 / 4 |
| frost | H-05 | defense | selective | 1 / 4 |
| frost | final | precision | delayed | 2 / 4 |
| frost | H-01 | precision | delayed | 0 / 4 |
| frost | H-05 | precision | delayed | 0 / 4 |
| frost | final | precision | early | 2 / 4 |
| frost | H-01 | precision | early | 0 / 4 |
| frost | H-05 | precision | early | 0 / 4 |
| standard | final | arcana | delayed | 1 / 4 |
| standard | H-01 | arcana | delayed | 0 / 4 |
| standard | H-05 | arcana | delayed | 0 / 4 |
| standard | final | arcana | early | 1 / 4 |
| standard | H-01 | arcana | early | 0 / 4 |
| standard | H-05 | arcana | early | 0 / 4 |
| standard | final | defense | none | 4 / 8 |
| standard | final | defense | early | 6 / 8 |
| standard | final | defense | delayed | 6 / 8 |
| standard | final | defense | selective | 2 / 8 |
| standard | H-01 | defense | none | 0 / 8 |
| standard | H-01 | defense | early | 0 / 8 |
| standard | H-01 | defense | delayed | 1 / 8 |
| standard | H-01 | defense | selective | 1 / 8 |
| standard | H-05 | defense | none | 1 / 8 |
| standard | H-05 | defense | early | 0 / 8 |
| standard | H-05 | defense | delayed | 2 / 8 |
| standard | H-05 | defense | selective | 1 / 8 |
| standard | final | precision | delayed | 4 / 4 |
| standard | H-01 | precision | delayed | 1 / 4 |
| standard | H-05 | precision | delayed | 0 / 4 |
| standard | final | precision | early | 4 / 4 |
| standard | H-01 | precision | early | 0 / 4 |
| standard | H-05 | precision | early | 0 / 4 |
| tactical | final | arcana | delayed | 1 / 4 |
| tactical | H-01 | arcana | delayed | 1 / 4 |
| tactical | H-05 | arcana | delayed | 0 / 4 |
| tactical | final | arcana | early | 1 / 4 |
| tactical | H-01 | arcana | early | 0 / 4 |
| tactical | H-05 | arcana | early | 0 / 4 |
| tactical | final | defense | none | 3 / 4 |
| tactical | final | defense | early | 2 / 4 |
| tactical | final | defense | delayed | 1 / 4 |
| tactical | final | defense | selective | 2 / 4 |
| tactical | H-01 | defense | none | 1 / 4 |
| tactical | H-01 | defense | early | 0 / 4 |
| tactical | H-01 | defense | delayed | 1 / 4 |
| tactical | H-01 | defense | selective | 0 / 4 |
| tactical | H-05 | defense | none | 0 / 4 |
| tactical | H-05 | defense | early | 0 / 4 |
| tactical | H-05 | defense | delayed | 2 / 4 |
| tactical | H-05 | defense | selective | 0 / 4 |
| tactical | final | precision | delayed | 1 / 4 |
| tactical | H-01 | precision | delayed | 2 / 4 |
| tactical | H-05 | precision | delayed | 1 / 4 |
| tactical | final | precision | early | 2 / 4 |
| tactical | H-01 | precision | early | 1 / 4 |
| tactical | H-05 | precision | early | 1 / 4 |

[집계·구간별 전투·시드별 결과 JSON](balance-results.json)
