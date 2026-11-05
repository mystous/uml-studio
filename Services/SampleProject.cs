using UmlStudio.Models;
using UmlStudio.Rendering;

namespace UmlStudio.Services;

/// <summary>Builds the rich demo model and Model-Builder templates.</summary>
public static class SampleProject
{
    private static UmlShape S(UmlDiagram d, ShapeKind k, string name, double x, double y, double w = 0, double h = 0,
        string? stereo = null, string[]? attrs = null, string[]? ops = null, string notes = "", bool fit = true)
    {
        var (dw, dh) = DiagramCatalog.DefaultSize(k);
        var s = new UmlShape
        {
            Kind = k, Name = name, X = x, Y = y,
            Width = w > 0 ? w : dw, Height = h > 0 ? h : dh,
            Stereotype = stereo ?? DiagramCatalog.DefaultStereotype(k),
            Attributes = attrs?.ToList() ?? new(), Operations = ops?.ToList() ?? new(),
            Notes = notes, Z = d.Shapes.Count
        };
        if (fit) DiagramRenderer.AutoFit(s, false);
        d.Shapes.Add(s);
        return s;
    }

    private static UmlConnector C(UmlDiagram d, ConnectorKind k, UmlShape a, UmlShape b, string label = "",
        string fm = "", string tm = "", double y = 0, string guard = "", RouteStyle route = RouteStyle.Direct, string fromRole = "", string toRole = "")
    {
        var c = new UmlConnector
        {
            Kind = k, FromId = a.Id, ToId = b.Id, Label = label, FromMultiplicity = fm, ToMultiplicity = tm,
            FromY = y, ToY = y, Guard = guard, Route = route, Stereotype = DiagramCatalog.DefaultStereotype(k),
            FromRole = fromRole, ToRole = toRole
        };
        d.Connectors.Add(c);
        return c;
    }

    public static UmlProject CreateDemo()
    {
        var p = new UmlProject { Name = "온라인 쇼핑몰 모델", Author = "UML Studio" };
        p.Root = new UmlPackage { Name = "쇼핑몰 모델", Notes = "온라인 쇼핑몰 시스템의 요구사항, 도메인, 동작, 아키텍처 모델입니다." };

        var req = new UmlPackage { Name = "1. 요구사항", Stereotype = "requirements", Notes = "액터와 유스케이스로 정리한 기능 요구사항" };
        var dom = new UmlPackage { Name = "2. 도메인 모델", Notes = "핵심 비즈니스 개념과 관계" };
        var svc = new UmlPackage { Name = "서비스 계층", Notes = "애플리케이션 서비스와 외부 연동 인터페이스" };
        var beh = new UmlPackage { Name = "3. 동작 모델", Notes = "주문 처리의 상호작용, 상태, 흐름" };
        var arch = new UmlPackage { Name = "4. 아키텍처", Notes = "배포 단위와 컴포넌트 구성" };
        dom.Packages.Add(svc);
        p.Root.Packages.AddRange(new[] { req, dom, beh, arch });

        dom.Diagrams.Add(DomainClass());
        svc.Diagrams.Add(ServiceClass());
        req.Diagrams.Add(UseCases());
        beh.Diagrams.Add(OrderSequence());
        beh.Diagrams.Add(OrderStates());
        beh.Diagrams.Add(PaymentActivity());
        arch.Diagrams.Add(Components());
        return p;
    }

