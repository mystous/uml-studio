namespace UmlStudio.Models;

public enum DiagramType
{
    Class,
    Sequence,
    Component,
    State,
    UseCase,
    Activity
}

public enum ShapeKind
{
    // Class
    Class,
    Interface,
    Enumeration,
    Package,
    Note,
    // Sequence
    ActorLifeline,
    ObjectLifeline,
    // Component
    Component,
    Port,
    Artifact,
    Node,
    // State
    State,
    InitialState,
    FinalState,
    Choice,
    // UseCase
    Actor,
    UseCase,
    SystemBoundary,
    // Activity
    Action,
    InitialNode,
    FinalNode,
    Decision,
    Merge,
    Fork,
    Join
}

public enum ConnectorKind
{
    Association,
    Inheritance,
    Realization,
    Aggregation,
    Composition,
    Dependency,
    MessageSync,
    MessageAsync,
    MessageReturn,
    CreateMessage,
    DestroyMessage,
    Transition,
    Include,
    Extend,
    ControlFlow,
    ObjectFlow,
    NoteLink
}

public static class DiagramCatalog
{
    public static string DisplayName(DiagramType t) => t switch
    {
        DiagramType.Class => "클래스",
        DiagramType.Sequence => "시퀀스",
        DiagramType.Component => "컴포넌트",
        DiagramType.State => "상태",
        DiagramType.UseCase => "유스케이스",
        DiagramType.Activity => "액티비티",
        _ => t.ToString()
    };

    public static string ShapeName(ShapeKind k) => k switch
    {
        ShapeKind.Class => "클래스",
        ShapeKind.Interface => "인터페이스",
        ShapeKind.Enumeration => "열거형",
        ShapeKind.Node => "노드",
        ShapeKind.Package => "패키지",
        ShapeKind.Note => "노트",
        ShapeKind.ActorLifeline => "액터 라이프라인",
        ShapeKind.ObjectLifeline => "객체 라이프라인",
        ShapeKind.Component => "컴포넌트",
        ShapeKind.Port => "포트",
        ShapeKind.Artifact => "아티팩트",
        ShapeKind.State => "상태",
        ShapeKind.InitialState => "시작 상태",
        ShapeKind.FinalState => "종료 상태",
        ShapeKind.Choice => "선택",
        ShapeKind.Actor => "액터",
        ShapeKind.UseCase => "유스케이스",
        ShapeKind.SystemBoundary => "시스템 경계",
        ShapeKind.Action => "액션",
        ShapeKind.InitialNode => "시작",
        ShapeKind.FinalNode => "종료",
        ShapeKind.Decision => "결정",
        ShapeKind.Merge => "병합",
        ShapeKind.Fork => "분기",
        ShapeKind.Join => "결합",
        _ => k.ToString()
    };

    public static string ConnectorName(ConnectorKind k) => k switch
    {
        ConnectorKind.Association => "연관",
        ConnectorKind.Inheritance => "상속",
        ConnectorKind.Realization => "실현",
        ConnectorKind.Aggregation => "집합",
        ConnectorKind.Composition => "합성",
        ConnectorKind.Dependency => "의존",
        ConnectorKind.MessageSync => "동기 메시지",
        ConnectorKind.MessageAsync => "비동기 메시지",
        ConnectorKind.MessageReturn => "반환",
        ConnectorKind.CreateMessage => "생성",
        ConnectorKind.DestroyMessage => "소멸",
        ConnectorKind.Transition => "전이",
        ConnectorKind.Include => "포함(include)",
        ConnectorKind.Extend => "확장(extend)",
        ConnectorKind.ControlFlow => "제어 흐름",
        ConnectorKind.ObjectFlow => "객체 흐름",
        ConnectorKind.NoteLink => "노트 링크",
        _ => k.ToString()
    };

