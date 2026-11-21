#include <QtTest>
#include <QDBusConnection>
#include <QDBusInterface>
#include <QDBusReply>
#include <QAction>
#include <KGlobalAccel>
class Integration : public QObject {
    Q_OBJECT
    bool visible=false;
    QAction menu{this}, cycle{this};
    int menuCount=0, cycleCount=0;
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
        menu.setObjectName("test-menu"); menu.setProperty("componentName","meta-cue-test");
        cycle.setObjectName("test-cycle"); cycle.setProperty("componentName","meta-cue-test");
        connect(&menu,&QAction::triggered,this,[this]{++menuCount;});
        connect(&cycle,&QAction::triggered,this,[this]{++cycleCount;});
        QVERIFY(KGlobalAccel::self()->setShortcut(&menu,{QKeySequence(Qt::Key_Meta)},KGlobalAccel::NoAutoloading));
        KGlobalAccel::stealShortcutSystemwide(QKeySequence(Qt::META | Qt::Key_QuoteLeft));
        QVERIFY(KGlobalAccel::self()->setShortcut(&cycle,{QKeySequence(Qt::META | Qt::Key_QuoteLeft)},KGlobalAccel::NoAutoloading));
        QTest::qWait(100);
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
        key(194,true); QTRY_VERIFY_WITH_TIMEOUT(!visible,100);
        key(194,false); QTest::qWait(350); QVERIFY(!visible);
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
    void plainMetaStillOpensMenu() {
        const int before=menuCount;
        key(125,true); key(125,false);
        QTRY_COMPARE_WITH_TIMEOUT(menuCount,before+1,500);
    }
    void triggeredHoldSuppressesMenu_data() {
        QTest::addColumn<uint>("meta");
        QTest::newRow("left-meta") << uint(125);
        QTest::newRow("right-meta") << uint(126);
    }
    void triggeredHoldSuppressesMenu() {
        QFETCH(uint,meta);
        const int before=menuCount;
        key(meta,true); QTRY_VERIFY_WITH_TIMEOUT(visible,500);
        key(meta,false); QTRY_VERIFY_WITH_TIMEOUT(!visible,100);
        QTest::qWait(150); QCOMPARE(menuCount,before);
        // Suppression must not leak into the following short tap.
        key(meta,true); key(meta,false);
        QTRY_COMPARE_WITH_TIMEOUT(menuCount,before+1,500);
    }
    void backtickKeepsOriginalShortcut() {
        const int menus=menuCount, cycles=cycleCount;
        key(125,true); QTRY_VERIFY_WITH_TIMEOUT(visible,500);
        key(41,true); key(41,false); key(125,false);
        QTRY_VERIFY_WITH_TIMEOUT(!visible,100);
        QTRY_COMPARE_WITH_TIMEOUT(cycleCount,cycles+1,500);
        QTest::qWait(100); QCOMPARE(menuCount,menus);
    }
    void unloadingRestoresShortcutHandling() {
        QDBusInterface manager("org.kde.KWin","/Plugins","org.kde.KWin.Plugins");
        manager.call("UnloadPlugin","meta_cue_input");
        const int before=menuCount;
        key(125,true); QTest::qWait(350); key(125,false);
        QTRY_COMPARE_WITH_TIMEOUT(menuCount,before+1,500);
    }
    void cleanupTestCase() {
        KGlobalAccel::self()->removeAllShortcuts(&menu);
        KGlobalAccel::self()->removeAllShortcuts(&cycle);
        QDBusInterface manager("org.kde.KWin","/Plugins","org.kde.KWin.Plugins");
        manager.call("UnloadPlugin","meta_cue_input");
        manager.call("UnloadPlugin","meta_cue_test_driver");
    }
};
QTEST_MAIN(Integration)
#include "test_integration.moc"
