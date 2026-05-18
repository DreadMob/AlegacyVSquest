# Quiz System

> **Documentation Version:** v3.1.0

---

## Overview

The quiz system provides interactive multiple-choice tests (4 options per question). Quizzes are used in quests to test player knowledge, with randomized answer order and score tracking.

---

## How It Works

1. A quest triggers the `showquiz <quizId>` action
2. The server loads the quiz definition and sends the first question
3. Answer options are randomized per player per question
4. The player answers questions sequentially
5. On completion, a result screen shows correct/wrong counts
6. The score is stored in player attributes for objective checking

---

## Configuration

Quizzes are loaded from mod assets: `config/quizzes/*.json`

### Quiz Definition

```json
{
  "id": "lore-quiz-chapter1",
  "titleLangKey": "yourmod:quiz-chapter1-title",
  "bodyLangKey": "yourmod:quiz-chapter1-body",
  "questionCount": 5,
  "neededCorrect": 3,
  "questionLangKeyFormat": "yourmod:quiz-chapter1-q{0}",
  "optionALangKeyFormat": "yourmod:quiz-chapter1-q{0}-a",
  "optionBLangKeyFormat": "yourmod:quiz-chapter1-q{0}-b",
  "optionCLangKeyFormat": "yourmod:quiz-chapter1-q{0}-c",
  "optionDLangKeyFormat": "yourmod:quiz-chapter1-q{0}-d",
  "correctOptions": [1, 3, 2, 4, 1],
  "scoreAttributeKey": "vsquest:quiz:lore-quiz-chapter1:score",
  "progressTemplateLangKey": "yourmod:quiz-progress-template",
  "resultTemplateLangKey": "yourmod:quiz-result-template",
  "retryButtonLangKey": "yourmod:quiz-retry",
  "closeButtonLangKey": "yourmod:quiz-close",
  "resultBodyLangKeys": [
    "yourmod:quiz-chapter1-result-bad",
    "yourmod:quiz-chapter1-result-ok",
    "yourmod:quiz-chapter1-result-good"
  ],
  "resultBodyScoreThresholds": [0, 3, 5]
}
```

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique quiz identifier |
| `titleLangKey` | string | Localization key for the dialog title |
| `bodyLangKey` | string | Localization key for the intro text |
| `questionCount` | int | Number of questions |
| `neededCorrect` | int | Minimum correct answers to "pass" |
| `questionLangKeyFormat` | string | Format string for question text (`{0}` = question number) |
| `optionALangKeyFormat` | string | Format string for option A |
| `optionBLangKeyFormat` | string | Format string for option B |
| `optionCLangKeyFormat` | string | Format string for option C |
| `optionDLangKeyFormat` | string | Format string for option D |
| `correctOptions` | int[] | Array of correct answers (1=A, 2=B, 3=C, 4=D) |
| `scoreAttributeKey` | string | Player attribute key to store the final score |
| `progressTemplateLangKey` | string | Progress display template (optional) |
| `resultTemplateLangKey` | string | Result display template (optional) |
| `retryButtonLangKey` | string | Retry button text |
| `closeButtonLangKey` | string | Close button text |
| `resultBodyLangKeys` | string[] | Result body texts by score threshold |
| `resultBodyScoreThresholds` | int[] | Score thresholds for result texts |

---

## Option Randomization

Answer options are randomized per player per question. The order is stored in player `WatchedAttributes` so reopening the same question shows the same order.

Storage key: `vsquest:quiz:{quizId}:order:{questionIndex}`

---

## Result Body Selection

If `resultBodyLangKeys` and `resultBodyScoreThresholds` are defined, the result text is selected based on the player's score:

```
score >= thresholds[2] → resultBodyLangKeys[2]
score >= thresholds[1] → resultBodyLangKeys[1]
score >= thresholds[0] → resultBodyLangKeys[0]
```

The system iterates from index 0 upward, selecting the last threshold the score meets or exceeds.

---

## Integration with Quests

### Starting a quiz (action)

```json
{
  "id": "showquiz",
  "args": ["lore-quiz-chapter1"]
}
```

### Checking the result (objective)

Use `checkvariable` to check the stored score:

```json
{
  "id": "checkvariable",
  "args": ["vsquest:quiz:lore-quiz-chapter1:score", ">=", "3"]
}
```

---

## Network Messages

| Message | Direction | Description |
|---------|-----------|-------------|
| `OpenQuizMessage` | Client → Server | Request to open/reset a quiz |
| `ShowQuizMessage` | Server → Client | Question or result data for display |
| `SubmitQuizAnswerMessage` | Client → Server | Player's answer or retry request |

---

## Notes

- The quiz automatically calls `checkobjective` on completion, allowing the quest to verify the result
- Retry resets all answers and re-randomizes option order
- Maximum 4 answer options per question
- Quizzes are loaded from all mods that provide `config/quizzes/*.json` assets
