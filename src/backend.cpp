#include <QGuiApplication>
#include <QQuickWindow>
#include <QQuickPaintedItem>
#include <QQmlExtensionPlugin>
#include <qqml.h>
#include <QPainter>
#include <QPointer>
#include <QTimer>
#include <QDBusConnection>
#include <QJsonDocument>
#include <QJsonArray>
#include <QJsonObject>
#include <QKeySequence>
#include <KConfig>
#include <KConfigGroup>
#include <PlasmaQuick/AppletQuickItem>
#include <Plasma/Applet>
#include <Plasma/Containment>
#include <algorithm>

class Badge final : public QQuickPaintedItem {
public:
    explicit Badge(QQuickItem *task) : QQuickPaintedItem(task) {
        setParent(task);
        setAcceptedMouseButtons(Qt::NoButton);
        setAcceptHoverEvents(false);
        setAcceptTouchEvents(false);
        setFlag(ItemIsFocusScope, false);
        setZ(1000);
        setWidth(18); setHeight(18);
        setAntialiasing(true);
    }
    int number = 0;
    void paint(QPainter *p) override {
        p->setRenderHint(QPainter::Antialiasing);
        p->setPen(QPen(QColor("#ffffff"), 1));
        p->setBrush(QColor("#18202b"));
        p->drawRoundedRect(QRectF(0.5,0.5,width()-1,height()-1), 5,5);
        QFont font = QGuiApplication::font();
        font.setPixelSize(12); font.setBold(true);
        p->setFont(font);
        p->drawText(boundingRect(), Qt::AlignCenter, QString::number(number));
    }
};

