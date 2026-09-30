import React from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { Tooltip } from "cs2/ui";
import styles from "./road-panel.module.scss";

const group = "Advanced_Graphics";
const debugEnabled$ = bindValue<boolean>(group, "debugEnabled", false);
const state$ = bindValue<string>(group, "roadSettings", "{}");
const sidewalkState$ = bindValue<string>(group, "sidewalkSettings", "{}");
const alleySidewalkState$ = bindValue<string>(group, "alleySidewalkSettings", "{}");
const railState$ = bindValue<string>(group, "railSettings", "{}");
const gravelState$ = bindValue<string>(group, "gravelSettings", "{}");
const shoulderState$ = bindValue<string>(group, "shoulderSettings", "{}");
type Surface = "road" | "sidewalk" | "alley" | "rail" | "gravel" | "shoulder";
const triggers: Record<Surface, string> = { road: "setRoadSetting", sidewalk: "setSidewalkSetting", alley: "setAlleySidewalkSetting", rail: "setRailSetting", gravel: "setGravelSetting", shoulder: "setShoulderSetting" };
const prefixes: Record<Surface, string> = { road: "", sidewalk: "Sidewalk", alley: "AlleySidewalk", rail: "RailGravel", gravel: "RoadGravel", shoulder: "GravelShoulder" };
type Key = "detail" | "uv" | "depth" | "tiling" | "offsetX" | "offsetY" |
    "rotation" | "cracks" | "crackSize" | "crackWidth" | "crackOpacity" | "randomness";
type State = Record<Key, number> & { show: boolean; enabled: boolean; probe?: boolean };
const initial: State = { show: true, enabled: false, detail: 0, uv: 100, depth: 0,
    tiling: 1, offsetX: 0, offsetY: 0, rotation: 0, cracks: 0, crackSize: 100, crackWidth: 100, crackOpacity: 100, randomness: 50 };

const controls: { key: Key; label: string; min: number; max: number; step: number }[] = [
    { key: "detail", label: "Detail", min: 0, max: 2, step: 1 },
    { key: "uv", label: "Uv", min: 100, max: 1000, step: 25 },
    { key: "depth", label: "Depth", min: 0, max: 1000, step: 25 },
    { key: "offsetX", label: "OffsetX", min: 0, max: 100, step: 5 },
    { key: "offsetY", label: "OffsetY", min: 0, max: 100, step: 5 },
    { key: "rotation", label: "Rotation", min: 0, max: 360, step: 1 },
    { key: "cracks", label: "Cracks", min: 0, max: 400, step: 10 },
    { key: "crackSize", label: "CrackSize", min: 25, max: 600, step: 25 },
    { key: "crackWidth", label: "CrackWidth", min: 25, max: 400, step: 25 },
    { key: "crackOpacity", label: "CrackOpacity", min: 0, max: 200, step: 10 },
    { key: "randomness", label: "Randomness", min: 0, max: 100, step: 10 },
    { key: "tiling", label: "Tiling", min: 0.1, max: 8, step: 0.1 },
];

type Control = typeof controls[number];

