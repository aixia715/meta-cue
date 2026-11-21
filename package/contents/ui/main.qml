import QtQuick
import org.kde.plasma.plasmoid
import org.kde.plasma.core as PlasmaCore
import "backend"
PlasmoidItem {
    id: root
    implicitWidth: 0
    implicitHeight: 0
    // HiddenStatus does not suppress the desktop containment's applet frame.
    Plasmoid.backgroundHints: PlasmaCore.Types.NoBackground
    Plasmoid.status: PlasmaCore.Types.HiddenStatus
    preferredRepresentation: fullRepresentation
    fullRepresentation: Item { implicitWidth: 0; implicitHeight: 0 }
    Backend { id: controller }
}
