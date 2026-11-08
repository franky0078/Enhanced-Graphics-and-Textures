import React from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { Scrollable, Tooltip } from "cs2/ui";
import styles from "./road-panel.module.scss";

const group = "Advanced_Graphics";
const normalMapFlags$ = bindValue<string>(group, "normalMapFlags", "0,0,0,0,0,0,0");
const state$ = bindValue<string>(group, "roadSettings", "{}");
const grassState$ = bindValue<string>(group, "grassSettings", "{}");
const footpathState$ = bindValue<string>(group, "footpathSettings", "{}");
const sidewalkState$ = bindValue<string>(group, "sidewalkSettings", "{}");
const alleySidewalkState$ = bindValue<string>(group, "alleySidewalkSettings", "{}");
const railState$ = bindValue<string>(group, "railSettings", "{}");
const gravelState$ = bindValue<string>(group, "gravelSettings", "{}");
const colorWindowPosition$ = bindValue<string>(group, "colorWindowPosition", "{}");
const textureGeneralWindowPosition$ = bindValue<string>(group, "textureGeneralWindowPosition", "{}");
const shoulderState$ = bindValue<string>(group, "shoulderSettings", "{}");
type Surface = "road" | "sidewalk" | "alley" | "rail" | "gravel" | "shoulder" | "grass" | "footpath";
const triggers: Record<Surface, string> = { footpath: "setFootpathSetting", grass: "setGrassSetting", road: "setRoadSetting", sidewalk: "setSidewalkSetting", alley: "setAlleySidewalkSetting", rail: "setRailSetting", gravel: "setGravelSetting", shoulder: "setShoulderSetting" };
const prefixes: Record<Surface, string> = { footpath: "Footpath", grass: "Grass", road: "", sidewalk: "Sidewalk", alley: "AlleySidewalk", rail: "RailGravel", gravel: "RoadGravel", shoulder: "GravelShoulder" };
type Key = "largeEndcapU" | "largeEndcapV" | "roadEndU" | "roadEndV" | "alleyEndcapU" | "alleyEndcapV" | "parkingEndcapU" | "parkingEndcapV" | "endcapU" | "endcapV" | "detail" | "uv" | "depth" | "tiling" | "parkingScale" | "parkingDepth" | "parkingSidewalkU" | "parkingSidewalkV" | "shiftU" | "shiftV" | "mipBias" | "offsetX" | "offsetY" |
    "rotation" | "cracks" | "crackSize" | "crackWidth" | "crackOpacity" | "crackDepth" | "crackRotation" | "crackOffsetX" | "crackOffsetY" | "randomness" |
    "crossingMode" | "crossingDepth" | "crossingGloss" | "crossingUv" | "crossingOffsetX" | "crossingOffsetY" | "crossingRotation";
type State = Record<Key, number> & { show: boolean; enabled: boolean; filterColor: number; filterOpacity: number; crossingColor: number; crossingOpacity: number; probe?: boolean; customCracks?: boolean; crackStatus?: number };
const initial: State = { show: true, enabled: false, filterColor: 0xffffff, filterOpacity: 100, detail: 0, uv: 100, depth: 0,
    crossingMode: 1, crossingDepth: 100, crossingGloss: 10, crossingUv: 100, crossingOffsetX: 0, crossingOffsetY: 0, crossingRotation: 0, crossingColor: 0xffffff, crossingOpacity: 100,
    largeEndcapU: 1, largeEndcapV: 1, roadEndU: 1, roadEndV: 1, alleyEndcapU: 1, alleyEndcapV: 1, parkingEndcapU: 1, parkingEndcapV: 1, endcapU: 1, endcapV: 1, tiling: 1, parkingScale: 1, parkingDepth: 1, parkingSidewalkU: 1, parkingSidewalkV: 1, shiftU: 0, shiftV: 0, mipBias: 0, offsetX: 0, offsetY: 0, rotation: 0, cracks: 0, crackSize: 100, crackWidth: 100, crackOpacity: 100, crackDepth: 100, crackRotation: 0, crackOffsetX: 0, crackOffsetY: 0, randomness: 50 };

const controls: { key: Key; label: string; min: number; max: number; step: number }[] = [
    { key: "detail", label: "Detail", min: 0, max: 2, step: 1 },
    { key: "uv", label: "Uv", min: 50, max: 3000, step: 25 },
    { key: "parkingScale", label: "ParkingScale", min: 0.1, max: 8, step: 0.05 },
    { key: "depth", label: "Depth", min: 0, max: 3000, step: 25 },
    { key: "parkingDepth", label: "ParkingDepth", min: 0, max: 8, step: 0.1 },
    { key: "offsetX", label: "OffsetX", min: 0, max: 300, step: 5 },
    { key: "offsetY", label: "OffsetY", min: 0, max: 300, step: 5 },
    { key: "rotation", label: "Rotation", min: 0, max: 360, step: 1 },
    { key: "cracks", label: "Cracks", min: 0, max: 400, step: 10 },
    { key: "crackSize", label: "CrackSize", min: 25, max: 600, step: 25 },
    { key: "crackWidth", label: "CrackWidth", min: 25, max: 400, step: 25 },
    { key: "crackOffsetX", label: "CrackOffsetX", min: 0, max: 300, step: 1 },
    { key: "crackOffsetY", label: "CrackOffsetY", min: 0, max: 300, step: 1 },
    { key: "crackRotation", label: "CrackRotation", min: 0, max: 360, step: 1 },
    { key: "crackDepth", label: "CrackDepth", min: 0, max: 3000, step: 25 },
    { key: "crackOpacity", label: "CrackOpacity", min: 0, max: 200, step: 10 },
    { key: "randomness", label: "Randomness", min: 0, max: 100, step: 10 },
    { key: "tiling", label: "Tiling", min: 0.1, max: 8, step: 0.05 },
    { key: "parkingSidewalkU", label: "ParkingSidewalkU", min: 0.1, max: 8, step: 0.05 },
    { key: "parkingSidewalkV", label: "ParkingSidewalkV", min: 0.1, max: 8, step: 0.05 },
    { key: "shiftU", label: "ShiftU", min: 0, max: 100, step: 1 },
    { key: "shiftV", label: "ShiftV", min: 0, max: 100, step: 1 },
];

