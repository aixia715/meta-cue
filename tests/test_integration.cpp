#include <QtTest>
#include <QDBusConnection>
#include <QDBusInterface>
#include <QDBusReply>
class Integration : public QObject {
    Q_OBJECT
    bool visible=false;
    void key(uint code,bool pressed) {
        QDBusInterface driver("org.kde.KWin","/MetaCue/Test","org.kde.MetaCue.Test");
        QVERIFY2(driver.call("Key",code,pressed).type()!=QDBusMessage::ErrorMessage,"Input driver failed");
        QCoreApplication::processEvents();
    }
private Q_SLOTS:
    void changed(bool value) { visible=value; }
    void initTestCase() {
        QDBusInterface manager("org.kde.KWin","/Plugins","org.kde.KWin.Plugins");
        QDBusReply<bool> input=manager.call("LoadPlugin","meta_cue_input");
        QVERIFY(input.isValid() && input.value());
        QDBusReply<bool> driver=manager.call("LoadPlugin","meta_cue_test_driver");
        QVERIFY(driver.isValid() && driver.value());
        QVERIFY(QDBusConnection::sessionBus().connect("org.kde.KWin","/MetaCue/Input","org.kde.MetaCue.Input","VisibleChanged",this,SLOT(changed(bool))));
    }
    void holdAndRelease() {
        QElapsedTimer clock; clock.start();
        key(125,true);
        QTest::qWait(200); QVERIFY(!visible);
        QTRY_VERIFY_WITH_TIMEOUT(visible,300);
        QVERIFY(clock.elapsed()>=280 && clock.elapsed()<550);
        key(125,false); QTRY_VERIFY_WITH_TIMEOUT(!visible,100);
    }
    void quickTap() {
        key(125,true); QTest::qWait(50); key(125,false);
        QTest::qWait(350); QVERIFY(!visible);
    }
    void otherKeyCancelsUntilNewPress() {
        key(125,true); QTRY_VERIFY_WITH_TIMEOUT(visible,500);
        key(18,true); QTRY_VERIFY_WITH_TIMEOUT(!visible,100);
        key(18,false); QTest::qWait(350); QVERIFY(!visible);
        key(125,false);
    }
    void preheldShift() {
        key(42,true); key(125,true); QTest::qWait(350); QVERIFY(!visible);
        key(42,false); QTest::qWait(350); QVERIFY(!visible); key(125,false);
    }
    void rightMeta() {
        key(126,true); QTRY_VERIFY_WITH_TIMEOUT(visible,500);
        key(126,false); QTRY_VERIFY_WITH_TIMEOUT(!visible,100);
    }
    void cleanupTestCase() {
        QDBusInterface manager("org.kde.KWin","/Plugins","org.kde.KWin.Plugins");
        manager.call("UnloadPlugin","meta_cue_input");
        manager.call("UnloadPlugin","meta_cue_test_driver");
    }
};
QTEST_GUILESS_MAIN(Integration)
#include "test_integration.moc"
