Missing Skeleton Data Scanner

프리팹에서 SkeletonDataAsset 참조가 누락된 항목을 찾아 표시하는 Unity Editor 도구의 코드 샘플입니다.

지정된 폴더와 하위 폴더의 프리팹을 순회해, SkeletonAnimation은 있지만 SkeletonDataAsset이 연결되지 않은 항목을 찾습니다. 프리팹의 Spine 데이터 연결 누락을 일괄 확인할 때 사용합니다.

검사 버튼을 누르면 결과 개수와 프리팹 목록을 표시하며, 재검사할 때 이전 결과를 초기화합니다.

각 프리팹의 루트와 활성 자식에서 처음 발견되는 SkeletonAnimation 하나를 검사합니다. SkeletonAnimation 자체가 없는 프리팹은 결과에 포함하지 않습니다.
