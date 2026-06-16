namespace SnapStack.Editor;

/// <summary>
/// Undo/Redo 단위 동작(EDT-10). 적용(Redo)·취소(Undo) 두 동작을 클로저로 들고 있다.
/// 각 드로잉(획 추가·도형 배치·모자이크·자르기)이 자신의 추가/제거 동작을 캡슐화해 push 한다.
/// </summary>
public sealed class EditAction
{
    /// <summary>되돌리기: 효과 제거(예: Canvas에서 도형 제거).</summary>
    public required Action Undo { get; init; }

    /// <summary>다시 적용: 효과 복원(예: Canvas에 도형 재추가).</summary>
    public required Action Redo { get; init; }

    /// <summary>상태바·디버그용 라벨.</summary>
    public string Label { get; init; } = "";
}

/// <summary>
/// 명령 스택(EDT-10). 모든 드로잉 동작을 Undo/Redo로 관리. 새 동작 push 시 redo 스택 비움.
/// </summary>
public sealed class UndoStack
{
    private readonly Stack<EditAction> _undo = new();
    private readonly Stack<EditAction> _redo = new();

    /// <summary>스택 변경(가능 여부·개수) 알림 — 뷰모델이 CanUndo/CanRedo 갱신.</summary>
    public event Action? Changed;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// 이미 화면에 적용된 동작을 등록한다(Redo는 호출하지 않음 — 이미 반영된 상태 전제).
    /// 새 동작이 들어오면 redo 히스토리는 폐기된다.
    /// </summary>
    public void Push(EditAction action)
    {
        _undo.Push(action);
        _redo.Clear();
        Changed?.Invoke();
    }

    /// <summary>직전 동작 되돌리기.</summary>
    public void Undo()
    {
        if (_undo.Count == 0) return;
        var a = _undo.Pop();
        a.Undo();
        _redo.Push(a);
        Changed?.Invoke();
    }

    /// <summary>되돌린 동작 다시 적용.</summary>
    public void Redo()
    {
        if (_redo.Count == 0) return;
        var a = _redo.Pop();
        a.Redo();
        _undo.Push(a);
        Changed?.Invoke();
    }

    /// <summary>전체 초기화(편집기 닫힘 등).</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke();
    }
}
