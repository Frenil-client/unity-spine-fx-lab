# Spine 텍스처를 Linear 색공간에서 올바르게 - sRGB / 알파 2축

> 셋업 단계에서 캐릭터가 하얗게/뿌옇게 나오던 문제의 원인과, **sRGB 플래그**와 **알파 premultiply
> 시점** 두 축을 어떻게 맞췄는지 정리. 이 레포의 "PMA 일관성" 설계 원칙이 여기서 나온다.

## 맥락 / 증상
프로젝트는 Linear 색공간 + URP 2D. 처음 spineboy 아틀라스로 캐릭터를 띄우자 색이 **하얗게 뜨거나
전체적으로 뿌옇게(washed out)** 나왔고, Spine 임포트 시 **"PMA 텍스처를 Linear 에서 쓴다"는 경고**가
떴다. 원인은 두 가지 독립된 축이 섞여 헷갈린 데 있었다.

---

## 1. 헷갈리는 두 축
- **축 A - 텍스처 sRGB 플래그(임포트 설정):** 색(albedo) 아틀라스는 sRGB **ON**, 노이즈/마스크 같은
  데이터 텍스처는 sRGB **OFF**. Linear 워크플로는 sRGB 텍스처를 샘플 시 linear 로 변환하고 출력 시
  되돌린다. 이 플래그가 틀리면 밝기가 어긋난다(너무 밝거나 뿌옇거나).
- **축 B - 알파 premultiply 시점:** Spine 아틀라스는 두 형태로 export 된다.
  - **PMA(premultiplied) 아틀라스:** export 때 `rgb = rgb * a` 를 미리 구워 둔다.
  - **straight 아틀라스:** rgb 는 그대로, 알파는 따로. premultiply 는 셰이더가 한다.

두 축을 따로 봐야 하는데 한 덩어리로 다루면 "뭘 바꿔야 정상인지" 가 안 보인다.

## 2. 왜 Linear 에서 PMA 베이크가 깨지나
PMA 아틀라스는 `rgb * a` 곱이 **export 시점(gamma 공간)** 에 일어난다. 그런데 Linear 워크플로는
텍스처를 **샘플한 뒤 linear 로 변환**한다. 곱셈과 색공간 변환의 순서가 어긋나(gamma 에서 곱한 값을
linear 로 펴는 꼴) **경계에 halo/검은 테두리**가 생기거나 색이 탁해진다. 이게 임포트 경고의 정체다.
PMA 를 그대로 쓰려면 색공간을 Gamma 로 내려야 하는데, 그건 프로젝트 전체 정책을 뒤집는 일이라 부적절.

## 3. 선택 - straight alpha + 셰이더 premultiply
색공간(Linear)은 유지하고, premultiply 를 **linear 공간 안에서** 하도록 옮긴다.

- **텍스처:** straight alpha 아틀라스를 쓰고 **sRGB ON**, **Alpha Is Transparency ON**.
- **머티리얼:** `Straight Alpha Texture`(셰이더 키워드 `_STRAIGHT_ALPHA_INPUT`) **ON**.
- **셰이더:** 샘플(자동으로 linear 변환됨) 뒤 premultiply 한다. `SpineFx_Common.hlsl / PremultiplyBase`:
  ```hlsl
  #if defined(_STRAIGHT_ALPHA_INPUT)
      c.rgb = tex.rgb * vertexColor.rgb * tex.a;   // linear 공간에서 premultiply
  #else
      c.rgb = tex.rgb * vertexColor.rgb;           // 이미 PMA 라면 곱하지 않음
  #endif
      c.a = tex.a * vertexColor.a;
  ```
- **블렌드:** `One / OneMinusSrcAlpha` (premultiplied 전제). 모든 효과도 알파를 함께 곱해
  premultiplied 상태를 유지한다.

곱이 gamma 가 아니라 linear 에서 일어나므로 경계가 정상이고 색도 정상으로 나온다.

## 4. 증상 -> 원인 매핑 (디버깅에서 배운 것)
| 증상 | 원인 |
|---|---|
| 전체가 하얗게 | straight 텍스처인데 셰이더가 premultiply 를 안 함(또는 알파 처리 누락) |
| 뿌옇게/washed out | 색 아틀라스 sRGB OFF (linear 변환이 안 됨) |
| 경계에 검은/흰 halo | PMA <-> straight 불일치 (PMA 텍스처를 straight 로 곱하거나 그 반대) |
| 노이즈가 너무 밝음 | 데이터 텍스처(노이즈/마스크)에 sRGB ON |

## 5. 이 레포의 고정 규칙
- **모든 SpineFx 계열 셰이더는 `_STRAIGHT_ALPHA_INPUT` 경로를 명시 처리** (straight 입력 -> 셰이더 premultiply).
- **색 아틀라스 sRGB ON**, **노이즈/마스크(`DissolveNoise`) sRGB OFF** (NoiseTextureGenerator 가 OFF 로 생성).
- 블렌드는 항상 `One / OneMinusSrcAlpha`, 효과 단계마다 알파를 곱해 premultiplied 를 깨지 않는다.
- 색공간은 **Linear 유지**. PMA 를 위해 Gamma 로 내리지 않는다.