const crossingControls: { key: Key; label: string; min: number; max: number; step: number }[] = [
    { key: "crossingDepth", label: "Depth", min: 0, max: 3000, step: 25 },
    { key: "crossingGloss", label: "RailCrossingGloss", min: 0, max: 100, step: 1 },
    { key: "crossingUv", label: "Uv", min: 10, max: 3000, step: 5 },
    { key: "crossingOffsetX", label: "OffsetX", min: 0, max: 300, step: 5 },
    { key: "crossingOffsetY", label: "OffsetY", min: 0, max: 300, step: 5 },
    { key: "crossingRotation", label: "Rotation", min: 0, max: 360, step: 1 },
];

export type SliderControl = { key: string; label: string; min: number; max: number; step: number };
type Control = SliderControl;

export function RoadSlider({ control, label, description, value, displayValue, onChange, onCommit }: {
    control: Control; label: string; description: string; value: number; displayValue?: string;
    onChange: (value: number, dragging?: boolean) => void; onCommit: () => void;
}) {
    const precision = Math.max(0, -Math.floor(Math.log10(control.step)));
    const track = React.useRef<HTMLDivElement>(null);
    const dragging = React.useRef(false);
    const onChangeRef = React.useRef(onChange);
    const onCommitRef = React.useRef(onCommit);
    onChangeRef.current = onChange;
    onCommitRef.current = onCommit;
    const [numberText, setNumberText] = React.useState(String(value));
    const numberFocused = React.useRef(false);
    const numberCancelled = React.useRef(false);
    React.useEffect(() => { if (!numberFocused.current) setNumberText(String(value)); }, [value]);
    const update = React.useCallback((clientX: number) => {
        const rect = track.current?.getBoundingClientRect();
        if (!rect || rect.width <= 0) return;
        // Keep the entire thumb inside the interactive track at both ends.
        const inset = 11 * (rect.height / 31);
        const fraction = Math.max(0, Math.min(1, (clientX - rect.left - inset) / Math.max(1, rect.width - inset * 2)));
        const steps = Math.round(fraction * (control.max - control.min) / control.step);
        onChangeRef.current(Number(Math.min(control.max, control.min + steps * control.step).toFixed(precision)), true);
    }, [control.min, control.max, control.step, precision]);
    const mouseMove = React.useCallback((event: MouseEvent) => {
        if (!dragging.current) return;
        event.preventDefault();
        update(event.clientX);
    }, [update]);
    const mouseUp = React.useCallback(() => {
        if (!dragging.current) return;
        dragging.current = false;
        window.removeEventListener("mousemove", mouseMove);
        window.removeEventListener("mouseup", mouseUp);
        onCommitRef.current();
    }, [mouseMove]);
    React.useEffect(() => () => {
        window.removeEventListener("mousemove", mouseMove);
        window.removeEventListener("mouseup", mouseUp);
    }, [mouseMove, mouseUp]);
    const commitNumber = () => {
        numberFocused.current = false;
        if (numberCancelled.current) { numberCancelled.current = false; setNumberText(String(value)); return; }
        const parsed = Number(numberText.trim().replace(",", "."));
        if (!numberText.trim() || !Number.isFinite(parsed)) { setNumberText(String(value)); return; }
        const clamped = Math.max(control.min, Math.min(control.max, parsed));
        const next = Number(clamped.toFixed(precision));
        setNumberText(String(next));
        onChange(next);
        onCommit();
    };
    const percent = 100 * (value - control.min) / (control.max - control.min);
    return <div className={styles.row}>
        <Tooltip tooltip={<><strong>{label}</strong><div>{description}</div></>}>
            <span className={styles.rowLabel}>{label}</span>
        </Tooltip>
        <div ref={track} className={styles.slider} role="slider" tabIndex={0}
            aria-label={label} aria-valuemin={control.min} aria-valuemax={control.max} aria-valuenow={value} aria-valuetext={displayValue}
            onMouseDown={event => {
                if (event.button !== 0) return;
                event.preventDefault(); event.stopPropagation();
                dragging.current = true;
                window.addEventListener("mousemove", mouseMove);
                window.addEventListener("mouseup", mouseUp);
                update(event.clientX);
            }}
            onKeyDown={event => {
                let next = value;
                if (event.key === "ArrowLeft" || event.key === "ArrowDown") next -= control.step;
                else if (event.key === "ArrowRight" || event.key === "ArrowUp") next += control.step;
                else if (event.key === "Home") next = control.min;
                else if (event.key === "End") next = control.max;
                else return;
                event.preventDefault();
                onChange(Number(Math.max(control.min, Math.min(control.max, next)).toFixed(precision)));
            }}>
            <div className={styles.sliderTrack}>
                <div className={styles.sliderFill} style={{ width: `${percent}%` }} />
                <div className={styles.sliderThumb} style={{ left: `${percent}%` }} />
            </div>
        </div>
        {displayValue !== undefined ? <output>{displayValue}</output> :
            <input className={styles.numberInput} type="text" inputMode="decimal" value={numberText}
                aria-label={`${label}: ${control.min}–${control.max}`}
                onPointerDown={event => event.stopPropagation()}
                onFocus={() => { numberFocused.current = true; numberCancelled.current = false; }}
                onChange={event => setNumberText(event.target.value)}
                onKeyDown={event => {
                    event.stopPropagation();
                    if (event.key === "Enter") event.currentTarget.blur();
                    if (event.key === "Escape") { numberCancelled.current = true; event.currentTarget.blur(); }
                }}
                onBlur={commitNumber} />}
    </div>;
}