    private static UmlDiagram DomainClass()
    {
        var d = new UmlDiagram { Name = "도메인 클래스", Type = DiagramType.Class, Notes = "주문 도메인의 핵심 엔터티와 관계를 보여줍니다." };
        var customer = S(d, ShapeKind.Class, "Customer", 60, 60, 210, 0, "entity",
            new[] { "- id: Long", "- name: String", "- email: String", "- grade: MemberGrade" },
            new[] { "+ placeOrder(cart: Cart): Order", "+ changeEmail(email: String): void" }, "회원 고객. 등급에 따라 할인율이 달라집니다.");
        var address = S(d, ShapeKind.Class, "Address", 60, 330, 210, 0, "value object",
            new[] { "- zipCode: String", "- street: String", "- city: String" }, new[] { "+ format(): String" });
        var order = S(d, ShapeKind.Class, "Order", 400, 60, 230, 0, "entity",
            new[] { "- id: Long", "- orderedAt: DateTime", "- status: OrderStatus", "- total: Money" },
            new[] { "+ addLine(p: Product, qty: int): void", "+ submit(): void", "+ cancel(reason: String): void", "+ calculateTotal(): Money" },
            "주문 애그리거트 루트. 주문 항목의 일관성을 책임집니다.");
        var line = S(d, ShapeKind.Class, "OrderLine", 400, 330, 230, 0, "entity",
            new[] { "- quantity: int", "- unitPrice: Money" }, new[] { "+ subtotal(): Money" });
        var product = S(d, ShapeKind.Class, "Product", 800, 330, 210, 0, "entity",
            new[] { "- sku: String", "- name: String", "- price: Money", "- stock: int" }, new[] { "+ isAvailable(qty: int): bool" });
        var category = S(d, ShapeKind.Class, "Category", 800, 560, 210, 0, null,
            new[] { "- code: String", "- name: String" }, Array.Empty<string>());
        var status = S(d, ShapeKind.Enumeration, "OrderStatus", 60, 560, 170, 0, "enumeration",
            new[] { "CREATED", "PAID", "SHIPPED", "DELIVERED", "CANCELLED" });
        var payment = S(d, ShapeKind.Class, "Payment", 800, 60, 210, 0, null,
            new[] { "# amount: Money", "# paidAt: DateTime" }, new[] { "+ pay(): Receipt {abstract}", "+ refund(): void" });
        payment.IsAbstract = true;
        var card = S(d, ShapeKind.Class, "CardPayment", 1110, 30, 190, 0, null,
            new[] { "- cardNo: String", "- approvalNo: String" }, new[] { "+ pay(): Receipt" });
        var bank = S(d, ShapeKind.Class, "BankTransfer", 1110, 210, 190, 0, null,
            new[] { "- bankCode: String", "- account: String" }, new[] { "+ pay(): Receipt" });
        var note = S(d, ShapeKind.Note, "노트", 400, 580, 250, 60, fit: false,
            notes: "Order는 애그리거트 루트입니다.\nOrderLine은 Order를 통해서만 변경됩니다.");

        C(d, ConnectorKind.Association, customer, order, "places", "1", "0..*", route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.Composition, address, customer, "", "1", "1..*", route: RouteStyle.Orthogonal, toRole: "+shipTo");
        C(d, ConnectorKind.Composition, line, order, "", "1..*", "1", route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.Association, line, product, "", "0..*", "1", route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.Aggregation, product, category, "", "0..*", "1", route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.Association, order, payment, "paid by", "1", "0..1", route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.Inheritance, card, payment, route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.Inheritance, bank, payment, route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.NoteLink, note, line);
        return d;
    }

    private static UmlDiagram ServiceClass()
    {
        var d = new UmlDiagram { Name = "서비스 인터페이스", Type = DiagramType.Class };
        var igw = S(d, ShapeKind.Interface, "IPaymentGateway", 420, 60, 250, 0, "interface", Array.Empty<string>(),
            new[] { "+ authorize(req: PayRequest): PayResult", "+ capture(txId: String): void", "+ cancel(txId: String): void" });
        var svc = S(d, ShapeKind.Class, "OrderService", 60, 60, 260, 0, "service",
            new[] { "- orders: OrderRepository", "- gateway: IPaymentGateway" },
            new[] { "+ placeOrder(cmd: PlaceOrder): OrderId", "+ cancelOrder(id: OrderId): void", "+ getOrder(id: OrderId): OrderDto" });
        var toss = S(d, ShapeKind.Class, "TossPaymentsAdapter", 300, 300, 230, 0, "adapter",
            new[] { "- apiKey: String" }, new[] { "+ authorize(req: PayRequest): PayResult", "+ capture(txId: String): void", "+ cancel(txId: String): void" });
        var kcp = S(d, ShapeKind.Class, "KcpAdapter", 580, 300, 230, 0, "adapter",
            new[] { "- siteCode: String" }, new[] { "+ authorize(req: PayRequest): PayResult", "+ capture(txId: String): void", "+ cancel(txId: String): void" });
        var repo = S(d, ShapeKind.Interface, "OrderRepository", 60, 300, 200, 0, "interface", Array.Empty<string>(),
            new[] { "+ save(o: Order): void", "+ findById(id: OrderId): Order" });
        C(d, ConnectorKind.Dependency, svc, igw, "uses", route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.Realization, toss, igw, route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.Realization, kcp, igw, route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.Dependency, svc, repo, "", route: RouteStyle.Orthogonal);
        return d;
    }

