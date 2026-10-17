import type { ModRegistrar } from "cs2/modding";
import { RoadPanel } from "./road-panel";
import { GraphicsPanel } from "./graphics-panel";
// import { registerSelectedInfoFontFix } from "./selected-info-font-fix";

const register: ModRegistrar = registry => {
    // registerSelectedInfoFontFix(registry);
    let panelClass = "";
    try {
        panelClass = (registry as any).registry.get(
            "game-ui/game/components/tool-options/tool-options-panel.module.scss"
        )?.classes?.toolOptionsPanel ?? "";
    } catch (error) {
        console.warn("[Enhanced Graphics and Textures] Game panel theme unavailable", error);
    }
    registry.append("GameTopLeft", () => <><RoadPanel panelClass={panelClass} /><GraphicsPanel panelClass={panelClass} /></>);
};

export default register;




