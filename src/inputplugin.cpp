#include "gesture.h"
#include <plugin.h>
#include <input.h>
#include <input_event.h>
#include <input_event_spy.h>
#include <wayland_server.h>
#include <main.h>
#include <core/session.h>
#include <KPluginFactory>
#include <KConfig>
#include <KConfigGroup>
#include <QDBusConnection>
#include <QTimer>
#include <kglobalacceld.h>

class MetaCueInput final : public KWin::Plugin, public KWin::InputEventSpy {
    Q_OBJECT
    Q_CLASSINFO("D-Bus Interface", "org.kde.MetaCue.Input")
public:
    MetaCueInput() {
        delay.setSingleShot(true);
        heartbeat.setInterval(100);
        connect(&delay, &QTimer::timeout, this, [this] {
            if (gesture.eligible() && !KWin::waylandServer()->isScreenLocked()) {
                // Suppress only this modifier-only sequence. Do not consume Meta
                // releases or alter Meta+number / other combined shortcuts.
                auto *daemon = qobject_cast<KGlobalAccelD *>(QDBusConnection::sessionBus().objectRegisteredAt("/kglobalaccel"));
                if (daemon && daemon->interface()) {
                    QMetaObject::invokeMethod(daemon->interface(), "cancelModiferOnlySequence", Qt::DirectConnection);
                }
                active = true;
                heartbeat.start();
                Q_EMIT VisibleChanged(true);
            }
        });
        connect(&heartbeat, &QTimer::timeout, this, [this] { Q_EMIT VisibleChanged(active); });
        connect(KWin::waylandServer(), &KWin::WaylandServer::lockStateChanged, this, &MetaCueInput::reset);
        connect(KWin::kwinApp()->session(), &KWin::Session::aboutToSleep, this, &MetaCueInput::reset);
        connect(KWin::kwinApp()->session(), &KWin::Session::activeChanged, this, &MetaCueInput::reset);
        QDBusConnection::sessionBus().registerObject("/MetaCue/Input", this, QDBusConnection::ExportAllSignals | QDBusConnection::ExportAllSlots);
        KWin::input()->installInputEventSpy(this);
    }
    ~MetaCueInput() override {
        cancel();
        KWin::input()->uninstallInputEventSpy(this);
        QDBusConnection::sessionBus().unregisterObject("/MetaCue/Input");
    }
    void keyboardKey(KWin::KeyboardKeyEvent *event) override {
        if (KWin::waylandServer()->isScreenLocked() || !KWin::kwinApp()->session()->isActive()) {
            reset();
            return;
        }
        if (event->state == KWin::KeyboardKeyState::Repeated) return;
        const bool meta = event->key == Qt::Key_Meta || event->key == Qt::Key_Super_L || event->key == Qt::Key_Super_R;
        const auto action = gesture.key(event->nativeScanCode, meta, event->state == KWin::KeyboardKeyState::Pressed);
        if (action == Gesture::Action::Cancel) cancel();
        else if (action == Gesture::Action::Arm) {
            KConfig config(QStringLiteral("meta-cuerc"), KConfig::SimpleConfig);
            const KConfigGroup general(&config, "General");
            if (general.readEntry("Enabled", true)) delay.start(qBound(100, general.readEntry("DelayMs", 300), 2000));
        }
    }
public Q_SLOTS:
    QString Version() const { return QStringLiteral("0.1.2 / KWin " KWIN_PLUGIN_VERSION_STRING); }
Q_SIGNALS:
    void VisibleChanged(bool visible);
private:
    void cancel() { delay.stop(); heartbeat.stop(); active = false; Q_EMIT VisibleChanged(false); }
    void reset() { cancel(); gesture.reset(); }
    Gesture gesture;
    QTimer delay, heartbeat;
    bool active = false;
};
class MetaCueFactory final : public KWin::PluginFactory {
    Q_OBJECT
    Q_PLUGIN_METADATA(IID PluginFactory_iid FILE "inputplugin.json")
    Q_INTERFACES(KWin::PluginFactory)
public:
    std::unique_ptr<KWin::Plugin> create() const override {
        if (!KWin::waylandServer() || !KWin::input() || !KWin::kwinApp()->session()) return {};
        return std::make_unique<MetaCueInput>();
    }
};
#include "inputplugin.moc"