    private static UmlDiagram UseCases()
    {
        var d = new UmlDiagram { Name = "주문 유스케이스", Type = DiagramType.UseCase };
        var sys = S(d, ShapeKind.SystemBoundary, "온라인 쇼핑몰", 220, 40, 440, 470);
        sys.Z = -1;
        var cust = S(d, ShapeKind.Actor, "고객", 80, 120);
        var admin = S(d, ShapeKind.Actor, "운영자", 80, 360);
        var pg = S(d, ShapeKind.Actor, "결제대행사", 760, 220);
        pg.Stereotype = "system";
        var browse = S(d, ShapeKind.UseCase, "상품 검색", 300, 80, 170, 60);
        var order = S(d, ShapeKind.UseCase, "주문하기", 300, 180, 170, 60);
        var pay = S(d, ShapeKind.UseCase, "결제하기", 480, 260, 160, 60);
        var coupon = S(d, ShapeKind.UseCase, "쿠폰 적용", 480, 140, 160, 60);
        var manage = S(d, ShapeKind.UseCase, "주문 관리", 300, 370, 170, 60);
        var login = S(d, ShapeKind.UseCase, "로그인", 480, 400, 150, 56);
        C(d, ConnectorKind.Association, cust, browse);
        C(d, ConnectorKind.Association, cust, order);
        C(d, ConnectorKind.Association, admin, manage);
        C(d, ConnectorKind.Association, pay, pg);
        C(d, ConnectorKind.Include, order, pay);
        C(d, ConnectorKind.Extend, coupon, order);
        C(d, ConnectorKind.Include, manage, login);
        return d;
    }

    private static UmlDiagram OrderSequence()
    {
        var d = new UmlDiagram { Name = "주문 처리 시퀀스", Type = DiagramType.Sequence, Notes = "고객 주문부터 결제 승인까지의 메시지 흐름" };
        var cust = S(d, ShapeKind.ActorLifeline, "고객", 40, 30, 100, 70);
        var ui = S(d, ShapeKind.ObjectLifeline, ":WebApp", 190, 40, 130, 46, "boundary");
        var svc = S(d, ShapeKind.ObjectLifeline, ":OrderService", 370, 40, 140, 46, "control");
        var repo = S(d, ShapeKind.ObjectLifeline, ":OrderRepository", 560, 40, 150, 46, "entity");
        var pg = S(d, ShapeKind.ObjectLifeline, ":PaymentGateway", 760, 40, 150, 46, "boundary");
        foreach (var l in new[] { cust, ui, svc, repo, pg }) l.LifelineLength = 470 - (l.Height - 46);
        C(d, ConnectorKind.MessageSync, cust, ui, "주문 버튼 클릭", y: 140);
        C(d, ConnectorKind.MessageSync, ui, svc, "placeOrder(cmd)", y: 180);
        C(d, ConnectorKind.MessageSync, svc, svc, "validateStock()", y: 215);
        C(d, ConnectorKind.MessageSync, svc, repo, "save(order)", y: 270);
        C(d, ConnectorKind.MessageReturn, repo, svc, "orderId", y: 305);
        C(d, ConnectorKind.MessageSync, svc, pg, "authorize(req)", y: 345);
        C(d, ConnectorKind.MessageReturn, pg, svc, "PayResult", y: 385);
        C(d, ConnectorKind.MessageReturn, svc, ui, "OrderId", y: 425);
        C(d, ConnectorKind.MessageAsync, ui, cust, "주문 완료 화면", y: 465);
        return d;
    }

    private static UmlDiagram OrderStates()
    {
        var d = new UmlDiagram { Name = "주문 상태 머신", Type = DiagramType.State };
        var init = S(d, ShapeKind.InitialState, "", 60, 128);
        var created = S(d, ShapeKind.State, "생성됨", 140, 100, 150, 0, attrs: new[] { "entry / 재고 예약" });
        var paid = S(d, ShapeKind.State, "결제완료", 380, 100, 150, 0, attrs: new[] { "entry / 영수증 발행" });
        var shipped = S(d, ShapeKind.State, "배송중", 620, 100, 150, 0, attrs: new[] { "do / 위치 추적" });
        var done = S(d, ShapeKind.State, "배송완료", 620, 280, 150, 64);
        var cancel = S(d, ShapeKind.State, "취소됨", 260, 280, 150, 0, attrs: new[] { "entry / 재고 복원", "entry / 환불 처리" });
        var fin = S(d, ShapeKind.FinalState, "", 860, 298);
        var fin2 = S(d, ShapeKind.FinalState, "", 160, 390);
        C(d, ConnectorKind.Transition, init, created);
        C(d, ConnectorKind.Transition, created, paid, "결제승인");
        C(d, ConnectorKind.Transition, paid, shipped, "출고");
        C(d, ConnectorKind.Transition, shipped, done, "수령확인", route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.Transition, created, cancel, "취소", guard: "결제 전", route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.Transition, paid, cancel, "취소", guard: "출고 전", route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.Transition, done, fin);
        C(d, ConnectorKind.Transition, cancel, fin2, route: RouteStyle.Orthogonal);
        return d;
    }

