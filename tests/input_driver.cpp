// Test-only driver. Loaded exclusively in an isolated virtual KWin session.
#include <plugin.h>
#include <input.h>
#include <keyboard_input.h>
#include <core/inputdevice.h>
#include <QDBusConnection>
class Driver : public KWin::Plugin {
    Q_OBJECT
    Q_CLASSINFO("D-Bus Interface", "org.kde.MetaCue.Test")
public:
    Driver() { QDBusConnection::sessionBus().registerObject("/MetaCue/Test",this,QDBusConnection::ExportAllSlots); }
public Q_SLOTS:
    void Key(uint scan, bool pressed) {
        KWin::input()->keyboard()->processKey(scan, pressed ? KWin::KeyboardKeyState::Pressed : KWin::KeyboardKeyState::Released,
            std::chrono::duration_cast<std::chrono::microseconds>(std::chrono::steady_clock::now().time_since_epoch()));
    }
};
class DriverFactory : public KWin::PluginFactory {
    Q_OBJECT
    Q_PLUGIN_METADATA(IID PluginFactory_iid FILE "input_driver.json")
    Q_INTERFACES(KWin::PluginFactory)
public:
    std::unique_ptr<KWin::Plugin> create() const override { return std::make_unique<Driver>(); }
};
#include "input_driver.moc"
