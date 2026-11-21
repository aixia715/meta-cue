import QtQuick
import org.kde.plasma.plasmoid
import org.kde.plasma.core as PlasmaCore
import "backend"
PlasmoidItem {
    id: root
    implicitWidth: 0
    implicitHeight: 0
    Plasmoid.status: PlasmaCore.Types.HiddenStatus
    preferredRepresentation: fullRepresentation
    fullRepresentation: Item { implicitWidth: 0; implicitHeight: 0 }
    Backend { id: controller }
}
