# MathGame

> A mobile math puzzle game focused on fast calculation, satisfying feedback, and maintainable gameplay architecture.
> 빠른 계산, 명확한 피드백, 유지보수 가능한 게임플레이 구조를 목표로 개발하는 모바일 수학 퍼즐 게임입니다.

[한국어](#한국어) · [English](#english)

---

# 한국어

## 프로젝트 소개

MathGame은 빠른 계산과 연속 정답에서 오는 몰입감을 핵심으로 하는 Unity 기반 모바일 퍼즐 게임입니다.

게임의 기본 아이디어는 단순합니다.

> **숫자 퍼즐을 빠르게 풀고, 정확한 판단과 연속 정답을 통해 Fever와 강한 피드백을 만들어내는 것**

이 프로젝트는 단순히 기능을 빠르게 구현하는 데 그치지 않고, 작은 규모의 모바일 게임에서도 유지보수성과 테스트 가능성을 확보할 수 있도록 구조적으로 설계하는 것을 목표로 합니다.

핵심 게임플레이는 Unity Scene, 네트워크 상태, 저장 구현 방식과 최대한 분리하고, 각 시스템의 책임을 명확히 나누어 기능 추가와 테스트가 쉬운 형태로 개발합니다.

---

## 프로젝트 목표

### 게임플레이

* 계산 규칙을 직관적으로 이해할 수 있어야 합니다.
* 빠르고 정확한 판단에 보상을 제공합니다.
* 연속 정답과 빠른 풀이를 Fever 시스템으로 연결합니다.
* 모바일 캐주얼 게임에 맞게 핵심 조작을 단순하게 유지합니다.
* 향후 스테이지 기믹, 성장, 하우징 등의 시스템으로 확장할 수 있도록 설계합니다.

### 개발

* 핵심 게임플레이가 네트워크 연결 여부에 의존하지 않도록 합니다.
* `MonoBehaviour` 실행 순서보다 명확한 상태(State)를 중심으로 게임 흐름을 관리합니다.
* 게임 규칙과 Unity UI / 연출 코드를 분리합니다.
* 시간, 랜덤, 저장, 외부 서비스 의존성을 인터페이스로 추상화합니다.
* 개발 초기부터 Edit Mode / Play Mode 테스트가 가능하도록 구성합니다.
* 전체 기능을 한 번에 만들지 않고 단계별로 검증하며 확장합니다.

---

## 게임 핵심 콘셉트

기본 플레이 흐름은 다음을 지향합니다.

1. 현재 퍼즐 상태를 확인합니다.
2. 올바른 계산 방법을 찾습니다.
3. 빠르게 정답을 입력하거나 숫자 블록을 선택합니다.
4. 정답에 대한 즉각적인 피드백을 받습니다.
5. 빠른 연속 정답으로 Fever를 쌓습니다.
6. 스테이지 목표를 달성하고 다음 단계로 진행합니다.

퍼즐 규칙은 프로토타이핑 과정에서 변경될 수 있지만, 규칙 변경이 전체 스테이지 흐름이나 저장 시스템의 재작성으로 이어지지 않도록 시스템 간 의존성을 분리합니다.

---

## Architecture

MathGame의 핵심 설계 원칙은 다음과 같습니다.

> **게임 규칙이 Unity Scene, 네트워크, 저장 구현체에 직접 의존하지 않도록 한다.**

```text
┌─────────────────────────────────────────────┐
│                  Bootstrap                  │
│              MathGameBootstrap              │
└──────────────────────┬──────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────┐
│                  Game Flow                  │
│                                             │
│      App Lifecycle / Stage Entry / Exit     │
└──────────────────────┬──────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────┐
│               StageController               │
│                                             │
│   Ready → Playing → Paused → Result         │
│                         ├─ Success           │
│                         └─ Failed            │
└──────────────────────┬──────────────────────┘
                       │
             ┌─────────┴─────────┐
             ▼                   ▼
┌──────────────────────┐  ┌──────────────────────┐
│    Gameplay Domain   │  │    Presentation      │
│                      │  │                      │
│ Puzzle Rules         │  │ Unity View           │
│ Board Logic          │  │ UI                   │
│ Score / Fever        │  │ Animation / VFX      │
│ Stage Conditions     │  │ Input                │
└──────────┬───────────┘  └──────────────────────┘
           │
           ▼
┌─────────────────────────────────────────────┐
│              Infrastructure                 │
│                                             │
│ ITimeProvider                               │
│ IRandomSource                               │
│ ISaveRepository                             │
│ Settings / Logging / Platform Services      │
└─────────────────────────────────────────────┘
```

---

## 설계 원칙

### 1. 명시적인 Stage State

스테이지 상태를 여러 bool 값으로 분산 관리하지 않고 명확한 State로 표현합니다.

```text
None
  ↓
Ready
  ↓
Playing
 ├───────────────┐
 ↓               ↓
Paused        Success
 │               │
 └→ Playing      └→ Completed
                 ↑
              Failed
```

`StageController`는 다음 책임을 가집니다.

* 스테이지 시작
* 일시정지
* 재개
* 성공 처리
* 실패 처리
* 잘못된 상태 전환 차단
* 앱 비활성화 및 백그라운드 진입 처리
* 현재 Stage State 제공

이를 통해 UI 이벤트나 `MonoBehaviour` 호출 순서 때문에 스테이지 상태가 암묵적으로 꼬이는 문제를 줄입니다.

---

### 2. Dependency Inversion

핵심 로직에서는 다음 API에 직접 의존하는 것을 최소화합니다.

```csharp
Time.time
UnityEngine.Random
PlayerPrefs
NetworkManager.Instance
```

대신 인터페이스를 통해 외부 의존성을 주입합니다.

```csharp
public interface ITimeProvider
{
    float DeltaTime { get; }
    float Time { get; }
}

public interface IRandomSource
{
    int Range(int minInclusive, int maxExclusive);
}

public interface ISaveRepository
{
    void Save<T>(string key, T value);
    bool TryLoad<T>(string key, out T value);
}
```

이 구조는 다음과 같은 장점을 가집니다.

* 테스트에서 시간을 직접 제어할 수 있습니다.
* 랜덤 결과를 고정하여 재현 가능한 테스트를 만들 수 있습니다.
* 저장 방식 변경 시 게임 로직의 수정 범위를 줄일 수 있습니다.
* Unity API와 핵심 도메인의 결합도를 낮출 수 있습니다.
* 네트워크 환경과 무관하게 핵심 게임플레이를 실행할 수 있습니다.

---

### 3. Offline-First Core Gameplay

핵심 퍼즐 플레이는 네트워크 연결 없이도 동작하는 것을 원칙으로 합니다.

```text
Network Available
      │
      ├── Analytics
      ├── Cloud Save
      ├── Ads
      └── Remote Configuration

Network Unavailable
      │
      └── Core MathGame Gameplay Continues
```

광고, 분석, 클라우드 저장, 원격 설정 등은 핵심 게임플레이를 보조하는 외부 서비스로 취급합니다.

---

### 4. Versioned Persistence

진행도와 설정 데이터는 버전을 포함하여 저장합니다.

```json
{
  "version": 1,
  "progress": {
    "highestStage": 12
  },
  "settings": {
    "bgm": true,
    "sfx": true
  }
}
```

저장 데이터 구조가 변경될 경우 명시적인 Migration 과정을 통해 이전 데이터와의 호환성을 유지할 수 있도록 설계합니다.

---

### 5. 테스트 가능한 게임 규칙

`GameObject`가 없어도 동작 가능한 로직은 최대한 순수 C# 영역으로 분리합니다.

예:

* 계산식 정답 검증
* 보드 좌표 및 이동 규칙
* 점수 계산
* Fever 증가 규칙
* 스테이지 성공 / 실패 조건
* 퍼즐 랜덤 생성
* State Transition 검증

이러한 영역은 Edit Mode Test로 검증합니다.

Unity Runtime 의존 기능은 Play Mode Test에서 검증합니다.

---

## 프로젝트 폴더 구조

```text
Assets/
└── MathGame/
    ├── Runtime/
    │   ├── Application/
    │   │   ├── Bootstrap/
    │   │   └── GameFlow/
    │   │
    │   ├── Stage/
    │   │   ├── StageController.cs
    │   │   ├── StageState.cs
    │   │   └── StageResult.cs
    │   │
    │   ├── Gameplay/
    │   │   ├── Board/
    │   │   ├── Puzzle/
    │   │   ├── Score/
    │   │   └── Fever/
    │   │
    │   ├── Presentation/
    │   │   ├── UI/
    │   │   ├── Input/
    │   │   ├── Animation/
    │   │   └── VFX/
    │   │
    │   ├── Infrastructure/
    │   │   ├── Time/
    │   │   ├── Random/
    │   │   ├── Save/
    │   │   └── Logging/
    │   │
    │   └── MathGame.Runtime.asmdef
    │
    └── Tests/
        ├── EditMode/
        │   └── MathGame.Tests.EditMode.asmdef
        │
        └── PlayMode/
            └── MathGame.Tests.PlayMode.asmdef
```

---

## Stage Lifecycle

```text
Application Start
       │
       ▼
MathGameBootstrap
       │
       ▼
Initialize Services
       │
       ▼
Create / Enter Stage
       │
       ▼
     Ready
       │
       ▼
    Playing
   ┌───┼──────────────┐
   │   │              │
   ▼   ▼              ▼
Pause Success       Failed
   │   │              │
   ▼   └──────┬───────┘
Resume        ▼
   │        Result
   └──► Playing
```

잘못된 상태 전환은 허용하지 않습니다.

```text
Success → Playing     ❌
Failed  → Paused      ❌
Ready   → Success     ❌

Paused  → Playing     ✅
Playing → Success     ✅
Playing → Failed      ✅
```

---

## Fever System

MathGame은 단순히 정답 여부만 확인하는 것이 아니라 빠른 정답을 연속으로 맞혔을 때 플레이 경험이 점점 강해지는 구조를 지향합니다.

Fever는 다음 요소를 활용할 수 있습니다.

* 연속 정답
* 빠른 풀이 시간
* 오답 없이 유지한 Streak

Fever 상태에서는 다음과 같은 보상을 적용할 수 있습니다.

* 점수 배율 증가
* VFX 강화
* 화면 연출 강화
* 사운드 단계 상승
* 특수 Fever Mode
* 성장 / 재화 보너스

```text
Puzzle Solved
     │
     ├── ScoreSystem
     ├── FeverSystem
     └── Presentation Feedback
```

---

## 주요 시스템 책임

| 시스템                 | 역할                             |
| ------------------- | ------------------------------ |
| `MathGameBootstrap` | 프로젝트 시작 및 Dependency 조립        |
| Game Flow           | Application / Stage 진입 및 종료 흐름 |
| `StageController`   | Stage 상태와 Lifecycle 관리         |
| Puzzle              | 문제 생성 및 정답 검증                  |
| Board               | 보드 상태 및 상호작용 규칙                |
| Score               | 점수 계산                          |
| Fever               | 빠른 풀이 / 연속 정답 보상               |
| Presentation        | UI, Animation, VFX             |
| `ITimeProvider`     | 시간 추상화                         |
| `IRandomSource`     | 랜덤 추상화                         |
| `ISaveRepository`   | 저장 시스템 추상화                     |
| Logging             | 구조화된 로그 및 디버깅                  |

---

## 개발 방식

```text
Requirement
    ↓
Design
    ↓
Interface / Data Model
    ↓
Implementation
    ↓
Edit Mode Test
    ↓
Play Mode Integration
    ↓
Manual Verification
    ↓
Next STEP
```

---

## STEP 1 — Project Foundation & Stage Lifecycle

### 목표

```text
Start
Pause
Resume
Success
Fail
```

### 구현 범위

* Application Bootstrap
* Assembly Definition
* Game Flow
* Stage Controller
* Stage State
* Pause / Resume
* Application Deactivation 대응
* `ITimeProvider`
* `IRandomSource`
* `ISaveRepository`
* Structured Logging
* Edit Mode Test
* Play Mode Test

---

## 테스트 전략

### Edit Mode Test

```text
✓ 정상적인 State Transition이 성공하는가
✓ 잘못된 State Transition이 차단되는가
✓ Playing 상태에서만 Pause 가능한가
✓ Paused 상태에서 Resume 가능한가
✓ Stage 종료 이벤트가 두 번 실행되지 않는가
✓ Puzzle 정답 판정이 정상적으로 동작하는가
✓ 빠른 정답에서 Fever가 증가하는가
```

### Play Mode Test

```text
✓ Bootstrap이 필요한 서비스를 생성하는가
✓ Scene에서 Stage가 정상 시작되는가
✓ Application Pause 시 Stage도 Pause 되는가
✓ Resume 시 이전 Stage 상태가 유지되는가
✓ 성공 / 실패 상태가 UI에 전달되는가
```

---

## Tech Stack

* **Engine:** Unity
* **Language:** C#
* **Target Platform:** Mobile
* **Architecture:** Layered Architecture / Dependency Inversion
* **Testing:** Unity Test Framework

  * Edit Mode
  * Play Mode
* **Version Control:** Git / GitHub

---

# English

## Overview

MathGame is a Unity-based mobile puzzle game built around fast arithmetic decisions, consecutive correct answers, and satisfying feedback.

The core idea is simple:

> **Solve number-based puzzles quickly and turn accuracy and speed into Fever, momentum, and stronger gameplay feedback.**

The project is also designed as an architecture study for a small-scale mobile game.

Instead of tightly coupling gameplay to Unity scene objects, networking, persistence, and presentation systems, MathGame separates responsibilities so that gameplay rules remain testable, maintainable, and easier to expand.

---

## Project Goals

### Gameplay

* Keep arithmetic rules immediately understandable.
* Reward fast and accurate decisions.
* Connect consecutive correct answers to a Fever system.
* Keep the core interaction lightweight for mobile casual players.
* Leave room for future stage gimmicks, progression, housing, and additional puzzle mechanics.

### Engineering

* Keep core gameplay independent from network availability.
* Control game flow through explicit states rather than implicit `MonoBehaviour` execution order.
* Separate gameplay rules from Unity presentation code.
* Abstract time, randomness, persistence, and external services behind interfaces.
* Support Edit Mode and Play Mode testing from the beginning.
* Build features incrementally through verified development steps.

---

## Architecture

> **Gameplay rules should not directly depend on Unity scenes, network availability, or persistence implementations.**

```text
┌─────────────────────────────────────────────┐
│                  Bootstrap                  │
│              MathGameBootstrap              │
└──────────────────────┬──────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────┐
│                  Game Flow                  │
│                                             │
│      App Lifecycle / Stage Entry / Exit     │
└──────────────────────┬──────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────┐
│               StageController               │
│                                             │
│   Ready → Playing → Paused → Result         │
│                         ├─ Success           │
│                         └─ Failed            │
└──────────────────────┬──────────────────────┘
                       │
             ┌─────────┴─────────┐
             ▼                   ▼
┌──────────────────────┐  ┌──────────────────────┐
│    Gameplay Domain   │  │    Presentation      │
│                      │  │                      │
│ Puzzle Rules         │  │ Unity View           │
│ Board Logic          │  │ UI                   │
│ Score / Fever        │  │ Animation / VFX      │
│ Stage Conditions     │  │ Input                │
└──────────┬───────────┘  └──────────────────────┘
           │
           ▼
┌─────────────────────────────────────────────┐
│              Infrastructure                 │
│                                             │
│ ITimeProvider                               │
│ IRandomSource                               │
│ ISaveRepository                             │
│ Settings / Logging / Platform Services      │
└─────────────────────────────────────────────┘
```

---

## Design Principles

### Explicit Stage State

Stage progression is represented through explicit states rather than scattered boolean flags.

### Dependency Inversion

Core gameplay code avoids directly depending on:

```csharp
Time.time
UnityEngine.Random
PlayerPrefs
NetworkManager.Instance
```

Instead, dependencies are accessed through interfaces such as:

```csharp
public interface ITimeProvider
{
    float DeltaTime { get; }
    float Time { get; }
}

public interface IRandomSource
{
    int Range(int minInclusive, int maxExclusive);
}

public interface ISaveRepository
{
    void Save<T>(string key, T value);
    bool TryLoad<T>(string key, out T value);
}
```

### Offline-First Core Gameplay

Core gameplay should continue even if external network services are unavailable.

### Versioned Persistence

Save data includes explicit version information so that future schema migrations can be handled safely.

### Testable Game Rules

Gameplay rules that do not require a `GameObject` should remain in plain C# whenever possible.

---

## Planned Project Structure

```text
Assets/
└── MathGame/
    ├── Runtime/
    │   ├── Application/
    │   ├── Stage/
    │   ├── Gameplay/
    │   ├── Presentation/
    │   ├── Infrastructure/
    │   └── MathGame.Runtime.asmdef
    │
    └── Tests/
        ├── EditMode/
        └── PlayMode/
```

---

## Stage Lifecycle

```text
Application Start
       │
       ▼
MathGameBootstrap
       │
       ▼
Initialize Services
       │
       ▼
Create / Enter Stage
       │
       ▼
     Ready
       │
       ▼
    Playing
   ┌───┼──────────────┐
   │   │              │
   ▼   ▼              ▼
Pause Success       Failed
   │   │              │
   ▼   └──────┬───────┘
Resume        ▼
   │        Result
   └──► Playing
```

---

## Fever System

Fever may respond to:

* consecutive correct answers;
* short answer times;
* maintaining a streak without mistakes.

Possible rewards include:

* score multipliers;
* stronger VFX;
* escalating audio;
* special Fever mode;
* progression bonuses.

---

## Development Strategy

```text
Requirement
    ↓
Design
    ↓
Interface / Data Model
    ↓
Implementation
    ↓
Edit Mode Test
    ↓
Play Mode Integration
    ↓
Manual Verification
    ↓
Next STEP
```

---

## STEP 1 — Project Foundation & Stage Lifecycle

### Goal

Support the following lifecycle:

```text
Start
Pause
Resume
Success
Fail
```

### Scope

* Application Bootstrap
* Assembly Definition
* Game Flow
* Stage Controller
* Stage State
* Pause / Resume
* Application Deactivation Handling
* `ITimeProvider`
* `IRandomSource`
* `ISaveRepository`
* Structured Logging
* Edit Mode Tests
* Play Mode Tests

---

## Tech Stack

* **Engine:** Unity
* **Language:** C#
* **Target Platform:** Mobile
* **Architecture:** Layered Architecture / Dependency Inversion
* **Testing:** Unity Test Framework
* **Version Control:** Git / GitHub

---

## Repository Philosophy

### Maintainability

Each system should have clear ownership and minimal hidden dependencies.

### Testability

Core gameplay rules should be verifiable without requiring full scene execution.

### Iterative Development

The project avoids building a large speculative architecture for every possible future feature.

> **The goal is not to build the most complicated architecture possible.
> The goal is to build the smallest architecture that remains clear as the game grows.**
