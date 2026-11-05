import React from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { Tooltip } from "cs2/ui";
import { RoadSlider, SliderControl } from "./road-panel";
import styles from "./road-panel.module.scss";

const group = "Advanced_Graphics";
const state$ = bindValue<string>(group, "graphicsSettings", "{}");
type GraphicKey = "TreeLodDistance" | "GlobalDetailScalePercent" | "TerrainTessellationLevel" |
    "TerrainPatchSizeUnits" | "ShadowResolution" | "ShadowDistanceMeters" | "SmallShadowCasterThreshold" |
    "TaaSharpenStrength" | "TaaStabilityEnabled" | "TaaStabilityStrength" | "BloomMode" | "BloomIntensity" | "SunBloomStrength" | "SkyExposure";
type State = Record<GraphicKey, number> & { show: boolean; x: number; y: number; DefaultShadowResolution: number; GameTessellation: number; GamePatchSize: number };
type Tab = "Details" | "Terrain" | "Shadows" | "Lighting" | "AntiAliasing";
const tabs: Tab[] = ["Details", "Terrain", "Shadows", "AntiAliasing", "Lighting"];
const initial: State = { show: false, x: -1, y: -1, TreeLodDistance: 100, GlobalDetailScalePercent: 100,
    TerrainTessellationLevel: 0, TerrainPatchSizeUnits: 0, ShadowResolution: 0, DefaultShadowResolution: 0, GameTessellation: 0, GamePatchSize: 0, ShadowDistanceMeters: 0,
    SmallShadowCasterThreshold: 0, TaaSharpenStrength: 0, TaaStabilityEnabled: 0, TaaStabilityStrength: 0.5, BloomMode: 0, BloomIntensity: 0.2,
    SunBloomStrength: 1, SkyExposure: 0 };
const controls: (SliderControl & { key: GraphicKey; tab: Tab })[] = [
    { key: "TaaStabilityStrength", label: "TaaStabilityStrength", tab: "AntiAliasing", min: 0, max: 1, step: 0.01 },
    { key: "TreeLodDistance", label: "TreeLodDistance", tab: "Details", min: 10, max: 500, step: 10 },
    { key: "GlobalDetailScalePercent", label: "GlobalDetailScalePercent", tab: "Details", min: 10, max: 500, step: 10 },
    { key: "TaaSharpenStrength", label: "TaaSharpenStrength", tab: "AntiAliasing", min: 0, max: 2, step: 0.05 },
    { key: "TerrainTessellationLevel", label: "TerrainTessellationLevel", tab: "Terrain", min: 0, max: 16, step: 1 },
    { key: "TerrainPatchSizeUnits", label: "TerrainPatchSizeUnits", tab: "Terrain", min: 0, max: 128, step: 1 },
    { key: "ShadowResolution", label: "ShadowResolution", tab: "Shadows", min: 0, max: 8192, step: 512 },
    { key: "ShadowDistanceMeters", label: "ShadowDistanceMeters", tab: "Shadows", min: 0, max: 2000, step: 50 },
    { key: "SmallShadowCasterThreshold", label: "SmallShadowCasterThreshold", tab: "Shadows", min: 0, max: 10, step: 1 },
    { key: "BloomIntensity", label: "BloomIntensity", tab: "Lighting", min: 0, max: 1, step: 0.01 },
    { key: "SunBloomStrength", label: "SunBloomStrength", tab: "Lighting", min: 0, max: 5, step: 0.05 },
    { key: "SkyExposure", label: "SkyExposure", tab: "Lighting", min: -5, max: 5, step: 0.05 },
];

