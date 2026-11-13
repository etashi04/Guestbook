# 설치기 재빌드

일반 설치에는 Python이나 컴파일러가 필요하지 않습니다. 설치 방법은 루트 README를 참고하세요.

이 도구는 공개 수동판의 완성된 패치 파일을 검증하고 설치기를 다시 빌드·포장합니다. 원본 게임에서 번역 에셋을 새로 만드는 도구는 아닙니다.

1. Windows x64에서 Python 3.11 이상과 .NET Framework C# 컴파일러를 준비합니다.
2. 같은 버전 수동 ZIP을 풀고 `패치파일` 폴더를 준비합니다.
3. 저장소 루트에서 아래 명령을 실행합니다. 출력 폴더는 새 경로여야 합니다.

```powershell
python build_release.py --payload-root "수동판/패치파일" --output "output/v1.0.0"
```

입력 4개 파일은 `manifest.json`의 패치 체크섬과 같아야 합니다. 공식 게임 실행 파일, 세이브, 원본 백업은 입력하지 않습니다. 아이콘과 Steam 상점 캡슐은 설치기에 내장됩니다. 캡슐 출처는 https://store.steampowered.com/app/4249520/ 입니다.

생성된 Auto ZIP·Manual ZIP·SHA256SUMS.txt를 함께 검증하세요. 컴파일 시각과 ZIP 메타데이터에 따라 재빌드 결과의 체크섬은 공식 배포본과 다를 수 있습니다. 새로 만든 파일에는 새 체크섬을 사용해야 합니다.
