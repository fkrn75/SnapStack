namespace SnapStack.Editor;

/// <summary>
/// 편집기 드로잉 도구(§3 EDT). 좌측 도구바에서 활성 도구를 고른다.
/// 펜·형광펜은 InkCanvas 경로, 나머지는 오버레이 Canvas 객체/픽셀 처리 경로.
/// </summary>
public enum EditorTool
{
    /// <summary>선택/이동(도구 해제 상태). 객체 선택·드래그 이동.</summary>
    Select,
    Pen,        // 펜/브러시 (EDT-01) — P
    Highlighter,// 형광펜 (EDT-02) — H
    Rectangle,  // 사각형 (EDT-03) — R
    Ellipse,    // 타원 (EDT-03) — O
    Line,       // 직선 (EDT-04) — L
    Arrow,      // 화살표 (EDT-04) — A
    Text,       // 텍스트 (EDT-05) — T
    Number,     // 번호 스탬프 (EDT-06) — N
    Mosaic,     // 모자이크 (EDT-07) — M
    Crop        // 자르기 (EDT-08) — C
}