export function GraphicsPanel({ panelClass }: { panelClass: string }) {
    const raw = useValue(state$);
    const value = React.useMemo(() => {
        try { return { ...initial, ...JSON.parse(raw) } as State; }
        catch { return initial; }
    }, [raw]);
    const { translate } = useLocalization();
    const text = (key: string, fallback: string) => translate(`AdvancedGraphics.UI.Graphics.${key}`, fallback) ?? fallback;
    const [open, setOpen] = React.useState(false);
    const [tab, setTab] = React.useState<Tab>("Details");
    const [draft, setDraft] = React.useState<State>(initial);
    const edits = React.useRef<Partial<Record<GraphicKey, number>>>({});
    const pending = React.useRef<{ key: GraphicKey; value: number } | null>(null);
    const timer = React.useRef<ReturnType<typeof setTimeout> | null>(null);
    const flush = React.useCallback(() => {
        if (timer.current !== null) clearTimeout(timer.current);
        timer.current = null;
        if (pending.current) {
            const change = pending.current;
            pending.current = null;
            trigger(group, "setGraphicsSetting", `${change.key}:${change.value}`);
        }
    }, []);
    React.useEffect(() => {
        for (const key of Object.keys(edits.current) as GraphicKey[]) {
            if (Math.abs(value[key] - edits.current[key]!) < 0.0001) delete edits.current[key];
        }
        setDraft({ ...value, ...edits.current });
    }, [value]);
    React.useEffect(() => {
        if (!value.show) { flush(); setOpen(false); }
    }, [value.show, flush]);
    React.useEffect(() => () => flush(), [flush]);

    const set = (key: GraphicKey, next: number) => {
        if (pending.current && pending.current.key !== key) flush();
        edits.current[key] = next;
        setDraft(previous => ({ ...previous, [key]: next }));
        pending.current = { key, value: next };
        // Live preview at a bounded rate, with the exact final value on mouse-up.
        if (timer.current === null) timer.current = setTimeout(flush, 80);
    };
    const reset = (key: GraphicKey | "all") => {
        flush();
        if (key === "all") edits.current = {};
        else {
            delete edits.current[key];
            if (key === "BloomMode" || key === "BloomIntensity") {
                delete edits.current.BloomMode;
                delete edits.current.BloomIntensity;
            }
        }
        trigger(group, "setGraphicsSetting", `reset:${key}`);
    };
    const panel = React.useRef<HTMLDivElement>(null);
    const [position, setPosition] = React.useState({ x: 540, y: 100 });
    const positionRef = React.useRef(position);
    const drag = React.useRef<{ x: number; y: number; left: number; top: number } | null>(null);
    const clampPosition = (next: { x: number; y: number }) => {
        const rect = panel.current?.getBoundingClientRect();
        return {
            x: Math.max(8, Math.min(Math.max(8, window.innerWidth - (rect?.width || 480) - 8), next.x)),
            y: Math.max(8, Math.min(Math.max(8, window.innerHeight - (rect?.height || 340) - 8), next.y)),
        };
    };
    const savePosition = () => trigger(group, "setGraphicsWindowPosition",
        `${positionRef.current.x.toFixed(1)}:${positionRef.current.y.toFixed(1)}`);
    const wasOpen = React.useRef(false);
    React.useLayoutEffect(() => {
        if (!open) { wasOpen.current = false; return; }
        const saved = Number.isFinite(value.x) && Number.isFinite(value.y) && value.x >= 0 && value.y >= 0;
        const next = clampPosition(saved && !wasOpen.current ? { x: value.x, y: value.y } : positionRef.current);
        wasOpen.current = true;
        positionRef.current = next;
        setPosition(next);
    }, [open, tab]);
    React.useEffect(() => {
        const move = (event: MouseEvent) => {
            if (!drag.current) return;
            const next = clampPosition({ x: drag.current.left + event.clientX - drag.current.x,
                y: drag.current.top + event.clientY - drag.current.y });
            positionRef.current = next;
            setPosition(next);
        };
        const up = () => {
            if (!drag.current) return;
            drag.current = null;
            savePosition();
        };
        const resize = () => {
            const next = clampPosition(positionRef.current);
            positionRef.current = next;
            setPosition(next);
        };
        window.addEventListener("mousemove", move);
        window.addEventListener("mouseup", up);
        window.addEventListener("resize", resize);
        return () => {
            window.removeEventListener("mousemove", move);
            window.removeEventListener("mouseup", up);
            window.removeEventListener("resize", resize);
            if (drag.current) savePosition();
        };
    }, []);

    if (!value.show) return null;
    const title = text("Title", "Graphics");
    const launcherTitle = text("Open", "Enhanced Graphics and Textures – open graphics editor");
    return <>
        <Tooltip tooltip={launcherTitle} direction="down">
            <button className={`${styles.launcher} ${open ? styles.active : ""}`} aria-label={launcherTitle}
                onClick={() => { flush(); setOpen(previous => !previous); }}>
                <svg viewBox="0 0 28 28" width="25" height="25" aria-hidden="true">
                    <rect x="3" y="4" width="22" height="16" rx="2" fill="none" stroke="currentColor" strokeWidth="2" />
                    <path d="M9 25h10m-5-5v5M6 16l5-5 4 4 4-6 3 7" fill="none" stroke="currentColor" strokeWidth="2" />
                    <circle cx="9" cy="8" r="1.5" fill="currentColor" />
                </svg>
            </button>
        </Tooltip>
        {open && <div ref={panel} className={`${panelClass} ${styles.panel} ${styles.graphicsPanel}`}
            style={{ left: `${position.x}px`, top: `${position.y}px` }}>
            <div className={styles.header} onMouseDown={event => {
                if (event.button !== 0 || (event.target as HTMLElement).closest("button")) return;
                event.preventDefault();
                drag.current = { x: event.clientX, y: event.clientY, left: position.x, top: position.y };
            }}>
                <strong>{title}</strong>
                <button className={styles.close} title={text("Close", "Close")} aria-label={text("Close", "Close")}
                    onClick={() => { flush(); setOpen(false); }}>×</button>
            </div>
            <div className={styles.body}>
                <div className={styles.surfaceTabs}>
                    {tabs.map(key => <button key={key} className={tab === key ? styles.selectedTab : ""}
                        onClick={() => { flush(); setTab(key); }}>{text(key, key)}</button>)}
                </div>
                {tab === "Terrain" && <p className={styles.textureHint}>
                    {text("TerrainGameValue", "0 = game value")}: {value.GameTessellation > 0 ? `${value.GameTessellation} / ${value.GamePatchSize}` : "–"}
                </p>}
                {tab === "Shadows" && <p className={styles.textureHint}>
                    {text("ShadowGameValue", "Default for shadow quality")}: {value.DefaultShadowResolution > 0 ? value.DefaultShadowResolution : "–"}
                    {draft.ShadowResolution === 0 ? ` (${text("GameDefault", "Game default")})` : ""}
                </p>}
                {tab === "AntiAliasing" && <div className={styles.source}>
                    <Tooltip tooltip={text("TipTaaStabilityEnabled", "Enable TAA anti-flicker override")}>
                        <button className={styles.moduleToggle} role="checkbox" aria-checked={draft.TaaStabilityEnabled !== 0}
                            aria-label={text("TaaStabilityEnabled", "Adjust TAA stability")}
                            onClick={() => { set("TaaStabilityEnabled", draft.TaaStabilityEnabled ? 0 : 1); flush(); }}>
                            <span aria-hidden="true" className={`${styles.toggleBox} ${draft.TaaStabilityEnabled ? styles.toggleOn : ""}`}>
                                {draft.TaaStabilityEnabled ? "✓" : ""}
                            </span>
                            {text("TaaStabilityEnabled", "Adjust TAA stability")}
                        </button>
                    </Tooltip>
                    <Tooltip tooltip={text("Reset", "Reset")}><button className={`${styles.reset} ${styles.graphicsReset}`}
                        aria-label={`${text("Reset", "Reset")} ${text("TaaStabilityEnabled", "Adjust TAA stability")}`}
                        onClick={() => reset("TaaStabilityEnabled")}>↺</button></Tooltip>
                </div>}
                {tab === "Lighting" && <div className={styles.source}>
                    <Tooltip tooltip={text("TipBloomMode", "Control Bloom")}><span>{text("BloomMode", "Bloom")}</span></Tooltip>
                    {["GameDefault", "On", "Off"].map((key, index) => <button key={key}
                        className={draft.BloomMode === index ? styles.selected : ""} aria-pressed={draft.BloomMode === index}
                        onClick={() => { set("BloomMode", index); flush(); }}>{text(key, key)}</button>)}
                    <Tooltip tooltip={text("Reset", "Reset")}><button className={`${styles.reset} ${styles.graphicsReset}`}
                        aria-label={`${text("Reset", "Reset")} ${text("BloomMode", "Bloom")}`}
                        onClick={() => reset("BloomMode")}>↺</button></Tooltip>
                </div>}
                {controls.filter(control => control.tab === tab).map(control => <div key={control.key} className={styles.graphicsControl}>
                    <RoadSlider control={control} label={text(control.key, control.label)}
                        description={text(`Tip${control.key}`, control.label)} value={draft[control.key]}
                        onChange={next => set(control.key, next)} onCommit={flush} />
                    <Tooltip tooltip={`${text("Reset", "Reset")} – ${text(control.key, control.label)}`}>
                        <button className={`${styles.reset} ${styles.graphicsReset}`}
                            aria-label={`${text("Reset", "Reset")} ${text(control.key, control.label)}`}
                            onClick={() => reset(control.key)}>↺</button>
                    </Tooltip>
                </div>)}
                <div className={styles.separator} />
                <Tooltip tooltip={text("TipResetAll", "Restore the graphics values in this window.")}>
                    <button className={styles.reset} onClick={() => reset("all")}>{text("ResetAll", "Reset graphics")}</button>
                </Tooltip>
            </div>
        </div>}
    </>;
}
