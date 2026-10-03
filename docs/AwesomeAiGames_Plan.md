# Awesome AI Games 대응 계획 (2026-10-02)

기준 리스트: [lappemic/awesome-ai-games](https://github.com/lappemic/awesome-ai-games) (README·CONTRIBUTING, 2026-09-19 커밋).
등재 조건: 브라우저에서 바로 플레이, AI 제작, **데모가 아닌 게임**(조작·목표·도전·재미), X 출처 트윗 ~600+ 좋아요와 "재밌다" 반응, 512px 이하 이미지 1장.
Competent Person은 ✨ Original(현장 위험 찾기, 리스트의 *Waldo*와 같은 계열)로 지원.

## 원칙
- Course 모드(5일 스토리, 출입·브리핑·툴박스 토크, 이수 코드, 연구 동의)는 그대로 둔다. 공개용 **Hazard Hunt**를 그 위에 얹는다.
- 아케이드 점수 = ECD와 같은 증거(발견·에너지 태그·위험 평정·통제, 오경보·사고 감점). 재미있게 이기는 길이 유능한 CP의 길이 되도록.
- 아케이드는 과정 진도를 쓰지 않는다(커리어·숙달·이월·이수 코드). 텔레메트리는 기존 동의 게이트 그대로이고, `session_start`에 `mode=arcade-*`가 남아 연구 데이터와 섞이지 않는다.
- 리더보드는 옵트인 공개 핸들만. 연구 테이블·로스터 코드와 연결하지 않는다.

## 1단계 — Hazard Hunt (구현)
| 항목 | 구현 |
|---|---|
| 즉시 시작 | 메뉴 맨 위 **PLAY NOW · HAZARD HUNT**(오늘의 Daily Site) / PRACTICE ROUND. `…/?daily` 링크는 메뉴 없이 바로 시작 |
| 3분 라운드 | 같은 DaySession, 시프트 시계만 600/180배(사고·크루 요청·속도 보너스 의미 유지). 출입·브리핑·미션 존 없음, 힌트 2개 고정 |
| 점수 | 발견 100 + 속도 ≤100(힌트/큐 시 절반), 에너지 +25, 위험 ±2 이내 +25, 통제 +50(최선 +50), 작업중지 유지 +25, 준수 확인 +50, 오경보 −50, 사고 −100, 전부 찾고 일찍 끝내면 남은 초×5. S = 80%↑ 무오경보·무사고 |
| Daily Site | UTC 날짜 → 같은 에피소드·시드(누구나 같은 현장), #1 = 2026-10-01 |
| 결과 카드 | Wordle식 그리드(🟩 조기·무도움, 🟨 늦게/힌트, 🟥 사고, ⬛ 놓침), 위험 이름 비공개. 폰은 공유 시트, 데스크톱은 클립보드 |
| 기록 | 이 기기의 첫 시도/최고 점수, 연속 일수. 다시 하기는 `(replay)` 표시 |

## 2단계 — 재도전 동기 (구현)
- **일일 리더보드** `/api/scores` + D1 `arcade_scores`: 첫 시도만, 오늘/어제만, 라운드당 1행, 핸들 3–12자 + 금칙어, 점수 상한(위험×350 + 900 + 1000), IP당 시간당 10회, 60일 후 삭제, 응답은 표시 필드만. 서버 셀프테스트 `Web/qa/api_scores.test.mjs`.
- **게임패드**: 스틱 이동/시선, A 행동, Y/View 태블릿, B 뒤로, LB 지도, Start 일시정지, 메뉴·태블릿 UI 내비게이션 + 선택 링.
- **다운로드·성능**: 빌드 리포트상 자산 138 MB 중 71%가 메시, 그중 NPC 7명 ≈ 90 MB(FBX 1.4 MB → 임포트 13 MB, 블렌드셰이프 노멀/탄젠트). NPC 머티리얼은 노멀맵이 없어 블렌드셰이프 노멀·탄젠트·애니메이션 임포트 제거. 모바일 첫 실행 그래픽 Low.

## 3단계 — 512px에서 팔리는 비주얼 (구현 1차)
- 히어로 컷 자동 캡처 `HeroShotTests` → `Captures/t_hero/`(에피소드별 원경·근경 × 기본/골든아워 + 라운드 HUD). 리스트 이미지와 출시 트윗 영상 후보.
- `PropDetail`: 컨테이너(골판·레일·코너 포스트·캐스팅·도어 바), 연료탱크(받침대 + 방류턱 트레이 + 펌프), 트렌치 박스(보강재·캡·나이프 엣지·인양 고리), 스프레더 파이프 + 칼라. 캡처 `Captures/t_variety/prop_*`.
- 다음: 20–30초 플레이 영상(발견 → 촬영 → 호루라기 → 크루 반응).

## 4단계 — 포먼 설득 (구현)
- Ray가 작업중지에 반발할 때 정해진 3개 답 외에 **자유 입력**. 점수가 되는 태도(단호·존중 / 공격적 / 수동)는 결정론 루브릭 `Core/SpeakUpRubric`이 정한다(유지 표현, 적대 표현·고함, 양보 표현; 이유·존중·도움 제안은 피드백 팁). 기존 3개 답이 각자 자기 태도로 분류되는지 테스트로 고정.
- AI 동의 시에만 모델이 그 태도에 맞춰 Ray의 반응을 연기(`CrewMember.React`, ReplyGuard, 실패 시 내장 대사). 입력한 문장은 텔레메트리에 남지 않고 `speakup_choice` 에 태도와 플래그만.
- 남은 선택: 친구 기록 고스트, 오픈소스 공개(서드파티 에셋 라이선스·키 점검 후).

## 등재 체크리스트
1. 라이브 링크에서 Hazard Hunt가 10초 안팎에 뜨는지(데스크톱) 확인.
2. 20–30초 영상 + 결과 카드로 출시 트윗. @threejs는 Unity라 기대하지 않음 → 건설·안전 교육 커뮤니티, Unity 채널.
3. 좋아요 ~600+ 및 "플레이가 재밌다" 반응 확인 후, `images/competent-person.webp`(≤512px) + 아래 항목으로 PR.

```markdown
#### [Competent Person](https://competent-person.pages.dev)

<img src="images/competent-person.webp" width="512" alt="Competent Person gameplay">

Original first-person construction-site hazard hunt. Spot, photograph and fix OSHA hazards before the crew gets hurt; daily site + leaderboard; stop-work calls under foreman pressure; AI-voiced crew you can talk to; Unity WebGL + Claude. Possible launch: ~YYYY-MM-DD. Source: [Tweet](…)
```
(출시일·트윗은 실제로 생긴 뒤에만 채움 — 리스트 규칙상 추정·가공 금지.)
