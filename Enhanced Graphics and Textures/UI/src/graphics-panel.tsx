import React from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { Tooltip } from "cs2/ui";
import { RoadSlider, SliderControl } from "./road-panel";
import styles from "./road-panel.module.scss";

const group = "Advanced_Graphics";
const state$ = bindValue<string>(group, "graphicsSettings", "{}");
type GraphicKey = "WearHidden" | "WearEnabled" | "WearOpacity" | "WearDepth" | "WearGloss" | "WearColor" | "TreeLodDistance" | "GlobalDetailScalePercent" | "TerrainTessellationLevel" |
    "TerrainPatchSizeUnits" | "ShadowResolution" | "ShadowDistanceMeters" | "SmallShadowCasterThreshold" |
    "TaaSharpenStrength" | "TaaStabilityEnabled" | "TaaStabilityStrength" | "BloomMode" | "BloomIntensity" | "SunBloomStrength" | "SkyExposure";
type State = Record<GraphicKey, number> & { WearMatches: number; show: boolean; x: number; y: number; DefaultShadowResolution: number; GameTessellation: number; GamePatchSize: number };
type Tab = "RoadWear" | "Details" | "Terrain" | "Shadows" | "Lighting" | "AntiAliasing";
const tabs: Tab[] = ["Details", "Terrain", "Shadows", "AntiAliasing", "Lighting", "RoadWear"];
const initial: State = { WearHidden: 0, WearEnabled: 0, WearMatches: -1, WearOpacity: 100, WearDepth: 100, WearGloss: -1, WearColor: 16777215, show: false, x: -1, y: -1, TreeLodDistance: 100, GlobalDetailScalePercent: 100,
    TerrainTessellationLevel: 0, TerrainPatchSizeUnits: 0, ShadowResolution: 0, DefaultShadowResolution: 0, GameTessellation: 0, GamePatchSize: 0, ShadowDistanceMeters: 0,
    SmallShadowCasterThreshold: 0, TaaSharpenStrength: 0, TaaStabilityEnabled: 0, TaaStabilityStrength: 0.5, BloomMode: 0, BloomIntensity: 0.2,
    SunBloomStrength: 1, SkyExposure: 0 };
const controls: (SliderControl & { key: GraphicKey; tab: Tab })[] = [
    { key: "WearOpacity", label: "WearOpacity", tab: "RoadWear", min: 0, max: 300, step: 1 },
    { key: "WearDepth", label: "WearDepth", tab: "RoadWear", min: 0, max: 1000, step: 1 },
    { key: "WearGloss", label: "WearGloss", tab: "RoadWear", min: -1, max: 100, step: 1 },
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
    const formatColor = (rgb: number) => "#" + rgb.toString(16).padStart(6, "0").toUpperCase();
    const [chooseColor, setChooseColor] = React.useState(false);
    const colorLabel = (key: string, fallback: string) => translate(`AdvancedGraphics.UI.${key}`, fallback) ?? fallback;
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
                    {tabs.map(key => {
                        const label = text(key, key);
                        const tabClass = [tab === key && styles.selectedTab, label.length > 9 && styles.longLabel].filter(Boolean).join(" ");
                        return <button key={key} className={tabClass}
                            onClick={() => { flush(); setTab(key); }}>{label}</button>;
                    })}
                </div>
                {tab === "RoadWear" && <>
                    <div className={styles.source}>
                        {["GameDefault", "WearEnabled"].map((key, mode) =>
                            <button type="button" key={key} aria-pressed={draft.WearEnabled === mode}
                                className={draft.WearEnabled === mode ? styles.selected : ""}
                                onClick={() => { set("WearEnabled", mode); flush(); }}>
                                {text(key, mode === 0 ? "Game default" : "Custom settings")}
                            </button>)}
                    </div>
                    {draft.WearEnabled !== 0 && <>
                    <div className={styles.source}>
                        <button type="button" className={styles.moduleToggle} role="checkbox" aria-checked={draft.WearHidden !== 0}
                            onClick={() => { set("WearHidden", draft.WearHidden ? 0 : 1); flush(); }}>
                            <span aria-hidden="true" className={`${styles.toggleBox} ${draft.WearHidden ? styles.toggleOn : ""}`}>
                                {draft.WearHidden ? "✓" : ""}
                            </span>
                            {text("WearHidden", "Hide road wear")}
                        </button>
                    </div>
                    <div className={styles.source}>
                        <button type="button" className={`${styles.reset} ${styles.wearColorButton}`} aria-expanded={chooseColor}
                            onClick={() => setChooseColor(previous => !previous)}>
                            <span className={styles.colorSwatch} style={{ backgroundColor: formatColor(draft.WearColor) }} />
                            {colorLabel("ChooseColor", "Choose color")}
                        </button>
                        <button className={styles.reset} title={text("Reset", "Reset")} onClick={() => reset("WearColor")}>↺</button>
                    </div>
                    {chooseColor && <>
                        <div className={styles.colorPresets} aria-label={colorLabel("ColorPresets", "Presets")}>
                            {[0x191918, 0x30302e, 0x454542, 0x5d5c58, 0x797872, 0x96948c,
                                0x483e34, 0x615242, 0x786754, 0x93816b, 0xac9b84, 0xc4b7a3].map(rgb =>
                                <button key={rgb} type="button" aria-label={formatColor(rgb)} aria-pressed={draft.WearColor === rgb}
                                    style={{ backgroundColor: formatColor(rgb) }} onClick={() => { set("WearColor", rgb); flush(); }} />)}
                        </div>
                        <div className={styles.wearChannels}>
                            {(["Red", "Green", "Blue"] as const).map((channel, index) => {
                                const shift = (2 - index) * 8;
                                const label = colorLabel("Color" + channel, channel);
                                return <RoadSlider key={channel} control={{ key: channel, label, min: 0, max: 255, step: 1 }}
                                    label={label} description={label} value={(draft.WearColor >> shift) & 255} onCommit={flush}
                                    onChange={v => set("WearColor", (draft.WearColor & ~(255 << shift)) | (v << shift))} />;
                            })}
                            <span aria-hidden="true" className={styles.wearColorPreview} style={{ backgroundColor: formatColor(draft.WearColor) }} />
                        </div>
                    </>}
                    {value.WearMatches >= 0 && <p className={styles.textureHint}>{text("WearMaterials", "Matched lane materials")} · {value.WearMatches}</p>}
                    </>}
                </>}
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
                {controls.filter(control => control.tab === tab && (tab !== "RoadWear" || draft.WearEnabled !== 0)).map(control => <div key={control.key} className={styles.graphicsControl}>
                    <RoadSlider control={control} label={text(control.key, control.label) + (tab === "RoadWear" ? " (%)" : "")}
                        description={text(`Tip${control.key}`, control.label) + (control.key === "WearGloss" ? " (−1 = " + text("GameDefault", "Game default") + ")" : "")} value={draft[control.key]}
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
