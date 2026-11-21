# SlotBattle 작업 지침

설계는 docs/DESIGN.md를 따른다. 설계와 다르게 구현해야 할 이유가 생기면
코드를 쓰기 전에 먼저 묻는다.

## 구조 규칙
- Assets/Scripts/Core는 UnityEngine을 참조하지 않는다.
- 게임 규칙은 전부 Core에 둔다. Presentation은 그리기와 입력 전달만 한다.
- Core의 난수는 SeededRandom만 쓴다. System.Random, UnityEngine.Random,
  DateTime.Now를 Core에서 쓰지 않는다.
- 시뮬레이션은 초당 20회 고정 간격으로 진행한다.
- 수치는 Assets/Data의 JSON에 둔다. 코드에 수치를 직접 적지 않는다.
- 유닛 특성은 JSON의 키워드로 표현하고, Core는 키워드별 처리기로 구현한다.
- Core는 사건(릴 정지, 잭팟, 소환, 타격, 사망, 성 피해)만 알린다.
  연출은 Presentation이 사건을 받아 재생하고, 연출 수치는 feel.json에 둔다.
- 씬은 Bootstrap 하나만 두고 화면 요소는 코드로 생성한다.
- 광고, 결제, 저장, 분석은 Services의 인터페이스를 통해서만 호출한다.

## 작업 방식
- docs/DESIGN.md 5장의 단계(M0~M5)를 한 번에 하나씩 진행한다.
  요청받지 않은 다음 단계를 미리 만들지 않는다.
- Core의 규칙을 추가하거나 바꾸면 Tests/Core.Tests에 테스트를 함께 쓴다.
- 작업을 마치기 전에 `dotnet test Tests/Core.Tests`를 실행해 통과를 확인한다.
- 밸런스에 영향을 주는 변경 후에는 `dotnet run --project Tools/BalanceSim`으로
  스테이지별 승률을 확인하고 결과를 보고한다.
- Unity 에디터에서만 확인할 수 있는 것(화면 배치, 터치 감각)은 검증했다고 하지
  말고, 내가 에디터에서 확인할 항목으로 정리해 준다.
- 단계가 끝나면 완료 기준별 충족 여부를 적어 보고한다.

## 금지
- 다른 게임의 이름, 캐릭터, 그림, 문구, 수치표를 가져오지 않는다.
- 현금이나 유료 화폐로 돌리는 슬롯, 유료 랜덤 상자를 만들지 않는다.