export function RoadPanel({ panelClass }: { panelClass: string }) {
    const raw = useValue(state$);
    const grassRaw = useValue(grassState$);
    const footpathRaw = useValue(footpathState$);
    const sidewalkRaw = useValue(sidewalkState$);
    const alleyRaw = useValue(alleySidewalkState$);
    const railRaw = useValue(railState$);
    const gravelRaw = useValue(gravelState$);
    const shoulderRaw = useValue(shoulderState$);
    const normalFlags = useValue(normalMapFlags$).split(",");

    const colorWindowRaw = useValue(colorWindowPosition$);
    const colorWindowPosition = React.useMemo(() => {
        try { return JSON.parse(colorWindowRaw) as { x?: number; y?: number }; }
        catch { return {}; }
    }, [colorWindowRaw]);
    const generalWindowRaw = useValue(textureGeneralWindowPosition$);
    const generalWindowPosition = React.useMemo(() => {
        try { return JSON.parse(generalWindowRaw) as { x?: number; y?: number }; }
        catch { return {}; }
    }, [generalWindowRaw]);
    const [generalOpen, setGeneralOpen] = React.useState(false);
    const [generalStart, setGeneralStart] = React.useState({ x: 650, y: 100 });
    const mainPanelRef = React.useRef<HTMLDivElement>(null);
    const [colorStart, setColorStart] = React.useState({ x: 650, y: 100 });
    const { translate } = useLocalization();
    const [open, setOpen] = React.useState(false);
    const [colorOpen, setColorOpen] = React.useState<"surface" | "crossing" | null>(null);
    const [surface, setSurface] = React.useState<Surface>("road");
    const [position, setPosition] = React.useState({ x: 86, y: 100 });
    const [draft, setDraft] = React.useState<State>(initial);
    const drag = React.useRef<{ x: number; y: number; left: number; top: number } | null>(null);
    const edits = React.useRef<Partial<Record<Surface, Partial<State>>>>({});
    const pendingChange = React.useRef<{ surface: Surface; key: Key; next: number } | null>(null);
    const pendingTimer = React.useRef<ReturnType<typeof setTimeout> | null>(null);
    const roadValue = React.useMemo(() => {
        try { return { ...initial, ...JSON.parse(raw) } as State; }
        catch { return initial; }
    }, [raw]);
    const sidewalkValue = React.useMemo(() => {
        try { return { ...initial, ...JSON.parse(sidewalkRaw) } as State; }
        catch { return initial; }
    }, [sidewalkRaw]);
    const alleyValue = React.useMemo(() => {
        try { return { ...initial, ...JSON.parse(alleyRaw) } as State; }
        catch { return initial; }
    }, [alleyRaw]);
    const railValue = React.useMemo(() => {
        try { return { ...initial, ...JSON.parse(railRaw) } as State; }
        catch { return { ...initial, show: false }; }
    }, [railRaw]);
    const gravelValue = React.useMemo(() => {
        try { return { ...initial, show: false, ...JSON.parse(gravelRaw) } as State; }
        catch { return { ...initial, show: false }; }
    }, [gravelRaw]);
    const shoulderValue = React.useMemo(() => {
        try { return { ...initial, show: false, ...JSON.parse(shoulderRaw) } as State; }
        catch { return { ...initial, show: false }; }
    }, [shoulderRaw]);
    const grassValue = React.useMemo(() => {
        try { return { ...initial, ...JSON.parse(grassRaw) } as State; }
        catch { return initial; }
    }, [grassRaw]);
    const footpathValue = React.useMemo(() => {
        try { return { ...initial, ...JSON.parse(footpathRaw) } as State; }
        catch { return initial; }
    }, [footpathRaw]);
    const railVisible = railValue.show;
    const values: Record<Surface, State> = { footpath: footpathValue, grass: grassValue, road: roadValue, sidewalk: sidewalkValue, alley: alleyValue, rail: railValue, gravel: gravelValue, shoulder: shoulderValue };
    const surfaces: Surface[] = ["road", "sidewalk", "footpath", "grass", "alley", "gravel", "shoulder", "rail"];
    const value = values[surface];
    React.useEffect(() => {
        if (!values[surface].show) setSurface(surfaces.find(key => values[key].show) ?? "road");
    }, [surface, footpathValue.show, grassValue.show, roadValue.show, sidewalkValue.show, alleyValue.show, railVisible, gravelValue.show, shoulderValue.show]);
    React.useEffect(() => {
        const local = edits.current[surface] ?? {};
        for (const key of Object.keys(local) as Key[])
            if (local[key] === value[key]) delete local[key];
        setDraft({ ...value, ...local });
    }, [surface, value]);
    const text = (key: string, fallback: string) => translate(`AdvancedGraphics.UI.${key}`, fallback) ?? fallback;

    React.useEffect(() => {
        const move = (event: MouseEvent) => {
            if (!drag.current) return;
            setPosition({
                x: Math.max(8, Math.min(window.innerWidth - (mainPanelRef.current?.getBoundingClientRect().width || 410) - 8, drag.current.left + event.clientX - drag.current.x)),
                y: Math.max(8, Math.min(window.innerHeight - (mainPanelRef.current?.getBoundingClientRect().height || 120) - 8, drag.current.top + event.clientY - drag.current.y)),
            });
        };
        const up = () => { drag.current = null; };
        window.addEventListener("mousemove", move);
        window.addEventListener("mouseup", up);
        return () => { window.removeEventListener("mousemove", move); window.removeEventListener("mouseup", up); };
    }, []);

    const flushPending = () => {
        if (pendingTimer.current !== null) clearTimeout(pendingTimer.current);
        pendingTimer.current = null;
        const pending = pendingChange.current;
        pendingChange.current = null;
        if (pending) trigger(group, triggers[pending.surface], `${pending.key}:${pending.next}`);
    };
    React.useEffect(() => () => {
        if (pendingTimer.current !== null) clearTimeout(pendingTimer.current);
    }, []);
    const set = (key: Key, next: number, dragging = false) => {
        const target = surface;
        edits.current[target] = { ...edits.current[target], [key]: next };
        setDraft(prev => ({ ...prev, [key]: next }));
        if (pendingChange.current && (pendingChange.current.key !== key || pendingChange.current.surface !== target)) flushPending();
        pendingChange.current = { surface: target, key, next };
        if (pendingTimer.current !== null) clearTimeout(pendingTimer.current);
        pendingTimer.current = dragging ? null : setTimeout(flushPending, 120);
    };
    if (!surfaces.some(key => values[key].show)) return null;
    const titles: Record<Surface, string> = { footpath: text("FootpathTitle", "Footpath"), grass: text("GrassTitle", "Sidewalk grass"), road: text("Title", "Road"), sidewalk: text("Sidewalk", "Sidewalk"), alley: text("AlleySidewalk", "Small-road sidewalk"), rail: text("Rail", "Rail"), gravel: text("RoadGravelTitle", "Gravel road"), shoulder: text("GravelShoulderTitle", "Gravel sidewalk") };
    const tabTitle = titles[surface];
    const crossingModes = [text("RailCrossingThrough", "Road and sidewalk"), text("RailCrossingCover", "Cover"), text("Custom", "Custom texture")];
    const launcherTitle = text("OpenTextureEditor", "Enhanced Graphics and Textures – open texture editor");
    const prefix = prefixes[surface];
    return <>
        <Tooltip tooltip={launcherTitle} direction="down">
        <button className={`${styles.launcher} ${open ? styles.active : ""}`}
            onClick={() => setOpen(!open)}
            aria-label={launcherTitle}>
            <svg viewBox="0 0 28 28" width="25" height="25" aria-hidden="true">
                <path d="M8 3h12l5 22H3L8 3Z" fill="none" stroke="currentColor" strokeWidth="2" />
                <path d="M14 5v4m0 4v4m0 4v3" stroke="currentColor" strokeWidth="2" />
            </svg>
        </button>
        </Tooltip>
        {open && <div ref={mainPanelRef} className={`${panelClass} ${styles.panel} ${styles.texturePanel}`}
            style={{ left: `${position.x}px`, top: `${position.y}px` }}>
            <div className={styles.header} onMouseDown={event => {
                if ((event.target as HTMLElement).closest("button")) return;
                drag.current = { x: event.clientX, y: event.clientY, left: position.x, top: position.y };
                event.preventDefault();
            }}>
                <strong>{tabTitle}</strong>
                <div className={styles.headerActions}>
                    <Tooltip tooltip={text("TextureGeneralTitle", "General texture settings")}>
                        <button type="button" className={`${styles.close} ${styles.settingsButton}`} aria-label={text("TextureGeneralTitle", "General texture settings")}
                            aria-pressed={generalOpen} onClick={() => {
                                flushPending();
                                const rect = mainPanelRef.current?.getBoundingClientRect();
                                setGeneralStart({ x: rect ? rect.right + 24 : position.x + 564, y: rect?.top ?? position.y });
                                setGeneralOpen(previous => !previous);
                            }}>
                            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 3h6l.6 2.5 2.2 1.3 2.5-.7 3 5.2-1.9 1.8v2.6l1.9 1.8-3 5.2-2.5-.7-2.2 1.3L15 25H9l-.6-2.5-2.2-1.3-2.5.7-3-5.2 1.9-1.8v-2.6L.7 10.5l3-5.2 2.5.7 2.2-1.3L9 3Z" transform="translate(1 0) scale(.85)" fill="none" stroke="currentColor" strokeWidth="2"/><circle cx="11.2" cy="11.9" r="3.3" fill="none" stroke="currentColor" strokeWidth="1.8"/></svg>
                        </button>
                    </Tooltip>
                    <button className={styles.close} onClick={() => setOpen(false)} title={text("Close", "Close")}>×</button>
                </div>
            </div>
            <div className={styles.textureTabs}>
                <div className={styles.surfaceTabs}>
                    {surfaces.filter(key => values[key].show).map(key => <div key={key} className={styles.tabGroup}>
                        <button className={surface === key ? styles.selectedTab : ""} onClick={() => { flushPending(); setDraft(values[key]); setSurface(key); }}>{titles[key]}</button>
                        <Tooltip tooltip={`${text("ColorFilter", "Color filter")} – ${titles[key]}`}>
                            <button className={styles.colorButton} aria-label={`${text("ChooseColor", "Choose color")} – ${titles[key]}`}
                                onClick={() => {
                                    flushPending(); setSurface(key); setDraft(values[key]);
                                    const rect = mainPanelRef.current?.getBoundingClientRect();
                                    setColorStart({ x: rect ? rect.right + 24 : position.x + 564, y: rect?.top ?? position.y });
                                    setColorOpen("surface");
                                }}>
                                <span className={styles.colorSwatch} style={{ backgroundColor: values[key].filterOpacity > 0 && values[key].filterColor !== 0xffffff ? colorHex(values[key].filterColor) : "transparent" }} />
                            </button>
                        </Tooltip>
                    </div>)}
                </div>
            </div>
            <Scrollable key={surface} className={styles.textureScroll} vertical horizontal={false} trackVisibility="scrollable">
                <div className={styles.textureContent}>
                <button type="button" className={styles.moduleToggle} role="checkbox" aria-checked={draft.enabled}
                    title={text(`${prefix}TipEnabled`, "Enable surface adjustments")}
                    onClick={() => {
                        flushPending();
                        const enabled = !draft.enabled;
                        setDraft(prev => ({ ...prev, enabled }));
                        trigger(group, triggers[surface], `enabled:${enabled ? 1 : 0}`);
                    }}>
                    <span className={`${styles.toggleBox} ${draft.enabled ? styles.toggleOn : ""}`} aria-hidden="true">
                        {draft.enabled ? "✓" : ""}
                    </span>
                    <span>{text(`${prefix}Enabled`, "Enable surface adjustments")}</span>
                </button>
                {controls.filter(control => (control.key === "crackDepth" || control.key === "crackRotation" || control.key === "crackOffsetX" || control.key === "crackOffsetY") ? (surface === "road" || surface === "sidewalk" || surface === "alley") : (control.key === "parkingScale" || control.key === "parkingDepth") ? surface === "road" :
                    (control.key === "parkingSidewalkU" || control.key === "parkingSidewalkV" || control.key === "shiftU" || control.key === "shiftV") ? surface === "sidewalk" :
                    (surface === "footpath" || surface === "grass" || surface === "shoulder" || surface === "rail" || surface === "gravel") ? ["detail", "uv", "depth", "offsetX", "offsetY", "rotation"].includes(control.key) : true).map(control => (surface === "footpath" || surface === "sidewalk") && control.key === "uv" ? { ...control, min: 10, step: 5 } : surface === "road" ? { ...control, max: ({ cracks: 1200, crackSize: 1800, crackWidth: 1200, crackOpacity: 600 } as Partial<Record<Key, number>>)[control.key] ?? control.max } : control).map((control, index) => <React.Fragment key={`${surface}:${control.key}`}>
                    {(index === 1 || control.key === "cracks" || control.key === "parkingSidewalkU") && <div className={styles.separator} />}
                    {surface === "road" && control.key === "cracks" && <>
                        <button type="button" className={styles.moduleToggle} role="checkbox" aria-checked={!!draft.customCracks}
                            title={text("CustomCracksHint", "Copy Road_Cracks.png to the crack folder. Dark cracks on white or transparent background. Relief is generated automatically.")}
                            onClick={() => { flushPending(); const enabled = !draft.customCracks; setDraft(prev => ({ ...prev, customCracks: enabled })); trigger(group, "setRoadSetting", `customCracks:${enabled ? 1 : 0}`); }}>
                            <span className={`${styles.toggleBox} ${draft.customCracks ? styles.toggleOn : ""}`} aria-hidden="true">{draft.customCracks ? "✓" : ""}</span>
                            <span>{text("CustomCracks", "Custom crack texture")}</span>
                        </button>
                        {draft.customCracks && <>
                            <p className={styles.textureHint}>{text("CracksNormalInfo", "Road_Cracks.png + optional Road_Cracks_Normal.png. Matching dimensions; automatic relief without a normal map.")}</p>
                            <p className={styles.textureHint}>{text(["CustomCracksPending", "CustomCracksLoaded", "CustomCracksMissing", "CustomCracksInvalid"][draft.crackStatus ?? 0], "")}</p>
                        </>}
                    </>}
                    <RoadSlider control={control}
                    label={text(`${prefix}${control.label}`, control.label)} value={draft[control.key]}
                    displayValue={control.key === "detail" ? [text("Original", "Original"), text("Generated", "Generated"), text("Custom", "Custom texture")][Math.max(0, Math.min(2, draft.detail))] : undefined}
                    description={text(`${prefix}Tip${control.label}`, "Adjust the surface appearance.")}
                    onChange={(next, dragging) => set(control.key, next, dragging)} onCommit={flushPending} />
                </React.Fragment>)}
                {draft.detail === 2 && <p className={styles.textureHint}>{text(`CustomFiles_${surface}`, "")}</p>}
                <div className={styles.textureActions}>
                    <button type="button" className={styles.reset}
                        title={text("ReloadSurfaceTextureHint", "Reload this tab's texture and optional normal map without changing its settings.")}
                        onClick={() => { flushPending(); trigger(group, "reloadSurfaceTexture", surface); }}>
                        {text("ReloadSurfaceTexture", "Reload texture")}
                    </button>
                    <button type="button" className={styles.reset}
                        title={text("OpenSurfaceTextureFolderHint", "Open this tab's texture folder.")}
                        onClick={() => { flushPending(); trigger(group, "openSurfaceTextureFolder", surface); }}>
                        {text("OpenSurfaceTextureFolder", "Open folder")}
                    </button>
                </div>
                {surface === "shoulder" && <p className={styles.textureHint}>{text("GravelShoulderHint", "Adjusts the original gravel road edge atlas.")}</p>}
                <Tooltip tooltip={text(`${prefix}TipReset`, "Restore surface defaults.")}><button className={styles.reset}
                    onClick={() => { flushPending(); edits.current[surface] = {}; trigger(group, triggers[surface], "reset"); }}>{text("Reset", "Reset")}</button></Tooltip>
                {surface === "rail" && <>
                    <div className={styles.separator} />
                    <RoadSlider control={{ key: "crossingMode", label: "RailCrossing", min: 0, max: 2, step: 1 }}
                        label={text("RailCrossing", "Level crossing")} value={draft.crossingMode}
                        description={crossingModes.join(" / ")} displayValue={crossingModes[Math.max(0, Math.min(2, draft.crossingMode))]}
                        onChange={(next, dragging) => {
                            if (next === 0 && colorOpen === "crossing") setColorOpen(null);
                            set("crossingMode", next, dragging);
                        }} onCommit={flushPending} />
                    {draft.crossingMode === 0 ? <p className={styles.textureHint}>
                        {text("RailCrossingThroughHint", "Makes the cover transparent so the road and sidewalk below show through.")}
                    </p> : <>
                        {crossingControls.map(control => <RoadSlider key={control.key} control={control}
                            label={text(control.label, control.label)} value={draft[control.key]}
                            description={text(`Tip${control.label}`, "Adjust the crossing cover.")}
                            onChange={(next, dragging) => set(control.key, next, dragging)} onCommit={flushPending} />)}
                    </>}
                    {draft.crossingMode === 2 && <>
                        <p className={styles.textureHint}>{text("RailCrossingFiles", "RailCrossing_BaseColor.png and optional RailCrossing_Normal.png. Uses the built-in cover if the color PNG is missing.")}</p>
                        <div className={styles.textureActions}>
                            <button type="button" className={styles.reset} onClick={() => { flushPending(); trigger(group, "reloadSurfaceTexture", "railCrossing"); }}>{text("ReloadSurfaceTexture", "Reload texture")}</button>
                            <button type="button" className={styles.reset} onClick={() => trigger(group, "openSurfaceTextureFolder", "railCrossing")}>{text("OpenSurfaceTextureFolder", "Open folder")}</button>
                        </div>
                    </>}
                    <div className={styles.textureActions}>
                        <button type="button" className={styles.reset} disabled={draft.crossingMode === 0} onClick={() => {
                            flushPending();
                            const rect = mainPanelRef.current?.getBoundingClientRect();
                            setColorStart({ x: rect ? rect.right + 24 : position.x + 564, y: rect?.top ?? position.y });
                            setColorOpen("crossing");
                        }}>{text("ColorFilter", "Color filter")}</button>
                        <button type="button" className={styles.reset} onClick={() => {
                            flushPending();
                            for (const key of [...crossingControls.map(control => control.key), "crossingMode"] as Key[]) delete edits.current.rail?.[key];
                            trigger(group, "setRailSetting", "crossingReset");
                        }}>{text("RailCrossingReset", "Reset crossing")}</button>
                    </div>
                </>}
                </div>
            </Scrollable>
        </div>}
        {open && generalOpen && <TextureGeneralWindow endcapValues={roadValue} endcapKind={surface === "alley" ? "alley" : surface === "road" ? "parking" : "sidewalk"} normalEnabled={normalFlags[["road", "sidewalk", "alley", "gravel", "shoulder", "rail", "grass", "footpath"].indexOf(surface)] === "1"} surfaceTitle={titles[surface]}
            onNormalChange={enabled => { flushPending(); trigger(group, "setNormalMapEnabled", `${surface}:${enabled ? 1 : 0}`); }} mipBias={roadValue.mipBias} text={text} panelClass={panelClass}
            savedPosition={generalWindowPosition} initialPosition={generalStart}
            onSavePosition={next => trigger(group, "setTextureGeneralWindowPosition", `${next.x.toFixed(1)}:${next.y.toFixed(1)}`)}
            onClose={() => setGeneralOpen(false)}
            onApply={next => { flushPending(); trigger(group, "setRoadSetting", `mipBias:${next}`); }} />}
        {open && colorOpen && values[surface].show && (colorOpen === "surface" || (surface === "rail" && draft.crossingMode !== 0)) &&
            <ColorFilterWindow surface={surface} mode={colorOpen === "crossing" ? draft.crossingMode : values[surface].detail}
            title={colorOpen === "crossing" ? text("RailCrossing", "Level crossing") : titles[surface]}
            color={colorOpen === "crossing" ? railValue.crossingColor : values[surface].filterColor}
            opacity={colorOpen === "crossing" ? railValue.crossingOpacity : values[surface].filterOpacity} text={text}
            panelClass={panelClass} savedPosition={colorWindowPosition} initialPosition={colorStart}
            onSavePosition={position => trigger(group, "setColorWindowPosition", `${position.x.toFixed(1)}:${position.y.toFixed(1)}`)}
            onClose={() => setColorOpen(null)}
            onApply={(color, opacity) => {
                flushPending();
                trigger(group, "setSurfaceFilterColor", colorOpen === "crossing" ? `railCrossing:${color}:${opacity}` : `${surface}:${color}:${opacity}:${values[surface].detail}`);
            }} />}
    </>;
}