    public static IReadOnlyList<(ShapeKind Kind, string Name)> ShapesFor(DiagramType t) => t switch
    {
        DiagramType.Class => new[]
        {
            (ShapeKind.Class, "클래스"),
            (ShapeKind.Interface, "인터페이스"),
            (ShapeKind.Enumeration, "열거형"),
            (ShapeKind.Package, "패키지")
        },
        DiagramType.Sequence => new[]
        {
            (ShapeKind.ActorLifeline, "액터"),
            (ShapeKind.ObjectLifeline, "객체")
        },
        DiagramType.Component => new[]
        {
            (ShapeKind.Component, "컴포넌트"),
            (ShapeKind.Port, "포트"),
            (ShapeKind.Artifact, "아티팩트"),
            (ShapeKind.Node, "노드"),
            (ShapeKind.Package, "패키지")
        },
        DiagramType.State => new[]
        {
            (ShapeKind.State, "상태"),
            (ShapeKind.InitialState, "시작"),
            (ShapeKind.FinalState, "종료"),
            (ShapeKind.Choice, "선택")
        },
        DiagramType.UseCase => new[]
        {
            (ShapeKind.Actor, "액터"),
            (ShapeKind.UseCase, "유스케이스"),
            (ShapeKind.SystemBoundary, "시스템 경계")
        },
        DiagramType.Activity => new[]
        {
            (ShapeKind.Action, "액션"),
            (ShapeKind.InitialNode, "시작"),
            (ShapeKind.FinalNode, "종료"),
            (ShapeKind.Decision, "결정"),
            (ShapeKind.Merge, "병합"),
            (ShapeKind.Fork, "분기"),
            (ShapeKind.Join, "결합")
        },
        _ => Array.Empty<(ShapeKind, string)>()
    };

    public static IReadOnlyList<(ConnectorKind Kind, string Name)> ConnectorsFor(DiagramType t) => t switch
    {
        DiagramType.Class => new[]
        {
            (ConnectorKind.Association, "연관"),
            (ConnectorKind.Inheritance, "상속"),
            (ConnectorKind.Realization, "실현"),
            (ConnectorKind.Aggregation, "집합"),
            (ConnectorKind.Composition, "합성"),
            (ConnectorKind.Dependency, "의존")
        },
        DiagramType.Sequence => new[]
        {
            (ConnectorKind.MessageSync, "동기"),
            (ConnectorKind.MessageAsync, "비동기"),
            (ConnectorKind.MessageReturn, "반환"),
            (ConnectorKind.CreateMessage, "생성"),
            (ConnectorKind.DestroyMessage, "소멸")
        },
        DiagramType.Component => new[]
        {
            (ConnectorKind.Dependency, "의존"),
            (ConnectorKind.Realization, "실현"),
            (ConnectorKind.Association, "연관"),
            (ConnectorKind.Aggregation, "집합")
        },
        DiagramType.State => new[]
        {
            (ConnectorKind.Transition, "전이")
        },
        DiagramType.UseCase => new[]
        {
            (ConnectorKind.Association, "연관"),
            (ConnectorKind.Include, "포함"),
            (ConnectorKind.Extend, "확장"),
            (ConnectorKind.Inheritance, "일반화")
        },
        DiagramType.Activity => new[]
        {
            (ConnectorKind.ControlFlow, "제어 흐름"),
            (ConnectorKind.ObjectFlow, "객체 흐름")
        },
        _ => Array.Empty<(ConnectorKind, string)>()
    };

    public static string DiagramKeyword(DiagramType t) => t switch
    {
        DiagramType.Class => "class",
        DiagramType.Sequence => "sd",
        DiagramType.Component => "cmp",
        DiagramType.State => "stm",
        DiagramType.UseCase => "uc",
        DiagramType.Activity => "act",
        _ => "diagram"
    };

    public static (double W, double H) DefaultSize(ShapeKind k) => k switch
    {
        ShapeKind.Class => (170, 90),
        ShapeKind.Interface => (170, 70),
        ShapeKind.Enumeration => (150, 90),
        ShapeKind.Package => (190, 120),
        ShapeKind.Note => (170, 70),
        ShapeKind.ActorLifeline => (100, 70),
        ShapeKind.ObjectLifeline => (130, 46),
        ShapeKind.Component => (170, 80),
        ShapeKind.Port => (16, 16),
        ShapeKind.Artifact => (140, 60),
        ShapeKind.Node => (200, 130),
        ShapeKind.State => (150, 64),
        ShapeKind.InitialState or ShapeKind.InitialNode => (24, 24),
        ShapeKind.FinalState or ShapeKind.FinalNode => (28, 28),
        ShapeKind.Choice or ShapeKind.Decision or ShapeKind.Merge => (40, 40),
        ShapeKind.Actor => (60, 90),
        ShapeKind.UseCase => (150, 64),
        ShapeKind.SystemBoundary => (360, 380),
        ShapeKind.Action => (150, 52),
        ShapeKind.Fork or ShapeKind.Join => (150, 8),
        _ => (140, 80)
    };

