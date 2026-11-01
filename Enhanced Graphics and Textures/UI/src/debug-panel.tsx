import React from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { Tooltip } from "cs2/ui";
import styles from "./road-panel.module.scss";

// In-game window with all debug tools. Visible only while "Show debug window" is on in the options.
const group = "Advanced_Graphics";
const state$ = bindValue<string>(group, "debugState", "{}");
type State = { show: boolean; recomputeTangents: boolean; atlasDiagnostic: boolean; railProbe: boolean; junctionMode: number; x: number; y: number };
const initial: State = { show: false, recomputeTangents: false, atlasDiagnostic: false, railProbe: false, junctionMode: 0, x: -1, y: -1 };
const act = (action: string) => trigger(group, "debugAction", action);

export function DebugPanel({ panelClass }: { panelClass: string }) {
    const raw = useValue(state$);
    const value = React.useMemo(() => {
        try { return { ...initial, ...JSON.parse(raw) } as State; }
        catch { return initial; }
    }, [raw]);
    const { translate } = useLocalization();
    const text = (key: string, fallback: string) => translate(`AdvancedGraphics.UI.Debug.${key}`, fallback) ?? fallback;
    const [open, setOpen] = React.useState(false);
    React.useEffect(() => { if (!value.show) setOpen(false); }, [value.show]);

    const panel = React.useRef<HTMLDivElement>(null);
    const [position, setPosition] = React.useState({ x: 560, y: 120 });
    const positionRef = React.useRef(position);
    const drag = React.useRef<{ x: number; y: number; left: number; top: number } | null>(null);
    const clampPosition = (next: { x: number; y: number }) => {
        const rect = panel.current?.getBoundingClientRect();
        return {
            x: Math.max(8, Math.min(Math.max(8, window.innerWidth - (rect?.width || 360) - 8), next.x)),
            y: Math.max(8, Math.min(Math.max(8, window.innerHeight - (rect?.height || 400) - 8), next.y)),
        };
    };
    const savePosition = () => trigger(group, "setDebugWindowPosition",
        `${positionRef.current.x.toFixed(1)}:${positionRef.current.y.toFixed(1)}`);
    const wasOpen = React.useRef(false);
    React.useLayoutEffect(() => {
        if (!open) { wasOpen.current = false; return; }
        const saved = value.x >= 0 && value.y >= 0;
        const next = clampPosition(saved && !wasOpen.current ? { x: value.x, y: value.y } : positionRef.current);
        wasOpen.current = true;
        positionRef.current = next;
        setPosition(next);
    }, [open]);
    React.useEffect(() => {
        const move = (event: MouseEvent) => {
            if (!drag.current) return;
            const next = clampPosition({ x: drag.current.left + event.clientX - drag.current.x,
                y: drag.current.top + event.clientY - drag.current.y });
            positionRef.current = next;
            setPosition(next);
        };
        const up = () => { if (drag.current) { drag.current = null; savePosition(); } };
        window.addEventListener("mousemove", move);
        window.addEventListener("mouseup", up);
        return () => { window.removeEventListener("mousemove", move); window.removeEventListener("mouseup", up); };
    }, []);

    if (!value.show) return null;
    const title = text("Title", "Debug");
    const toggle = (checked: boolean, label: string, tip: string, onClick: () => void) =>
        <Tooltip tooltip={tip}>
            <button className={styles.moduleToggle} role="checkbox" aria-checked={checked} aria-label={label} onClick={onClick}>
                <span aria-hidden="true" className={`${styles.toggleBox} ${checked ? styles.toggleOn : ""}`}>{checked ? "✓" : ""}</span>
                {label}
            </button>
        </Tooltip>;
    const button = (action: string, label: string, tip: string) =>
        <Tooltip tooltip={tip}><button className={styles.reset} onClick={() => act(action)}>{label}</button></Tooltip>;
    const modes = [0, 1, 2, 3, 4, 5, 6];
    return <>
        <Tooltip tooltip={text("Open", "Enhanced Graphics and Textures – debug tools")} direction="down">
            <button className={`${styles.launcher} ${open ? styles.active : ""}`} aria-label={title}
                onClick={() => setOpen(previous => !previous)}>
                <svg viewBox="0 0 28 28" width="25" height="25" aria-hidden="true">
                    <ellipse cx="14" cy="16" rx="6" ry="8" fill="none" stroke="currentColor" strokeWidth="2" />
                    <path d="M14 8V5M10 6l-2-2m10 2 2-2M8 14H3m22 0h-5M8 20l-4 3m20-3-4 3M14 10v14" fill="none" stroke="currentColor" strokeWidth="2" />
                </svg>
            </button>
        </Tooltip>
        {open && <div ref={panel} className={`${panelClass} ${styles.panel}`}
            style={{ left: `${position.x}px`, top: `${position.y}px`, width: "360rem" }}>
            <div className={styles.header} onMouseDown={event => {
                if (event.button !== 0 || (event.target as HTMLElement).closest("button")) return;
                event.preventDefault();
                drag.current = { x: event.clientX, y: event.clientY, left: position.x, top: position.y };
            }}>
                <strong>{title}</strong>
                <button className={styles.close} title={text("Close", "Close")} aria-label={text("Close", "Close")}
                    onClick={() => setOpen(false)}>×</button>
            </div>
            <div className={styles.body}>
                <strong>{text("Sidewalk", "Sidewalk")}</strong>
                {toggle(value.atlasDiagnostic, text("AtlasDiagnostic", "Color-code RoadEU atlas regions"),
                    text("TipAtlasDiagnostic", "Red/yellow/blue/cyan/magenta/green per atlas region. Turn off after the screenshot."),
                    () => act(`atlasDiagnostic:${value.atlasDiagnostic ? 0 : 1}`))}
                {toggle(value.recomputeTangents, text("RecomputeTangents", "Corners/ends: recompute tangents (test)"),
                    text("TipRecomputeTangents", "Test: recomputes tangents to match the new slabs at rebuilt corners and road ends. Off by default. Rebuilds within 2 s."),
                    () => act(`recomputeTangents:${value.recomputeTangents ? 0 : 1}`))}
                <div className={styles.source}>
                    <Tooltip tooltip={text("TipJunctionMode", "0 linear (normal), 1 smoothstep, 2 t², 3 1-(1-t)², 4 no sync, 5 full, 6 paint remapped corner pieces.")}>
                        <span>{text("JunctionMode", "Junction mode")}</span>
                    </Tooltip>
                    {modes.map(mode => <button key={mode} className={value.junctionMode === mode ? styles.selected : ""}
                        aria-pressed={value.junctionMode === mode} onClick={() => act(`junctionMode:${mode}`)}>{mode}</button>)}
                </div>
                {button("exportSidewalkAtlas", text("ExportSidewalkAtlas", "Export sidewalk atlas"), text("TipExport", "Writes files to the diagnostics folder."))}
                {button("sidewalkScaleReport", text("SidewalkScaleReport", "Write sidewalk scale report"), text("TipSidewalkScaleReport", "SidewalkScale.txt: width and texture range of every sidewalk piece per road type."))}
                {button("openDiagnostics", text("OpenDiagnostics", "Open diagnostics folder"), text("TipOpenDiagnostics", "JunctionRamps.txt, JunctionWorld.txt and atlas exports."))}
                <div className={styles.separator} />
                <strong>{text("Roads", "Roads and grass")}</strong>
                {button("inspectRoad", text("InspectRoad", "Inspect road materials"), text("TipExport", "Writes files to the diagnostics folder."))}
                {button("exportGravel", text("ExportGravel", "Export gravel road materials"), text("TipExport", "Writes files to the diagnostics folder."))}
                {button("exportGrass", text("ExportGrass", "Export grass materials"), text("TipExport", "Writes files to the diagnostics folder."))}
                {button("openGrassFolder", text("OpenGrassFolder", "Open grass diagnostics folder"), text("TipOpenFolder", "Opens the folder in Explorer."))}
                <div className={styles.separator} />
                <strong>{text("Rail", "Rail")}</strong>
                {toggle(value.railProbe, text("RailProbe", "Identify track materials"),
                    text("TipRailProbe", "Magenta = TrainSubwayTrack, yellow = GravelShoulder, green = Gravel, red = rail decals, cyan = TrainRails."),
                    () => act("railProbe"))}
                {button("exportRail", text("ExportRail", "Export rail materials"), text("TipExport", "Writes files to the diagnostics folder."))}
                {button("openRailFolder", text("OpenRailFolder", "Open rail diagnostics folder"), text("TipOpenFolder", "Opens the folder in Explorer."))}
                <div className={styles.separator} />
                {button("resetModes", text("ResetModes", "Turn all test modes off"), text("TipResetModes", "Atlas colors, junction mode and rail probe back to normal."))}
            </div>
        </div>}
    </>;
}

