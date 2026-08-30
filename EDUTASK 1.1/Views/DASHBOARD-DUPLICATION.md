# Dashboard duplication

`DirectorStaffDashboardPage` and `TeacherDashboardPage` are near-clones. This
note records exactly what overlaps, so the refactor can be done deliberately
rather than rediscovered. Nothing here is a bug — it is all working code.

Measured 2026-08-24.

| | DirectorStaff | Teacher |
|---|---|---|
| `.xaml.cs` | 767 lines | 651 lines |
| `.xaml` | 266 unique lines | 234 unique lines |
| Identical code-behind lines | — | 427 of 651 (~65%) |
| Identical XAML lines | — | 165 |

## Why this was not deduplicated

The two pages differ precisely where it matters most: **who is allowed to
approve, validate, and request revisions.** A mechanical merge is exactly the
kind that quietly grants a teacher a director-only action. Any refactor here
needs the permission paths re-tested on a device against a live database, not
just a green build.

## Identical — safe to lift into a shared base as-is

Byte-for-byte identical after whitespace normalisation. ~112 lines per file.

| Method | Lines |
|---|---|
| `WireProfileIcon` | 17 |
| `OnDateFilterClicked` | 13 |
| `OnDeadlineGroupTapped` | 10 |
| `StyleTaskToolsButton` | 10 |
| `UpdateGreeting` | 9 |
| `UpdateFilterChipStyles` | 8 |
| `ShowDateFilterPressedState` | 7 |
| `OnClearSearchTapped` | 5 |
| `OnSearchIconTapped` | 5 |
| `OnSearchTextChanged` | 5 |
| `OnTaskToolsDateFilterClicked` | 5 |
| `OnTaskToolsHistoryClicked` | 5 |
| `SelectStatusFilterAsync` | 5 |
| `OnTaskToolsClicked` | 2 |
| `OnTaskToolsDismissClicked` | 2 |
| `OnAllTasksClicked` | 1 |
| `OnEmptyViewOverdueClicked` | 1 |
| `OnOverdueTasksClicked` | 1 |
| `OnTodayTasksClicked` | 1 |

These seven field declarations are also identical in both files:

```
_currentFilter   _db   _deadlineFilter   _expandedTaskGroups
_isCompletedTodayExpanded   _isTodayExpanded   _loadedTasks
```

## Differs — needs judgement, not a copy

| Method | DirectorStaff | Teacher | What actually differs |
|---|---|---|---|
| `LoadTasks` | 123 | 89 | Different queries and permission gating. The real divergence. |
| `PopulateTodayAndCompletedSections` | 56 | 12 | Director builds far richer sections. |
| `OnSubtaskDiscussionClicked` | 23 | 21 | Identity passed to the discussion page. |
| `ApplyTaskFilters` | 24 | 22 | Teacher filters to own assignments. |
| `OnProfileIconTapped` | 20 | 22 | `User` vs `Teachers` session object. |
| `BuildDeadlineGroups` | 18 | 17 | **Formatting only**, plus Teacher sets `TaskTitle`. Nearly free to merge. |
| `OnCompletionHistoryClicked` | 13 | 11 | Different target page. |
| `OnProofHistoryClicked` | 9 | 6 | Director gets review actions; teacher gets read-only. **Permission-bearing.** |
| `OnAppearing` | 7 | 9 | Teacher has a `_teachersLoaded` warm-up path. |
| `OnNotificationIconTapped` | 4 | 7 | `User` vs `Teachers` recipient. |
| `OnTodayToggleClicked` | 6 | 6 | **One line.** `UpdateTodayTasksVisibility()` vs `UpdateTaskSectionVisibility()`. |
| `OnCompletedTodayToggleClicked` | 6 | 6 | **One line.** `UpdateCompletedTodayTasksVisibility()` vs `UpdateTaskSectionVisibility()`. |

## Suggested order, if picked up later

1. **Naming drift first.** The two `*ToggleClicked` pairs differ by a single
   method name. Reconciling `UpdateTodayTasksVisibility` /
   `UpdateCompletedTodayTasksVisibility` against `UpdateTaskSectionVisibility`
   turns two more methods identical for almost no risk.
2. **Then the 19 identical methods** plus the seven shared fields into a
   `DashboardPageBase : EduTaskPage`. Mechanical, compiler-checked.
3. **`BuildDeadlineGroups` next** — the bodies already agree apart from
   formatting.
4. **Leave `LoadTasks`, `PopulateTodayAndCompletedSections` and
   `OnProofHistoryClicked` per-page**, or make them `abstract`/`virtual`. These
   carry the role-specific behaviour and are where a careless merge causes a
   privilege bug.

Shared pure helpers already extracted: `DeadlineTaskGroup.FormatHeader`,
`TaskPalette.PriorityColor`, `TaskPalette.PriorityRank`.
