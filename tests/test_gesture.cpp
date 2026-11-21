#include "gesture.h"
#include <QtTest>
class TestGesture : public QObject {
    Q_OBJECT
private Q_SLOTS:
    void holdRelease() {
        Gesture s;
        QCOMPARE(s.key(125,true,true), Gesture::Action::Arm);
        QVERIFY(s.eligible());
        QCOMPARE(s.key(125,true,false), Gesture::Action::Cancel);
        QVERIFY(!s.eligible());
    }
    void chordStaysCancelled() {
        Gesture s;
        s.key(125,true,true);
        QCOMPARE(s.key(3,false,true), Gesture::Action::Cancel);
        QVERIFY(!s.eligible());
        s.key(3,false,false);
        QVERIFY(!s.eligible());
        s.key(125,true,false);
        QCOMPARE(s.key(126,true,true), Gesture::Action::Arm);
    }
    void preheldModifier() {
        Gesture s;
        s.key(42,false,true);
        QCOMPARE(s.key(125,true,true), Gesture::Action::Cancel);
        s.key(42,false,false);
        QVERIFY(!s.eligible());
        s.key(125,true,false);
        QVERIFY(!s.eligible());
    }
    void repeatedMetaDoesNotRearm() {
        Gesture s;
        s.key(125,true,true);
        QCOMPARE(s.key(125,true,true), Gesture::Action::None);
        QVERIFY(s.eligible());
    }
    void twoMetaKeysCancel() {
        Gesture s;
        s.key(125,true,true);
        QCOMPARE(s.key(126,true,true), Gesture::Action::Cancel);
        s.key(126,true,false);
        QVERIFY(!s.eligible());
        s.key(125,true,false);
        QCOMPARE(s.key(126,true,true), Gesture::Action::Arm);
    }
    void resumeNeedsFreshPress() {
        Gesture s;
        s.key(125,true,true);
        s.reset();
        QVERIFY(!s.eligible());
        s.key(125,true,false);
        QVERIFY(!s.eligible());
        QCOMPARE(s.key(125,true,true), Gesture::Action::Arm);
    }
};
QTEST_GUILESS_MAIN(TestGesture)
#include "test_gesture.moc"
