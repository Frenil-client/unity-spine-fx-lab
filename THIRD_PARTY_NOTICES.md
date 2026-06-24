# Third Party Notices

이 프로젝트는 다음 서드파티 소프트웨어 및 자산을 사용한다.
모든 자작 자산은 본인 소유이며, **특정 게임(트릭컬 등) IP의 모델·아틀라스·텍스처 등
추출 IP 자산은 일절 포함하지 않는다.**

---

## 런타임 / 패키지

### Unity Universal Render Pipeline (URP)
- 버전: 17.3.0 (Unity 6000.3.x)
- 라이선스: Unity Companion License / Unity 패키지 약관
- © Unity Technologies

### spine-unity (Esoteric Software)
- Spine 2D 스켈레탈 애니메이션 Unity 런타임
- 라이선스: **Spine Runtimes License Agreement** (MIT 아님 — 별도 약관)
  - Spine 런타임 사용에는 유효한 Spine 라이선스가 필요하다.
  - http://esotericsoftware.com/spine-runtimes-license
- © Esoteric Software LLC
- https://github.com/EsotericSoftware/spine-runtimes

> ⚠️ **라이선스 체크포인트**: spine-unity 런타임 코드는 위 약관을 따른다.
> 이 레포에는 런타임 임포트분과 셰이더/스크립트만 두고, 상용 게임에서 추출한
> 스켈레톤·아틀라스·텍스처는 포함하지 않는다.

---

## 아트 자산

### Spine 스켈레톤 / 아틀라스 — 공식 예제 에셋
- `Assets/Art/Spine/spineboy-4.3/*`
- 출처: **Esoteric Software 공식 예제** (spine-runtimes `examples/`)
  - spineboy — github.com/EsotericSoftware/spine-runtimes/tree/4.3/examples/spineboy
- 라이선스: 동봉된 [`spineboy-4.3/license.txt`](Assets/Art/Spine/spineboy-4.3/license.txt) (© 2013 Esoteric Software LLC)
  - **이미지** — license 파일을 함께 두는 조건으로 **재배포 가능**, **비상업적 사용 한정**
  - **프로젝트/스켈레톤 파일** — **public domain** (파생 작업 자유)
  - → 본 레포(비상업 포트폴리오)는 license.txt 동봉 상태로 재배포 조건을 충족한다.
- 추출 IP 에셋은 사용하지 않으며, 자작/CC0 에셋도 필요 시 추가한다.

### 텍스처 (디졸브 노이즈 / 마스크 / SDF 맵)
- `Assets/Art/Textures/*`
- 본 프로젝트 내에서 직접 제작/베이크한 자작 텍스처.

---

## 고지 갱신 규칙
서드파티 패키지·자산을 추가할 때마다 이 문서에 항목을 추가한다.
(라이선스 종류 · 출처 URL · 사용 범위 명시)
