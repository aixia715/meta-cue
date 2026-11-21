#pragma once
#include <set>

// Pure state machine. No key text, logging, injection or shortcut interception.
class Gesture {
public:
    enum class Action { None, Arm, Cancel };
    Action key(unsigned code, bool meta, bool pressed) {
        if (pressed) {
            if (!down.insert(code).second) return Action::None;
            if (meta) {
                metas.insert(code);
                if (down.size() == 1 && !blocked) return Action::Arm;
            }
            blocked = true;
            return Action::Cancel;
        }
        down.erase(code);
        metas.erase(code);
        if (metas.empty()) {
            blocked = false;
            return Action::Cancel;
        }
        return Action::None;
    }
    bool eligible() const { return !blocked && metas.size() == 1 && down.size() == 1; }
    void reset() { down.clear(); metas.clear(); blocked = false; }
private:
    std::set<unsigned> down, metas;
    bool blocked = false;
};