class Backend : public QObject {
    Q_OBJECT
    Q_CLASSINFO("D-Bus Interface", "org.kde.MetaCue")
public:
    explicit Backend(QObject *parent = nullptr) : QObject(parent) {
        auto bus = QDBusConnection::sessionBus();
        owner = bus.registerService("org.kde.MetaCue");
        if (!owner) return;
        bus.registerObject("/MetaCue", this, QDBusConnection::ExportAllSlots);
        bus.connect("org.kde.KWin", "/MetaCue/Input", "org.kde.MetaCue.Input", "VisibleChanged", this, SLOT(inputVisible(bool)));
        watchdog.setSingleShot(true);
        connect(&watchdog, &QTimer::timeout, this, &Backend::hide);
        refresh.setInterval(40);
        connect(&refresh, &QTimer::timeout, this, &Backend::draw);
    }
    ~Backend() override {
        hide();
        for (const auto &badge : badges) if (badge) delete badge;
        if (owner) {
            QDBusConnection::sessionBus().unregisterObject("/MetaCue");
            QDBusConnection::sessionBus().unregisterService("org.kde.MetaCue");
        }
    }
public Q_SLOTS:
    void inputVisible(bool visible) {
        if (!visible) { hide(); return; }
        if (!refresh.isActive()) { readBindings(); refresh.start(); }
        watchdog.start(350); // Fail closed if the input plugin disappears.
        draw();
    }
    QString Status() {
        auto tasks = locate();
        QJsonArray items;
        for (auto *task : tasks) {
            const auto pos = task->mapToScene(QPointF());
            items.append(QJsonObject{{"index",task->property("index").toInt()+1},
                                     {"x",pos.x()},{"y",pos.y()},
                                     {"width",task->width()},{"height",task->height()}});
        }
        return QString::fromUtf8(QJsonDocument(QJsonObject{{"version","0.1.0"},{"reason",reason},
                    {"tasks",items},{"showing",refresh.isActive()},{"badges",visibleCount}}).toJson(QJsonDocument::Compact));
    }
    void Preview() { // Manual visual diagnostic; never changes focus or input.
        readBindings(); refresh.start(); watchdog.start(2000); draw();
    }
    void Stop() { hide(); }
private:
    static void findManagers(QQuickItem *item, QList<PlasmaQuick::AppletQuickItem *> &out) {
        if (auto *appletItem = qobject_cast<PlasmaQuick::AppletQuickItem *>(item)) {
            if (appletItem->applet()) {
                const auto id = appletItem->applet()->pluginMetaData().pluginId();
                if (id == "org.kde.plasma.icontasks" || id == "org.kde.plasma.taskmanager") out.append(appletItem);
            }
        }
        for (auto *child : item->childItems()) findManagers(child, out);
    }
    QList<QQuickItem *> locate() {
        QList<PlasmaQuick::AppletQuickItem *> managers;
        for (auto *window : QGuiApplication::allWindows()) {
            if (auto *quick = qobject_cast<QQuickWindow *>(window)) findManagers(quick->contentItem(), managers);
        }
        if (managers.size() != 1) { reason = "expected-one-task-manager"; return {}; }
        auto *root = managers.first();
        if (root->applet()->pluginMetaData().pluginId() != "org.kde.plasma.icontasks" || root->applet()->location() != Plasma::Types::BottomEdge) {
            reason = "unsupported-layout"; return {};
        }
        if (!root->window() || !root->window()->isVisible() || !root->isVisible() || root->opacity() < 0.99 || root->applet()->containment()->isUserConfiguring()) {
            reason = "panel-hidden-or-editing"; return {};
        }
        auto *list = qobject_cast<QQuickItem *>(root->property("taskList").value<QObject *>());
        if (!list) { reason = "task-list-unavailable"; return {}; }
        // Avoid showing a stale number during a rearrangement animation or drag.
        if (list->property("animationsRunning").toInt() > 0 || root->property("dragSource").value<QObject *>()) {
            reason = "tasks-moving"; return {};
        }
        QList<QQuickItem *> tasks;
        for (auto *item : list->childItems()) {
            if (item->property("tasksRoot").value<QObject *>() != root || item->property("inPopup").toBool()) continue;
            if (!item->isVisible() || item->width() < 18 || item->height() < 18) { reason = "task-not-ready"; return {}; }
            tasks.append(item);
        }
        std::sort(tasks.begin(), tasks.end(), [](auto *a, auto *b) { return a->property("index").toInt() < b->property("index").toInt(); });
        for (int i=0; i<tasks.size(); ++i) {
            if (tasks[i]->property("index").toInt() != i) { reason = "ambiguous-task-order"; return {}; }
        }
        for (auto *task : tasks) {
            if (qAbs(task->y() - tasks.first()->y()) > 2) { reason = "multiple-task-rows"; return {}; }
        }
        reason = tasks.isEmpty() ? "no-tasks" : "ready";
        return tasks;
    }
    void readBindings() {
        KConfig config(QStringLiteral("kglobalshortcutsrc"), KConfig::SimpleConfig);
        const KConfigGroup group(&config, "plasmashell");
        for (int i=1;i<=9;++i) {
            const QString entry = group.readEntry(QStringLiteral("activate task manager entry %1").arg(i), QString());
            const QString current = entry.section(',',0,0);
            const auto alternatives = current.split(QChar('\t'));
            allowed[i] = std::any_of(alternatives.begin(), alternatives.end(), [i](const QString &s) {
                return QKeySequence::fromString(s) == QKeySequence(QStringLiteral("Meta+%1").arg(i));
            });
        }
    }
    void hide() {
        watchdog.stop(); refresh.stop(); visibleCount = 0;
        for (const auto &badge : badges) if (badge) badge->setVisible(false);
    }
    void draw() {
        for (const auto &badge : badges) if (badge) badge->setVisible(false);
        visibleCount = 0;
        const auto tasks = locate();
        for (auto *task : tasks) {
            const int n = task->property("index").toInt()+1;
            if (n<1 || n>9 || !allowed[n]) continue;
            Badge *badge = nullptr;
            for (const auto &existing : badges) if (existing && existing->parentItem()==task) { badge=existing; break; }
            if (!badge) {
                badge = new Badge(task);
                badges.append(badge);
            }
            badge->number = n;
            badge->setX((task->width()-badge->width())/2);
            badge->setY(0);
            badge->update(); badge->setVisible(true);
            ++visibleCount;
        }
        badges.removeIf([](const auto &p) { return p.isNull(); });
    }
    bool owner=false;
    bool allowed[10] = {};
    int visibleCount=0;
    QString reason;
    QTimer watchdog, refresh;
    QList<QPointer<Badge>> badges;
};
class BackendPlugin final : public QQmlExtensionPlugin {
    Q_OBJECT
    Q_PLUGIN_METADATA(IID QQmlExtensionInterface_iid)
public:
    void registerTypes(const char *uri) override { qmlRegisterType<Backend>(uri,1,0,"Backend"); }
};
#include "backend.moc"