    /// <summary>Default fill / stroke per element kind.</summary>
    public static (string Fill, string Stroke) DefaultColors(ShapeKind k) => k switch
    {
        ShapeKind.Class => ("#FFF7D6", "#8C6D1F"),
        ShapeKind.Interface => ("#EDE4F7", "#6A4C93"),
        ShapeKind.Enumeration => ("#E6F4EA", "#3C7A4E"),
        ShapeKind.Package => ("#FCEBD2", "#9A6A2F"),
        ShapeKind.Note => ("#FFFDE5", "#A39E6B"),
        ShapeKind.ActorLifeline => ("#FFFFFF", "#3F4B5E"),
        ShapeKind.ObjectLifeline => ("#E3ECF8", "#3E5F8A"),
        ShapeKind.Component => ("#DDEBFA", "#2F5D8F"),
        ShapeKind.Port => ("#FFFFFF", "#2F5D8F"),
        ShapeKind.Artifact => ("#F1F3F5", "#5A6470"),
        ShapeKind.Node => ("#E9EEF3", "#4A5A6A"),
        ShapeKind.State => ("#E4F1FB", "#2E6A99"),
        ShapeKind.Choice => ("#FFFFFF", "#333333"),
        ShapeKind.Actor => ("#FFFFFF", "#3F4B5E"),
        ShapeKind.UseCase => ("#E8F5E0", "#4A7D2E"),
        ShapeKind.SystemBoundary => ("#FAFBFD", "#5B6B80"),
        ShapeKind.Action => ("#E6F4EC", "#2F7A55"),
        ShapeKind.Decision or ShapeKind.Merge => ("#FFF3E0", "#A0661E"),
        _ => ("#FFFFFF", "#222222")
    };

    public static string DefaultName(ShapeKind k) => k switch
    {
        ShapeKind.Class => "Class",
        ShapeKind.Interface => "IInterface",
        ShapeKind.Enumeration => "Enum",
        ShapeKind.Package => "Package",
        ShapeKind.Note => "노트",
        ShapeKind.ActorLifeline => "Actor",
        ShapeKind.ObjectLifeline => "object",
        ShapeKind.Component => "Component",
        ShapeKind.Port => "port",
        ShapeKind.Artifact => "artifact.jar",
        ShapeKind.Node => "Server",
        ShapeKind.State => "State",
        ShapeKind.InitialState or ShapeKind.InitialNode => "Initial",
        ShapeKind.FinalState or ShapeKind.FinalNode => "Final",
        ShapeKind.Choice or ShapeKind.Decision => "",
        ShapeKind.Merge => "",
        ShapeKind.Actor => "Actor",
        ShapeKind.UseCase => "Use Case",
        ShapeKind.SystemBoundary => "System",
        ShapeKind.Action => "Action",
        ShapeKind.Fork or ShapeKind.Join => "",
        _ => k.ToString()
    };

    public static string DefaultStereotype(ShapeKind k) => k switch
    {
        ShapeKind.Interface => "interface",
        ShapeKind.Enumeration => "enumeration",
        ShapeKind.Component => "component",
        ShapeKind.Artifact => "artifact",
        ShapeKind.Node => "device",
        _ => ""
    };

    public static string DefaultStereotype(ConnectorKind k) => k switch
    {
        ConnectorKind.Include => "include",
        ConnectorKind.Extend => "extend",
        _ => ""
    };

    /// <summary>Elements that represent model elements (shown in the Project Browser).</summary>
    public static bool IsModelElement(ShapeKind k) => k is not (ShapeKind.Note or ShapeKind.Fork or ShapeKind.Join
        or ShapeKind.Merge or ShapeKind.Decision or ShapeKind.Choice or ShapeKind.InitialNode or ShapeKind.InitialState
        or ShapeKind.FinalNode or ShapeKind.FinalState or ShapeKind.Port);

    public static bool HasFeatures(ShapeKind k) => k is ShapeKind.Class or ShapeKind.Interface or ShapeKind.Enumeration
        or ShapeKind.State or ShapeKind.Component or ShapeKind.ObjectLifeline;
}