function TextureGeneralWindow({ endcapValues, endcapKind, normalEnabled, surfaceTitle, onNormalChange, mipBias, text, panelClass, savedPosition, initialPosition, onSavePosition, onClose, onApply }: {
    endcapValues: State; endcapKind: EndcapKind;
    normalEnabled: boolean; surfaceTitle: string; onNormalChange: (enabled: boolean) => void;
    mipBias: number; text: (key: string, fallback: string) => string; panelClass: string;
    savedPosition: { x?: number; y?: number }; initialPosition: { x: number; y: number };
    onSavePosition: (position: { x: number; y: number }) => void;
    onClose: () => void; onApply: (value: number) => void;
}) {
    const [draft, setDraft] = React.useState(mipBias);
    const [position, setPosition] = React.useState(initialPosition);
    const panelRef = React.useRef<HTMLDivElement>(null);
    const positionRef = React.useRef(position);
    const saveRef = React.useRef(onSavePosition);
    const applyRef = React.useRef(onApply);
    saveRef.current = onSavePosition; applyRef.current = onApply;
    const pending = React.useRef<number | null>(null);
    const timer = React.useRef<ReturnType<typeof setTimeout> | null>(null);
    const drag = React.useRef<{ x: number; y: number; left: number; top: number } | null>(null);
    const clampPosition = (next: { x: number; y: number }) => {
        const rect = panelRef.current?.getBoundingClientRect();
        return { x: Math.max(8, Math.min(Math.max(8, window.innerWidth - (rect?.width || 420) - 8), next.x)),
            y: Math.max(8, Math.min(Math.max(8, window.innerHeight - (rect?.height || 220) - 8), next.y)) };
    };
    const commit = () => {
        if (timer.current !== null) clearTimeout(timer.current);
        timer.current = null;
        if (pending.current === null) return;
        const next = pending.current; pending.current = null; applyRef.current(next);
    };
    React.useEffect(() => { if (pending.current === null) setDraft(mipBias); }, [mipBias]);
    React.useLayoutEffect(() => {
        const saved = Number.isFinite(savedPosition.x) && Number.isFinite(savedPosition.y) &&
            (savedPosition.x ?? -1) >= 0 && (savedPosition.y ?? -1) >= 0;
        const next = clampPosition(saved ? { x: savedPosition.x!, y: savedPosition.y! } : initialPosition);
        positionRef.current = next; setPosition(next);
    }, []);
    React.useEffect(() => {
        const move = (event: MouseEvent) => {
            if (!drag.current) return;
            const next = clampPosition({ x: drag.current.left + event.clientX - drag.current.x,
                y: drag.current.top + event.clientY - drag.current.y });
            positionRef.current = next; setPosition(next);
        };
        const up = () => {
            if (!drag.current) return;
            drag.current = null; saveRef.current(positionRef.current);
        };
        const resize = () => {
            const next = clampPosition(positionRef.current);
            positionRef.current = next; setPosition(next);
        };
        window.addEventListener("mousemove", move); window.addEventListener("mouseup", up); window.addEventListener("resize", resize);
        return () => {
            window.removeEventListener("mousemove", move); window.removeEventListener("mouseup", up); window.removeEventListener("resize", resize);
            if (drag.current) saveRef.current(positionRef.current);
            commit();
        };
    }, []);
    return <div ref={panelRef} className={`${panelClass} ${styles.panel} ${styles.generalWindow}`}
        style={{ left: `${position.x}px`, top: `${position.y}px` }}>
        <div className={styles.header} onMouseDown={event => {
            if (event.button !== 0 || (event.target as HTMLElement).closest("button")) return;
            drag.current = { x: event.clientX, y: event.clientY, left: position.x, top: position.y };
            event.preventDefault();
        }}>
            <strong>{text("TextureGeneralTitle", "General texture settings")}</strong>
            <button className={styles.close} onClick={() => { commit(); onClose(); }} title={text("Close", "Close")}>×</button>
        </div>
        <div className={styles.body}>
            <button type="button" className={styles.moduleToggle} role="checkbox" aria-checked={normalEnabled}
                title={text("UseCustomNormalHint", "Off: automatic relief. On: use optional custom normals when available.")}
                onClick={() => onNormalChange(!normalEnabled)}>
                <span className={`${styles.toggleBox} ${normalEnabled ? styles.toggleOn : ""}`} aria-hidden="true">{normalEnabled ? "✓" : ""}</span>
                <span>{text("UseCustomNormal", "Use custom normal map")} – {surfaceTitle}</span>
            </button>
            <EndcapSliders values={endcapValues} initialKind={endcapKind} text={text} />
            <p className={styles.textureHint}>{text("TextureGeneralHint", "Applies to all surfaces edited by this mod.")}</p>
            <RoadSlider control={{ key: "mipBias", label: "SurfaceMipBias", min: -1, max: 1, step: 0.1 }}
                label={text("SurfaceMipBias", "Texture mip bias")} description={text("TipSurfaceMipBias", "Adjust distant texture detail.")}
                value={draft} onChange={(next, dragging) => {
                    setDraft(next); pending.current = next;
                    if (timer.current !== null) clearTimeout(timer.current);
                    timer.current = dragging ? null : setTimeout(commit, 120);
                }} onCommit={commit} />
            <p className={styles.textureHint}>{text("TextureFiltering16", "Anisotropic filtering: 16×")}</p>
            <div className={styles.generalActions}>
                <button className={styles.reset} title={text("ReloadAllCustomTexturesHint", "Reloads all custom color and normal maps, including the road crack mask.")}
                    onClick={() => { commit(); trigger(group, "reloadAllCustomTextures", "reload"); }}>{text("ReloadAllCustomTextures", "Reload all custom textures")}</button>
                <button className={styles.reset} onClick={() => { pending.current = 0; setDraft(0); commit(); }}>{text("Reset", "Reset")}</button>
            </div>
        </div>
    </div>;
}

