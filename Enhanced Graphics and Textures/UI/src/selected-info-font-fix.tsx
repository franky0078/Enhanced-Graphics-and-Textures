import type { LocComponent, LocalizedFractionProps, Localization } from "cs2/l10n";
import { useLocalization } from "cs2/l10n";
import type { ModRegistrar } from "cs2/modding";
import { bindValue, useValue } from "cs2/api";

const luminaNumberFix$ = bindValue<boolean>("Advanced_Graphics", "luminaNumberFix", false);

// Correct only spacing in value/total displays; keep localized numbers and units.
export const normalizeFractionSpacing = (value: string): string => value
    .replace(/[\u00a0\u2000-\u200b\u202f\u205f\u2060\ufeff]/g, " ")
    .replace(/[\u200e\u200f\u202a-\u202e\u2066-\u2069]/g, "");

export const registerSelectedInfoFontFix: ModRegistrar = (moduleRegistry) => {
    const path = "game-ui/common/localization/localized-fraction.tsx";
    const Original = moduleRegistry.registry.get(path)?.LocalizedFraction as LocComponent<LocalizedFractionProps> | undefined;
    if (!Original || typeof Original.renderString !== "function") {
        console.warn("[Enhanced Graphics and Textures] Fraction spacing correction unavailable: localized fraction formatter not found");
        return;
    }

    const renderString = (localization: Localization, props: LocalizedFractionProps) => {
        const value = Original.renderString(localization, props);
        return luminaNumberFix$.value ? normalizeFractionSpacing(value) : value;
    };

    moduleRegistry.extend(path, "LocalizedFraction", () => {
        const CorrectedFraction = (props: LocalizedFractionProps) => {
            const localization = useLocalization();
            const enabled = useValue(luminaNumberFix$);
            // Keep other mods' components and their reactive bindings when disabled.
            if (!enabled) return <Original {...props} />;
            return <>{normalizeFractionSpacing(Original.renderString(localization, props))}</>;
        };
        return Object.assign(CorrectedFraction, {
            renderString,
            propsAreEqual: Original.propsAreEqual,
        });
    });
};