function RoadSlider({ control, label, description, value, displayValue, onChange, onCommit }: {
    control: Control; label: string; description: string; value: number; displayValue?: string;
    onChange: (value: number, dragging?: boolean) => void; onCommit: () => void;
}) {
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
        onChangeRef.current(Number(Math.min(control.max, control.min + steps * control.step).toFixed(1)), true);
    }, [control]);
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
        const next = control.key === "tiling" ? Math.round(clamped * 10) / 10 : Math.round(clamped);
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
            aria-label={label} aria-valuemin={control.min} aria-valuemax={control.max} aria-valuenow={value}
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
                onChange(Number(Math.max(control.min, Math.min(control.max, next)).toFixed(1)));
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
    const sidewalkRaw = useValue(sidewalkState$);
    const alleyRaw = useValue(alleySidewalkState$);
    const railRaw = useValue(railState$);
    const gravelRaw = useValue(gravelState$);
    const shoulderRaw = useValue(shoulderState$);
    const debugEnabled = useValue(debugEnabled$);
    const { translate } = useLocalization();
    const [open, setOpen] = React.useState(false);
    const [surface, setSurface] = React.useState<Surface>("road");
    const [position, setPosition] = React.useState({ x: 86, y: 100 });
    const [draft, setDraft] = React.useState<State>(initial);
    const drag = React.useRef<{ x: number; y: number; left: number; top: number } | null>(null);
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
    const railVisible = railValue.show;
    const values: Record<Surface, State> = { road: roadValue, sidewalk: sidewalkValue, alley: alleyValue, rail: railValue, gravel: gravelValue, shoulder: shoulderValue };
    const surfaces: Surface[] = ["road", "sidewalk", "alley", "gravel", "shoulder", "rail"];
    const value = values[surface];
    React.useEffect(() => {
        if (!values[surface].show) setSurface(surfaces.find(key => values[key].show) ?? "road");
    }, [surface, roadValue.show, sidewalkValue.show, alleyValue.show, railVisible, gravelValue.show, shoulderValue.show]);
    React.useEffect(() => setDraft(value), [value]);
    const text = (key: string, fallback: string) => translate(`AdvancedGraphics.UI.${key}`, fallback) ?? fallback;

    React.useEffect(() => {
        const move = (event: MouseEvent) => {
            if (!drag.current) return;
            setPosition({
                x: Math.max(8, Math.min(window.innerWidth - 410, drag.current.left + event.clientX - drag.current.x)),
                y: Math.max(8, Math.min(window.innerHeight - 120, drag.current.top + event.clientY - drag.current.y)),
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
        setDraft(prev => ({ ...prev, [key]: next }));
        if (pendingChange.current && (pendingChange.current.key !== key || pendingChange.current.surface !== target)) flushPending();
        pendingChange.current = { surface: target, key, next };
        if (pendingTimer.current !== null) clearTimeout(pendingTimer.current);
        pendingTimer.current = dragging ? null : setTimeout(flushPending, 120);
    };
    if (!surfaces.some(key => values[key].show)) return null;
    const titles: Record<Surface, string> = { road: text("Title", "Road"), sidewalk: text("Sidewalk", "Sidewalk"), alley: text("AlleySidewalk", "Small-road sidewalk"), rail: text("Rail", "Rail"), gravel: text("RoadGravelTitle", "Gravel road"), shoulder: text("GravelShoulderTitle", "Gravel sidewalk") };
    const tabTitle = titles[surface];
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
        {open && <div className={`${panelClass} ${styles.panel}`}
            style={{ left: `${position.x}px`, top: `${position.y}px` }}>
            <div className={styles.header} onMouseDown={event => {
                if ((event.target as HTMLElement).closest("button")) return;
                drag.current = { x: event.clientX, y: event.clientY, left: position.x, top: position.y };
                event.preventDefault();
            }}>
                <strong>{tabTitle}</strong>
                <button className={styles.close} onClick={() => setOpen(false)} title={text("Close", "Close")}>×</button>
            </div>
            <div className={styles.body}>
                <div className={styles.surfaceTabs}>
                    {surfaces.filter(key => values[key].show).map(key => <button key={key} className={surface === key ? styles.selectedTab : ""} onClick={() => { flushPending(); setDraft(values[key]); setSurface(key); }}>{titles[key]}</button>)}
                </div>
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
                {controls.filter(control => (surface === "shoulder" || surface === "rail" || surface === "gravel") ? ["detail", "uv", "depth", "offsetX", "offsetY", "rotation"].includes(control.key) : true).map((control, index) => <React.Fragment key={`${surface}:${control.key}`}>
                    {(index === 1 || control.key === "cracks") && <div className={styles.separator} />}
                    <RoadSlider control={control}
                    label={text(`${prefix}${control.label}`, control.label)} value={draft[control.key]}
                    displayValue={control.key === "detail" ? [text("Original", "Original"), text("Generated", "Generated"), text("Custom", "Custom PNG")][Math.max(0, Math.min(2, draft.detail))] : undefined}
                    description={text(`${prefix}Tip${control.label}`, "Adjust the surface appearance.")}
                    onChange={(next, dragging) => set(control.key, next, dragging)} onCommit={flushPending} />
                </React.Fragment>)}
                {draft.detail === 2 && <div className={styles.textureHint}>{text(`${prefix}CustomHint`, "Copy the custom PNG to the texture folder and reload it in options.")}</div>}
                {surface === "gravel" && draft.detail === 2 && <button className={styles.reset} onClick={() => { flushPending(); trigger(group, "reloadGravelTexture", "reload"); }}>{text("RoadGravelReload", "Reload custom texture")}</button>}
                {surface === "shoulder" && draft.detail === 2 && <button className={styles.reset} onClick={() => { flushPending(); trigger(group, "reloadGravelShoulderTexture", "reload"); }}>{text("RoadGravelReload", "Reload custom texture")}</button>}
                {surface === "shoulder" && <p className={styles.textureHint}>{text("GravelShoulderHint", "Adjusts the original gravel road edge atlas.")}</p>}
                <Tooltip tooltip={text(`${prefix}TipReset`, "Restore surface defaults.")}><button className={styles.reset}
                    onClick={() => { pendingChange.current = null; if (pendingTimer.current !== null) clearTimeout(pendingTimer.current); pendingTimer.current = null; trigger(group, triggers[surface], "reset"); }}>{text("Reset", "Reset")}</button></Tooltip>
                {surface === "rail" && debugEnabled && <>
                    <button className={styles.reset} onClick={() => trigger(group, "probeRailMaterials", "toggle")}>{railValue.probe ? text("RailProbeOff", "Disable color probe") : text("RailProbeOn", "Identify track materials")}</button>
                    <p className={styles.textureHint}>{text("RailProbeHint", "Magenta = TrainSubwayTrack, yellow = GravelShoulder, green = Gravel, red = rail decals, cyan = TrainRails. Disable after a screenshot.")}</p>
                    <p className={styles.textureHint}>{text("RailDiagnosticDescription", "Rail shader colors are being inspected separately; track gravel controls are active above.")}</p>
                    <button className={styles.reset} onClick={() => trigger(group, "inspectRailMaterials", "export")}>{text("RailExport", "Export rail materials")}</button>
                    <p className={styles.textureHint}>{text("RailExportHint", "Find RailMaterials.txt in RailTextures/Diagnostics.")}</p>
                </>}
            </div>
        </div>}
    </>;
}


