import type { LocComponent, LocalizedFractionProps, LocalizedNumberProps, LocalizedProps, Localization } from "cs2/l10n";
import { useLocalization } from "cs2/l10n";
import type { ModRegistrar } from "cs2/modding";
import { bindValue, useValue } from "cs2/api";
import type { FunctionComponent } from "react";

const luminaNumberFix$ = bindValue<boolean>("Advanced_Graphics", "luminaNumberFix", false);

// Keep localized numbers, signs and units; only replace unsupported spacing.
export const normalizeFractionSpacing = (value: string): string => value
    .replace(/[\u00a0\u2000-\u200b\u202f\u205f\u2060\ufeff]/g, " ")
    .replace(/[\u200e\u200f\u202a-\u202e\u2066-\u2069]/g, "");

const numericElementTypes = new Set([
    "Game.UI.Localization.LocalizedNumber",
    "Game.UI.Localization.LocalizedFraction",
    "Game.UI.Localization.LocalizedBounds",
]);

export const registerSelectedInfoFontFix: ModRegistrar = (moduleRegistry) => {
    // Tooltips can use Localized instead of rendering LocalizedNumber directly.
    const registerFormatter = <P extends object,>(
        path: string, exportName: string, shouldCorrect: (props: P) => boolean,
    ) => {
        const Original = moduleRegistry.registry.get(path)?.[exportName] as LocComponent<P> | undefined;
        if (!Original || typeof Original.renderString !== "function") {
            console.warn(`[Enhanced Graphics and Textures] Number spacing correction unavailable: ${exportName} formatter not found`);
            return;
        }
        // These localization props have no ref; retain the memo component at runtime.
        const OriginalComponent = Original as unknown as FunctionComponent<P>;

        const renderString = (localization: Localization, props: P) => {
            const value = Original.renderString(localization, props);
            return luminaNumberFix$.value && shouldCorrect(props) ? normalizeFractionSpacing(value) : value;
        };

        moduleRegistry.extend(path, exportName, () => {
            const CorrectedNumber = (props: P) => {
                const localization = useLocalization();
                const enabled = useValue(luminaNumberFix$);
                // Keep other mods' components and their reactive bindings when disabled.
                if (!enabled || !shouldCorrect(props)) return <OriginalComponent {...props} />;
                return <>{normalizeFractionSpacing(Original.renderString(localization, props))}</>;
            };
            return Object.assign(CorrectedNumber, {
                renderString,
                propsAreEqual: Original.propsAreEqual,
            });
        });
    };

    registerFormatter<LocalizedFractionProps>(
        "game-ui/common/localization/localized-fraction.tsx", "LocalizedFraction", () => true);
    registerFormatter<LocalizedNumberProps>(
        "game-ui/common/localization/localized-number.tsx", "LocalizedNumber", () => true);
    registerFormatter<LocalizedProps>(
        "game-ui/common/localization/localized.tsx", "Localized",
        props => numericElementTypes.has(props.value?.__Type));
};