function colorHex(value: number) {
    return `#${(value & 0xffffff).toString(16).padStart(6, "0").toUpperCase()}`;
}

function ColorFilterWindow({ surface, mode, title, color, opacity, text, panelClass, savedPosition, initialPosition, onSavePosition, onClose, onApply }: {
    surface: Surface; mode: number; title: string; color: number; opacity: number; text: (key: string, fallback: string) => string; panelClass: string;
    savedPosition: { x?: number; y?: number }; initialPosition: { x: number; y: number };
    onSavePosition: (position: { x: number; y: number }) => void;
    onClose: () => void; onApply: (color: number, opacity: number) => void;
}) {
    const [draft, setDraft] = React.useState(color);
    const [draftOpacity, setDraftOpacity] = React.useState(opacity);
    const [hex, setHex] = React.useState(colorHex(color));
    const [position, setPosition] = React.useState(initialPosition);
    const panelRef = React.useRef<HTMLDivElement>(null);
    const positionRef = React.useRef(position);
    const saveRef = React.useRef(onSavePosition);
    saveRef.current = onSavePosition;
    const drag = React.useRef<{ x: number; y: number; left: number; top: number } | null>(null);
    const clampPosition = (next: { x: number; y: number }) => {
        const rect = panelRef.current?.getBoundingClientRect();
        return { x: Math.max(8, Math.min(Math.max(8, window.innerWidth - (rect?.width || 420) - 8), next.x)),
            y: Math.max(8, Math.min(Math.max(8, window.innerHeight - (rect?.height || 420) - 8), next.y)) };
    };
    React.useLayoutEffect(() => {
        const saved = Number.isFinite(savedPosition.x) && Number.isFinite(savedPosition.y) &&
            (savedPosition.x ?? -1) >= 0 && (savedPosition.y ?? -1) >= 0;
        const next = clampPosition(saved ? { x: savedPosition.x!, y: savedPosition.y! } : initialPosition);
        positionRef.current = next; setPosition(next);
    }, []);
    React.useEffect(() => { setDraft(color); setDraftOpacity(opacity); setHex(colorHex(color)); }, [surface, mode, color, opacity]);
    React.useEffect(() => {
        const move = (event: MouseEvent) => {
            if (!drag.current) return;
            const next = clampPosition({ x: drag.current.left + event.clientX - drag.current.x,
                y: drag.current.top + event.clientY - drag.current.y });
            positionRef.current = next; setPosition(next);
        };
        const up = () => {
            if (!drag.current) return;
            drag.current = null; saveRef.current(positionRef.current);
        };
        const resize = () => {
            const next = clampPosition(positionRef.current);
            positionRef.current = next; setPosition(next);
        };
        window.addEventListener("mousemove", move); window.addEventListener("mouseup", up); window.addEventListener("resize", resize);
        return () => {
            window.removeEventListener("mousemove", move); window.removeEventListener("mouseup", up); window.removeEventListener("resize", resize);
            if (drag.current) saveRef.current(positionRef.current);
        };
    }, []);
    const choose = (value: number) => {
        setDraft(value); setHex(colorHex(value));
        // A new RGB selection reactivates a filter that was reset.
        setDraftOpacity(previous => previous === 0 ? 100 : previous);
    };
    const parsedHex = () => /^#?[0-9a-f]{6}$/i.test(hex.trim()) ? parseInt(hex.trim().replace("#", ""), 16) : draft;
    const commitHex = () => {
        const next = parsedHex();
        if (next !== draft) choose(next); else setHex(colorHex(draft));
    };
    const presets = [0xffffff, 0xd6d3cc, 0xb5b1a8, 0x8c8982, 0x66645f, 0x45443f,
        0xd9c9aa, 0xb8a17d, 0x967b59, 0x795f48, 0x8b7566, 0x72745a];
    return <div ref={panelRef} className={`${panelClass} ${styles.panel} ${styles.colorWindow}`} role="dialog" aria-label={`${text("ColorFilter", "Color filter")} – ${title}`}
        style={{ left: `${position.x}px`, top: `${position.y}px` }}>
        <div className={styles.header} onMouseDown={event => {
            if (event.button !== 0 || (event.target as HTMLElement).closest("button")) return;
            drag.current = { x: event.clientX, y: event.clientY, left: positionRef.current.x, top: positionRef.current.y }; event.preventDefault();
        }}>
            <strong>{text("ColorFilter", "Color filter")} – {title}</strong>
            <button className={styles.close} onClick={onClose} aria-label={text("Close", "Close")}>×</button>
        </div>
        <div className={styles.body}>
            <div className={styles.colorPreview}><span style={{ backgroundColor: colorHex(draft) }} />
                <strong>{(draftOpacity === 0 || draft === 0xffffff) ? text("NoColorFilter", "No color filter") : colorHex(draft)}</strong></div>
            <p className={styles.textureHint}>{text("ColorFilterTip", "Transparency 100% disables the color filter.")}</p>
            <div className={styles.colorPresets} aria-label={text("ColorPresets", "Presets")}>
                {presets.map(value => <button key={value} style={{ backgroundColor: colorHex(value) }}
                    aria-label={`${text("ChooseColor", "Choose color")} ${colorHex(value)}`} aria-pressed={draft === value}
                    onClick={() => choose(value)} />)}
            </div>
            {[["ColorRed", "Red", 16], ["ColorGreen", "Green", 8], ["ColorBlue", "Blue", 0]].map(([key, fallback, shift]) => {
                const bits = shift as number;
                return <RoadSlider key={key} control={{ key: "detail", label: key as string, min: 0, max: 255, step: 1 }}
                    label={text(key as string, fallback as string)} description={text(key as string, fallback as string)}
                    value={(draft >> bits) & 255} onCommit={() => {}}
                    onChange={value => choose((draft & ~(255 << bits)) | (value << bits))} />;
            })}
            <RoadSlider control={{ key: "detail", label: "FilterTransparency", min: 0, max: 100, step: 1 }}
                label={text("FilterTransparency", "Transparency")} description={text("FilterTransparencyTip", "0% = full filter; 100% = no filter.")}
                value={100 - draftOpacity} onChange={value => setDraftOpacity(100 - value)} onCommit={() => {}} />
            <div className={styles.row}><label className={styles.rowLabel} htmlFor="egt-filter-hex">{text("ColorHex", "Hex value")}</label>
                <input id="egt-filter-hex" className={styles.numberInput} type="text" value={hex} maxLength={7}
                    onChange={event => setHex(event.target.value)} onBlur={commitHex}
                    onKeyDown={event => { event.stopPropagation(); if (event.key === "Enter") event.currentTarget.blur(); }} /></div>
            <div className={styles.colorActions}>
                <button className={styles.reset} onClick={() => { const next = parsedHex(); setDraft(next); setHex(colorHex(next)); onApply(next, draftOpacity); }}>{text("ApplyColor", "Apply")}</button>
                <button className={styles.reset} onClick={() => { setDraft(0xffffff); setHex("#FFFFFF"); setDraftOpacity(0); onApply(0xffffff, 0); }}>{text("ResetColor", "Reset color")}</button>
            </div>
        </div>
    </div>;
}