    private static UmlDiagram PaymentActivity()
    {
        var d = new UmlDiagram { Name = "결제 흐름", Type = DiagramType.Activity };
        var init = S(d, ShapeKind.InitialNode, "", 288, 30);
        var a1 = S(d, ShapeKind.Action, "결제 수단 선택", 225, 90);
        var dec = S(d, ShapeKind.Decision, "", 280, 180);
        var card = S(d, ShapeKind.Action, "카드 승인 요청", 80, 260);
        var bank = S(d, ShapeKind.Action, "가상계좌 발급", 370, 260);
        var merge = S(d, ShapeKind.Merge, "", 280, 350);
        var fork = S(d, ShapeKind.Fork, "", 225, 430);
        var mail = S(d, ShapeKind.Action, "확인 메일 발송", 80, 480);
        var stock = S(d, ShapeKind.Action, "재고 차감", 370, 480);
        var join = S(d, ShapeKind.Join, "", 225, 570);
        var fin = S(d, ShapeKind.FinalNode, "", 286, 610);
        C(d, ConnectorKind.ControlFlow, init, a1);
        C(d, ConnectorKind.ControlFlow, a1, dec);
        C(d, ConnectorKind.ControlFlow, dec, card, guard: "카드", route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.ControlFlow, dec, bank, guard: "계좌이체", route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.ControlFlow, card, merge, route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.ControlFlow, bank, merge, route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.ControlFlow, merge, fork);
        C(d, ConnectorKind.ControlFlow, fork, mail, route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.ControlFlow, fork, stock, route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.ControlFlow, mail, join, route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.ControlFlow, stock, join, route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.ControlFlow, join, fin);
        return d;
    }

    private static UmlDiagram Components()
    {
        var d = new UmlDiagram { Name = "시스템 구성요소", Type = DiagramType.Component };
        var web = S(d, ShapeKind.Node, "Web Server", 40, 60, 240, 220, "execution environment");
        web.Z = -2;
        var app = S(d, ShapeKind.Node, "App Server", 340, 60, 420, 220, "execution environment");
        app.Z = -2;
        var front = S(d, ShapeKind.Component, "Storefront UI", 70, 140, 180, 70);
        var api = S(d, ShapeKind.Component, "Order API", 370, 130, 170, 0, attrs: new[] { "REST /orders", "REST /payments" });
        var pay = S(d, ShapeKind.Component, "Payment Module", 570, 130, 170, 70);
        var db = S(d, ShapeKind.Node, "Database", 370, 360, 200, 110, "database");
        var jar = S(d, ShapeKind.Artifact, "order-service.jar", 600, 380, 160, 56);
        var ext = S(d, ShapeKind.Component, "PG Gateway", 820, 140, 160, 70, "external");
        C(d, ConnectorKind.Dependency, front, api, "HTTPS");
        C(d, ConnectorKind.Dependency, api, pay, "");
        C(d, ConnectorKind.Dependency, api, db, "JDBC", route: RouteStyle.Orthogonal);
        C(d, ConnectorKind.Dependency, pay, ext, "REST");
        C(d, ConnectorKind.Realization, jar, api, "manifest", route: RouteStyle.Orthogonal);
        return d;
    }

    /// <summary>Model Builder lite: root + packages + empty diagrams.</summary>
    public static UmlProject CreateTemplate(string name, bool full)
    {
        var p = new UmlProject { Name = name };
        p.Root = new UmlPackage { Name = name };
        if (!full)
        {
            var pkg = new UmlPackage { Name = "도메인 모델" };
            pkg.Diagrams.Add(new UmlDiagram { Name = "클래스 다이어그램", Type = DiagramType.Class });
            p.Root.Packages.Add(pkg);
            return p;
        }
        (string, DiagramType, string)[] parts =
        {
            ("요구사항", DiagramType.UseCase, "유스케이스 다이어그램"),
            ("도메인 모델", DiagramType.Class, "클래스 다이어그램"),
            ("상호작용", DiagramType.Sequence, "시퀀스 다이어그램"),
            ("상태 모델", DiagramType.State, "상태 머신"),
            ("업무 흐름", DiagramType.Activity, "액티비티 다이어그램"),
            ("아키텍처", DiagramType.Component, "컴포넌트 다이어그램")
        };
        foreach (var (pn, t, dn) in parts)
        {
            var pkg = new UmlPackage { Name = pn };
            pkg.Diagrams.Add(new UmlDiagram { Name = dn, Type = t });
            p.Root.Packages.Add(pkg);
        }
        return p;
    }
}