type EndcapKind = "sidewalk" | "alley" | "parking" | "roadend" | "large";
function EndcapSliders({ values, initialKind, text }: { values: State; initialKind: EndcapKind; text: (key: string, fallback: string) => string }) {
    const [kind, setKind] = React.useState<EndcapKind>(initialKind);
    const u = kind === "large" ? values.largeEndcapU : kind === "alley" ? values.alleyEndcapU : kind === "parking" ? values.parkingEndcapU : kind === "roadend" ? values.roadEndU : values.endcapU;
    const v = kind === "large" ? values.largeEndcapV : kind === "alley" ? values.alleyEndcapV : kind === "parking" ? values.parkingEndcapV : kind === "roadend" ? values.roadEndV : values.endcapV;
    const [draft, setDraft] = React.useState({ endcapU: u, endcapV: v });
    React.useEffect(() => setKind(initialKind), [initialKind]);
    React.useEffect(() => setDraft({ endcapU: u, endcapV: v }), [kind, u, v]);
    const labels: Record<EndcapKind, string> = {
        sidewalk: text("EndcapNormal", "Normal roads"),
        large: text("EndcapLarge", "Large roads (32 m+)"),
        alley: text("EndcapAlley", "Small-road sidewalks"),
        parking: text("EndcapParking", "Parking roads"),
        roadend: text("EndcapRoadEnd", "Normal roads: round ends (< 32 m)")
    };
    return <>
        <p className={styles.textureHint}>{text("EndcapHint", "Choose a road type. Edits take effect only when you click Apply. Higher values repeat the texture more often.")}</p>
        <div className={styles.endcapKinds}>
            {(["sidewalk", "large", "alley", "parking", "roadend"] as const).map(item => <button key={item} className={styles.reset}
                aria-pressed={kind === item} onClick={() => setKind(item)}>{kind === item ? "✓ " : ""}{labels[item]}</button>)}
        </div>
        {(["endcapU", "endcapV"] as const).map(key => <RoadSlider key={key}
            control={{ key, label: key, min: 0.1, max: 8, step: 0.05 }}
            label={text(key, key === "endcapU" ? "End tiling U" : "End tiling V")}
            description={text("EndcapHint", "Edits take effect only when you click Apply.")}
            value={draft[key]} onChange={next => setDraft(previous => ({ ...previous, [key]: next }))}
            onCommit={() => {}} />)}
        <div className={styles.colorActions}>
            <button className={styles.reset} onClick={() =>
                trigger(group, "applyEndcap", `${kind}:${draft.endcapU}:${draft.endcapV}`)}>{text("ApplyColor", "Apply")}</button>
            <button className={styles.reset} onClick={() => setDraft({ endcapU: 1, endcapV: 1 })}>
                {text("ResetEndcaps", "Reset end tiling")}</button>
        </div>
    </>;
}


