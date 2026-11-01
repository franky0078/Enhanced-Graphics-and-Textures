using System.Collections.Generic;
using Colossal;

namespace Advanced_Graphics
{
    public sealed class GraphicsLocale : IDictionarySource
    {
        private readonly Setting m_Setting;
        private readonly string m_Language;

        public GraphicsLocale(Setting setting, string language)
        {
            m_Setting = setting;
            m_Language = language;
        }

        private void AddCustomNormalEntries(Dictionary<string, string> entries)
        {
            string[] actions;
            switch (m_Language)
            {
                case "de": actions = new[] { "Textur neu laden", "Ordner öffnen", "Lädt die Textur und optionale Normalmap dieses Tabs neu. Die Einstellungen bleiben erhalten.", "Öffnet den Texturordner dieses Tabs." }; break;
                case "es": actions = new[] { "Recargar textura", "Abrir carpeta", "Recarga la textura y el mapa normal opcional de esta pestaña sin cambiar los ajustes.", "Abre la carpeta de texturas de esta pestaña." }; break;
                case "fr": actions = new[] { "Recharger la texture", "Ouvrir le dossier", "Recharge la texture et la normal map facultative de cet onglet sans modifier les réglages.", "Ouvre le dossier des textures de cet onglet." }; break;
                case "it": actions = new[] { "Ricarica texture", "Apri cartella", "Ricarica la texture e la normal map facoltativa di questa scheda senza modificare le impostazioni.", "Apre la cartella delle texture di questa scheda." }; break;
                default: actions = new[] { "Reload texture", "Open folder", "Reloads this tab's texture and optional normal map without changing its settings.", "Opens this tab's texture folder." }; break;
            }
            entries["AdvancedGraphics.UI.ReloadSurfaceTexture"] = actions[0];
            entries["AdvancedGraphics.UI.OpenSurfaceTextureFolder"] = actions[1];
            entries["AdvancedGraphics.UI.ReloadSurfaceTextureHint"] = actions[2];
            entries["AdvancedGraphics.UI.OpenSurfaceTextureFolderHint"] = actions[3];

            string hint;
            switch (m_Language)
            {
                case "de": hint = "{0}_BaseColor.png und optional {0}_Normal.png im selben Texturordner. Normalmap: RGB, OpenGL/+Y, quadratisch, maximal 4096 × 4096. Ohne gültige Normalmap bleibt die bisherige Tiefenberechnung erhalten. Neu laden liest beide Dateien. Mit eigener Normalmap: 0 % flach, 100 % Originalstärke; UV und Drehung gelten gemeinsam."; break;
                case "es": hint = "{0}_BaseColor.png y, opcionalmente, {0}_Normal.png en la misma carpeta. Normal: RGB, OpenGL/+Y, cuadrada, máximo 4096 × 4096. Sin una normal válida se conserva el cálculo anterior. Recargar lee ambos archivos. Con normal propia: 0 % plano, 100 % intensidad original; UV y rotación compartidos."; break;
                case "fr": hint = "{0}_BaseColor.png et, en option, {0}_Normal.png dans le même dossier. Normale : RGB, OpenGL/+Y, carrée, maximum 4096 × 4096. Sans normale valide, le calcul précédent est conservé. Recharger relit les deux fichiers. Avec une normale personnelle : 0 % plat, 100 % force originale ; UV et rotation communs."; break;
                case "it": hint = "{0}_BaseColor.png e, facoltativamente, {0}_Normal.png nella stessa cartella. Normale: RGB, OpenGL/+Y, quadrata, massimo 4096 × 4096. Senza una normale valida rimane il calcolo precedente. Ricaricare legge entrambi i file. Con normale personale: 0 % piatto, 100 % intensità originale; UV e rotazione comuni."; break;
                default: hint = "{0}_BaseColor.png and optional {0}_Normal.png in the same texture folder. Normal: RGB, OpenGL/+Y, square, up to 4096 × 4096. Without a valid normal map, the previous depth calculation remains. Reload reads both files. With a custom normal: 0 % flat, 100 % original strength; UV and rotation apply to both."; break;
            }
            string[] files = { "Road", "Sidewalk", "AlleySidewalk", "RoadGravel", "GravelShoulder", "RailGravel", "Grass", "Footpath" };
            string[] keys = { "CustomHint", "SidewalkCustomHint", "AlleySidewalkCustomHint", "RoadGravelCustomHint", "GravelShoulderCustomHint", "RailGravelCustomHint", "GrassCustomHint", "FootpathCustomHint" };
            string[] reload = { nameof(Setting.ReloadRoadTexture), nameof(Setting.ReloadSidewalkTexture),
                nameof(Setting.ReloadAlleySidewalkTexture), nameof(Setting.ReloadGravelTexture),
                nameof(Setting.ReloadGravelShoulderTexture), nameof(Setting.ReloadRailTexture), nameof(Setting.ReloadGrassTexture), nameof(Setting.ReloadFootpathTexture) };
            for (int i = 0; i < files.Length; i++)
            {
                string text = string.Format(hint, files[i]);
                entries["AdvancedGraphics.UI." + keys[i]] = text;
                entries[m_Setting.GetOptionDescLocaleID(reload[i])] = text;
            }
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            Mod.Graphics?.Capture();
            var detail = Mod.Graphics?.BaseDetail ?? "?";
            var shadow = Mod.Graphics?.DefaultShadowResolution?.ToString() ?? "?";
            var culling = Mod.Graphics?.BaseShadowCulling ?? "?";
            string[] t;
            switch (m_Language)
            {
                case "de":
                    t = new[] { "Enhanced Graphics & Textures", "Allgemein", "Bäume", "Details", "Cache", "Terrain", "Schatten", "Zurücksetzen",
                        "Baum-LOD-Distanz", "Standard: 100 %. Höher: detaillierte Bäume auch weiter entfernt, meist weniger FPS. Niedriger: frühere Vereinfachung, meist mehr FPS. Verbessert keine vereinfachten Modelle der Assets.",
                        "Globale Objektdetails", $"Standard: 100 % (Spielwert {detail}). Höher: mehr Details an allen Objekten einschließlich Bäumen, meist weniger FPS. Niedriger: weniger Details, meist mehr FPS. Werte über der Spielgrenze sind experimentell.",
                        "Geometrie-Cache (MB)", "Standard: 4096 MB. Höher: mehr Geometriedaten im Speicher, mehr VRAM-Bedarf; nicht automatisch bessere Grafik oder mehr FPS. Niedriger: weniger VRAM-Bedarf, bei zu wenig Cache möglicherweise Ruckler. Spielmenü bis 4096 MB, Mod bis 16384 MB.",
                        "Terrain-Tessellation", "Standard: 5. Höher: potenziell feinere Geländegeometrie, meist langsamer. Niedriger: gröberes Gelände, meist schneller. Spielmenü bis 5; höhere Werte sind experimentell und können begrenzt werden.",
                        "Terrain-Patchgröße", "Standard: 64. Kleiner: feinere Unterteilung des Terrains, oft bessere Übergänge, aber mehr Zeichenaufwand und eventuell weniger FPS. Größer: gröbere Unterteilung, weniger Zeichenaufrufe und oft mehr FPS. Spielmenü bis 64.",
                        "Schattenauflösung", $"Spielvorgabe der Schattenqualität: {shadow} Pixel. 0 = Spielvorgabe; 8192 ist eine zusätzliche Mod-Einstellung. Höher: schärfere Schatten, mehr VRAM- und GPU-Bedarf, oft weniger FPS. Niedriger: weichere oder pixeligere Schatten, oft mehr FPS. Schatten im Spielmenü aktivieren.",
                        "Schattenreichweite (m)", "Standard: Spielwert (0 = kein Eingriff). Höher: Sonnenschatten bis in größere Entfernung, oft weniger FPS. Niedriger: kürzere Schattenreichweite, oft mehr FPS. Wirkt nur bei aktivierten Schatten.",
                        "Auf Standard zurücksetzen", "Alle Werte zurücksetzen",
                        "Baum-LOD-Distanz auf 100 % zurücksetzen.", "Globale Objektdetails auf 100 % zurücksetzen.",
                        "Geometrie-Cache auf 4096 MB zurücksetzen.", "Terrain-Tessellation auf 5 zurücksetzen.",
                        "Terrain-Patchgröße auf 64 zurücksetzen.", $"Schattenauflösung auf {shadow} Pixel (Spielvorgabe der Schattenqualität; 0) zurücksetzen.",
                        "Schattenreichweite auf den Spielwert zurücksetzen (0 = kein Eingriff).",
                        "Alle Optionen auf ihre Standardwerte zurücksetzen.",
                        "Schärfe", "Kleine Schattenwerfer", $"Standard: Spielwerte (Höhe / Volumen: {culling}; 0 = kein Eingriff). Höher: mehr kleine Schatten ausgeblendet, oft schneller, aber weniger feine Schatten. Niedriger: mehr kleine Schatten, meist langsamer. Wirkt nur bei aktivierten Schatten.",
                        "TAA-Schärfe", "Standard: Kamerawert (0 = kein Eingriff). Schritte von 0,05; 0,50 entspricht HDRP-Stärke 0,50. Höher: schärferes TAA-Bild, aber mögliches Flimmern und dunkle Kanten. Niedriger: weicher. Wirkt nur mit TAA, nicht mit DLSS/FSR2.",
                        "Schwellenwert für kleine Schattenwerfer auf die Spielwerte zurücksetzen.", "TAA-Schärfe auf den ursprünglichen Kamerawert zurücksetzen." };
                    break;
                case "es":
                    t = new[] { "Enhanced Graphics & Textures", "General", "Árboles", "Detalles", "Caché", "Terreno", "Sombras", "Restablecer",
                        "Distancia LOD de árboles", "Predeterminado: 100 %. Más alto: árboles detallados más lejos, normalmente menos FPS. Más bajo: se simplifican antes, normalmente más FPS. No mejora los modelos simplificados del propio recurso.",
                        "Detalle global de objetos", $"Predeterminado: 100 % (juego {detail}). Más alto: más detalle en todos los objetos, incluidos árboles, normalmente menos FPS. Más bajo: menos detalle, normalmente más FPS. Por encima del juego es experimental.",
                        "Caché de geometría (MB)", "Predeterminado: 4096 MB. Más alto: más geometría en memoria y mayor uso de VRAM; no garantiza mejor imagen ni más FPS. Más bajo: menos VRAM, pero un caché insuficiente puede provocar tirones. Juego hasta 4096 MB; mod hasta 16384 MB.",
                        "Teselación del terreno", "Predeterminado: 5. Más alto: terreno potencialmente más detallado, normalmente más lento. Más bajo: terreno más simple, normalmente más rápido. Juego hasta 5; por encima es experimental y puede limitarse.",
                        "Tamaño de parche del terreno", "Predeterminado: 64. Más pequeño: subdivisión más fina y posibles mejores transiciones, pero más trabajo de dibujo y quizá menos FPS. Más grande: menos llamadas de dibujo y normalmente más FPS. Juego hasta 64.",
                        "Resolución de sombras", $"Valor de la calidad de sombras: {shadow} píxeles. 0 = valor del juego; 8192 es una opción adicional del mod. Más alto: sombras más nítidas, más uso de VRAM y GPU, normalmente menos FPS. Más bajo: sombras más borrosas o pixeladas, normalmente más FPS. Activa las sombras en el juego.",
                        "Distancia de sombras (m)", "Predeterminado: valor del juego (0 = sin modificar). Más alto: sombras solares visibles más lejos, normalmente menos FPS. Más bajo: menor alcance, normalmente más FPS. Requiere sombras activas.",
                        "Restablecer valor", "Restablecer todo",
                        "Restablecer la distancia LOD de árboles al 100 %.", "Restablecer el detalle global al 100 %.",
                        "Restablecer la caché de geometría a 4096 MB.", "Restablecer la teselación del terreno a 5.",
                        "Restablecer el tamaño de parche a 64.", $"Restablecer la resolución de sombras a {shadow} píxeles (valor de la calidad de sombras; 0).",
                        "Restablecer la distancia de sombras al juego (0 = sin modificar).", "Restablecer todas las opciones a sus valores predeterminados.",
                        "Nitidez", "Objetos pequeños que proyectan sombras", $"Predeterminado: juego (altura / volumen: {culling}; 0 = sin modificar). Más alto: oculta más sombras pequeñas, suele mejorar FPS, pero pierde sombras finas. Más bajo: más sombras pequeñas, suele reducir FPS. Requiere sombras activas.",
                        "Nitidez TAA", "Predeterminado: valor de la cámara (0 = sin modificar). Pasos de 0,05; 0,50 corresponde a intensidad HDRP 0,50. Más alto: imagen TAA más nítida, pero posible parpadeo y bordes oscuros. Más bajo: más suave. Solo con TAA, no DLSS/FSR2.",
                        "Restablecer los objetos pequeños que proyectan sombras al juego.", "Restablecer la nitidez TAA al valor original de la cámara." };
                    break;
                case "fr":
                    t = new[] { "Enhanced Graphics & Textures", "Général", "Arbres", "Détails", "Cache", "Terrain", "Ombres", "Réinitialiser",
                        "Distance LOD des arbres", "Défaut : 100 %. Plus élevé : arbres détaillés plus loin, généralement moins de FPS. Plus bas : simplification plus tôt, généralement plus de FPS. Ne modifie pas les modèles simplifiés des ressources.",
                        "Détails globaux des objets", $"Défaut : 100 % (jeu {detail}). Plus élevé : plus de détails sur tous les objets, arbres compris, généralement moins de FPS. Plus bas : moins de détails, généralement plus de FPS. Au-delà du jeu : expérimental.",
                        "Cache de géométrie (Mo)", "Défaut : 4096 Mo. Plus élevé : plus de géométrie en mémoire et plus de VRAM ; ni meilleure image ni plus de FPS garantis. Plus bas : moins de VRAM, mais un cache insuffisant peut causer des saccades. Jeu jusqu'à 4096 Mo ; mod jusqu'à 16384 Mo.",
                        "Tessellation du terrain", "Défaut : 5. Plus élevé : terrain potentiellement plus détaillé, généralement plus lent. Plus bas : terrain plus simple, généralement plus rapide. Jeu jusqu'à 5 ; au-delà, expérimental et parfois limité.",
                        "Taille des parcelles", "Défaut : 64. Plus petit : terrain subdivisé plus finement et transitions parfois meilleures, mais plus de rendu et parfois moins de FPS. Plus grand : moins d'appels de rendu, souvent plus de FPS. Jeu jusqu'à 64.",
                        "Résolution des ombres", $"Valeur de la qualité des ombres : {shadow} pixels. 0 = valeur du jeu ; 8192 est une option supplémentaire du mod. Plus élevé : ombres plus nettes, plus de VRAM et de GPU, souvent moins de FPS. Plus bas : ombres plus floues ou pixelisées, souvent plus de FPS. Activez les ombres dans le jeu.",
                        "Portée des ombres (m)", "Défaut : valeur du jeu (0 = aucun changement). Plus élevé : ombres du soleil plus loin, souvent moins de FPS. Plus bas : portée réduite, souvent plus de FPS. Nécessite les ombres activées.",
                        "Rétablir la valeur", "Tout réinitialiser",
                        "Rétablir la distance LOD des arbres à 100 %.", "Rétablir les détails globaux à 100 %.",
                        "Rétablir le cache de géométrie à 4096 Mo.", "Rétablir la tessellation du terrain à 5.",
                        "Rétablir la taille des parcelles à 64.", $"Rétablir la résolution des ombres à {shadow} pixels (valeur de la qualité des ombres ; 0).",
                        "Rétablir la portée des ombres au jeu (0 = aucun changement).", "Rétablir toutes les options à leurs valeurs par défaut.",
                        "Netteté", "Petits objets projetant des ombres", $"Défaut : jeu (hauteur / volume : {culling} ; 0 = aucun changement). Plus élevé : masque plus de petites ombres, souvent plus rapide, mais moins de détails. Plus bas : plus de petites ombres, souvent plus lent. Nécessite les ombres activées.",
                        "Netteté TAA", "Défaut : valeur de la caméra (0 = aucun changement). Pas de 0,05 ; 0,50 correspond à une force HDRP de 0,50. Plus élevé : image TAA plus nette, mais scintillement et contours sombres possibles. Plus bas : plus douce. TAA uniquement, sans DLSS/FSR2.",
                        "Rétablir le seuil des petits objets projetant des ombres au jeu.", "Rétablir la netteté TAA à la valeur initiale de la caméra." };
                    break;
                case "it":
                    t = new[] { "Enhanced Graphics & Textures", "Generale", "Alberi", "Dettagli", "Cache", "Terreno", "Ombre", "Ripristina",
                        "Distanza LOD degli alberi", "Predefinito: 100%. Più alto: alberi dettagliati più lontano, di solito meno FPS. Più basso: semplificazione anticipata, di solito più FPS. Non migliora i modelli semplificati degli asset.",
                        "Dettagli globali degli oggetti", $"Predefinito: 100% (gioco {detail}). Più alto: più dettaglio per tutti gli oggetti, alberi inclusi, di solito meno FPS. Più basso: meno dettaglio, di solito più FPS. Oltre il limite del gioco è sperimentale.",
                        "Cache geometria (MB)", "Predefinito: 4096 MB. Più alto: più geometria in memoria e più VRAM; qualità e FPS non migliorano automaticamente. Più basso: meno VRAM, ma una cache insufficiente può causare scatti. Gioco fino a 4096 MB; mod fino a 16384 MB.",
                        "Tessellazione terreno", "Predefinito: 5. Più alto: geometria del terreno potenzialmente più fine, di solito più lento. Più basso: terreno più semplice, di solito più veloce. Gioco fino a 5; oltre è sperimentale e può essere limitato.",
                        "Dimensione patch terreno", "Predefinito: 64. Più piccolo: suddivisione più fine e possibili transizioni migliori, ma più lavoro di disegno e forse meno FPS. Più grande: meno chiamate di disegno, spesso più FPS. Gioco fino a 64.",
                        "Risoluzione ombre", $"Valore della qualità ombre: {shadow} pixel. 0 = valore del gioco; 8192 è un’impostazione aggiuntiva del mod. Più alto: ombre più nitide, più VRAM e GPU, spesso meno FPS. Più basso: ombre più sfocate o pixelate, spesso più FPS. Attiva le ombre nel gioco.",
                        "Distanza ombre (m)", "Predefinito: valore del gioco (0 = nessuna modifica). Più alto: ombre solari più lontane, spesso meno FPS. Più basso: distanza minore, spesso più FPS. Richiede le ombre attive.",
                        "Ripristina valore", "Ripristina tutto",
                        "Ripristina la distanza LOD degli alberi al 100%.", "Ripristina i dettagli globali al 100%.",
                        "Ripristina la cache geometria a 4096 MB.", "Ripristina la tessellazione terreno a 5.",
                        "Ripristina la dimensione patch a 64.", $"Ripristina la risoluzione ombre a {shadow} pixel (valore della qualità ombre; 0).",
                        "Ripristina la distanza ombre al gioco (0 = nessuna modifica).", "Ripristina tutte le opzioni ai valori predefiniti.",
                        "Nitidezza", "Piccoli oggetti che proiettano ombre", $"Predefinito: gioco (altezza / volume: {culling}; 0 = nessuna modifica). Più alto: esclude più ombre piccole, spesso più FPS, ma meno dettagli. Più basso: più ombre piccole, spesso meno FPS. Richiede le ombre attive.",
                        "Nitidezza TAA", "Predefinito: valore della camera (0 = nessuna modifica). Passi di 0,05; 0,50 equivale a intensità HDRP 0,50. Più alto: immagine TAA più nitida, ma possibili sfarfallii e contorni scuri. Più basso: più morbida. Solo con TAA, non DLSS/FSR2.",
                        "Ripristina la soglia per le ombre piccole al valore del gioco.", "Ripristina la nitidezza TAA al valore iniziale della camera." };
                    break;
                default:
                    t = new[] { "Enhanced Graphics & Textures", "General", "Trees", "Details", "Cache", "Terrain", "Shadows", "Reset",
                        "Tree LOD distance", "Default: 100%. Higher: detailed trees farther away, usually fewer FPS. Lower: trees simplify sooner, usually more FPS. Cannot improve an asset's simplified model.",
                        "Global object detail", $"Default: 100% (game value {detail}). Higher: more detail on all objects including trees, usually fewer FPS. Lower: less detail, usually more FPS. Beyond the game range is experimental.",
                        "Geometry cache (MB)", "Default: 4096 MB. Higher: more geometry kept in memory and more VRAM used; neither better image quality nor more FPS is guaranteed. Lower: less VRAM, but too little cache may cause stutter. Game menu up to 4096 MB; mod up to 16384 MB.",
                        "Terrain tessellation", "Default: 5. Higher: potentially finer terrain geometry, usually slower. Lower: simpler terrain, usually faster. Game menu up to 5; higher values are experimental and may be clamped.",
                        "Terrain patch size", "Default: 64. Smaller: finer terrain subdivision and possibly better transitions, but more draw work and possibly fewer FPS. Larger: fewer draw calls, often more FPS. Game menu up to 64.",
                        "Shadow resolution", $"Shadow quality default: {shadow} pixels. 0 = game default; 8192 is an additional mod setting. Higher: sharper shadows, more VRAM and GPU work, often fewer FPS. Lower: softer or more pixelated shadows, often more FPS. Enable shadows in game options.",
                        "Shadow distance (m)", "Default: game value (0 = no override). Higher: sun shadows visible farther away, often fewer FPS. Lower: shorter reach, often more FPS. Requires shadows enabled in game options.",
                        "Restore default", "Reset all values",
                        "Restore tree LOD distance to 100%.", "Restore global object detail to 100%.",
                        "Restore geometry cache to 4096 MB.", "Restore terrain tessellation to 5.",
                        "Restore terrain patch size to 64.", $"Restore shadow resolution to {shadow} pixels (shadow quality default; 0).",
                        "Restore shadow distance to the game value (0 = no override).", "Restore every option to its default value.",
                        "Sharpness", "Small shadow casters", $"Default: game values (height / volume: {culling}; 0 = no override). Higher: culls more small shadows, often faster but loses fine shadows. Lower: keeps more small shadows, usually slower. Requires shadows enabled.",
                        "TAA sharpness", "Default: camera value (0 = no override). Steps of 0.05; 0.50 equals HDRP strength 0.50. Higher: sharper TAA image but possible flicker and dark outlines. Lower: softer. Only affects TAA, not DLSS/FSR2.",
                        "Restore small shadow-caster thresholds to game values.", "Restore TAA sharpness to the camera's original value." };
                    break;
            }

            var entries = new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), t[0] },
                { m_Setting.GetOptionTabLocaleID(Setting.MainSection), t[1] }
            };
            string aboutLabel;
            string versionLabel;
            string versionDescription;
            switch (m_Language)
            {
                case "de":
                    aboutLabel = "Über die Mod";
                    versionLabel = "Version";
                    versionDescription = "Version der aktuell geladenen Enhanced Graphics and Textures DLL.";
                    break;
                case "es":
                    aboutLabel = "Acerca del mod";
                    versionLabel = "Versión";
                    versionDescription = "Versión de la DLL de Enhanced Graphics and Textures cargada actualmente.";
                    break;
                case "fr":
                    aboutLabel = "À propos du mod";
                    versionLabel = "Version";
                    versionDescription = "Version de la DLL Enhanced Graphics and Textures actuellement chargée.";
                    break;
                case "it":
                    aboutLabel = "Informazioni sulla mod";
                    versionLabel = "Versione";
                    versionDescription = "Versione della DLL di Enhanced Graphics and Textures attualmente caricata.";
                    break;
                default:
                    aboutLabel = "About the mod";
                    versionLabel = "Version";
                    versionDescription = "Version of the currently loaded Enhanced Graphics and Textures DLL.";
                    break;
            }
            entries.Add(m_Setting.GetOptionGroupLocaleID(Setting.AboutGroup), aboutLabel);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.Version)), versionLabel);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.Version)), versionDescription);

            var groups = new[] { Setting.TreeGroup, Setting.DetailGroup, Setting.CacheGroup,
                Setting.TerrainGroup, Setting.ShadowsGroup, Setting.ResetGroup };
            for (var i = 0; i < groups.Length; i++)
                entries.Add(m_Setting.GetOptionGroupLocaleID(groups[i]), t[2 + i]);
            entries.Add(m_Setting.GetOptionGroupLocaleID(Setting.SharpnessGroup), t[32]);
            var controls = new[] { nameof(Setting.TreeLodDistance), nameof(Setting.GlobalDetailScalePercent),
                nameof(Setting.GeometryCacheSizeMB), nameof(Setting.TerrainTessellationLevel),
                nameof(Setting.TerrainPatchSizeUnits), nameof(Setting.ShadowResolution),
                nameof(Setting.ShadowDistanceMeters) };
            for (var i = 0; i < controls.Length; i++)
            {
                entries.Add(m_Setting.GetOptionLabelLocaleID(controls[i]), t[8 + i * 2]);
                entries.Add(m_Setting.GetOptionDescLocaleID(controls[i]), t[9 + i * 2]);
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.SmallShadowCasterThreshold)), t[33]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.SmallShadowCasterThreshold)), t[34]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.TaaSharpenStrength)), t[35]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.TaaSharpenStrength)), t[36]);
            var resets = new[] { nameof(Setting.ResetTree), nameof(Setting.ResetDetail),
                nameof(Setting.ResetCache), nameof(Setting.ResetTessellation),
                nameof(Setting.ResetPatchSize), nameof(Setting.ResetShadowResolution),
                nameof(Setting.ResetShadowDistance) };
            for (var i = 0; i < resets.Length; i++)
            {
                var reset = resets[i];
                entries.Add(m_Setting.GetOptionLabelLocaleID(reset), t[22]);
                entries.Add(m_Setting.GetOptionDescLocaleID(reset), t[24 + i]);
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetAll)), t[23]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.ResetAll)), t[31]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetSmallShadows)), t[22]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.ResetSmallShadows)), t[37]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetTaaSharpen)), t[22]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.ResetTaaSharpen)), t[38]);
            string[] road;
            switch (m_Language)
            {
                case "de":
                    road = new[] { "Straßenmaterialien", "Straßenmaterialien prüfen",
                        "Liest einmalig die geladenen Fahrbahnmaterialien, Shader und Textur-Slots aus und schreibt sie ins Spielprotokoll. Eine Stadt muss geladen sein. Diese Diagnose verändert keine Straßen." };
                    break;
                case "es":
                    road = new[] { "Materiales de carretera", "Inspeccionar materiales de carretera",
                        "Registra una vez los materiales de calzada cargados, sus shaders y texturas. Debe haber una ciudad cargada. No modifica las carreteras." };
                    break;
                case "fr":
                    road = new[] { "Matériaux routiers", "Inspecter les matériaux routiers",
                        "Consigne une fois les matériaux de chaussée chargés, leurs shaders et textures. Chargez une ville au préalable. Ne modifie pas les routes." };
                    break;
                case "it":
                    road = new[] { "Materiali stradali", "Analizza materiali stradali",
                        "Registra una volta i materiali della carreggiata caricati, i loro shader e le texture. Apri prima una città. Non modifica le strade." };
                    break;
                default:
                    road = new[] { "Road materials", "Inspect road materials",
                        "Logs loaded carriageway materials, shaders and texture slots once. Open a city first. This inspection does not change roads." };
                    break;
            }
            entries.Add(m_Setting.GetOptionTabLocaleID(Setting.RoadSection), road[0]);
            entries.Add(m_Setting.GetOptionGroupLocaleID(Setting.RoadGroup), road[0]);
            string[] roadGroups;
            switch (m_Language)
            {
                case "de": roadGroups = new[] { "UV-Größe und Tiefenwirkung", "Risse und Kachelung" }; break;
                case "es": roadGroups = new[] { "Tamaño UV y relieve", "Grietas y repetición" }; break;
                case "fr": roadGroups = new[] { "Taille UV et relief", "Fissures et répétition" }; break;
                case "it": roadGroups = new[] { "Dimensione UV e rilievo", "Crepe e ripetizione" }; break;
                default: roadGroups = new[] { "UV size and depth", "Cracks and tiling" }; break;
            }
            entries.Add(m_Setting.GetOptionGroupLocaleID(Setting.RoadUvGroup), roadGroups[0]);
            entries.Add(m_Setting.GetOptionGroupLocaleID(Setting.RoadCrackGroup), roadGroups[1]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.InspectRoadMaterials)), road[1]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.InspectRoadMaterials)), road[2]);
            string[] asphalt;
            switch (m_Language)
            {
                case "de":
                    asphalt = new[] { "Asphaltstruktur", "0 = Originaltextur; 1 = gleichmäßige generierte Asphaltkörnung; 2 = eigene Road_BaseColor.png. Bei aktivierten Asphalt-Anpassungen wirken UV, Tiefenwirkung und Risse mit jeder Textur.",
                        "UV-Größe", "100 % = ursprüngliche Mustergröße; bis 3000 %. Höher vergrößert die Asphaltkörnung. Die Rissgröße wird separat eingestellt.",
                        "Texturkachelung", "0,1 bis 8,0 in 0,1-Schritten. Eigene Rissmuster: Dichtefaktor 0,25 bei 1; sanfter Anstieg auf 0,60 bei 8. Unter 1 werden weniger Risse verteilt. Die Asphalt-Korngröße bleibt unverändert.",
                        "UV-Versatz X", "Verschiebt das gekachelte Asphaltmuster horizontal. 100 % entspricht wieder 0 %.",
                        "UV-Versatz Y", "Verschiebt das gekachelte Asphaltmuster vertikal. 100 % entspricht wieder 0 %.",
                        "UV-Drehung", "Dreht das Asphaltmuster und seine Tiefenwirkung von 0 bis 360 Grad in Schritten von 1 Grad.",
                        "Asphalt zurücksetzen", "Stellt die originalen Straßentexturen und alle Asphaltregler wieder her." };
                    break;
                case "es":
                    asphalt = new[] { "Detalle del asfalto", "0 = textura original; 1 = asfalto generado; 2 = Road_BaseColor.png propia. Si los ajustes están activados, UV, relieve y grietas funcionan en todos los niveles.",
                        "Tamaño UV", "100 % = tamaño original; hasta 3000 %. Aumenta el grano del asfalto; el tamaño de las grietas es independiente.",
                        "Repetición de textura", "De 0,1 a 8,0 en pasos de 0,1. Patrones propios: factor de densidad 0,25 en 1; aumento suave hasta 0,60 en 8. Por debajo de 1 hay menos grietas. La grana del asfalto no cambia.",
                        "Desplazamiento UV X", "Desplaza el patrón horizontalmente; 100 % equivale a 0 %.",
                        "Desplazamiento UV Y", "Desplaza el patrón verticalmente; 100 % equivale a 0 %.",
                        "Rotación UV", "Gira el patrón y su relieve de 0 a 360 grados en pasos de 1 grado.",
                        "Restablecer asfalto", "Restaura las texturas originales y todos los controles del asfalto." };
                    break;
                case "fr":
                    asphalt = new[] { "Détails de l’asphalte", "0 = texture d’origine ; 1 = asphalte généré ; 2 = votre Road_BaseColor.png. Quand les réglages sont actifs, UV, relief et fissures fonctionnent à tous les niveaux.",
                        "Taille UV", "100 % = taille d’origine ; jusqu’à 3000 %. Agrandit le grain ; la taille des fissures se règle séparément.",
                        "Répétition de texture", "De 0,1 à 8,0 par pas de 0,1. Motifs personnalisés : densité 0,25 à 1, augmentant doucement jusqu’à 0,60 à 8. Sous 1, moins de fissures. Le grain de l’asphalte reste inchangé.",
                        "Décalage UV X", "Décale le motif horizontalement ; 100 % revient à 0 %.",
                        "Décalage UV Y", "Décale le motif verticalement ; 100 % revient à 0 %.",
                        "Rotation UV", "Fait pivoter le motif et son relief de 0 à 360 degrés par pas de 1 degré.",
                        "Rétablir l’asphalte", "Restaure les textures d’origine et tous les réglages d’asphalte." };
                    break;
                case "it":
                    asphalt = new[] { "Dettaglio asfalto", "0 = texture originale; 1 = asfalto generato; 2 = Road_BaseColor.png personale. Con le regolazioni attive, UV, rilievo e crepe funzionano a tutti i livelli.",
                        "Dimensione UV", "100% = dimensione originale; fino a 3000%. Ingrandisce la grana; la dimensione delle crepe è separata.",
                        "Ripetizione texture", "Da 0,1 a 8,0 con passi di 0,1. Motivi personalizzati: densità 0,25 a 1, aumentando gradualmente fino a 0,60 a 8. Sotto 1 ci sono meno crepe. La grana dell’asfalto non cambia.",
                        "Spostamento UV X", "Sposta il motivo in orizzontale; 100% equivale a 0%.",
                        "Spostamento UV Y", "Sposta il motivo in verticale; 100% equivale a 0%.",
                        "Rotazione UV", "Ruota il motivo e il rilievo da 0 a 360 gradi in passi di 1 grado.",
                        "Ripristina asfalto", "Ripristina le texture originali e tutte le regolazioni dell’asfalto." };
                    break;
                default:
                    asphalt = new[] { "Asphalt detail", "0 = original texture; 1 = uniform generated asphalt grain; 2 = your Road_BaseColor.png. With asphalt adjustments enabled, UV, depth, and cracks work at every level.",
                        "UV size", "100% = original pattern size; up to 3000%. Enlarges asphalt grain; crack size is controlled separately.",
                        "Texture tiling", "0.1 to 8.0 in 0.1 steps. Custom patterns: density factor 0.25 at 1, rising gradually to 0.60 at 8. Below 1, fewer cracks are placed. Asphalt grain size is unchanged.",
                        "UV offset X", "Shifts the asphalt pattern horizontally. 100% wraps back to 0%.",
                        "UV offset Y", "Shifts the asphalt pattern vertically. 100% wraps back to 0%.",
                        "UV rotation", "Rotates the pattern and its surface depth from 0 to 360 degrees in 1-degree steps.",
                        "Reset asphalt", "Restores the original street textures and all asphalt controls." };
                    break;
            }
            string[] roadEnabled;
            switch (m_Language)
            {
                case "de": roadEnabled = new[] { "Asphalt-Anpassungen aktivieren", "Aktiviert alle Asphaltregler auch für die Originaltextur. Aus stellt die ursprünglichen Straßentexturen und Normalmaps wieder her, ohne deine Werte zu löschen." }; break;
                case "es": roadEnabled = new[] { "Activar ajustes del asfalto", "Activa todos los ajustes también con la textura original. Al desactivar, se restauran las texturas y normales originales sin perder los valores." }; break;
                case "fr": roadEnabled = new[] { "Activer les réglages de l’asphalte", "Active tous les réglages avec la texture d’origine. Désactiver restaure les textures et normales d’origine sans effacer les valeurs." }; break;
                case "it": roadEnabled = new[] { "Attiva le regolazioni dell’asfalto", "Attiva tutte le regolazioni anche con la texture originale. Disattivando si ripristinano texture e normali originali senza cancellare i valori." }; break;
                default: roadEnabled = new[] { "Enable asphalt adjustments", "Applies all asphalt controls to every texture, including the original. Turning this off restores the original road textures and normal maps without deleting your values." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.RoadAsphaltEnabled)), roadEnabled[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.RoadAsphaltEnabled)), roadEnabled[1]);
            entries.Add("AdvancedGraphics.UI.Enabled", roadEnabled[0]);
            entries.Add("AdvancedGraphics.UI.TipEnabled", roadEnabled[1]);
            var roadSettings = new[] { nameof(Setting.RoadAsphaltDetail), nameof(Setting.RoadUvSizePercent), nameof(Setting.RoadTextureTiling),
                nameof(Setting.RoadUvOffsetX), nameof(Setting.RoadUvOffsetY), nameof(Setting.RoadUvRotation),
                nameof(Setting.ResetRoadAsphalt) };
            for (var i = 0; i < roadSettings.Length; i++)
            {
                entries.Add(m_Setting.GetOptionLabelLocaleID(roadSettings[i]), asphalt[i * 2]);
                entries.Add(m_Setting.GetOptionDescLocaleID(roadSettings[i]), asphalt[i * 2 + 1]);
            }
            string[] source;
            switch (m_Language)
            {
                case "de": source = new[] { "Straßentextur", "Generiert oder eigene Road_BaseColor.png. Optional kann Road_Normal.png verwendet werden; andernfalls bleibt die automatische Tiefenberechnung erhalten. Asphaltstruktur muss mindestens 1 sein.", "Generiert", "Eigene Textur" }; break;
                case "es": source = new[] { "Textura de carretera", "Generada o Road_BaseColor.png propia. Road_Normal.png es opcional; sin él se conserva el cálculo automático del relieve. El detalle debe ser 1 o superior.", "Generada", "Textura propia" }; break;
                case "fr": source = new[] { "Texture de route", "Générée ou votre Road_BaseColor.png. Road_Normal.png est facultatif ; sinon le relief est calculé automatiquement. Le niveau de détail doit être au moins 1.", "Générée", "Texture personnalisée" }; break;
                case "it": source = new[] { "Texture stradale", "Generata o Road_BaseColor.png personalizzata. Road_Normal.png è facoltativa; altrimenti il rilievo viene calcolato automaticamente. Il dettaglio deve essere almeno 1.", "Generata", "Texture personalizzata" }; break;
                default: source = new[] { "Road texture", "Generated or custom Road_BaseColor.png. Optional Road_Normal.png is used when present; otherwise the automatic depth calculation remains. Asphalt detail must be at least 1.", "Generated", "Custom texture" }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.AsphaltSource)), source[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.AsphaltSource)), source[1]);
            entries.Add(m_Setting.GetEnumValueLocaleID(Setting.RoadTextureSource.Generated), source[2]);
            entries.Add(m_Setting.GetEnumValueLocaleID(Setting.RoadTextureSource.Custom), source[3]);
            string[] textureFiles;
            switch (m_Language)
            {
                case "de": textureFiles = new[] { "Texturordner öffnen", "Öffnet den lokalen Ordner für Road_BaseColor.png. Kopiere deine eigene PNG hinein; sie wird nicht mit der Mod veröffentlicht.", "Eigene Textur neu laden", "Lädt Road_BaseColor.png erneut, nachdem du die Datei kopiert oder ersetzt hast. Asphaltstruktur muss auf 2 stehen.", "Stufe 2: Road_BaseColor.png im Texturordner der Mod-Optionen ablegen und danach neu laden." }; break;
                case "es": textureFiles = new[] { "Abrir carpeta de texturas", "Abre la carpeta local para Road_BaseColor.png. El PNG propio no se distribuye con el mod.", "Recargar textura propia", "Vuelve a leer Road_BaseColor.png. Selecciona el nivel 2.", "Nivel 2: coloca Road_BaseColor.png en la carpeta abierta desde las opciones y vuelve a cargarlo." }; break;
                case "fr": textureFiles = new[] { "Ouvrir le dossier des textures", "Ouvre le dossier local pour Road_BaseColor.png. Votre PNG n’est pas distribué avec le mod.", "Recharger la texture personnelle", "Relit Road_BaseColor.png. Choisissez le niveau 2.", "Niveau 2 : placez Road_BaseColor.png dans le dossier ouvert depuis les options, puis rechargez-le." }; break;
                case "it": textureFiles = new[] { "Apri cartella texture", "Apre la cartella locale per Road_BaseColor.png. Il Texture personalizzata non viene distribuito con la mod.", "Ricarica texture personale", "Rilegge Road_BaseColor.png. Seleziona il livello 2.", "Livello 2: copia Road_BaseColor.png nella cartella aperta dalle opzioni e ricaricala." }; break;
                default: textureFiles = new[] { "Open texture folder", "Opens the local folder for Road_BaseColor.png. Your PNG is not distributed with the mod.", "Reload custom texture", "Reads Road_BaseColor.png again after copying or replacing the file. Select asphalt detail level 2.", "Level 2: put Road_BaseColor.png in the folder opened from mod options, then reload it." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenRoadTextureFolder)), textureFiles[0]);
            string[] grassSurvey;
            switch (m_Language)
            {
                case "de": grassSurvey = new[] { "Gehweg-Gras untersuchen", "Nach dem Laden einer Stadt mit Grasstreifen die Materialdaten und Texturvorschauen nach Textures/Grass/Diagnostics exportieren. Verändert keine Materialien. Ein einmaliger kurzer Ruckler ist möglich.", "Gras-Diagnoseordner öffnen", "Öffnet Textures/Grass/Diagnostics mit GrassMaterials.txt und den exportierten PNG-Vorschauen." }; break;
                case "es": grassSurvey = new[] { "Examinar césped de las aceras", "Tras cargar una ciudad con franjas de césped, exporta materiales y vistas previas a Textures/Grass/Diagnostics sin modificarlos. Puede producir una breve pausa.", "Abrir diagnóstico del césped", "Abre Textures/Grass/Diagnostics con GrassMaterials.txt y las vistas previas PNG." }; break;
                case "fr": grassSurvey = new[] { "Examiner l’herbe des trottoirs", "Après le chargement d’une ville avec des bandes d’herbe, exporte les matériaux et aperçus dans Textures/Grass/Diagnostics sans les modifier. Une courte pause est possible.", "Ouvrir le diagnostic de l’herbe", "Ouvre Textures/Grass/Diagnostics avec GrassMaterials.txt et les aperçus PNG." }; break;
                case "it": grassSurvey = new[] { "Esamina erba dei marciapiedi", "Dopo aver caricato una città con fasce erbose, esporta materiali e anteprime in Textures/Grass/Diagnostics senza modificarli. È possibile una breve pausa.", "Apri diagnosi erba", "Apre Textures/Grass/Diagnostics con GrassMaterials.txt e le anteprime PNG." }; break;
                default: grassSurvey = new[] { "Inspect roadside grass", "After loading a city with grass strips, exports materials and texture previews to Textures/Grass/Diagnostics without modifying them. A brief pause may occur.", "Open grass diagnostic folder", "Opens Textures/Grass/Diagnostics with GrassMaterials.txt and the exported PNG previews." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.ExportGrassMaterials)), grassSurvey[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.ExportGrassMaterials)), grassSurvey[1]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenGrassDiagnosticFolder)), grassSurvey[2]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenGrassDiagnosticFolder)), grassSurvey[3]);
            string[] gravelSurvey;
            switch (m_Language)
            {
                case "de": gravelSurvey = new[] { "Schotter-Straßenmaterialien exportieren", "Nach dem Laden einer Stadt mit Schotterstraßen Fahrbahn (Schotter) und Rand (GravelShoulder) samt Shaderwerten und Texturen nach ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel/Diagnostics exportieren." }; break;
                case "es": gravelSurvey = new[] { "Exportar materiales de calles de grava", "Después de cargar una ciudad con calles de grava, exporta Gravel y GravelShoulder a ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel/Diagnostics." }; break;
                case "fr": gravelSurvey = new[] { "Exporter les matériaux des routes en gravier", "Après avoir chargé une ville avec des routes en gravier, exporte Gravel et GravelShoulder dans ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel/Diagnostics." }; break;
                case "it": gravelSurvey = new[] { "Esporta materiali delle strade sterrate", "Dopo aver caricato una città con strade sterrate, esporta Gravel e GravelShoulder in ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel/Diagnostics." }; break;
                default: gravelSurvey = new[] { "Export gravel road materials", "After loading a city with gravel roads, export Gravel and GravelShoulder shader values and textures to ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel/Diagnostics." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.ExportGravelRoadMaterials)), gravelSurvey[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.ExportGravelRoadMaterials)), gravelSurvey[1]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenRoadTextureFolder)), textureFiles[1]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.ReloadRoadTexture)), textureFiles[2]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.ReloadRoadTexture)), textureFiles[3]);
            entries.Add("AdvancedGraphics.UI.CustomHint", textureFiles[4]);
            string[] depth;
            switch (m_Language)
            {
                case "de": depth = new[] { "Tiefenwirkung", "0 = originale Straßen-Normalmap. 100 bis 3000 verstärkt Asphalt- und Rissrelief passend zur UV-Größe bei allen Texturstufen. Keine echte Geometrie oder Parallax-Verdeckung." }; break;
                case "es": depth = new[] { "Relieve", "0 = normal original. De 100 a 3000 refuerza el relieve y las grietas con todas las texturas. No añade geometría ni paralaje real." }; break;
                case "fr": depth = new[] { "Relief", "0 = normale d’origine. De 100 à 3000 accentue le relief et les fissures avec toutes les textures. Sans géométrie ni parallaxe réelle." }; break;
                case "it": depth = new[] { "Rilievo", "0 = normale originale. Da 100 a 3000 accentua grana e crepe su tutte le texture. Nessuna geometria o parallasse reale." }; break;
                default: depth = new[] { "Surface depth", "0 = original road normal. 100 to 3000 strengthens asphalt and crack relief at every texture level. No geometry or true parallax occlusion." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.RoadDepthStrengthPercent)), depth[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.RoadDepthStrengthPercent)), depth[1]);
            string[] cracks;
            switch (m_Language)
            {
                case "de": cracks = new[] { "Rissanzahl", "0 = keine zusätzlichen Risse. Bis zu 1200 Risse bei allen Asphaltstufen. Kachelung erhöht die Verteilung auf maximal 2400 Risse; mehr Risse verlängern die Neuberechnung.",
                    "Rissgröße", "Länge zusätzlicher Risse: 25 % = fein, 100 % = normal, bis 1800 % = sehr groß. Unabhängig von der UV-Größe; bei allen Stufen mit Rissanzahl über 0.",
                    "Risszufall", "0 = gleichmäßig verteilte, ruhigere Risse; 100 = unregelmäßigere Positionen und Verläufe. Die Anordnung bleibt bei gleichen Einstellungen reproduzierbar." }; break;
                case "es": cracks = new[] { "Cantidad de grietas", "0 = ninguna. Hasta 1200 en todos los niveles; la repetición aumenta la distribución hasta un máximo de 2400. Más grietas requieren más tiempo.",
                    "Tamaño de grietas", "Longitud: 25 % = finas, 100 % = normal, hasta 1800 % = muy grandes. Independiente del tamaño UV.",
                    "Aleatoriedad de grietas", "0 = distribución más regular; 100 = posiciones y formas más irregulares. Los mismos ajustes producen el mismo patrón." }; break;
                case "fr": cracks = new[] { "Nombre de fissures", "0 = aucune. Jusqu’à 1200 à tous les niveaux ; la répétition augmente la répartition jusqu’à 2400. Plus de fissures augmentent le calcul.",
                    "Taille des fissures", "Longueur : 25 % = fines, 100 % = normal, jusqu’à 1800 % = très grandes. Indépendante de la taille UV.",
                    "Aléa des fissures", "0 = répartition plus régulière ; 100 = positions et tracés plus irréguliers. Les mêmes réglages reproduisent le même motif." }; break;
                case "it": cracks = new[] { "Numero di crepe", "0 = nessuna. Fino a 1200 su tutti i livelli; la ripetizione aumenta la distribuzione fino a 2400. Più crepe richiedono più calcolo.",
                    "Dimensione crepe", "Lunghezza: 25% = sottili, 100% = normale, fino a 1800% = molto grandi. Indipendente dalla dimensione UV.",
                    "Casualità crepe", "0 = distribuzione più regolare; 100 = posizioni e tracciati più irregolari. Le stesse impostazioni riproducono lo stesso motivo." }; break;
                default: cracks = new[] { "Crack count", "0 = no extra cracks. Up to 1200 on every asphalt level. Tiling increases distribution up to 2400 cracks; more cracks take longer to rebuild.",
                    "Crack size", "Length: 25% = fine, 100% = normal, up to 1800% = very large. Independent of UV size; works on all levels when count is above 0.",
                    "Crack randomness", "0 = more regular placement and calmer paths; 100 = more irregular positions and paths. Identical settings reproduce the same pattern." }; break;
            }
            var crackSettings = new[] { nameof(Setting.RoadCrackCount), nameof(Setting.RoadCrackSizePercent),
                nameof(Setting.RoadCrackRandomnessPercent) };
            for (var i = 0; i < crackSettings.Length; i++)
            {
                entries.Add(m_Setting.GetOptionLabelLocaleID(crackSettings[i]), cracks[i * 2]);
                entries.Add(m_Setting.GetOptionDescLocaleID(crackSettings[i]), cracks[i * 2 + 1]);
            }
            string[] width;
            switch (m_Language)
            {
                case "de": width = new[] { "Rissbreite", "25 % = schmal, 100 % = bisherige Breite, bis 1200 % = breiter. Wirkt auf zusätzliche Risse bei allen Stufen; größere Breite kann die Neuberechnung verlangsamen." }; break;
                case "es": width = new[] { "Ancho de grietas", "25 % = estrecho, 100 % = ancho anterior, hasta 1200 % = más ancho. Afecta a las grietas añadidas en todos los niveles." }; break;
                case "fr": width = new[] { "Largeur des fissures", "25 % = fin, 100 % = largeur précédente, jusqu’à 1200 % = plus large. Agit sur les fissures ajoutées à tous les niveaux." }; break;
                case "it": width = new[] { "Larghezza crepe", "25% = strette, 100% = larghezza precedente, fino a 1200% = più larghe. Agisce sulle crepe aggiunte a tutti i livelli." }; break;
                default: width = new[] { "Crack width", "25% = narrow, 100% = previous width, up to 1200% = wider. Affects cracks added to any asphalt level; wider cracks can take longer to rebuild." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.RoadCrackWidthPercent)), width[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.RoadCrackWidthPercent)), width[1]);
            string[] crackOffset;
            switch (m_Language)
            {
                case "de": crackOffset = new[] { "Rissversatz X", "Rissversatz Y", "Verschiebt nur zusätzliche Straßenrisse entlang der Texturachse. 100 % entspricht einer ganzen Kachel. Straßentextur und Rissform bleiben unverändert; eigene Riss-Normalmaps werden mitverschoben." }; break;
                case "es": crackOffset = new[] { "Desplazamiento X de grietas", "Desplazamiento Y de grietas", "Desplaza solo las grietas añadidas a lo largo del eje de textura. 100 % equivale a una repetición completa. Conserva la textura vial y la forma de las grietas; desplaza también sus mapas normales." }; break;
                case "fr": crackOffset = new[] { "Décalage X des fissures", "Décalage Y des fissures", "Décale uniquement les fissures ajoutées le long de l’axe de texture. 100 % correspond à une tuile complète. Préserve la texture routière et la forme des fissures ; décale aussi leurs normal maps." }; break;
                case "it": crackOffset = new[] { "Spostamento X delle crepe", "Spostamento Y delle crepe", "Sposta solo le crepe aggiunte lungo l’asse della texture. 100% equivale a una ripetizione completa. Mantiene la texture stradale e la forma delle crepe; sposta anche le loro normal map." }; break;
                default: crackOffset = new[] { "Crack offset X", "Crack offset Y", "Shifts only added road cracks along the texture axis. 100% equals one full tile. Preserves road texture and crack shapes; custom crack normal maps move with the cracks." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.RoadCrackOffsetX)), crackOffset[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.RoadCrackOffsetX)), crackOffset[2]);
            entries.Add("AdvancedGraphics.UI.CrackOffsetX", crackOffset[0]);
            entries.Add("AdvancedGraphics.UI.TipCrackOffsetX", crackOffset[2]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.RoadCrackOffsetY)), crackOffset[1]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.RoadCrackOffsetY)), crackOffset[2]);
            entries.Add("AdvancedGraphics.UI.CrackOffsetY", crackOffset[1]);
            entries.Add("AdvancedGraphics.UI.TipCrackOffsetY", crackOffset[2]);
            string[] crackRotation;
            switch (m_Language)
            {
                case "de": crackRotation = new[] { "Rissdrehung", "Dreht zusätzliche Straßenrisse um ihren jeweiligen Startpunkt, unabhängig von der Straßentextur. 0° = bisherige Ausrichtung; 360° = 0°. Eigene Riss-Normalmaps werden mitgedreht." }; break;
                case "es": crackRotation = new[] { "Rotación de grietas", "Gira las grietas añadidas alrededor de su punto de origen, independientemente de la textura de carretera. 0° = orientación anterior; 360° = 0°. Los mapas normales de grietas también giran." }; break;
                case "fr": crackRotation = new[] { "Rotation des fissures", "Tourne les fissures ajoutées autour de leur point de départ, indépendamment de la texture routière. 0° = orientation précédente ; 360° = 0°. Les normal maps des fissures tournent également." }; break;
                case "it": crackRotation = new[] { "Rotazione delle crepe", "Ruota le crepe aggiunte attorno al loro punto iniziale, indipendentemente dalla texture stradale. 0° = orientamento precedente; 360° = 0°. Ruota anche le normal map delle crepe." }; break;
                default: crackRotation = new[] { "Crack rotation", "Rotates added road cracks around their individual origins, independently of the road texture. 0° = previous orientation; 360° = 0°. Custom crack normal maps rotate with the cracks." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.RoadCrackRotation)), crackRotation[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.RoadCrackRotation)), crackRotation[1]);
            entries.Add("AdvancedGraphics.UI.CrackRotation", crackRotation[0]);
            entries.Add("AdvancedGraphics.UI.TipCrackRotation", crackRotation[1]);
            string[] crackDepth;
            switch (m_Language)
            {
                case "de": crackDepth = new[] { "Riss-Tiefenwirkung", "Tiefenwirkung zusätzlicher Straßenrisse, unabhängig vom Asphalt. 0 % = flach, 100 % = normal. Gilt auch für eigene Riss-Normalmaps; keine Änderung der Geometrie." }; break;
                case "es": crackDepth = new[] { "Relieve de grietas", "Relieve de las grietas añadidas, independiente del asfalto. 0 % = plano, 100 % = normal. También afecta a los mapas normales de grietas personalizados; no modifica la geometría." }; break;
                case "fr": crackDepth = new[] { "Relief des fissures", "Relief des fissures ajoutées, indépendant de l’asphalte. 0 % = plat, 100 % = normal. Agit aussi sur les normal maps personnalisées des fissures ; ne modifie pas la géométrie." }; break;
                case "it": crackDepth = new[] { "Rilievo delle crepe", "Rilievo delle crepe aggiunte, indipendente dall’asfalto. 0% = piatto, 100% = normale. Agisce anche sulle normal map personalizzate delle crepe; non modifica la geometria." }; break;
                default: crackDepth = new[] { "Crack depth", "Relief of added road cracks, independent of asphalt depth. 0% = flat, 100% = normal. Also affects custom crack normal maps; does not change geometry." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.RoadCrackDepthStrengthPercent)), crackDepth[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.RoadCrackDepthStrengthPercent)), crackDepth[1]);
            entries.Add("AdvancedGraphics.UI.CrackDepth", crackDepth[0]);
            entries.Add("AdvancedGraphics.UI.TipCrackDepth", crackDepth[1]);
            string[] sidewalkCrackTips;
            switch (m_Language)
            {
                case "de": sidewalkCrackTips = new[] { "Steuert die Tiefenwirkung zusätzlicher Gehwegrisse unabhängig vom Belag. 0 % = flach, 100 % = normal.", "Dreht zusätzliche Gehwegrisse unabhängig vom Belag. 360° entspricht 0°.", "Verschiebt zusätzliche Gehwegrisse entlang der jeweiligen Texturachse. 100 % entspricht einer ganzen Kachel." }; break;
                case "es": sidewalkCrackTips = new[] { "Controla el relieve de las grietas añadidas a la acera, independientemente del pavimento. 0 % = plano, 100 % = normal.", "Gira las grietas añadidas a la acera sin girar el pavimento. 360° equivale a 0°.", "Desplaza las grietas añadidas a la acera a lo largo del eje de textura. 100 % equivale a una repetición completa." }; break;
                case "fr": sidewalkCrackTips = new[] { "Contrôle le relief des fissures ajoutées au trottoir indépendamment du revêtement. 0 % = plat, 100 % = normal.", "Tourne les fissures ajoutées au trottoir indépendamment du revêtement. 360° correspond à 0°.", "Décale les fissures ajoutées au trottoir le long de l’axe de texture. 100 % correspond à une tuile complète." }; break;
                case "it": sidewalkCrackTips = new[] { "Controlla il rilievo delle crepe aggiunte al marciapiede indipendentemente dal rivestimento. 0% = piatto, 100% = normale.", "Ruota le crepe aggiunte al marciapiede senza ruotare il rivestimento. 360° equivale a 0°.", "Sposta le crepe aggiunte al marciapiede lungo l’asse della texture. 100% equivale a una ripetizione completa." }; break;
                default: sidewalkCrackTips = new[] { "Controls added sidewalk crack depth independently of the surface. 0% = flat, 100% = normal.", "Rotates added sidewalk cracks independently of the surface. 360° equals 0°.", "Shifts added sidewalk cracks along the texture axis. 100% equals one full tile." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.SidewalkCrackDepthStrengthPercent)), crackDepth[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.SidewalkCrackDepthStrengthPercent)), sidewalkCrackTips[0]);
            entries.Add("AdvancedGraphics.UI.SidewalkCrackDepth", crackDepth[0]);
            entries.Add("AdvancedGraphics.UI.SidewalkTipCrackDepth", sidewalkCrackTips[0]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.SidewalkCrackRotation)), crackRotation[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.SidewalkCrackRotation)), sidewalkCrackTips[1]);
            entries.Add("AdvancedGraphics.UI.SidewalkCrackRotation", crackRotation[0]);
            entries.Add("AdvancedGraphics.UI.SidewalkTipCrackRotation", sidewalkCrackTips[1]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.SidewalkCrackOffsetX)), crackOffset[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.SidewalkCrackOffsetX)), sidewalkCrackTips[2]);
            entries.Add("AdvancedGraphics.UI.SidewalkCrackOffsetX", crackOffset[0]);
            entries.Add("AdvancedGraphics.UI.SidewalkTipCrackOffsetX", sidewalkCrackTips[2]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.SidewalkCrackOffsetY)), crackOffset[1]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.SidewalkCrackOffsetY)), sidewalkCrackTips[2]);
            entries.Add("AdvancedGraphics.UI.SidewalkCrackOffsetY", crackOffset[1]);
            entries.Add("AdvancedGraphics.UI.SidewalkTipCrackOffsetY", sidewalkCrackTips[2]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.AlleySidewalkCrackDepthStrengthPercent)), crackDepth[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.AlleySidewalkCrackDepthStrengthPercent)), sidewalkCrackTips[0]);
            entries.Add("AdvancedGraphics.UI.AlleySidewalkCrackDepth", crackDepth[0]);
            entries.Add("AdvancedGraphics.UI.AlleySidewalkTipCrackDepth", sidewalkCrackTips[0]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.AlleySidewalkCrackRotation)), crackRotation[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.AlleySidewalkCrackRotation)), sidewalkCrackTips[1]);
            entries.Add("AdvancedGraphics.UI.AlleySidewalkCrackRotation", crackRotation[0]);
            entries.Add("AdvancedGraphics.UI.AlleySidewalkTipCrackRotation", sidewalkCrackTips[1]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.AlleySidewalkCrackOffsetX)), crackOffset[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.AlleySidewalkCrackOffsetX)), sidewalkCrackTips[2]);
            entries.Add("AdvancedGraphics.UI.AlleySidewalkCrackOffsetX", crackOffset[0]);
            entries.Add("AdvancedGraphics.UI.AlleySidewalkTipCrackOffsetX", sidewalkCrackTips[2]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.AlleySidewalkCrackOffsetY)), crackOffset[1]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.AlleySidewalkCrackOffsetY)), sidewalkCrackTips[2]);
            entries.Add("AdvancedGraphics.UI.AlleySidewalkCrackOffsetY", crackOffset[1]);
            entries.Add("AdvancedGraphics.UI.AlleySidewalkTipCrackOffsetY", sidewalkCrackTips[2]);
            string[] opacity;
            switch (m_Language)
            {
                case "de": opacity = new[] { "Rissdeckkraft", "Standard: 100 %. 0 % blendet zusätzliche Risse aus; 600 % macht sie dunkler und verstärkt ihr Relief. Wirkt bei allen Stufen mit Rissanzahl über 0. Höhere Deckkraft kann unnatürlich aussehen." }; break;
                case "es": opacity = new[] { "Opacidad de grietas", "Predeterminado: 100 %. 0 % oculta las grietas añadidas; 600 % las oscurece y refuerza su relieve. Requiere más de 0 grietas en cualquier nivel." }; break;
                case "fr": opacity = new[] { "Opacité des fissures", "Par défaut : 100 %. 0 % masque les fissures ajoutées ; 600 % les assombrit et renforce leur relief. Nécessite des fissures actives à tous les niveaux." }; break;
                case "it": opacity = new[] { "Opacità crepe", "Predefinito: 100%. 0% nasconde le crepe aggiunte; 600% le scurisce e ne aumenta il rilievo. Richiede crepe attive a qualsiasi livello." }; break;
                default: opacity = new[] { "Crack opacity", "Default: 100%. 0% hides extra cracks; 600% darkens them and strengthens their relief. Requires a crack count above 0 on any level. High opacity can look unnatural." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.RoadCrackOpacityPercent)), opacity[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.RoadCrackOpacityPercent)), opacity[1]);
            string[] panel;
            switch (m_Language)
            {
                case "de": panel = new[] { "Asphalt-Fenster", "Asphalt-Button im Spiel anzeigen", "Zeigt einen Button während des Spiels. Er öffnet ein verschiebbares Fenster zur direkten Einstellung des Asphalts ohne die unscharfe Optionsansicht.", "Asphalt", "Asphalt", "Schließen", "Textur", "Generiert", "Eigene Textur", "Asphaltstruktur", "UV-Größe", "Tiefenwirkung", "Kachelung", "UV-Versatz X", "UV-Versatz Y", "Drehung", "Rissanzahl", "Rissgröße", "Risszufall", "Zurücksetzen", "Rissbreite", "Rissdeckkraft" }; break;
                case "es": panel = new[] { "Ventana de asfalto", "Mostrar botón de asfalto", "Muestra un botón durante el juego para ajustar el asfalto directamente en una ventana movible.", "Asfalto", "Asfalto", "Cerrar", "Textura", "Generada", "Textura propia", "Detalle", "Tamaño UV", "Relieve", "Repetición", "Desplazamiento X", "Desplazamiento Y", "Rotación", "Grietas", "Tamaño de grietas", "Variación", "Restablecer", "Ancho de grietas", "Opacidad de grietas" }; break;
                case "fr": panel = new[] { "Fenêtre asphalte", "Afficher le bouton asphalte", "Affiche un bouton en jeu ouvrant une fenêtre déplaçable pour régler l’asphalte directement.", "Asphalte", "Asphalte", "Fermer", "Texture", "Générée", "Texture personnalisée", "Détails", "Taille UV", "Relief", "Répétition", "Décalage X", "Décalage Y", "Rotation", "Fissures", "Taille fissures", "Variation", "Réinitialiser", "Largeur des fissures", "Opacité des fissures" }; break;
                case "it": panel = new[] { "Finestra asfalto", "Mostra pulsante asfalto", "Mostra un pulsante nel gioco che apre una finestra mobile per modificare direttamente l’asfalto.", "Asfalto", "Asfalto", "Chiudi", "Texture", "Generata", "Texture personalizzata", "Dettaglio", "Dimensione UV", "Rilievo", "Ripetizione", "Offset X", "Offset Y", "Rotazione", "Crepe", "Dimensione crepe", "Casualità", "Ripristina", "Larghezza crepe", "Opacità crepe" }; break;
                default: panel = new[] { "Road panel", "Show asphalt button in game", "Shows a button during gameplay to open a movable asphalt settings window without the blurred options view.", "Asphalt", "Asphalt", "Close", "Texture", "Generated", "Custom texture", "Detail", "UV size", "Surface depth", "Tiling", "UV offset X", "UV offset Y", "Rotation", "Crack count", "Crack size", "Randomness", "Reset", "Crack width", "Crack opacity" }; break;
            }
            entries.Add(m_Setting.GetOptionGroupLocaleID(Setting.RoadPanelGroup), panel[0]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShowRoadPanelButton)), panel[1]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.ShowRoadPanelButton)), panel[2]);
            string[] keys = { "Button", "Title", "Close", "Source", "Generated", "Custom", "Detail", "Uv", "Depth", "Tiling", "OffsetX", "OffsetY", "Rotation", "Cracks", "CrackSize", "Randomness", "Reset", "CrackWidth", "CrackOpacity" };
            for (int i = 0; i < keys.Length; i++)
                entries.Add("AdvancedGraphics.UI." + keys[i], panel[i + 3]);
            string originalRoadLabel;
            switch (m_Language)
            {
                case "de": originalRoadLabel = "Original"; break;
                case "es": originalRoadLabel = "Original"; break;
                case "fr": originalRoadLabel = "Originale"; break;
                case "it": originalRoadLabel = "Originale"; break;
                default: originalRoadLabel = "Original"; break;
            }
            entries.Add("AdvancedGraphics.UI.Original", originalRoadLabel);
            entries.Add("AdvancedGraphics.UI.TipSource", source[1]);
            entries.Add("AdvancedGraphics.UI.TipDetail", asphalt[1]);
            entries.Add("AdvancedGraphics.UI.TipUv", asphalt[3]);
            entries.Add("AdvancedGraphics.UI.TipDepth", depth[1]);
            entries.Add("AdvancedGraphics.UI.TipTiling", asphalt[5]);
            entries.Add("AdvancedGraphics.UI.TipOffsetX", asphalt[7]);
            entries.Add("AdvancedGraphics.UI.TipOffsetY", asphalt[9]);
            entries.Add("AdvancedGraphics.UI.TipRotation", asphalt[11]);
            entries.Add("AdvancedGraphics.UI.TipCracks", cracks[1]);
            entries.Add("AdvancedGraphics.UI.TipCrackSize", cracks[3]);
            entries.Add("AdvancedGraphics.UI.TipCrackWidth", width[1]);
            entries.Add("AdvancedGraphics.UI.TipCrackOpacity", opacity[1]);
            entries.Add("AdvancedGraphics.UI.TipRandomness", cracks[5]);
            entries.Add("AdvancedGraphics.UI.TipReset", asphalt[13]);
            string[] sidewalk;
            switch (m_Language)
            {
                case "de": sidewalk = new[] { "Gehweg", "Straßengehweg", "Straßengehwege anpassen", "Ändert nur die Gehwegfläche der RoadEU-Straßen. Eigenständige Fußwege behalten ihre Textur. Aus stellt die Straßenfläche wieder her, ohne deine Werte zu löschen.",
                    "Gehwegstruktur", "0 = Original; 1 = generierte Oberfläche; 2 = eigene Sidewalk_BaseColor.png. Alle Regler wirken bei aktivierter Anpassung.",
                    "Texturordner öffnen", "Öffnet den lokalen Ordner für Sidewalk_BaseColor.png. Die PNG wird nicht mit der Mod veröffentlicht.",
                    "Eigene Gehwegtextur neu laden", "Lädt Sidewalk_BaseColor.png nach dem Kopieren erneut. Struktur muss auf 2 stehen.",
                    "UV-Größe und Tiefenwirkung", "Risse und Kachelung", "Sidewalk_BaseColor.png in den Gehweg-Texturordner kopieren und in den Optionen neu laden.", "Stellt die ursprüngliche Gehwegtextur und alle Gehwegregler wieder her." }; break;
                case "es": sidewalk = new[] { "Acera", "Acera de carretera", "Ajustar aceras de carretera", "Solo cambia la acera de las carreteras RoadEU. Los senderos independientes conservan su textura. Al desactivar se restaura la original.",
                    "Textura de acera", "0 = original; 1 = generada; 2 = Sidewalk_BaseColor.png propia. Los ajustes funcionan cuando están activados.",
                    "Abrir carpeta de texturas", "Abre la carpeta local de Sidewalk_BaseColor.png. La PNG no se publica con el mod.",
                    "Recargar textura de acera", "Recarga Sidewalk_BaseColor.png. Selecciona el nivel 2.",
                    "Tamaño UV y relieve", "Grietas y repetición", "Copia Sidewalk_BaseColor.png en la carpeta de aceras y recárgala en las opciones.", "Restablece la textura y todos los ajustes de acera." }; break;
                case "fr": sidewalk = new[] { "Trottoir", "Trottoir de route", "Modifier les trottoirs de route", "Modifie seulement le trottoir des routes RoadEU. Les chemins piétons indépendants gardent leur texture. Désactiver restaure l’original.",
                    "Texture du trottoir", "0 = origine ; 1 = générée ; 2 = votre Sidewalk_BaseColor.png. Les réglages agissent une fois activés.",
                    "Ouvrir le dossier des textures", "Ouvre le dossier local de Sidewalk_BaseColor.png. Le fichier PNG n’est pas publié avec le mod.",
                    "Recharger la texture du trottoir", "Recharge Sidewalk_BaseColor.png. Choisissez le niveau 2.",
                    "Taille UV et relief", "Fissures et répétition", "Copiez Sidewalk_BaseColor.png dans le dossier du trottoir puis rechargez-la dans les options.", "Restaure la texture et les réglages du trottoir." }; break;
                case "it": sidewalk = new[] { "Marciapiede", "Marciapiede stradale", "Regola marciapiedi stradali", "Cambia solo i marciapiedi delle strade RoadEU. I percorsi pedonali separati mantengono la loro texture. Disattivando si ripristina l’originale.",
                    "Texture marciapiede", "0 = originale; 1 = generata; 2 = Sidewalk_BaseColor.png personale. Le regolazioni agiscono quando attive.",
                    "Apri cartella texture", "Apre la cartella locale di Sidewalk_BaseColor.png. La PNG non viene pubblicata con la mod.",
                    "Ricarica texture marciapiede", "Ricarica Sidewalk_BaseColor.png. Seleziona il livello 2.",
                    "Dimensione UV e rilievo", "Crepe e ripetizione", "Copia Sidewalk_BaseColor.png nella cartella marciapiede e ricaricala nelle opzioni.", "Ripristina texture e regolazioni del marciapiede." }; break;
                default: sidewalk = new[] { "Sidewalk", "Street sidewalk", "Adjust street sidewalks", "Changes only the sidewalk region of RoadEU streets. Standalone footpaths retain their texture. Turning this off restores the street surface without deleting your values.",
                    "Sidewalk detail", "0 = original; 1 = generated surface; 2 = your Sidewalk_BaseColor.png. Controls work when adjustments are enabled.",
                    "Open texture folder", "Opens the local folder for Sidewalk_BaseColor.png. The PNG is not distributed with the mod.",
                    "Reload custom sidewalk texture", "Reloads Sidewalk_BaseColor.png after copying it. Select detail level 2.",
                    "UV size and depth", "Cracks and tiling", "Copy Sidewalk_BaseColor.png into the sidewalk texture folder, then reload it in the options.", "Restores the original sidewalk texture and all sidewalk controls." }; break;
            }
            entries.Add(m_Setting.GetOptionTabLocaleID(Setting.SidewalkSection), sidewalk[0]);
            entries.Add(m_Setting.GetOptionGroupLocaleID(Setting.SidewalkGroup), sidewalk[1]);
            string[] sidewalkButton;
            switch (m_Language)
            {
                case "de": sidewalkButton = new[] { "Gehweg-Tab im Spiel anzeigen", "Zeigt den Gehweg-Tab im Spielfenster. Der Button bleibt sichtbar, solange Asphalt oder Gehweg eingeblendet ist." }; break;
                case "es": sidewalkButton = new[] { "Mostrar pestaña de acera", "Muestra la pestaña de acera en la ventana del juego. El botón permanece visible si se muestra asfalto o acera." }; break;
                case "fr": sidewalkButton = new[] { "Afficher l’onglet trottoir", "Affiche l’onglet trottoir en jeu. Le bouton reste visible si l’asphalte ou le trottoir est affiché." }; break;
                case "it": sidewalkButton = new[] { "Mostra scheda marciapiede", "Mostra la scheda marciapiede nel gioco. Il pulsante resta visibile se è mostrato l’asfalto o il marciapiede." }; break;
                default: sidewalkButton = new[] { "Show sidewalk tab in game", "Shows the sidewalk tab in the game panel. The button remains visible while either asphalt or sidewalk is shown." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShowSidewalkPanelButton)), sidewalkButton[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.ShowSidewalkPanelButton)), sidewalkButton[1]);
            string[] atlas;
            switch (m_Language)
            {
                case "de": atlas = new[] { "Gehweg-Atlas für Diagnose exportieren", "Speichert Farb-, Masken- und Normaldaten der Straße sowie die aktiven Gehwegtexturen im Unterordner Diagnostics. Nach dem Laden einer Stadt und dem Einstellen der Gehwegregler verwenden." }; break;
                case "es": atlas = new[] { "Exportar atlas de acera", "Guarda los atlas de color, máscara y normales, así como las texturas activas de la acera, en Diagnostics. Úsalo tras cargar una ciudad y ajustar la acera." }; break;
                case "fr": atlas = new[] { "Exporter l’atlas du trottoir", "Enregistre les atlas de couleur, de masque et de normales ainsi que les textures actives du trottoir dans Diagnostics. À utiliser après le chargement d’une ville." }; break;
                case "it": atlas = new[] { "Esporta atlante marciapiede", "Salva gli atlanti colore, maschera e normali e le texture attive del marciapiede in Diagnostics. Usare dopo aver caricato una città e regolato il marciapiede." }; break;
                default: atlas = new[] { "Export sidewalk atlas for diagnosis", "Saves street color, mask, and normal atlases plus active sidewalk textures in Diagnostics. Use after loading a city and adjusting the street sidewalk." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.ExportSidewalkAtlas)), atlas[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.ExportSidewalkAtlas)), atlas[1]);
            string[] atlasDiagnostic;
            switch (m_Language)
            {
                case "de": atlasDiagnostic = new[] { "RoadEU-Atlasbereiche farbig markieren", "Nur zur Diagnose: färbt die RoadEU-Atlasbereiche vorübergehend rot, gelb, blau, türkis, magenta und grün. Gehweg-Anpassung muss aktiv sein. Nach einem Screenshot wieder ausschalten; die Originaltexturen werden wiederhergestellt." }; break;
                case "es": atlasDiagnostic = new[] { "Marcar regiones del atlas RoadEU", "Solo para diagnóstico: colorea temporalmente las regiones del atlas. Activa los ajustes de acera, toma una captura y desactiva esta opción para restaurar las texturas." }; break;
                case "fr": atlasDiagnostic = new[] { "Colorer les régions de l’atlas RoadEU", "Diagnostic temporaire : colore les régions de l’atlas. Activez les réglages du trottoir, prenez une capture puis désactivez cette option pour restaurer les textures." }; break;
                case "it": atlasDiagnostic = new[] { "Colora le regioni dell’atlante RoadEU", "Solo diagnostica: colora temporaneamente le regioni dell’atlante. Attiva il marciapiede, acquisisci uno screenshot e disattiva per ripristinare le texture." }; break;
                default: atlasDiagnostic = new[] { "Color-code RoadEU atlas regions", "Temporary diagnostic: colors RoadEU atlas regions red, yellow, blue, cyan, magenta, and green. Enable sidewalk adjustments, take a screenshot, then turn this off to restore original textures." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.DiagnoseRoadEUAtlas)), atlasDiagnostic[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.DiagnoseRoadEUAtlas)), atlasDiagnostic[1]);
            entries.Add(m_Setting.GetOptionGroupLocaleID(Setting.SidewalkUvGroup), sidewalk[10]);
            entries.Add(m_Setting.GetOptionGroupLocaleID(Setting.SidewalkCrackGroup), sidewalk[11]);
            var sidewalkTop = new[] { nameof(Setting.SidewalkEnabled), nameof(Setting.SidewalkDetail),
                nameof(Setting.OpenSidewalkTextureFolder), nameof(Setting.ReloadSidewalkTexture) };
            for (int i = 0; i < sidewalkTop.Length; i++)
            {
                entries.Add(m_Setting.GetOptionLabelLocaleID(sidewalkTop[i]), sidewalk[2 + i * 2]);
                entries.Add(m_Setting.GetOptionDescLocaleID(sidewalkTop[i]), sidewalk[3 + i * 2]);
            }
            var sidewalkKeys = new[] { nameof(Setting.SidewalkUvSizePercent), nameof(Setting.SidewalkDepthStrengthPercent),
                nameof(Setting.SidewalkUvOffsetX), nameof(Setting.SidewalkUvOffsetY), nameof(Setting.SidewalkUvRotation),
                nameof(Setting.SidewalkCrackCount), nameof(Setting.SidewalkCrackSizePercent), nameof(Setting.SidewalkCrackWidthPercent),
                nameof(Setting.SidewalkCrackOpacityPercent), nameof(Setting.SidewalkCrackRandomnessPercent), nameof(Setting.SidewalkTextureTiling) };
            var sidewalkUIKeys = new[] { "Uv", "Depth", "OffsetX", "OffsetY", "Rotation", "Cracks", "CrackSize", "CrackWidth", "CrackOpacity", "Randomness", "Tiling" };
            var labels = new[] { asphalt[2], depth[0], asphalt[6], asphalt[8], asphalt[10], cracks[0], cracks[2], width[0], opacity[0], cracks[4], asphalt[4] };
            var descriptions = new[] { asphalt[3], depth[1], asphalt[7], asphalt[9], asphalt[11], cracks[1], cracks[3], width[1], opacity[1], cracks[5], asphalt[5] };
            descriptions[5] = descriptions[5].Replace("1200", "400").Replace("2400", "800");
            descriptions[6] = descriptions[6].Replace("1800", "600");
            descriptions[7] = descriptions[7].Replace("1200", "400");
            descriptions[8] = descriptions[8].Replace("600", "200");
            switch (m_Language)
            {
                case "de": descriptions[10] = "0,1 bis 8,0 in 0,1-Schritten. Unter 1 weniger zusätzliche Risse; über 1 mehr. Die Korngröße bleibt unverändert."; break;
                case "es": descriptions[10] = "De 0,1 a 8,0 en pasos de 0,1. Por debajo de 1 hay menos grietas añadidas; por encima hay más. La grana no cambia."; break;
                case "fr": descriptions[10] = "De 0,1 à 8,0 par pas de 0,1. Sous 1, moins de fissures ajoutées ; au-dessus, davantage. Le grain reste inchangé."; break;
                case "it": descriptions[10] = "Da 0,1 a 8,0 con passi di 0,1. Sotto 1 ci sono meno crepe aggiunte; sopra 1 di più. La grana non cambia."; break;
                default: descriptions[10] = "0.1 to 8.0 in 0.1 steps. Below 1, fewer extra cracks; above 1, more. Grain size is unchanged."; break;
            }
            for (int i = 0; i < sidewalkKeys.Length; i++)
            {
                entries.Add(m_Setting.GetOptionLabelLocaleID(sidewalkKeys[i]), labels[i]);
                entries.Add(m_Setting.GetOptionDescLocaleID(sidewalkKeys[i]), descriptions[i]);
                entries.Add("AdvancedGraphics.UI.Sidewalk" + sidewalkUIKeys[i], labels[i]);
                entries.Add("AdvancedGraphics.UI.SidewalkTip" + sidewalkUIKeys[i], descriptions[i]);
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetSidewalk)), panel[19]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.ResetSidewalk)), sidewalk[13]);
            entries.Add("AdvancedGraphics.UI.Sidewalk", sidewalk[0]);
            entries.Add("AdvancedGraphics.UI.SidewalkEnabled", sidewalk[2]);
            entries.Add("AdvancedGraphics.UI.SidewalkTipEnabled", sidewalk[3]);
            entries.Add("AdvancedGraphics.UI.SidewalkDetail", sidewalk[4]);
            entries.Add("AdvancedGraphics.UI.SidewalkTipDetail", sidewalk[5]);
            entries.Add("AdvancedGraphics.UI.SidewalkCustomHint", sidewalk[12]);
            entries.Add("AdvancedGraphics.UI.SidewalkTipReset", sidewalk[13]);
            string[] rail;
            switch (m_Language)
            {
                case "de": rail = new[] { "Schiene", "Schienenmaterialien", "Schienen und Kies untersuchen", "Ermittelt nach dem Laden einer Stadt Gleis-, Schwellen- und Schottermaterialien und exportiert deren Texturen. Die Spielmaterialien werden nicht verändert.", "Diagnoseordner öffnen", "Öffnet ModsData/Enhanced_Graphics_and_Textures/Textures/Rail/Diagnostics mit RailMaterials.txt und den exportierten PNG-Dateien." }; break;
                case "es": rail = new[] { "Vías", "Materiales ferroviarios", "Examinar vías y balasto", "Tras cargar una ciudad, exporta los materiales y texturas de rieles, traviesas y balasto sin modificar el juego.", "Abrir carpeta de diagnóstico", "Abre ModsData/Enhanced_Graphics_and_Textures/Textures/Rail/Diagnostics con RailMaterials.txt y los PNG exportados." }; break;
                case "fr": rail = new[] { "Rails", "Matériaux ferroviaires", "Examiner rails et ballast", "Après le chargement d’une ville, exporte les matériaux et textures des rails, traverses et ballast sans modifier le jeu.", "Ouvrir le dossier de diagnostic", "Ouvre ModsData/Enhanced_Graphics_and_Textures/Textures/Rail/Diagnostics avec RailMaterials.txt et les PNG exportés." }; break;
                case "it": rail = new[] { "Binari", "Materiali ferroviari", "Esamina rotaie e pietrisco", "Dopo aver caricato una città, esporta materiali e texture di rotaie, traversine e pietrisco senza modificarli.", "Apri cartella diagnostica", "Apre ModsData/Enhanced_Graphics_and_Textures/Textures/Rail/Diagnostics con RailMaterials.txt e i PNG esportati." }; break;
                default: rail = new[] { "Rail", "Rail materials", "Inspect rails and ballast", "After loading a city, exports rail, sleeper and ballast materials and textures without modifying the game.", "Open diagnostic folder", "Opens ModsData/Enhanced_Graphics_and_Textures/Textures/Rail/Diagnostics with RailMaterials.txt and the exported PNG files." }; break;
            }
            entries.Add(m_Setting.GetOptionTabLocaleID(Setting.RailSection), rail[0]);
            entries.Add(m_Setting.GetOptionGroupLocaleID(Setting.RailGroup), rail[1]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.ExportRailMaterials)), rail[2]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.ExportRailMaterials)), rail[3]);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenRailDiagnosticFolder)), rail[4]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenRailDiagnosticFolder)), rail[5]);
            string[] railPanel;
            switch (m_Language)
            {
                case "de": railPanel = new[] { "Rail-Tab im Spiel anzeigen", "Zeigt Schotterregler und Schienendiagnose im Spielfenster.", "Der Kies ist separat einstellbar. Die Schiene nutzt Virtual Texturing; für Farbregler fehlen noch ihre Shader-Eigenschaften.", "Schieneneigenschaften exportieren", "Nach dem Export RailMaterials.txt aus ModsData/Enhanced_Graphics_and_Textures/Textures/Rail/Diagnostics hochladen; Schienenmaterial 6 enthält die Shader-Werte." }; break;
                case "es": railPanel = new[] { "Mostrar pestaña de vías", "Muestra los ajustes del balasto y el diagnóstico de rieles.", "El balasto se puede ajustar. Para las vías con texturas virtuales faltan sus propiedades de color.", "Exportar propiedades de los rieles", "Comparte RailMaterials.txt de ModsData/Enhanced_Graphics_and_Textures/Textures/Rail/Diagnostics; el material 6 contiene las propiedades del shader." }; break;
                case "fr": railPanel = new[] { "Afficher l’onglet rails", "Affiche les réglages du ballast et le diagnostic des rails.", "Le ballast est réglable. Les rails utilisent les textures virtuelles ; il faut leurs propriétés de couleur.", "Exporter les propriétés des rails", "Fournissez RailMaterials.txt de ModsData/Enhanced_Graphics_and_Textures/Textures/Rail/Diagnostics ; le matériau 6 contient les propriétés du shader." }; break;
                case "it": railPanel = new[] { "Mostra scheda binari", "Mostra le regolazioni del pietrisco e la diagnosi delle rotaie.", "Il pietrisco è regolabile. Per le rotaie con texture virtuali servono le proprietà del colore.", "Esporta proprietà delle rotaie", "Condividi RailMaterials.txt da ModsData/Enhanced_Graphics_and_Textures/Textures/Rail/Diagnostics; il materiale 6 contiene le proprietà shader." }; break;
                default: railPanel = new[] { "Show rail tab in game", "Shows gravel controls and rail diagnostics in game.", "Track gravel is adjustable. Steel rails use virtual texturing; their shader color controls still need to be identified.", "Export rail shader properties", "After exporting, share RailMaterials.txt from ModsData/Enhanced_Graphics_and_Textures/Textures/Rail/Diagnostics; material 6 lists the rail shader properties." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShowRailPanelButton)), railPanel[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.ShowRailPanelButton)), railPanel[1]);
            entries.Add("AdvancedGraphics.UI.Rail", rail[0]);
            entries.Add("AdvancedGraphics.UI.RailDiagnosticDescription", railPanel[2]);
            entries.Add("AdvancedGraphics.UI.RailExport", railPanel[3]);
            entries.Add("AdvancedGraphics.UI.RailExportHint", railPanel[4]);
            string[] probe;
            switch (m_Language)
            {
                case "de": probe = new[] { "Gleismaterialien markieren", "Farbmarkierung ausschalten", "Magenta = TrainSubwayTrack, Gelb = GravelShoulder, Grün = Schotter, Rot = Gleisdekor, Cyan = TrainRails. Nach dem Screenshot wieder ausschalten." }; break;
                case "es": probe = new[] { "Marcar materiales de vías", "Desactivar colores de prueba", "Magenta = TrainSubwayTrack, amarillo = GravelShoulder, verde = Gravel, rojo = calcomanías ferroviarias, cian = TrainRails. Desactiva tras la captura." }; break;
                case "fr": probe = new[] { "Repérer les matériaux des rails", "Désactiver les couleurs de test", "Magenta = TrainSubwayTrack, jaune = GravelShoulder, vert = Gravel, rouge = décalcomanies ferroviaires, cyan = TrainRails. Désactivez après la capture." }; break;
                case "it": probe = new[] { "Evidenzia materiali dei binari", "Disattiva colori di prova", "Magenta = TrainSubwayTrack, giallo = GravelShoulder, verde = Gravel, rosso = decalcomanie ferroviarie, ciano = TrainRails. Disattiva dopo lo screenshot." }; break;
                default: probe = new[] { "Identify track materials", "Disable color probe", "Magenta = TrainSubwayTrack, yellow = GravelShoulder, green = Gravel, red = rail decals, cyan = TrainRails. Disable after a screenshot." }; break;
            }
            entries.Add("AdvancedGraphics.UI.RailProbeOn", probe[0]);
            entries.Add("AdvancedGraphics.UI.RailProbeOff", probe[1]);
            entries.Add("AdvancedGraphics.UI.RailProbeHint", probe[2]);
            string[] gravel;
            switch (m_Language)
            {
                case "de": gravel = new[] { "Schotter: UV und Tiefe", "Gleisschotter anpassen", "Ändert ausschließlich die TrainGravelWorldspace-Textur der Gleise; Schienen und Schwellen bleiben unverändert.", "Schotterstruktur", "0 = Original; 1 = mitgelieferter generierter Schotter mit passender Normalmap (Standard); 2 = eigene RailGravel_BaseColor.png.", "Schotter-UV-Größe", "100 = ursprüngliche Größe; höher vergrößert die Kiesstruktur, bis 3000.", "Schotter-Tiefenwirkung", "Generiert/eigene Normalmap: 0 % flach, 100 % ursprüngliche Stärke. Ohne eigene Normalmap wird das Relief aus der Farbe abgeleitet. Keine Geometrie.", "Schotter-Versatz X", "Verschiebt die Schottertextur horizontal.", "Schotter-Versatz Y", "Verschiebt die Schottertextur vertikal.", "Schotter-Drehung", "Freie Drehung von 0 bis 360 Grad in 1°-Schritten. Bei Winkeln außerhalb von Vierteldrehungen können Kachelränder sichtbar werden.", "Schotter zurücksetzen", "Aktiviert generierten Schotter: UV 150, Tiefe 200, X 0, Y 10, Rotation 53°. Setzt alle Schotterprofile und den Farbfilter zurück.", "Schotterordner öffnen", "Öffnet den Ordner für RailGravel_BaseColor.png und optional RailGravel_Normal.png.", "Eigene Schottertextur neu laden", "Lädt Farbtextur und optionale Normal Map erneut.", "RailGravel_BaseColor.png und optional RailGravel_Normal.png (RGB, OpenGL/+Y) in den Texturordner kopieren und in den Optionen neu laden. Beide müssen nahtlos kachelbar sein." }; break;
                case "es": gravel = new[] { "Balasto: UV y relieve", "Ajustar balasto", "Solo cambia TrainGravelWorldspace; rieles y traviesas permanecen intactos.", "Textura del balasto", "0 = original; 1 = balasto generado incluido con mapa normal (predeterminado); 2 = RailGravel_BaseColor.png propia.", "Tamaño UV del balasto", "100 = tamaño original; valores mayores amplían el grano, hasta 3000.", "Relieve del balasto", "Generado/normal propia: 0 % plano, 100 % intensidad original. Sin normal propia, relieve desde el color. Sin geometría.", "Desplazamiento X", "Desplaza el balasto horizontalmente.", "Desplazamiento Y", "Desplaza el balasto verticalmente.", "Rotación del balasto", "Rotación libre de 0 a 360 grados en pasos de 1°. Pueden aparecer juntas con ángulos distintos de cuartos de vuelta.", "Restablecer balasto", "Activa balasto generado: UV 150, relieve 200, X 0, Y 10, rotación 53°. Restablece perfiles y filtro.", "Abrir carpeta del balasto", "Abre la carpeta de RailGravel_BaseColor.png y la normal opcional RailGravel_Normal.png.", "Recargar balasto personal", "Recarga el color y la normal opcional.", "Copia RailGravel_BaseColor.png y opcionalmente RailGravel_Normal.png (RGB, OpenGL/+Y), y recarga en opciones. Ambas deben repetirse sin juntas." }; break;
                case "fr": gravel = new[] { "Ballast : UV et relief", "Modifier le ballast", "Modifie uniquement TrainGravelWorldspace ; rails et traverses restent intacts.", "Texture du ballast", "0 = origine ; 1 = ballast généré fourni avec sa normale (par défaut) ; 2 = RailGravel_BaseColor.png personnelle.", "Taille UV du ballast", "100 = taille d’origine ; plus grand agrandit les pierres, jusqu’à 3000.", "Relief du ballast", "Généré/normale personnelle : 0 % plat, 100 % intensité initiale. Sans normale personnelle, relief calculé depuis la couleur. Sans géométrie.", "Décalage X", "Décale le ballast horizontalement.", "Décalage Y", "Décale le ballast verticalement.", "Rotation du ballast", "Rotation libre de 0 à 360 degrés par pas de 1°. Des raccords peuvent apparaître aux angles autres que les quarts de tour.", "Réinitialiser le ballast", "Active le ballast généré : UV 150, relief 200, X 0, Y 10, rotation 53°. Réinitialise les profils et le filtre.", "Ouvrir le dossier ballast", "Ouvre le dossier pour RailGravel_BaseColor.png et RailGravel_Normal.png facultative.", "Recharger le ballast personnel", "Recharge la couleur et la normale facultative.", "Copiez RailGravel_BaseColor.png et éventuellement RailGravel_Normal.png (RGB, OpenGL/+Y), puis rechargez dans les options. Les deux doivent être répétables sans raccord visible." }; break;
                case "it": gravel = new[] { "Pietrisco: UV e rilievo", "Modifica pietrisco", "Cambia solo TrainGravelWorldspace; rotaie e traversine restano intatte.", "Struttura pietrisco", "0 = originale; 1 = pietrisco generato incluso con normale (predefinito); 2 = RailGravel_BaseColor.png personale.", "Dimensione UV pietrisco", "100 = originale; valori maggiori ingrandiscono la grana fino a 3000.", "Rilievo pietrisco", "Generata/normale personale: 0 % piatta, 100 % intensità originale. Senza normale personale, rilievo dal colore. Nessuna geometria.", "Spostamento X", "Sposta il pietrisco in orizzontale.", "Spostamento Y", "Sposta il pietrisco in verticale.", "Rotazione pietrisco", "Rotazione libera da 0 a 360 gradi con passi di 1°. Possono comparire giunture con angoli diversi dai quarti di giro.", "Ripristina pietrisco", "Attiva il pietrisco generato: UV 150, rilievo 200, X 0, Y 10, rotazione 53°. Ripristina profili e filtro.", "Apri cartella pietrisco", "Apre la cartella di RailGravel_BaseColor.png e della normale opzionale RailGravel_Normal.png.", "Ricarica pietrisco personale", "Ricarica colore e normale opzionale.", "Copia RailGravel_BaseColor.png e facoltativamente RailGravel_Normal.png (RGB, OpenGL/+Y), poi ricarica nelle opzioni. Entrambe devono essere ripetibili senza giunture." }; break;
                default: gravel = new[] { "Track gravel UV and depth", "Enable track gravel adjustments", "Changes only the track's TrainGravelWorldspace texture; steel rails and sleepers remain unchanged.", "Gravel texture", "0 = original; 1 = bundled generated ballast with matching normal map (default); 2 = custom RailGravel_BaseColor.png.", "Gravel UV size", "100 = original size; higher enlarges stones up to 3000.", "Gravel depth", "Generated/custom normal: 0% flat, 100% authored strength. Without a custom normal, relief is derived from color. No geometry.", "Gravel offset X", "Shifts the gravel pattern horizontally.", "Gravel offset Y", "Shifts the gravel pattern vertically.", "Gravel rotation", "Free rotation from 0 to 360 degrees in 1-degree steps. Tile boundaries may become visible at angles other than quarter turns.", "Reset gravel", "Enables generated ballast: UV 150, depth 200, X 0, Y 10, rotation 53°. Resets all ballast profiles and the color filter.", "Open gravel texture folder", "Opens the folder for RailGravel_BaseColor.png and optional RailGravel_Normal.png.", "Reload custom gravel texture", "Reloads color and optional normal map.", "Copy RailGravel_BaseColor.png and optional RailGravel_Normal.png (RGB, OpenGL/+Y) to the texture folder and reload in options. Both must tile seamlessly." }; break;
            }
            entries.Add(m_Setting.GetOptionGroupLocaleID(Setting.RailUvGroup), gravel[0]);
            var gravelOptions = new[] { nameof(Setting.RailGravelEnabled), nameof(Setting.RailGravelDetail),
                nameof(Setting.RailGravelUvSizePercent), nameof(Setting.RailGravelDepthStrengthPercent),
                nameof(Setting.RailGravelUvOffsetX), nameof(Setting.RailGravelUvOffsetY),
                nameof(Setting.RailGravelUvRotation), nameof(Setting.ResetRailGravel),
                nameof(Setting.OpenRailTextureFolder), nameof(Setting.ReloadRailTexture) };
            for (int i = 0; i < gravelOptions.Length; i++)
            {
                entries.Add(m_Setting.GetOptionLabelLocaleID(gravelOptions[i]), gravel[1 + i * 2]);
                entries.Add(m_Setting.GetOptionDescLocaleID(gravelOptions[i]), gravel[2 + i * 2]);
            }
            entries.Add("AdvancedGraphics.UI.RailGravelEnabled", gravel[1]);
            entries.Add("AdvancedGraphics.UI.RailGravelTipEnabled", gravel[2]);
            var gravelUiKeys = new[] { "Detail", "Uv", "Depth", "OffsetX", "OffsetY", "Rotation" };
            for (int i = 0; i < gravelUiKeys.Length; i++)
            {
                entries.Add("AdvancedGraphics.UI.RailGravel" + gravelUiKeys[i], gravel[3 + i * 2]);
                entries.Add("AdvancedGraphics.UI.RailGravelTip" + gravelUiKeys[i], gravel[4 + i * 2]);
            }
            entries.Add("AdvancedGraphics.UI.RailGravelTipReset", gravel[16]);
            entries.Add("AdvancedGraphics.UI.RailGravelCustomHint", gravel[21]);
            AddRoadGravelEntries(entries);
            AddSurfaceLayoutEntries(entries);
            string launcherTooltip;
            switch (m_Language)
            {
                case "de": launcherTooltip = "Enhanced Graphics and Textures – Textur-Editor öffnen"; break;
                case "es": launcherTooltip = "Enhanced Graphics and Textures – abrir editor de texturas"; break;
                case "fr": launcherTooltip = "Enhanced Graphics and Textures – ouvrir l’éditeur de textures"; break;
                case "it": launcherTooltip = "Enhanced Graphics and Textures – apri editor delle texture"; break;
                default: launcherTooltip = "Enhanced Graphics and Textures – open texture editor"; break;
            }
            string interfaceLabel;
            string appButtonLabel;
            string appButtonDescription;
            switch (m_Language)
            {
                case "de":
                    interfaceLabel = "Benutzeroberfläche";
                    appButtonLabel = "App-Button anzeigen";
                    appButtonDescription = "Zeigt den App-Button für den Textur-Editor in der Ingame-UI an. Alle Oberflächen-Tabs sind verfügbar; die gespeicherten Reglerprofile bleiben erhalten.";
                    break;
                case "es":
                    interfaceLabel = "Interfaz";
                    appButtonLabel = "Mostrar botón de la aplicación";
                    appButtonDescription = "Muestra el botón del editor de texturas en el juego. Todas las pestañas están disponibles y se conservan los perfiles guardados.";
                    break;
                case "fr":
                    interfaceLabel = "Interface";
                    appButtonLabel = "Afficher le bouton de l’application";
                    appButtonDescription = "Affiche le bouton de l’éditeur de textures dans le jeu. Tous les onglets sont disponibles et les profils enregistrés sont conservés.";
                    break;
                case "it":
                    interfaceLabel = "Interfaccia";
                    appButtonLabel = "Mostra pulsante dell’app";
                    appButtonDescription = "Mostra il pulsante dell’editor di texture nel gioco. Tutte le schede sono disponibili e i profili salvati vengono mantenuti.";
                    break;
                default:
                    interfaceLabel = "Interface";
                    appButtonLabel = "Show app button";
                    appButtonDescription = "Shows the in-game texture editor app button. All surface tabs are available and saved slider profiles are retained.";
                    break;
            }
            entries.Add(m_Setting.GetOptionGroupLocaleID(Setting.LauncherGroup), interfaceLabel);
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShowAppButton)), appButtonLabel);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.ShowAppButton)), appButtonDescription);
            entries["AdvancedGraphics.UI.OpenTextureEditor"] = launcherTooltip;
            AddParkingScaleEntries(entries);
            AddParkingSidewalkEntries(entries);
            AddParkingDepthEntries(entries);
            AddSurfaceSamplingEntries(entries);
            AddColorFilterEntries(entries);
            AddTaaEntries(entries);
            AddLightingEntries(entries);
            AddGraphicsPanelEntries(entries);
            AddLuminaNumberFixEntries(entries);
            AddCustomCrackEntries(entries);
            AddJunctionOverlayEntries(entries);
            AddGrassEntries(entries);
            AddFootpathEntries(entries);
            AddCustomNormalEntries(entries);
            AddTextureFileControls(entries);
            AddNormalToggleEntries(entries);
            AddEndcapEntries(entries);
            AddDebugWindowEntries(entries);
            return entries;
        }

        private void AddDebugWindowEntries(Dictionary<string, string> entries)
        {
            bool de = m_Language == "de";
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShowDebugWindow))] = de ? "Debug-Fenster anzeigen" : "Show debug window";
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ShowDebugWindow))] = de
                ? "Zeigt im Spiel ein eigenes Fenster mit allen Debug- und Diagnosefunktionen. Nur zum Testen. Beim Ausschalten werden alle Testmodi (Atlas-Farben, Einmündungs-Modus, Gleis-Markierung) zurückgesetzt."
                : "Shows an in-game window with all debug and diagnostic tools. For testing only. Turning it off resets every test mode (atlas colors, junction mode, rail probe).";
            const string prefix = "AdvancedGraphics.UI.Debug.";
            var texts = de ? new[] {
                "Title", "Debug", "Open", "Enhanced Graphics and Textures – Debug-Werkzeuge", "Close", "Schließen",
                "Sidewalk", "Gehweg", "AtlasDiagnostic", "RoadEU-Atlasbereiche farbig markieren",
                "TipAtlasDiagnostic", "Rot/Gelb/Blau/Türkis/Magenta/Grün je Atlasbereich. Nach dem Screenshot wieder ausschalten.",
                "RecomputeTangents", "Ecken/Enden: Tangenten neu berechnen (Test)", "TipRecomputeTangents", "Test: berechnet an umgebauten Ecken und Straßenenden die Tangenten passend zu den neuen Platten. Standard aus. Wird in 2 s neu aufgebaut.",
                "JunctionMode", "Einmündungs-Modus",
                "TipJunctionMode", "0 linear (normal), 1 Smoothstep, 2 t², 3 1-(1-t)², 4 ohne Sync, 5 voll, 6 umgemappte Eckteile einfärben.",
                "ExportSidewalkAtlas", "Gehweg-Atlas exportieren", "TipExport", "Schreibt Dateien in den Diagnose-Ordner.",
                "SidewalkScaleReport", "Gehweg-Skalierungsbericht schreiben", "TipSidewalkScaleReport", "SidewalkScale.txt: Breite und Texturbereich jedes Gehweg-Teils je Straßentyp.",
                "OpenDiagnostics", "Diagnose-Ordner öffnen", "TipOpenDiagnostics", "JunctionRamps.txt, JunctionWorld.txt und Atlas-Exporte.",
                "Roads", "Straßen und Gras", "InspectRoad", "Straßenmaterialien untersuchen",
                "ExportGravel", "Schotterstraßen-Materialien exportieren", "ExportGrass", "Grasmaterialien exportieren",
                "OpenGrassFolder", "Gras-Diagnoseordner öffnen", "TipOpenFolder", "Öffnet den Ordner im Explorer.",
                "Rail", "Gleise", "RailProbe", "Gleismaterialien markieren",
                "TipRailProbe", "Magenta = TrainSubwayTrack, Gelb = GravelShoulder, Grün = Schotter, Rot = Gleisdekor, Cyan = TrainRails.",
                "ExportRail", "Gleismaterialien exportieren", "OpenRailFolder", "Gleis-Diagnoseordner öffnen",
                "ResetModes", "Alle Testmodi ausschalten", "TipResetModes", "Atlas-Farben, Einmündungs-Modus und Gleis-Markierung zurück auf normal." }
                : new[] {
                "Title", "Debug", "Open", "Enhanced Graphics and Textures – debug tools", "Close", "Close",
                "Sidewalk", "Sidewalk", "AtlasDiagnostic", "Color-code RoadEU atlas regions",
                "TipAtlasDiagnostic", "Red/yellow/blue/cyan/magenta/green per atlas region. Turn off after the screenshot.",
                "RecomputeTangents", "Corners/ends: recompute tangents (test)", "TipRecomputeTangents", "Test: recomputes tangents to match the new slabs at rebuilt corners and road ends. Off by default. Rebuilds within 2 s.",
                "JunctionMode", "Junction mode",
                "TipJunctionMode", "0 linear (normal), 1 smoothstep, 2 t², 3 1-(1-t)², 4 no sync, 5 full, 6 paint remapped corner pieces.",
                "ExportSidewalkAtlas", "Export sidewalk atlas", "TipExport", "Writes files to the diagnostics folder.",
                "SidewalkScaleReport", "Write sidewalk scale report", "TipSidewalkScaleReport", "SidewalkScale.txt: width and texture range of every sidewalk piece per road type.",
                "OpenDiagnostics", "Open diagnostics folder", "TipOpenDiagnostics", "JunctionRamps.txt, JunctionWorld.txt and atlas exports.",
                "Roads", "Roads and grass", "InspectRoad", "Inspect road materials",
                "ExportGravel", "Export gravel road materials", "ExportGrass", "Export grass materials",
                "OpenGrassFolder", "Open grass diagnostics folder", "TipOpenFolder", "Opens the folder in Explorer.",
                "Rail", "Rail", "RailProbe", "Identify track materials",
                "TipRailProbe", "Magenta = TrainSubwayTrack, yellow = GravelShoulder, green = Gravel, red = rail decals, cyan = TrainRails.",
                "ExportRail", "Export rail materials", "OpenRailFolder", "Open rail diagnostics folder",
                "ResetModes", "Turn all test modes off", "TipResetModes", "Atlas colors, junction mode and rail probe back to normal." };
            for (int i = 0; i + 1 < texts.Length; i += 2) entries[prefix + texts[i]] = texts[i + 1];
        }

        private void AddJunctionOverlayEntries(Dictionary<string, string> entries)
        {
            string label, hint;
            switch (m_Language)
            {
                case "de": label = "Gerade Gehwegplatten an Gasseneinmündungen (Test)";
                    hint = "Standard aus. Ersetzt an Einmündungen von Gassen ohne Gehweg die gestreckte Gehwegtextur der Bordsteinrundung durch ein gerades Plattenraster. Das Spiel zeichnet dafür eine angepasste Kopie des Kreuzungsmeshes; Geometrie, Licht und Wetter bleiben original. Spielstände werden nicht verändert."; break;
                case "es": label = "Losas rectas en cruces con callejones (prueba)";
                    hint = "Desactivado por defecto. En cruces con callejones sin acera, sustituye la textura estirada de la esquina por una cuadrícula recta. El juego dibuja una copia ajustada de la malla del cruce; geometría, luz y clima no cambian. No modifica las partidas."; break;
                case "fr": label = "Dalles droites aux entrées de ruelles (test)";
                    hint = "Désactivé par défaut. Aux entrées de ruelles sans trottoir, remplace la texture étirée de l'arrondi par une grille droite. Le jeu dessine une copie ajustée du maillage du carrefour ; géométrie, lumière et météo restent d'origine. Les sauvegardes ne sont pas modifiées."; break;
                case "it": label = "Lastre dritte agli incroci con vicoli (test)";
                    hint = "Disattivato per default. Agli incroci con vicoli senza marciapiede sostituisce la texture stirata della curva con una griglia dritta. Il gioco disegna una copia adattata della mesh dell'incrocio; geometria, luce e meteo restano originali. I salvataggi non vengono modificati."; break;
                default: label = "Straight sidewalk slabs at alley junctions (test)";
                    hint = "Off by default. At junctions with sidewalk-free alleys, replaces the stretched sidewalk texture of the curb rounding with a straight slab grid. The game draws an adjusted copy of the junction mesh; geometry, lighting and weather stay vanilla. Saves are not modified."; break;
            }
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.SidewalkJunctionOverlay))] = label;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.SidewalkJunctionOverlay))] = hint;
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.JunctionSyncBlendMode))] = m_Language == "de"
                ? "Einmündung: Sync-Überblendung (Debug)" : "Junction sync blend (debug)";
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.JunctionSyncBlendMode))] = m_Language == "de"
                ? "0 linear, 1 Smoothstep, 2 t², 3 1-(1-t)², 4 ohne Sync, 5 voll, 6 umgemappte Fläche einfärben (mit Atlas-Diagnose grün)."
                : "0 linear, 1 smoothstep, 2 t², 3 1-(1-t)², 4 no sync, 5 full, 6 paint the remapped area (green with the atlas diagnostic).";
        }

        private void AddGrassEntries(Dictionary<string, string> entries)
        {
            string[] t;
            switch (m_Language)
            {
                case "de": t = new[] { "Gehweg-Gras", "Gehweg-Gras anpassen", "Passt Grasstreifen an Straßen und Gehwegen an.", "Grastextur", "0: Original. 1: Generiertes Gras. 2: Eigene Grass_BaseColor.png.", "Texturgröße", "50 bis 3000. Größere Werte vergrößern das Grasmuster.", "Tiefenwirkung", "0: Originalrelief im Originalmodus, flach bei generierter/eigener Textur. Höhere Werte verstärken das Relief. Keine echte Geometrie.", "UV-Versatz X", "UV-Versatz Y", "Verschiebt die Grastextur zwischen 0 und 300.", "Drehung", "Dreht Grastextur und Normalmap zwischen 0 und 360 Grad.", "Texturordner öffnen", "Öffnet Textures/Grass für Grass_BaseColor.png.", "Eigene Grastextur neu laden", "Lädt Grass_BaseColor.png erneut. Modus 2 wählen; quadratische PNG bis 4096 Pixel.", "Zurücksetzen", "Setzt Gehweg-Gras und alle drei Modusprofile zurück.", "Grass_BaseColor.png in Textures/Grass ablegen und neu laden. Quadratische PNG bis 4096 Pixel." }; break;
                case "es": t = new[] { "Césped de acera", "Ajustar césped de acera", "Ajusta las franjas de césped junto a calles y aceras.", "Textura de césped", "0: Original. 1: Césped generado. 2: Grass_BaseColor.png propia.", "Tamaño de textura", "De 50 a 3000. Valores mayores amplían el patrón.", "Relieve", "0: relieve original en modo original, plano en otros modos. Valores mayores refuerzan el relieve sin añadir geometría.", "Desplazamiento UV X", "Desplazamiento UV Y", "Desplaza la textura entre 0 y 300.", "Rotación", "Gira la textura y las normales entre 0 y 360 grados.", "Abrir carpeta de texturas", "Abre Textures/Grass para Grass_BaseColor.png.", "Recargar textura de césped", "Recarga Grass_BaseColor.png. Selecciona el modo 2; PNG cuadrado de hasta 4096 píxeles.", "Restablecer", "Restablece el césped y los tres perfiles de modo.", "Coloca Grass_BaseColor.png en Textures/Grass y recarga. PNG cuadrado de hasta 4096 píxeles." }; break;
                case "fr": t = new[] { "Herbe de trottoir", "Ajuster l’herbe de trottoir", "Ajuste les bandes d’herbe le long des routes et trottoirs.", "Texture d’herbe", "0 : Original. 1 : Herbe générée. 2 : Votre Grass_BaseColor.png.", "Taille de texture", "De 50 à 3000. Les valeurs élevées agrandissent le motif.", "Relief", "0 : relief original en mode original, plat dans les autres modes. Les valeurs élevées renforcent le relief sans ajouter de géométrie.", "Décalage UV X", "Décalage UV Y", "Décale la texture de 0 à 300.", "Rotation", "Tourne la texture et les normales de 0 à 360 degrés.", "Ouvrir le dossier des textures", "Ouvre Textures/Grass pour Grass_BaseColor.png.", "Recharger la texture d’herbe", "Recharge Grass_BaseColor.png. Choisir le mode 2 ; PNG carré jusqu’à 4096 pixels.", "Réinitialiser", "Réinitialise l’herbe et les trois profils de mode.", "Placer Grass_BaseColor.png dans Textures/Grass et recharger. PNG carré jusqu’à 4096 pixels." }; break;
                case "it": t = new[] { "Erba del marciapiede", "Regola erba del marciapiede", "Regola le fasce erbose lungo strade e marciapiedi.", "Texture erba", "0: Originale. 1: Erba generata. 2: Grass_BaseColor.png personale.", "Dimensione texture", "Da 50 a 3000. Valori maggiori ingrandiscono il motivo.", "Rilievo", "0: rilievo originale in modalità originale, piatto nelle altre modalità. Valori maggiori rafforzano il rilievo senza aggiungere geometria.", "Spostamento UV X", "Spostamento UV Y", "Sposta la texture tra 0 e 300.", "Rotazione", "Ruota texture e normali tra 0 e 360 gradi.", "Apri cartella texture", "Apre Textures/Grass per Grass_BaseColor.png.", "Ricarica texture erba", "Ricarica Grass_BaseColor.png. Scegliere modalità 2; PNG quadrato fino a 4096 pixel.", "Ripristina", "Ripristina l’erba e tutti e tre i profili.", "Copia Grass_BaseColor.png in Textures/Grass e ricarica. PNG quadrato fino a 4096 pixel." }; break;
                default: t = new[] { "Sidewalk grass", "Adjust sidewalk grass", "Adjusts grass strips along roads and sidewalks.", "Grass texture", "0: Original. 1: Generated grass. 2: Custom Grass_BaseColor.png.", "Texture size", "50 to 3000. Higher values enlarge the grass pattern.", "Depth", "0: authored relief in original mode, flat in generated/custom modes. Higher values strengthen relief without adding geometry.", "UV offset X", "UV offset Y", "Shifts the grass texture from 0 to 300.", "Rotation", "Rotates the grass texture and normal map from 0 to 360 degrees.", "Open texture folder", "Opens Textures/Grass for Grass_BaseColor.png.", "Reload custom grass texture", "Reloads Grass_BaseColor.png. Select mode 2; square PNG up to 4096 pixels.", "Reset", "Resets grass settings and all three mode profiles.", "Place Grass_BaseColor.png in Textures/Grass, then reload it. Square PNG up to 4096 pixels." }; break;
            }
            entries[m_Setting.GetOptionTabLocaleID(Setting.GrassSection)] = t[0];
            entries[m_Setting.GetOptionGroupLocaleID(Setting.GrassGroup)] = t[0];
            entries["AdvancedGraphics.UI.GrassTitle"] = t[0];
            string[] properties = { nameof(Setting.GrassEnabled), nameof(Setting.GrassDetail),
                nameof(Setting.GrassUvSizePercent), nameof(Setting.GrassDepthStrengthPercent),
                nameof(Setting.GrassUvOffsetX), nameof(Setting.GrassUvOffsetY), nameof(Setting.GrassUvRotation),
                nameof(Setting.OpenGrassTextureFolder), nameof(Setting.ReloadGrassTexture), nameof(Setting.ResetGrass) };
            string[] ui = { "Enabled", "Detail", "Uv", "Depth", "OffsetX", "OffsetY", "Rotation", "Open", "Reload", "Reset" };
            int[] labels = { 1, 3, 5, 7, 9, 10, 12, 14, 16, 18 };
            int[] tips = { 2, 4, 6, 8, 11, 11, 13, 15, 17, 19 };
            for (int i = 0; i < properties.Length; i++)
            {
                entries[m_Setting.GetOptionLabelLocaleID(properties[i])] = t[labels[i]];
                entries[m_Setting.GetOptionDescLocaleID(properties[i])] = t[tips[i]];
                entries["AdvancedGraphics.UI.Grass" + ui[i]] = t[labels[i]];
                entries["AdvancedGraphics.UI.GrassTip" + ui[i]] = t[tips[i]];
            }
            entries["AdvancedGraphics.UI.GrassCustomHint"] = t[20];
        }

        private void AddFootpathEntries(Dictionary<string, string> entries)
        {
            string[] t;
            switch (m_Language)
            {
                case "de": t = new[] { "Fußweg", "Fußweg anpassen", "Passt frei platzierbare gepflasterte Fußwege an.", "Fußwegtextur", "0: Original. 1: Generierter Schotter. 2: Eigene Footpath_BaseColor.png.", "Texturgröße", "10 bis 3000. Größere Werte vergrößern das Pflastermuster.", "Tiefenwirkung", "0: Originalrelief im Originalmodus, flach bei generierter/eigener Textur. Höhere Werte verstärken das Relief. Keine echte Geometrie.", "UV-Versatz X", "UV-Versatz Y", "Verschiebt die Fußwegtextur zwischen 0 und 300.", "Drehung", "Dreht Fußwegtextur und Normalmap zwischen 0 und 360 Grad.", "Texturordner öffnen", "Öffnet Textures/Footpath für Footpath_BaseColor.png.", "Eigene Fußwegtextur neu laden", "Lädt Footpath_BaseColor.png erneut. Modus 2 wählen; quadratische PNG bis 4096 Pixel.", "Zurücksetzen", "Setzt Fußweg und alle drei Modusprofile zurück.", "Footpath_BaseColor.png in Textures/Footpath ablegen und neu laden. Quadratische PNG bis 4096 Pixel." }; break;
                case "es": t = new[] { "Sendero peatonal", "Ajustar sendero peatonal", "Ajusta los senderos pavimentados independientes.", "Textura del sendero", "0: Original. 1: Grava generada. 2: Footpath_BaseColor.png propia.", "Tamaño de textura", "De 10 a 3000. Valores mayores amplían el patrón.", "Relieve", "0: relieve original en modo original, plano en otros modos. Valores mayores refuerzan el relieve sin añadir geometría.", "Desplazamiento UV X", "Desplazamiento UV Y", "Desplaza la textura entre 0 y 300.", "Rotación", "Gira la textura y las normales entre 0 y 360 grados.", "Abrir carpeta de texturas", "Abre Textures/Footpath para Footpath_BaseColor.png.", "Recargar textura del sendero", "Recarga Footpath_BaseColor.png. Selecciona el modo 2; PNG cuadrado de hasta 4096 píxeles.", "Restablecer", "Restablece el sendero y los tres perfiles de modo.", "Coloca Footpath_BaseColor.png en Textures/Footpath y recarga. PNG cuadrado de hasta 4096 píxeles." }; break;
                case "fr": t = new[] { "Chemin piéton", "Ajuster le chemin piéton", "Ajuste les chemins pavés indépendants.", "Texture du chemin", "0 : Original. 1 : Gravier généré. 2 : Votre Footpath_BaseColor.png.", "Taille de texture", "De 10 à 3000. Les valeurs élevées agrandissent le motif.", "Relief", "0 : relief original en mode original, plat dans les autres modes. Les valeurs élevées renforcent le relief sans ajouter de géométrie.", "Décalage UV X", "Décalage UV Y", "Décale la texture de 0 à 300.", "Rotation", "Tourne la texture et les normales de 0 à 360 degrés.", "Ouvrir le dossier des textures", "Ouvre Textures/Footpath pour Footpath_BaseColor.png.", "Recharger la texture du chemin", "Recharge Footpath_BaseColor.png. Choisir le mode 2 ; PNG carré jusqu’à 4096 pixels.", "Réinitialiser", "Réinitialise le chemin et les trois profils de mode.", "Placer Footpath_BaseColor.png dans Textures/Footpath et recharger. PNG carré jusqu’à 4096 pixels." }; break;
                case "it": t = new[] { "Percorso pedonale", "Regola percorso pedonale", "Regola i percorsi pavimentati indipendenti.", "Texture percorso", "0: Originale. 1: Ghiaia generata. 2: Footpath_BaseColor.png personale.", "Dimensione texture", "Da 10 a 3000. Valori maggiori ingrandiscono il motivo.", "Rilievo", "0: rilievo originale in modalità originale, piatto nelle altre modalità. Valori maggiori rafforzano il rilievo senza aggiungere geometria.", "Spostamento UV X", "Spostamento UV Y", "Sposta la texture tra 0 e 300.", "Rotazione", "Ruota texture e normali tra 0 e 360 gradi.", "Apri cartella texture", "Apre Textures/Footpath per Footpath_BaseColor.png.", "Ricarica texture percorso", "Ricarica Footpath_BaseColor.png. Scegliere modalità 2; PNG quadrato fino a 4096 pixel.", "Ripristina", "Ripristina il percorso e tutti e tre i profili.", "Copia Footpath_BaseColor.png in Textures/Footpath e ricarica. PNG quadrato fino a 4096 pixel." }; break;
                default: t = new[] { "Footpath", "Adjust footpath", "Adjusts independently placed paved footpaths.", "Footpath texture", "0: Original. 1: Generated gravel. 2: Custom Footpath_BaseColor.png.", "Texture size", "10 to 3000. Higher values enlarge the paving pattern.", "Depth", "0: authored relief in original mode, flat in generated/custom modes. Higher values strengthen relief without adding geometry.", "UV offset X", "UV offset Y", "Shifts the footpath texture from 0 to 300.", "Rotation", "Rotates the footpath texture and normal map from 0 to 360 degrees.", "Open texture folder", "Opens Textures/Footpath for Footpath_BaseColor.png.", "Reload custom footpath texture", "Reloads Footpath_BaseColor.png. Select mode 2; square PNG up to 4096 pixels.", "Reset", "Resets footpath settings and all three mode profiles.", "Place Footpath_BaseColor.png in Textures/Footpath, then reload it. Square PNG up to 4096 pixels." }; break;
            }
            entries[m_Setting.GetOptionTabLocaleID(Setting.FootpathSection)] = t[0];
            entries[m_Setting.GetOptionGroupLocaleID(Setting.FootpathGroup)] = t[0];
            entries["AdvancedGraphics.UI.FootpathTitle"] = t[0];
            string[] properties = { nameof(Setting.FootpathEnabled), nameof(Setting.FootpathDetail),
                nameof(Setting.FootpathUvSizePercent), nameof(Setting.FootpathDepthStrengthPercent),
                nameof(Setting.FootpathUvOffsetX), nameof(Setting.FootpathUvOffsetY), nameof(Setting.FootpathUvRotation),
                nameof(Setting.OpenFootpathTextureFolder), nameof(Setting.ReloadFootpathTexture), nameof(Setting.ResetFootpath) };
            string[] ui = { "Enabled", "Detail", "Uv", "Depth", "OffsetX", "OffsetY", "Rotation", "Open", "Reload", "Reset" };
            int[] labels = { 1, 3, 5, 7, 9, 10, 12, 14, 16, 18 };
            int[] tips = { 2, 4, 6, 8, 11, 11, 13, 15, 17, 19 };
            for (int i = 0; i < properties.Length; i++)
            {
                entries[m_Setting.GetOptionLabelLocaleID(properties[i])] = t[labels[i]];
                entries[m_Setting.GetOptionDescLocaleID(properties[i])] = t[tips[i]];
                entries["AdvancedGraphics.UI.Footpath" + ui[i]] = t[labels[i]];
                entries["AdvancedGraphics.UI.FootpathTip" + ui[i]] = t[tips[i]];
            }
            entries["AdvancedGraphics.UI.FootpathCustomHint"] = t[20];
        }

        private void AddCustomCrackEntries(Dictionary<string, string> entries)
        {
            string[] labels;
            switch (m_Language)
            {
                case "de": labels = new[] { "Eigene Riss-Textur", "Road_Cracks.png in den Riss-Ordner kopieren: dunkle Risse auf weißem oder transparentem Hintergrund, maximal 4096 × 4096 Pixel. Tiefenwirkung wird automatisch berechnet. Anzahl muss größer als 0 sein. Breite streckt das Muster quer.", "Riss-Ordner öffnen", "Riss-Textur neu laden", "Wird bei aktivierter Straße und Rissanzahl größer als 0 geladen.", "Eigene Riss-Textur geladen.", "Road_Cracks.png fehlt; generierte Risse werden verwendet.", "PNG ungültig oder ohne dunkle Risse; generierte Risse werden verwendet." }; break;
                default: labels = new[] { "Custom crack texture", "Copy Road_Cracks.png to the crack folder: dark cracks on white or transparent background, up to 4096 × 4096 pixels. Relief is generated automatically. Set count above 0. Width stretches the pattern sideways.", "Open crack folder", "Reload crack texture", "Loads when road adjustments are enabled and crack count is above 0.", "Custom crack texture loaded.", "Road_Cracks.png missing; using generated cracks.", "Invalid PNG or no dark cracks; using generated cracks." }; break;
                case "es": labels = new[] { "Textura de grietas propia", "Copia Road_Cracks.png en la carpeta de grietas: grietas oscuras sobre fondo blanco o transparente, hasta 4096 × 4096 píxeles. Relieve automático. Cantidad mayor que 0. El ancho estira el patrón lateralmente.", "Abrir carpeta de grietas", "Recargar textura de grietas", "Se carga con los ajustes de carretera activados y cantidad mayor que 0.", "Textura de grietas cargada.", "Falta Road_Cracks.png; se usan grietas generadas.", "PNG no válido o sin grietas oscuras; se usan grietas generadas." }; break;
                case "fr": labels = new[] { "Texture de fissures personnalisée", "Copiez Road_Cracks.png dans le dossier des fissures : fissures sombres sur fond blanc ou transparent, jusqu’à 4096 × 4096 pixels. Relief automatique. Nombre supérieur à 0. La largeur étire le motif latéralement.", "Ouvrir le dossier des fissures", "Recharger la texture de fissures", "Chargement avec les réglages de route activés et un nombre supérieur à 0.", "Texture de fissures chargée.", "Road_Cracks.png absent ; fissures générées utilisées.", "PNG invalide ou sans fissures sombres ; fissures générées utilisées." }; break;
                case "it": labels = new[] { "Texture crepe personalizzata", "Copia Road_Cracks.png nella cartella delle crepe: crepe scure su sfondo bianco o trasparente, fino a 4096 × 4096 pixel. Rilievo automatico. Quantità maggiore di 0. La larghezza allunga il motivo lateralmente.", "Apri cartella crepe", "Ricarica texture crepe", "Caricamento con regolazioni stradali attive e quantità maggiore di 0.", "Texture crepe caricata.", "Road_Cracks.png mancante; vengono usate crepe generate.", "PNG non valido o senza crepe scure; vengono usate crepe generate." }; break;
            }
            string[] keys = { "CustomCracks", "CustomCracksHint", "OpenCrackFolder", "ReloadCracks", "CustomCracksPending", "CustomCracksLoaded", "CustomCracksMissing", "CustomCracksInvalid" };
            for (int i = 0; i < keys.Length; i++) entries["AdvancedGraphics.UI." + keys[i]] = labels[i];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.UseCustomRoadCracks))] = labels[0];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.UseCustomRoadCracks))] = labels[1];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenRoadCrackFolder))] = labels[2];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenRoadCrackFolder))] = labels[1];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ReloadRoadCracks))] = labels[3];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ReloadRoadCracks))] = labels[1];
        }

        private void AddLuminaNumberFixEntries(Dictionary<string, string> entries)
        {
            string label, description;
            switch (m_Language)
            {
                case "de":
                    label = "Lumina-Zahlenkorrektur";
                    description = "Behebt Kästchen durch Sonderleerzeichen in Zahlenanzeigen bei aktivem Lumina, einschließlich Angestellte, Stufe, Lager und Winkel beim Straßenbau. Standardmäßig ausgeschaltet.";
                    break;
                case "es":
                    label = "Corrección de cifras para Lumina";
                    description = "Corrige los cuadros causados por espacios especiales en cifras con Lumina activo, incluidos empleados, nivel, almacenamiento y ángulos al construir carreteras. Desactivada por defecto.";
                    break;
                case "fr":
                    label = "Correction des nombres pour Lumina";
                    description = "Corrige les carrés causés par des espaces spéciaux dans les nombres avec Lumina activé, notamment les employés, le niveau, le stockage et les angles lors de la construction de routes. Désactivée par défaut.";
                    break;
                case "it":
                    label = "Correzione dei numeri per Lumina";
                    description = "Corregge i quadratini causati da spazi speciali nei numeri con Lumina attivo, inclusi dipendenti, livello, magazzino e angoli durante la costruzione di strade. Disattivata per impostazione predefinita.";
                    break;
                default:
                    label = "Lumina number display fix";
                    description = "Fixes boxes caused by special spaces in number displays when Lumina is enabled, including employees, level, storage and angles during road construction. Disabled by default.";
                    break;
            }
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.LuminaNumberFix))] = label;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.LuminaNumberFix))] = description;
        }

        private void AddEndcapEntries(Dictionary<string, string> entries)
        {
            string[] text;
            switch (m_Language)
            {
                case "de": text = new[] { "Abschluss-Kachelung U", "Abschluss-Kachelung V", "Straßentyp wählen. Änderungen und Zurücksetzen wirken erst nach Übernehmen. Höhere Werte wiederholen die Textur häufiger. Gemeinsame Alley-/Parkplatz-Endstücke verwenden die Einbahn-Gehweg-Werte.", "Abschluss-Kachelung zurücksetzen", "Normale Straßen", "Einbahn-Gehwege", "Parkplatzstraßen", "Übernehmen" }; break;
                case "fr": text = new[] { "Répétition des extrémités U", "Répétition des extrémités V", "Choisissez le type de route. Modifications et réinitialisation prennent effet après Appliquer. Les valeurs élevées répètent davantage la texture. Les extrémités communes aux petites routes et parkings utilisent les valeurs des petites routes.", "Réinitialiser les extrémités", "Routes normales", "Petites routes", "Routes de stationnement", "Appliquer" }; break;
                case "es": text = new[] { "Repetición en extremos U", "Repetición en extremos V", "Elige el tipo de carretera. Los cambios y el restablecimiento se aplican con Aplicar. Los valores altos repiten más la textura. Los extremos compartidos entre calles pequeñas y aparcamientos usan los valores de calles pequeñas.", "Restablecer extremos", "Carreteras normales", "Calles pequeñas", "Calles de aparcamiento", "Aplicar" }; break;
                case "it": text = new[] { "Ripetizione estremità U", "Ripetizione estremità V", "Scegli il tipo di strada. Modifiche e ripristino hanno effetto dopo Applica. Valori alti ripetono di più la texture. Le estremità condivise fra strade piccole e parcheggi usano i valori delle strade piccole.", "Ripristina estremità", "Strade normali", "Strade piccole", "Strade con parcheggi", "Applica" }; break;
                default: text = new[] { "End tiling U", "End tiling V", "Choose a road type. Changes and Reset take effect only after Apply. Higher values repeat the texture more often. Shared small-road/parking end meshes use the small-road values.", "Reset end tiling", "Normal roads", "Small-road sidewalks", "Parking roads", "Apply" }; break;
            }
            entries["AdvancedGraphics.UI.endcapU"] = text[0];
            entries["AdvancedGraphics.UI.endcapV"] = text[1];
            entries["AdvancedGraphics.UI.EndcapHint"] = text[2];
            entries["AdvancedGraphics.UI.ResetEndcaps"] = text[3];
            entries["AdvancedGraphics.UI.EndcapNormal"] = text[4];
            string large = m_Language == "de" ? "Große Straßen (ab 32 m)" : m_Language == "fr" ? "Grandes routes (32 m et plus)"
                : m_Language == "es" ? "Carreteras grandes (32 m o más)" : m_Language == "it" ? "Strade grandi (da 32 m)" : "Large roads (32 m+)";
            entries["AdvancedGraphics.UI.EndcapLarge"] = large;
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.LargeRoadEndcapUDraft))] = large + " – " + text[0];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.LargeRoadEndcapUDraft))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.LargeRoadEndcapVDraft))] = large + " – " + text[1];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.LargeRoadEndcapVDraft))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ApplyLargeRoadEndcap))] = large + " – " + text[7];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ApplyLargeRoadEndcap))] = text[2];
            entries["AdvancedGraphics.UI.EndcapAlley"] = text[5];
            entries["AdvancedGraphics.UI.EndcapParking"] = text[6];
            entries["AdvancedGraphics.UI.EndcapRoadEnd"] = m_Language == "de" ? "Normale Straßen: runde Enden (< 32 m)" : m_Language == "fr" ? "Routes normales : extrémités arrondies (< 32 m)"
                : m_Language == "es" ? "Carreteras normales: extremos redondos (< 32 m)" : m_Language == "it" ? "Strade normali: estremità arrotondate (< 32 m)" : "Normal roads: round ends (< 32 m)";
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.SidewalkEndcapU))] = text[4] + " – " + text[0];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.SidewalkEndcapU))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.SidewalkEndcapUDraft))] = text[4] + " – " + text[0];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.SidewalkEndcapUDraft))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.SidewalkEndcapV))] = text[4] + " – " + text[1];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.SidewalkEndcapV))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.SidewalkEndcapVDraft))] = text[4] + " – " + text[1];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.SidewalkEndcapVDraft))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ApplySidewalkEndcap))] = text[7] + " – " + text[4];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ApplySidewalkEndcap))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.AlleyEndcapU))] = text[5] + " – " + text[0];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.AlleyEndcapU))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.AlleyEndcapUDraft))] = text[5] + " – " + text[0];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.AlleyEndcapUDraft))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.AlleyEndcapV))] = text[5] + " – " + text[1];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.AlleyEndcapV))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.AlleyEndcapVDraft))] = text[5] + " – " + text[1];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.AlleyEndcapVDraft))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ApplyAlleyEndcap))] = text[7] + " – " + text[5];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ApplyAlleyEndcap))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ParkingEndcapU))] = text[6] + " – " + text[0];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ParkingEndcapU))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ParkingEndcapUDraft))] = text[6] + " – " + text[0];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ParkingEndcapUDraft))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ParkingEndcapV))] = text[6] + " – " + text[1];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ParkingEndcapV))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ParkingEndcapVDraft))] = text[6] + " – " + text[1];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ParkingEndcapVDraft))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ApplyParkingEndcap))] = text[7] + " – " + text[6];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ApplyParkingEndcap))] = text[2];
        }

        private void AddNormalToggleEntries(Dictionary<string, string> entries)
        {
            string label, hint;
            switch (m_Language)
            {
                case "de": label = "Eigene Normalmap verwenden"; hint = "Standard aus: bisherige automatische Tiefenberechnung. An: optionale Normalmap verwenden, wenn vorhanden. Im Straßen-Tab gilt dies auch für Riss-Normalmaps. Mitgelieferte generierte Texturen bleiben unverändert."; break;
                case "es": label = "Usar normal propia"; hint = "Desactivado por defecto: relieve automático. Activado: normal opcional si existe. En Carretera incluye las grietas. Las texturas generadas no cambian."; break;
                case "fr": label = "Utiliser une normale personnelle"; hint = "Désactivé par défaut : relief automatique. Activé : normale facultative si présente. Route inclut les fissures. Les textures générées restent inchangées."; break;
                case "it": label = "Usa normale personale"; hint = "Disattivata per default: rilievo automatico. Attivata: normale facoltativa se presente. Strada include le crepe. Le texture generate non cambiano."; break;
                default: label = "Use custom normal map"; hint = "Off by default: previous automatic relief. On: use optional normal maps when available. The Road tab also controls crack normals. Bundled generated textures are unchanged."; break;
            }
            entries["AdvancedGraphics.UI.UseCustomNormal"] = label;
            entries["AdvancedGraphics.UI.UseCustomNormalHint"] = hint;
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.UseRoadNormalMap))] = label;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.UseRoadNormalMap))] = hint;
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.UseSidewalkNormalMap))] = label;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.UseSidewalkNormalMap))] = hint;
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.UseAlleySidewalkNormalMap))] = label;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.UseAlleySidewalkNormalMap))] = hint;
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.UseRoadGravelNormalMap))] = label;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.UseRoadGravelNormalMap))] = hint;
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.UseGravelShoulderNormalMap))] = label;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.UseGravelShoulderNormalMap))] = hint;
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.UseRailNormalMap))] = label;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.UseRailNormalMap))] = hint;
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.UseGrassNormalMap))] = label;
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.UseFootpathNormalMap))] = label;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.UseGrassNormalMap))] = hint;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.UseFootpathNormalMap))] = hint;
        }

        private void AddTextureFileControls(Dictionary<string, string> entries)
        {
            string label, hint, files, cracks;
            switch (m_Language)
            {
                case "de":
                    label = "Alle Custom-Texturen neu laden";
                    hint = "Lädt alle eigenen Farbtexturen, optionalen Normalmaps und die Straßen-Rissmaske neu. Einstellungen bleiben erhalten.";
                    files = "Farbe: {0}_BaseColor.png · Normalmap (optional): {0}_Normal.png. Beide im selben Texturordner.";
                    cracks = "Road_Cracks.png + optional Road_Cracks_Normal.png im Riss-Ordner. Gleiche Bildmaße, Normalmap in RGB/OpenGL (+Y). Ohne Normalmap wird die Tiefe automatisch berechnet."; break;
                case "es":
                    label = "Recargar todas las texturas propias";
                    hint = "Recarga colores, normales opcionales y la máscara de grietas. Conserva los ajustes.";
                    files = "Color: {0}_BaseColor.png · Normal opcional: {0}_Normal.png. Ambos en la misma carpeta.";
                    cracks = "Road_Cracks.png + Road_Cracks_Normal.png opcional en la carpeta de grietas. Mismas dimensiones, normal RGB/OpenGL (+Y). Sin normal, relieve automático."; break;
                case "fr":
                    label = "Recharger toutes les textures personnelles";
                    hint = "Recharge les couleurs, les normales facultatives et le masque de fissures. Conserve les réglages.";
                    files = "Couleur : {0}_BaseColor.png · Normale facultative : {0}_Normal.png. Dans le même dossier.";
                    cracks = "Road_Cracks.png + Road_Cracks_Normal.png facultative dans le dossier des fissures. Dimensions identiques, normale RGB/OpenGL (+Y). Sans normale, relief automatique."; break;
                case "it":
                    label = "Ricarica tutte le texture personali";
                    hint = "Ricarica colori, normali facoltative e maschera delle crepe. Mantiene le impostazioni.";
                    files = "Colore: {0}_BaseColor.png · Normale facoltativa: {0}_Normal.png. Nella stessa cartella.";
                    cracks = "Road_Cracks.png + Road_Cracks_Normal.png facoltativa nella cartella delle crepe. Stesse dimensioni, normale RGB/OpenGL (+Y). Senza normale, rilievo automatico."; break;
                default:
                    label = "Reload all custom textures";
                    hint = "Reloads all custom color textures, optional normal maps and the road crack mask. Keeps your settings.";
                    files = "Color: {0}_BaseColor.png · Optional normal: {0}_Normal.png. Both in the same texture folder.";
                    cracks = "Road_Cracks.png + optional Road_Cracks_Normal.png in the crack folder. Matching dimensions, RGB/OpenGL (+Y) normal. Without a normal map, relief is generated automatically."; break;
            }
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ReloadAllCustomTextures))] = label;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ReloadAllCustomTextures))] = hint;
            entries["AdvancedGraphics.UI.ReloadAllCustomTextures"] = label;
            entries["AdvancedGraphics.UI.ReloadAllCustomTexturesHint"] = hint;
            entries["AdvancedGraphics.UI.CracksNormalInfo"] = cracks;
            entries["AdvancedGraphics.UI.CustomCracksHint"] = cracks;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.UseCustomRoadCracks))] = cracks;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ReloadRoadCracks))] = cracks;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenRoadCrackFolder))] = cracks;
            string[] surfaces = { "road", "sidewalk", "alley", "gravel", "shoulder", "rail", "grass", "footpath" };
            string[] names = { "Road", "Sidewalk", "AlleySidewalk", "RoadGravel", "GravelShoulder", "RailGravel", "Grass", "Footpath" };
            for (int i = 0; i < surfaces.Length; i++)
                entries["AdvancedGraphics.UI.CustomFiles_" + surfaces[i]] = string.Format(files, names[i]);
        }

        private void AddSurfaceSamplingEntries(Dictionary<string, string> entries)
        {
            string[] text;
            switch (m_Language)
            {
                case "de": text = new[] { "Textur-Mip-Bias", "Gilt gemeinsam für alle von dieser Mod bearbeiteten Oberflächen, einschließlich Parkflächen. Negative Werte erhalten mehr Details in der Entfernung, können aber Flimmern verstärken. Positive Werte glätten stärker. Bereich -1 bis 1; Standard 0. Anisotrope Filterung: 16×.", "Allgemeine Textureinstellungen", "Gilt gemeinsam für alle von der Mod bearbeiteten Oberflächen.", "Anisotrope Filterung: 16×" }; break;
                case "es": text = new[] { "Sesgo mip de texturas", "Se aplica a todas las superficies modificadas por este mod, incluidos los aparcamientos. Los valores negativos conservan más detalles a distancia, pero pueden aumentar el parpadeo. Los positivos suavizan más. Rango de -1 a 1; predeterminado 0. Filtrado anisotrópico: 16×.", "Ajustes generales de texturas", "Se aplica a todas las superficies modificadas por el mod.", "Filtrado anisotrópico: 16×" }; break;
                case "fr": text = new[] { "Biais mip des textures", "S’applique à toutes les surfaces modifiées par ce mod, y compris les places de stationnement. Les valeurs négatives préservent les détails au loin mais peuvent accentuer le scintillement. Les valeurs positives lissent davantage. Plage de -1 à 1 ; défaut 0. Filtrage anisotrope : 16×.", "Paramètres généraux des textures", "S’applique à toutes les surfaces modifiées par le mod.", "Filtrage anisotrope : 16×" }; break;
                case "it": text = new[] { "Bias mip delle texture", "Si applica a tutte le superfici modificate da questa mod, inclusi i parcheggi. I valori negativi preservano i dettagli a distanza ma possono aumentare lo sfarfallio. I valori positivi rendono le texture più morbide. Intervallo da -1 a 1; predefinito 0. Filtro anisotropico: 16×.", "Impostazioni generali delle texture", "Si applica a tutte le superfici modificate dalla mod.", "Filtro anisotropico: 16×" }; break;
                default: text = new[] { "Texture mip bias", "Applies to all surfaces edited by this mod, including parking spaces. Negative values preserve more distant detail but may increase shimmering. Positive values soften textures. Range -1 to 1; default 0. Anisotropic filtering: 16×.", "General texture settings", "Applies to all surfaces edited by this mod.", "Anisotropic filtering: 16×" }; break;
            }
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.SurfaceTextureMipBias))] = text[0];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.SurfaceTextureMipBias))] = text[1];
            entries[m_Setting.GetOptionGroupLocaleID(Setting.TextureGeneralGroup)] = text[2];
            entries["AdvancedGraphics.UI.TextureGeneralTitle"] = text[2];
            entries["AdvancedGraphics.UI.TextureGeneralHint"] = text[3];
            entries["AdvancedGraphics.UI.TextureFiltering16"] = text[4];
            entries["AdvancedGraphics.UI.SurfaceMipBias"] = text[0];
            entries["AdvancedGraphics.UI.TipSurfaceMipBias"] = text[1];
        }

        private void AddParkingDepthEntries(Dictionary<string, string> entries)
        {
            string[] text;
            switch (m_Language)
            {
                case "de": text = new[] { "Parkflächen-Tiefenfaktor", "Multipliziert die Tiefenwirkung der Straße auf Parkflächen. 0 = flach; 1 = unverändert; 2 = stärker. Bereich 0 bis 8 in 0,1-Schritten. Für sichtbare Wirkung muss die Straßen-Tiefenwirkung größer als 0 sein. Getrennt je Straßen-Texturmodus gespeichert." }; break;
                case "es": text = new[] { "Factor de relieve del aparcamiento", "Multiplica el relieve de la carretera en las plazas de aparcamiento. 0 = plano; 1 = sin cambios; 2 = más fuerte. De 0 a 8 en pasos de 0,1. El relieve de la carretera debe ser mayor que 0. Se guarda por modo de textura." }; break;
                case "fr": text = new[] { "Facteur de relief du stationnement", "Multiplie le relief de la route sur les places de stationnement. 0 = plat ; 1 = inchangé ; 2 = plus fort. De 0 à 8 par pas de 0,1. Le relief de la route doit être supérieur à 0. Enregistré par mode de texture." }; break;
                case "it": text = new[] { "Fattore rilievo parcheggio", "Moltiplica il rilievo stradale nei posti auto. 0 = piatto; 1 = invariato; 2 = più forte. Da 0 a 8 con passi di 0,1. Il rilievo stradale deve essere maggiore di 0. Salvato per modalità texture." }; break;
                default: text = new[] { "Parking depth factor", "Multiplies road surface depth on parking spaces. 0 = flat; 1 = unchanged; 2 = stronger. Range 0 to 8 in 0.1 steps. Road surface depth must be above 0 for a visible effect. Saved separately per road texture mode." }; break;
            }
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ParkingDepthStrengthFactor))] = text[0];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ParkingDepthStrengthFactor))] = text[1];
            entries["AdvancedGraphics.UI.ParkingDepth"] = text[0];
            entries["AdvancedGraphics.UI.TipParkingDepth"] = text[1];
        }

        private void AddParkingScaleEntries(Dictionary<string, string> entries)
        {
            string label, description;
            switch (m_Language)
            {
                case "de":
                    label = "Parkflächen-Faktor";
                    description = "Skalierung der Asphalttextur auf Parkflächen: 0,1 bis 8,0 in 0,1-Schritten. 1,0 = ursprüngliche Mod-Skalierung; höher = feinere, kleiner wirkende Struktur, niedriger = gröbere Struktur. Wirkt auf Farbe und Tiefenwirkung. Wird für Original, Generiert und eigene PNG separat gespeichert.";
                    break;
                case "es":
                    label = "Factor de aparcamiento";
                    description = "Escala del asfalto en las plazas de aparcamiento: de 0,1 a 8,0, en pasos de 0,1. 1,0 = escala inicial del mod; mayor = textura más fina y pequeña, menor = textura más gruesa. Afecta al color y al relieve. Se guarda por separado para Original, Generada y PNG propio.";
                    break;
                case "fr":
                    label = "Facteur de stationnement";
                    description = "Échelle de l'asphalte des places de stationnement : de 0,1 à 8,0, par pas de 0,1. 1,0 = échelle initiale du mod ; plus haut = texture plus fine et plus petite, plus bas = texture plus grossière. Modifie la couleur et le relief. Enregistré séparément pour Original, Générée et PNG personnalisé.";
                    break;
                case "it":
                    label = "Fattore parcheggio";
                    description = "Scala dell'asfalto dei posti auto: da 0,1 a 8,0, con incrementi di 0,1. 1,0 = scala iniziale della mod; più alto = trama più fine e piccola, più basso = trama più grossolana. Agisce sul colore e sul rilievo. Salvato separatamente per Originale, Generata e PNG personalizzato.";
                    break;
                default:
                    label = "Parking surface factor";
                    description = "Asphalt scale on parking spaces: 0.1 to 8.0 in 0.1 steps. 1.0 = initial mod scale; higher = finer, smaller texture, lower = coarser texture. Affects color and surface depth. Saved separately for Original, Generated and Custom texture.";
                    break;
            }
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ParkingTextureScaleFactor))] = label;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ParkingTextureScaleFactor))] = description;
            entries["AdvancedGraphics.UI.ParkingScale"] = label;
            entries["AdvancedGraphics.UI.TipParkingScale"] = description;
        }

        private void AddParkingSidewalkEntries(Dictionary<string, string> entries)
        {
            string u, v, tip;
            switch (m_Language)
            {
                case "de":
                    u = "Parkstraße Gehweg U"; v = "Parkstraße Gehweg V";
                    tip = "Feinabgleich des Gehwegs auf der Parkseite von Parkstraßen, damit er wie der normale Gehweg auf der anderen Seite aussieht. 1,0 = Standard der Mod; wird für Original, Generiert und eigene PNG getrennt gespeichert; höher = mehr Wiederholungen (kleinere Platten), niedriger = größere Platten. U = quer zur Straße, V = entlang der Straße.";
                    break;
                case "es":
                    u = "Acera de aparcamiento U"; v = "Acera de aparcamiento V";
                    tip = "Ajuste fino de la acera del lado de aparcamiento para que coincida con la acera normal del otro lado. 1,0 = escala por defecto del mod; se guarda por separado para Original, Generada y PNG propio; mayor = más repeticiones (losas más pequeñas), menor = losas más grandes. U = a lo ancho de la calle, V = a lo largo.";
                    break;
                case "fr":
                    u = "Trottoir stationnement U"; v = "Trottoir stationnement V";
                    tip = "Réglage fin du trottoir côté stationnement pour qu'il ressemble au trottoir normal de l'autre côté. 1,0 = échelle par défaut du mod ; enregistré séparément pour Original, Générée et PNG personnalisé ; plus haut = plus de répétitions (dalles plus petites), plus bas = dalles plus grandes. U = en travers de la rue, V = le long de la rue.";
                    break;
                case "it":
                    u = "Marciapiede parcheggio U"; v = "Marciapiede parcheggio V";
                    tip = "Regolazione fine del marciapiede sul lato parcheggio affinché corrisponda al marciapiede normale dall'altro lato. 1,0 = scala predefinita della mod; salvato separatamente per Originale, Generata e PNG personalizzato; più alto = più ripetizioni (lastre più piccole), più basso = lastre più grandi. U = trasversale alla strada, V = lungo la strada.";
                    break;
                default:
                    u = "Parking road sidewalk U"; v = "Parking road sidewalk V";
                    tip = "Fine tuning of the sidewalk on the parking side of parking roads so it matches the normal sidewalk on the other side. 1.0 = mod default; saved separately for Original, Generated and Custom texture; higher = more repeats (smaller slabs), lower = larger slabs. U = across the road, V = along the road.";
                    break;
            }
            string shiftU, shiftV, shiftTip;
            switch (m_Language)
            {
                case "de":
                    shiftU = "Normaler Gehweg Versatz quer"; shiftV = "Normaler Gehweg Versatz längs";
                    shiftTip = "Verschiebt nur das Plattenmuster des normalen Gehwegs (nicht die Parkseite), in Prozent einer Kachel: quer = Längsfugen nach links/rechts, längs = Querfugen nach vorne/hinten. Die Plattengröße bleibt gleich. Wird für Original, Generiert und eigene PNG getrennt gespeichert.";
                    break;
                case "es":
                    shiftU = "Desplazamiento acera normal (ancho)"; shiftV = "Desplazamiento acera normal (largo)";
                    shiftTip = "Desplaza solo el patrón de losas de la acera normal (no el lado de aparcamiento), en porcentaje de un mosaico. El tamaño de las losas no cambia. Se guarda por separado para Original, Generada y PNG propio.";
                    break;
                case "fr":
                    shiftU = "Décalage trottoir normal (travers)"; shiftV = "Décalage trottoir normal (long)";
                    shiftTip = "Décale uniquement le motif des dalles du trottoir normal (pas le côté stationnement), en pourcentage d'une tuile. La taille des dalles ne change pas. Enregistré séparément pour Original, Générée et PNG personnalisé.";
                    break;
                case "it":
                    shiftU = "Spostamento marciapiede normale (trasversale)"; shiftV = "Spostamento marciapiede normale (longitudinale)";
                    shiftTip = "Sposta solo il motivo delle lastre del marciapiede normale (non il lato parcheggio), in percentuale di una piastrella. La dimensione delle lastre non cambia. Salvato separatamente per Originale, Generata e PNG personalizzato.";
                    break;
                default:
                    shiftU = "Normal sidewalk shift across"; shiftV = "Normal sidewalk shift along";
                    shiftTip = "Shifts only the slab pattern of the normal sidewalk (not the parking side), in percent of one tile: across = joints along the road move left/right, along = cross joints move forward/back. Slab size stays the same. Saved separately for Original, Generated and Custom texture.";
                    break;
            }
            entries["AdvancedGraphics.UI.SidewalkShiftU"] = shiftU;
            entries["AdvancedGraphics.UI.SidewalkShiftV"] = shiftV;
            entries["AdvancedGraphics.UI.SidewalkTipShiftU"] = shiftTip;
            entries["AdvancedGraphics.UI.SidewalkTipShiftV"] = shiftTip;
            entries["AdvancedGraphics.UI.SidewalkParkingSidewalkU"] = u;
            entries["AdvancedGraphics.UI.SidewalkParkingSidewalkV"] = v;
            entries["AdvancedGraphics.UI.SidewalkTipParkingSidewalkU"] = tip;
            entries["AdvancedGraphics.UI.SidewalkTipParkingSidewalkV"] = tip;
        }

        private void AddSurfaceLayoutEntries(Dictionary<string, string> entries)
        {
            string road, alley, gravelSidewalk;
            switch (m_Language)
            {
                case "de": road = "Straße"; alley = "Einbahn-Gehweg"; gravelSidewalk = "Schotter-Gehweg"; break;
                case "es": road = "Carretera"; alley = "Acera de calle pequeña"; gravelSidewalk = "Acera de grava"; break;
                case "fr": road = "Route"; alley = "Trottoir de petite rue"; gravelSidewalk = "Trottoir en gravier"; break;
                case "it": road = "Strada"; alley = "Marciapiede strade strette"; gravelSidewalk = "Marciapiede ghiaia"; break;
                default: road = "Road"; alley = "Small-road sidewalk"; gravelSidewalk = "Gravel sidewalk"; break;
            }
            entries["AdvancedGraphics.UI.Title"] = road;
            entries[m_Setting.GetOptionTabLocaleID(Setting.RoadSection)] = road;
            entries["AdvancedGraphics.UI.AlleySidewalk"] = alley;
            entries["AdvancedGraphics.UI.GravelShoulderTitle"] = gravelSidewalk;
            entries[m_Setting.GetOptionGroupLocaleID(Setting.ShoulderGroup)] = gravelSidewalk;
            entries[m_Setting.GetOptionTabLocaleID(Setting.GravelSidewalkSection)] = gravelSidewalk;
            entries[m_Setting.GetOptionTabLocaleID(Setting.AlleySidewalkSection)] = alley;
            entries[m_Setting.GetOptionGroupLocaleID(Setting.AlleySidewalkGroup)] = alley;
            entries[m_Setting.GetOptionGroupLocaleID(Setting.AlleySidewalkUvGroup)] = entries[m_Setting.GetOptionGroupLocaleID(Setting.SidewalkUvGroup)];
            entries[m_Setting.GetOptionGroupLocaleID(Setting.AlleySidewalkCrackGroup)] = entries[m_Setting.GetOptionGroupLocaleID(Setting.SidewalkCrackGroup)];
            foreach (var item in new List<KeyValuePair<string, string>>(entries))
            {
                if (!item.Key.StartsWith("AdvancedGraphics.UI.Sidewalk", System.StringComparison.Ordinal)) continue;
                entries[item.Key.Replace("AdvancedGraphics.UI.Sidewalk", "AdvancedGraphics.UI.AlleySidewalk")] =
                    item.Value.Replace("Sidewalk_BaseColor.png", "AlleySidewalk_BaseColor.png").Replace("/Textures/Sidewalk", "/Textures/AlleySidewalk");
            }
            entries["AdvancedGraphics.UI.AlleySidewalk"] = alley;
            var options = new[] { "SidewalkEnabled", "ShowSidewalkPanelButton", "SidewalkDetail", "OpenSidewalkTextureFolder", "ReloadSidewalkTexture",
                "SidewalkUvSizePercent", "SidewalkDepthStrengthPercent", "SidewalkUvOffsetX", "SidewalkUvOffsetY", "SidewalkUvRotation",
                "SidewalkCrackCount", "SidewalkCrackSizePercent", "SidewalkCrackWidthPercent", "SidewalkCrackOpacityPercent", "SidewalkCrackRandomnessPercent", "SidewalkTextureTiling", "ResetSidewalk" };
            foreach (var option in options)
            {
                string target = option.Replace("Sidewalk", "AlleySidewalk");
                entries[m_Setting.GetOptionLabelLocaleID(target)] = entries[m_Setting.GetOptionLabelLocaleID(option)];
                entries[m_Setting.GetOptionDescLocaleID(target)] = entries[m_Setting.GetOptionDescLocaleID(option)]
                    .Replace("Sidewalk_BaseColor.png", "AlleySidewalk_BaseColor.png")
                    .Replace("SidewalkTextures", "AlleySidewalkTextures");
            }
            entries["AdvancedGraphics.UI.AlleySidewalkEnabled"] = alley;
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.AlleySidewalkEnabled))] = alley;
            entries["AdvancedGraphics.UI.AlleySidewalkCustomHint"] = "AlleySidewalk_BaseColor.png: ModsData/Enhanced_Graphics_and_Textures/Textures/AlleySidewalk";
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShowAlleySidewalkPanelButton))] = alley + " — " + entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShowSidewalkPanelButton))];
            entries["AdvancedGraphics.UI.GravelShoulderEnabled"] = gravelSidewalk;
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.GravelShoulderEnabled))] = gravelSidewalk;
            string alleyTip;
            switch (m_Language)
            {
                case "de": alleyTip = "Passt die Gehwege kleiner Straßen separat an. Normale Straßengehwege verwenden ihre eigenen Einstellungen."; break;
                case "es": alleyTip = "Ajusta por separado las aceras de calles pequeñas."; break;
                case "fr": alleyTip = "Règle séparément les trottoirs des petites rues."; break;
                case "it": alleyTip = "Regola separatamente i marciapiedi delle strade strette."; break;
                default: alleyTip = "Adjusts small-road sidewalks independently of regular street sidewalks."; break;
            }
            entries["AdvancedGraphics.UI.AlleySidewalkTipEnabled"] = alleyTip;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.AlleySidewalkEnabled))] = alleyTip;
            if (m_Language == "de")
            {
                entries["AdvancedGraphics.UI.Enabled"] = "Straße anpassen";
                entries["AdvancedGraphics.UI.Detail"] = "Straßenstruktur";
                entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.RoadAsphaltEnabled))] = "Straße anpassen";
                entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.RoadAsphaltDetail))] = "Straßenstruktur";
                entries[m_Setting.GetOptionGroupLocaleID(Setting.RoadGroup)] = "Straße";
                entries["AdvancedGraphics.UI.AlleySidewalkEnabled"] = "Einbahn-Gehweg anpassen";
                entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.AlleySidewalkEnabled))] = "Einbahn-Gehweg anpassen";
                entries["AdvancedGraphics.UI.GravelShoulderEnabled"] = "Schotter-Gehweg anpassen";
                entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.GravelShoulderEnabled))] = "Schotter-Gehweg anpassen";
            }
        }

        private void AddRoadGravelEntries(Dictionary<string, string> entries)
        {
            string[] labels;
            string[] tips;
            switch (m_Language)
            {
                case "de":
                    labels = new[] { "Schotter", "Straßenschotter aktivieren", "Schotterstruktur", "UV-Größe", "Tiefenwirkung", "Versatz X", "Versatz Y", "Drehung", "Schottertextur neu laden", "Straßenschotter zurücksetzen", "Texturordner öffnen", "Schotter-Tabs im Spiel anzeigen", "Straßenrand", "Straßenrand aktivieren", "Rand-UV-Größe", "Rand-Tiefenwirkung", "Straßenrand zurücksetzen" };
                    tips = new[] { "Ändert das Schotter-Material der Fahrbahn.", "0 = Original; 1 = hellerer, feinerer Straßenschotter; 2 = eigene RoadGravel_BaseColor.png.", "100 = ursprüngliche Größe. Höhere Werte vergrößern die Steine.", "0 = ursprüngliche Normalmap bei Original, flach bei Generiert. Höhere Werte verstärken das passende Relief; keine echte Geometrie.", "Verschiebt die Textur horizontal.", "Verschiebt die Textur vertikal.", "Dreht Farbe und Normalmap gemeinsam.", "Lädt RoadGravel_BaseColor.png aus ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel neu.", "Stellt Originalmaterial und Standardwerte wieder her.", "Öffnet ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel.", "Zeigt Schotter und Straßenrand im Texturfenster.", "Ändert den seitlichen GravelShoulder-Streifen.", "100 = Originalmaßstab. Höhere Werte vergrößern die vorhandene Struktur.", "0 = ursprüngliches Relief. Höhere Werte verstärken die Original-Normalmap.", "Stellt den Originalatlas wieder her.", "Der Straßenrand verwendet seine eigene Atlastextur. Hier werden Größe und Tiefe der Originalstruktur angepasst.", "RoadGravel_BaseColor.png in ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel kopieren und neu laden." };
                    break;
                case "es":
                    labels = new[] { "Grava", "Activar grava de calles", "Textura", "Tamaño UV", "Relieve", "Desplazamiento X", "Desplazamiento Y", "Rotación", "Recargar textura", "Restablecer grava", "Abrir carpeta", "Mostrar pestañas de grava", "Borde", "Activar borde", "Tamaño UV del borde", "Relieve del borde", "Restablecer borde" };
                    tips = new[] { "Modifica el material Gravel de la calzada.", "0 = original; 1 = grava más clara y fina; 2 = RoadGravel_BaseColor.png propia.", "100 = tamaño original; valores mayores amplían las piedras.", "0 = normal original en Original, plana en Generado. Valores mayores refuerzan el relieve; sin geometría real.", "Desplaza la textura horizontalmente.", "Desplaza la textura verticalmente.", "Gira el color y la normal juntos.", "Recarga RoadGravel_BaseColor.png desde ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel.", "Restaura material y ajustes originales.", "Abre ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel.", "Muestra las pestañas de grava y borde.", "Modifica la franja lateral GravelShoulder.", "100 = escala original; valores mayores amplían la estructura.", "0 = relieve original; valores mayores refuerzan la normal original.", "Restaura el atlas original.", "El borde usa su propio atlas. Ajusta el tamaño y relieve originales.", "Copia RoadGravel_BaseColor.png en ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel y recarga." };
                    break;
                case "fr":
                    labels = new[] { "Gravier", "Activer le gravier routier", "Texture", "Taille UV", "Relief", "Décalage X", "Décalage Y", "Rotation", "Recharger la texture", "Réinitialiser le gravier", "Ouvrir le dossier", "Afficher les onglets gravier", "Accotement", "Activer l’accotement", "Taille UV du bord", "Relief du bord", "Réinitialiser le bord" };
                    tips = new[] { "Modifie le matériau Gravel de la chaussée.", "0 = origine ; 1 = gravier plus clair et fin ; 2 = RoadGravel_BaseColor.png personnelle.", "100 = taille originale ; une valeur supérieure agrandit les pierres.", "0 = normale originale en Origine, plate en Généré. Les valeurs supérieures renforcent le relief ; sans géométrie réelle.", "Décale horizontalement.", "Décale verticalement.", "Tourne la couleur et la normale ensemble.", "Recharge RoadGravel_BaseColor.png dans ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel.", "Restaure le matériau et les réglages originaux.", "Ouvre ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel.", "Affiche les onglets gravier et accotement.", "Modifie la bande latérale GravelShoulder.", "100 = échelle originale ; plus grand agrandit la structure.", "0 = relief original ; plus grand renforce la normale originale.", "Restaure l’atlas original.", "Le bord utilise son propre atlas. Réglez la taille et le relief d’origine.", "Copiez RoadGravel_BaseColor.png dans ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel puis rechargez." };
                    break;
                case "it":
                    labels = new[] { "Ghiaia", "Attiva ghiaia stradale", "Struttura", "Dimensione UV", "Rilievo", "Spostamento X", "Spostamento Y", "Rotazione", "Ricarica texture", "Ripristina ghiaia", "Apri cartella", "Mostra schede ghiaia", "Bordo", "Attiva bordo", "Dimensione UV bordo", "Rilievo bordo", "Ripristina bordo" };
                    tips = new[] { "Modifica il materiale Gravel della carreggiata.", "0 = originale; 1 = ghiaia più chiara e fine; 2 = RoadGravel_BaseColor.png personale.", "100 = dimensione originale; valori maggiori ingrandiscono i sassi.", "0 = normale originale in Originale, piatta in Generata. Valori maggiori rafforzano il rilievo; nessuna geometria reale.", "Sposta orizzontalmente.", "Sposta verticalmente.", "Ruota colore e normale insieme.", "Ricarica RoadGravel_BaseColor.png da ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel.", "Ripristina materiale e valori originali.", "Apre ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel.", "Mostra le schede ghiaia e bordo.", "Modifica la fascia laterale GravelShoulder.", "100 = scala originale; valori maggiori ingrandiscono la struttura.", "0 = rilievo originale; valori maggiori rafforzano la normale originale.", "Ripristina l’atlante originale.", "Il bordo usa un atlante proprio. Regola dimensione e rilievo originali.", "Copia RoadGravel_BaseColor.png in ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel e ricarica." };
                    break;
                default:
                    labels = new[] { "Gravel", "Enable road gravel", "Texture", "UV size", "Surface depth", "Offset X", "Offset Y", "Rotation", "Reload texture", "Reset road gravel", "Open texture folder", "Show gravel tabs in game", "Road edge", "Enable road edge", "Edge UV size", "Edge depth", "Reset road edge" };
                    tips = new[] { "Changes the road surface Gravel material.", "0 = original; 1 = lighter, finer road gravel; 2 = custom RoadGravel_BaseColor.png.", "100 = original size; higher values enlarge the stones.", "0 = original normal in Original, flat in Generated. Higher values strengthen matching relief; no real geometry.", "Shifts the texture horizontally.", "Shifts the texture vertically.", "Rotates color and normal together.", "Reloads RoadGravel_BaseColor.png from ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel.", "Restores original material and defaults.", "Opens ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel.", "Shows gravel and road edge tabs.", "Changes the GravelShoulder side strip.", "100 = original scale; higher values enlarge the existing pattern.", "0 = original relief; higher values strengthen the original normal.", "Restores the original atlas.", "The road edge uses a separate atlas. Adjusts its original pattern size and depth.", "Copy RoadGravel_BaseColor.png to ModsData/Enhanced_Graphics_and_Textures/Textures/Gravel and reload." };
                    break;
            }
            entries.Add(m_Setting.GetOptionTabLocaleID(Setting.GravelSection), labels[0]);
            entries.Add(m_Setting.GetOptionGroupLocaleID(Setting.GravelGroup), labels[0]);
            entries.Add(m_Setting.GetOptionGroupLocaleID(Setting.ShoulderGroup), labels[12]);
            string[] options = { nameof(Setting.RoadGravelEnabled), nameof(Setting.RoadGravelDetail), nameof(Setting.RoadGravelUvSizePercent),
                nameof(Setting.RoadGravelDepthStrengthPercent), nameof(Setting.RoadGravelUvOffsetX), nameof(Setting.RoadGravelUvOffsetY),
                nameof(Setting.RoadGravelUvRotation), nameof(Setting.ReloadGravelTexture), nameof(Setting.ResetRoadGravel),
                nameof(Setting.OpenGravelTextureFolder), nameof(Setting.ShowGravelPanelButton), nameof(Setting.GravelShoulderEnabled),
                nameof(Setting.GravelShoulderUvSizePercent), nameof(Setting.GravelShoulderDepthStrengthPercent), nameof(Setting.ResetGravelShoulder) };
            for (int i = 0; i < options.Length; i++)
            {
                entries.Add(m_Setting.GetOptionLabelLocaleID(options[i]), labels[i < 11 ? i + 1 : i + 2]);
                entries.Add(m_Setting.GetOptionDescLocaleID(options[i]), tips[i]);
            }
            entries.Add("AdvancedGraphics.UI.RoadGravelTitle", labels[0]);
            entries.Add("AdvancedGraphics.UI.RoadGravelEnabled", labels[1]);
            entries.Add("AdvancedGraphics.UI.RoadGravelTipEnabled", tips[0]);
            string[] keys = { "Detail", "Uv", "Depth", "OffsetX", "OffsetY", "Rotation" };
            for (int i = 0; i < keys.Length; i++)
            {
                entries.Add("AdvancedGraphics.UI.RoadGravel" + keys[i], labels[i + 2]);
                entries.Add("AdvancedGraphics.UI.RoadGravelTip" + keys[i], tips[i + 1]);
            }
            entries.Add("AdvancedGraphics.UI.RoadGravelReload", labels[8]);
            entries.Add("AdvancedGraphics.UI.RoadGravelTipReset", tips[8]);
            entries.Add("AdvancedGraphics.UI.RoadGravelCustomHint", tips[16]);
            entries.Add("AdvancedGraphics.UI.GravelShoulderTitle", labels[12]);
            entries.Add("AdvancedGraphics.UI.GravelShoulderEnabled", labels[13]);
            entries.Add("AdvancedGraphics.UI.GravelShoulderTipEnabled", tips[11]);
            entries.Add("AdvancedGraphics.UI.GravelShoulderUv", labels[14]);
            entries.Add("AdvancedGraphics.UI.GravelShoulderTipUv", tips[12]);
            entries.Add("AdvancedGraphics.UI.GravelShoulderDepth", labels[15]);
            entries.Add("AdvancedGraphics.UI.GravelShoulderTipDepth", tips[13]);
            entries.Add("AdvancedGraphics.UI.GravelShoulderTipReset", tips[14]);
            entries.Add("AdvancedGraphics.UI.GravelShoulderHint", tips[15]);
            if (m_Language == "de") entries["AdvancedGraphics.UI.GravelShoulderHint"] = "Eigene Textur für den Schotter-Gehweg; Größe, Tiefe, Versatz und Drehung sind separat einstellbar.";
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.GravelShoulderDetail))] = labels[2];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.GravelShoulderDetail))] = tips[1].Replace("RoadGravel_BaseColor.png", "GravelShoulder_BaseColor.png");
            entries["AdvancedGraphics.UI.GravelShoulderDetail"] = labels[2];
            entries["AdvancedGraphics.UI.GravelShoulderTipDetail"] = tips[1].Replace("RoadGravel_BaseColor.png", "GravelShoulder_BaseColor.png");
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.GravelShoulderUvOffsetX))] = labels[5];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.GravelShoulderUvOffsetX))] = tips[4];
            entries["AdvancedGraphics.UI.GravelShoulderOffsetX"] = labels[5];
            entries["AdvancedGraphics.UI.GravelShoulderTipOffsetX"] = tips[4];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.GravelShoulderUvOffsetY))] = labels[6];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.GravelShoulderUvOffsetY))] = tips[5];
            entries["AdvancedGraphics.UI.GravelShoulderOffsetY"] = labels[6];
            entries["AdvancedGraphics.UI.GravelShoulderTipOffsetY"] = tips[5];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.GravelShoulderUvRotation))] = labels[7];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.GravelShoulderUvRotation))] = tips[6];
            entries["AdvancedGraphics.UI.GravelShoulderRotation"] = labels[7];
            entries["AdvancedGraphics.UI.GravelShoulderTipRotation"] = tips[6];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ReloadGravelShoulderTexture))] = labels[8];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ReloadGravelShoulderTexture))] = tips[7].Replace("RoadGravel", "GravelShoulder").Replace("/Textures/Gravel", "/Textures/GravelSidewalk");
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenGravelShoulderTextureFolder))] = labels[10];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenGravelShoulderTextureFolder))] = tips[9].Replace("RoadGravel", "GravelShoulder").Replace("/Textures/Gravel", "/Textures/GravelSidewalk");
            entries["AdvancedGraphics.UI.GravelShoulderCustomHint"] = "GravelShoulder_BaseColor.png → ModsData/Enhanced_Graphics_and_Textures/Textures/GravelSidewalk";

        }

        private void AddColorFilterEntries(Dictionary<string, string> entries)
        {
            string[] labels;
            switch (m_Language)
            {
                case "de": labels = new[] { "Farbfilter", "Farbauswahl", "Rot", "Grün", "Blau", "Hex-Wert", "Übernehmen", "Farbe zurücksetzen", "Farbvorgaben", "Wählt den Farbfilter für diesen Tab. Transparenz 100 % deaktiviert den Filter und zeigt die Textur ohne Einfärbung. Zum Anwenden die Flächenanpassung aktivieren.", "Transparenz", "0 % = voller Farbfilter; 100 % = Textur ohne Farbfilter. Die Transparenz der Oberfläche bleibt unverändert.", "Kein Farbfilter" }; break;
                case "es": labels = new[] { "Filtro de color", "Elegir color", "Rojo", "Verde", "Azul", "Valor hexadecimal", "Aplicar", "Restablecer color", "Colores predefinidos", "Filtro para esta pestaña. Transparencia 100 % desactiva el filtro. Activa los ajustes de superficie para aplicarlo.", "Transparencia", "0 % = filtro completo; 100 % = textura sin filtro. No cambia la transparencia de la superficie.", "Sin filtro de color" }; break;
                case "fr": labels = new[] { "Filtre de couleur", "Choisir une couleur", "Rouge", "Vert", "Bleu", "Valeur hexadécimale", "Appliquer", "Réinitialiser la couleur", "Couleurs prédéfinies", "Filtre pour cet onglet. Une transparence de 100 % désactive le filtre. Activez les réglages de surface pour l’appliquer.", "Transparence", "0 % = filtre complet ; 100 % = texture sans filtre. Ne change pas la transparence de la surface.", "Sans filtre de couleur" }; break;
                case "it": labels = new[] { "Filtro colore", "Scegli colore", "Rosso", "Verde", "Blu", "Valore esadecimale", "Applica", "Ripristina colore", "Colori predefiniti", "Filtro per questa scheda. Trasparenza 100 % disattiva il filtro. Attiva le regolazioni della superficie per applicarlo.", "Trasparenza", "0 % = filtro completo; 100 % = texture senza filtro. Non cambia la trasparenza della superficie.", "Nessun filtro colore" }; break;
                default: labels = new[] { "Color filter", "Choose color", "Red", "Green", "Blue", "Hex value", "Apply", "Reset color", "Presets", "Color filter for this tab. Transparency 100% disables the filter. Enable surface adjustments to apply it.", "Transparency", "0% = full color filter; 100% = texture without a filter. Surface transparency is unchanged.", "No color filter" }; break;
            }
            string[] keys = { "ColorFilter", "ChooseColor", "ColorRed", "ColorGreen", "ColorBlue", "ColorHex", "ApplyColor", "ResetColor", "ColorPresets", "ColorFilterTip", "FilterTransparency", "FilterTransparencyTip", "NoColorFilter" };
            for (int i = 0; i < keys.Length; i++) entries["AdvancedGraphics.UI." + keys[i]] = labels[i];
        }

        public void Unload() { }
        private void AddLightingEntries(Dictionary<string, string> entries)
        {
            string[] text;
            switch (m_Language)
            {
                case "de":
                    text = new[]
                    {
                        "Bloom und Sonnenhalo",
                        "Himmel",
                        "Bloom",
                        "Spielstandard folgt dem Spiel. An verwendet die Intensität darunter; Aus deaktiviert den allgemeinen Bloom-Effekt. Eine geänderte Intensität überschreibt auch den Spielstandard. Zurücksetzen gibt die Steuerung an das Spiel zurück.",
                        "Bloom-Intensität",
                        "Leuchten um helle Bildbereiche. Bereich 0–1; 0 entfernt das Leuchten. Ausgangswert 0,20. Zurücksetzen stellt die aktuellen Spieleinstellungen wieder her.",
                        "Sun Bloom (Sonnenhalo)",
                        "Separater HDRP-Sonnenhalo: 0 = aus, 1 = Original. Über 1 wird der Halo kräftiger, breiter und weicher; bis 5. Sichtbar rund um die Sonne, wenn sie im Bild liegt. Bei fehlendem Halo dient ein neutraler 2-Grad-Halo als Basis. Unabhängig vom allgemeinen Bloom; Sonnenscheibe und Lens Flare bleiben unverändert.",
                        "Sky Exposure (Himmelsbelichtung)",
                        "Belichtungskorrektur des Himmels in EV: -5–5, 0 = Original. Folgt der Tag-/Nachtbelichtung des Spiels und beeinflusst auch die Beleuchtung durch den Himmel.",
                        "Bloom zurücksetzen",
                        "Entfernt die Bloom-Übersteuerung und übernimmt die aktuellen Spieleinstellungen.",
                        "Sun Bloom zurücksetzen",
                        "Stellt den ursprünglichen Sonnenhalo wieder her (Stärke 1).",
                        "Sky Exposure zurücksetzen",
                        "Entfernt die Belichtungskorrektur des Himmels (0).",
                        "Spielstandard",
                        "An",
                        "Aus",
                    };
                    break;
                case "es":
                    text = new[]
                    {
                        "Bloom y halo solar",
                        "Cielo",
                        "Bloom",
                        "Predeterminado sigue al juego. Activado usa la intensidad indicada; Desactivado elimina el bloom global. Cambiar la intensidad también modifica Predeterminado. Restablecer devuelve el control al juego.",
                        "Intensidad de bloom",
                        "Resplandor alrededor de zonas brillantes. Rango 0–1; 0 elimina el resplandor. Valor inicial 0,20. Restablecer recupera los ajustes actuales del juego.",
                        "Sun Bloom (halo solar)",
                        "Halo solar HDRP independiente: 0 = desactivado, 1 = original. Por encima de 1 aumenta la intensidad, el tamaño y la suavidad, hasta 5. Visible alrededor del sol cuando está en pantalla. Sin halo se usa una base neutra de 2 grados. Independiente del bloom global; no cambia el disco solar ni el destello de lente.",
                        "Sky Exposure (exposición del cielo)",
                        "Corrección de exposición del cielo en EV: -5–5, 0 = original. Sigue la exposición diurna/nocturna del juego y afecta a la iluminación del cielo.",
                        "Restablecer Bloom",
                        "Elimina la modificación de bloom y sigue los ajustes actuales del juego.",
                        "Restablecer Sun Bloom",
                        "Recupera el halo solar original (intensidad 1).",
                        "Restablecer Sky Exposure",
                        "Elimina la corrección de exposición del cielo (0).",
                        "Predeterminado del juego",
                        "Activado",
                        "Desactivado",
                    };
                    break;
                case "fr":
                    text = new[]
                    {
                        "Bloom et halo solaire",
                        "Ciel",
                        "Bloom",
                        "Par défaut suit le jeu. Activé utilise l’intensité indiquée ; Désactivé supprime le bloom global. Une intensité modifiée remplace aussi la valeur par défaut. Réinitialiser rend le contrôle au jeu.",
                        "Intensité du bloom",
                        "Lueur autour des zones lumineuses. Plage 0–1 ; 0 supprime la lueur. Valeur initiale 0,20. Réinitialiser restaure les réglages actuels du jeu.",
                        "Sun Bloom (halo solaire)",
                        "Halo solaire HDRP séparé : 0 = désactivé, 1 = original. Au-dessus de 1, le halo devient plus intense, plus large et plus doux, jusqu’à 5. Visible autour du soleil lorsqu’il est à l’écran. Sans halo, une base neutre de 2 degrés est utilisée. Indépendant du bloom global ; le disque solaire et le lens flare restent inchangés.",
                        "Sky Exposure (exposition du ciel)",
                        "Correction de l’exposition du ciel en EV : -5–5, 0 = original. Suit l’exposition jour/nuit du jeu et affecte aussi l’éclairage du ciel.",
                        "Réinitialiser Bloom",
                        "Supprime la modification du bloom et suit les réglages actuels du jeu.",
                        "Réinitialiser Sun Bloom",
                        "Restaure le halo solaire original (intensité 1).",
                        "Réinitialiser Sky Exposure",
                        "Supprime la correction de l’exposition du ciel (0).",
                        "Réglage du jeu",
                        "Activé",
                        "Désactivé",
                    };
                    break;
                case "it":
                    text = new[]
                    {
                        "Bloom e alone solare",
                        "Cielo",
                        "Bloom",
                        "Predefinito segue il gioco. Attivo usa l’intensità indicata; Disattivo elimina il bloom globale. Un’intensità modificata sostituisce anche il valore predefinito. Ripristina restituisce il controllo al gioco.",
                        "Intensità bloom",
                        "Bagliore intorno alle aree luminose. Intervallo 0–1; 0 elimina il bagliore. Valore iniziale 0,20. Ripristina recupera le impostazioni attuali del gioco.",
                        "Sun Bloom (alone solare)",
                        "Alone solare HDRP separato: 0 = disattivo, 1 = originale. Sopra 1 aumenta intensità, dimensione e morbidezza, fino a 5. Visibile intorno al sole quando è sullo schermo. Se manca l’alone viene usata una base neutra di 2 gradi. Indipendente dal bloom globale; il disco solare e il lens flare restano invariati.",
                        "Sky Exposure (esposizione del cielo)",
                        "Correzione dell’esposizione del cielo in EV: -5–5, 0 = originale. Segue l’esposizione giorno/notte del gioco e influenza anche l’illuminazione del cielo.",
                        "Ripristina Bloom",
                        "Rimuove la modifica del bloom e segue le impostazioni attuali del gioco.",
                        "Ripristina Sun Bloom",
                        "Ripristina l’alone solare originale (intensità 1).",
                        "Ripristina Sky Exposure",
                        "Rimuove la correzione dell’esposizione del cielo (0).",
                        "Predefinito del gioco",
                        "Attivo",
                        "Disattivo",
                    };
                    break;
                default:
                    text = new[]
                    {
                        "Bloom and sun halo",
                        "Sky",
                        "Bloom",
                        "Game default follows the game. On uses the intensity below; Off disables screen-wide bloom. A changed intensity also overrides Game default. Reset releases the override.",
                        "Bloom intensity",
                        "Screen-wide glow around bright areas. Range 0–1; 0 removes the glow. Initial value 0.20. Reset restores the current game settings.",
                        "Sun Bloom (sun halo)",
                        "Separate HDRP sun halo: 0 = off, 1 = original. Above 1 it becomes stronger, wider and softer, up to 5. Visible around the sun when it is on screen. A missing halo uses a neutral 2-degree base. Independent of screen-wide Bloom; the solar disc and lens flare remain unchanged.",
                        "Sky Exposure",
                        "Sky exposure correction in EV: -5–5, 0 = original. Follows the game’s day/night sky exposure and also affects sky lighting.",
                        "Reset Bloom",
                        "Releases the Bloom override and follows the current game settings.",
                        "Reset Sun Bloom",
                        "Restores the original sun halo (strength 1).",
                        "Reset Sky Exposure",
                        "Removes the sky exposure correction (0).",
                        "Game default",
                        "On",
                        "Off",
                    };
                    break;
            }
            entries.Add(m_Setting.GetOptionGroupLocaleID(Setting.BloomGroup), text[0]);
            entries.Add(m_Setting.GetOptionGroupLocaleID(Setting.SkyGroup), text[1]);
            var controls = new[] { nameof(Setting.BloomMode), nameof(Setting.BloomIntensity),
                nameof(Setting.SunBloomStrength), nameof(Setting.SkyExposure), nameof(Setting.ResetBloom),
                nameof(Setting.ResetSunBloom), nameof(Setting.ResetSkyExposure) };
            for (var i = 0; i < controls.Length; i++)
            {
                entries.Add(m_Setting.GetOptionLabelLocaleID(controls[i]), text[2 + i * 2]);
                entries.Add(m_Setting.GetOptionDescLocaleID(controls[i]), text[3 + i * 2]);
            }
            entries.Add(m_Setting.GetEnumValueLocaleID(Setting.BloomControl.GameDefault), text[16]);
            entries.Add(m_Setting.GetEnumValueLocaleID(Setting.BloomControl.Enabled), text[17]);
            entries.Add(m_Setting.GetEnumValueLocaleID(Setting.BloomControl.Disabled), text[18]);
        }
        private void AddTaaEntries(Dictionary<string, string> entries)
        {
            string[] text;
            switch (m_Language)
            {
                case "de":
                    text = new[] { "Kantenglättung", "TAA-Stabilisierung anpassen", "Aktiviert ausschließlich den Anti-Flimmer-Regler für TAA. Aus stellt den ursprünglichen Kamerawert wieder her. TAA-Qualität, History-Schärfe und Kantenschutz bleiben beim Spiel. Wirkt nur mit TAA, nicht mit DLSS/FSR2.", "TAA-Stabilisierung", "Tatsächlicher HDRP-Anti-Flimmer-Wert: 0,00–1,00 in 0,01-Schritten. 0 deaktiviert nur den zusätzlichen Anti-Flimmer-Mechanismus, nicht TAA. Ausgangswert 0,50; wird nur bei aktivierter Anpassung angewendet. Höhere Werte können mehr Nachziehen verursachen; die sichtbare Wirkung hängt von Bewegung und Bildinhalt ab. Kleine und große Werte vergleichen.", "TAA-Stabilisierung zurücksetzen", "Deaktiviert die Anpassung und setzt den Regler auf 0,50 zurück.", "Spielvorgabe der Schattenqualität" };
                    break;
                case "es":
                    text = new[] { "Antialiasing", "Ajustar estabilización TAA", "Activa solo el control antiparpadeo de TAA. Desactivado restaura el valor original de la cámara. Calidad TAA, nitidez del historial y protección de bordes permanecen bajo control del juego. Solo TAA, no DLSS/FSR2.", "Estabilización TAA", "Valor antiparpadeo HDRP: 0,00–1,00 en pasos de 0,01. 0 desactiva solo el mecanismo antiparpadeo adicional, no TAA. Valor inicial 0,50; se aplica solo con el ajuste activado. Los valores altos pueden aumentar las estelas; el efecto depende del movimiento y de la imagen. Compara valores bajos y altos.", "Restablecer estabilización TAA", "Desactiva el ajuste y restablece el control a 0,50.", "Valor de la calidad de sombras" };
                    break;
                case "fr":
                    text = new[] { "Anticrénelage", "Régler la stabilisation TAA", "Active uniquement le réglage antiscintillement TAA. Désactivé restaure la valeur initiale de la caméra. Qualité TAA, netteté de l’historique et protection des contours restent gérées par le jeu. TAA uniquement, sans DLSS/FSR2.", "Stabilisation TAA", "Valeur antiscintillement HDRP : 0,00–1,00 par pas de 0,01. 0 désactive uniquement le mécanisme antiscintillement supplémentaire, pas TAA. Valeur initiale 0,50 ; appliquée uniquement lorsque le réglage est activé. Des valeurs élevées peuvent augmenter les traînées ; l’effet dépend du mouvement et de l’image. Comparez les valeurs faibles et élevées.", "Réinitialiser la stabilisation TAA", "Désactive le réglage et rétablit le curseur à 0,50.", "Valeur de la qualité des ombres" };
                    break;
                case "it":
                    text = new[] { "Antialiasing", "Regola stabilizzazione TAA", "Attiva solo il controllo antisfarfallio TAA. Disattivato ripristina il valore originale della camera. Qualità TAA, nitidezza della cronologia e protezione dei bordi restano gestite dal gioco. Solo TAA, non DLSS/FSR2.", "Stabilizzazione TAA", "Valore antisfarfallio HDRP: 0,00–1,00 a passi di 0,01. 0 disattiva solo il meccanismo antisfarfallio aggiuntivo, non TAA. Valore iniziale 0,50; applicato solo quando il controllo è attivo. Valori elevati possono aumentare le scie; l’effetto dipende dal movimento e dall’immagine. Confronta valori bassi e alti.", "Ripristina stabilizzazione TAA", "Disattiva il controllo e ripristina il cursore a 0,50.", "Valore della qualità ombre" };
                    break;
                default:
                    text = new[] { "Anti-aliasing", "Adjust TAA stability", "Enables only the TAA anti-flicker control. Off restores the original camera value. TAA quality, history sharpening and dark-edge protection remain controlled by the game. Only affects TAA, not DLSS/FSR2.", "TAA stability", "Actual HDRP anti-flicker value: 0.00–1.00 in steps of 0.01. 0 disables only the additional anti-flicker mechanism, not TAA. Initial value 0.50; applied only while the override is enabled. Higher values may increase trails; the effect depends on motion and image content. Compare low and high values.", "Reset TAA stability", "Disables the override and resets the slider to 0.50.", "Shadow quality default" };
                    break;
            }
            entries[m_Setting.GetOptionGroupLocaleID(Setting.SharpnessGroup)] = text[0];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.TaaStabilityEnabled))] = text[1];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.TaaStabilityEnabled))] = text[2];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.TaaStabilityStrength))] = text[3];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.TaaStabilityStrength))] = text[4];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetTaaStability))] = text[5];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ResetTaaStability))] = text[6];
            const string prefix = "AdvancedGraphics.UI.Graphics.";
            entries[prefix + "AntiAliasing"] = text[0];
            entries[prefix + "ShadowGameValue"] = text[7];
        }
        private void AddGraphicsPanelEntries(Dictionary<string, string> entries)
        {
            string[] text;
            switch (m_Language)
            {
                case "de":
                    text = new[]
                    {
                        "Grafik",
                        "Enhanced Graphics and Textures – Grafikfenster öffnen",
                        "Bloom / Himmel",
                        "Schließen",
                        "Zurücksetzen",
                        "Grafik zurücksetzen",
                        "Setzt nur die Grafikwerte dieses Fensters zurück.",
                        "Spielstandard",
                        "An",
                        "Aus",
                        "Grafik-App-Button anzeigen",
                        "Zeigt das separate Grafikfenster in der Ingame-UI. Die Stadt bleibt sichtbar, Änderungen wirken direkt und die Fensterposition wird gespeichert.",
                        "Textur-App-Button anzeigen",
                    };
                    break;
                case "es":
                    text = new[]
                    {
                        "Gráficos",
                        "Enhanced Graphics and Textures – abrir editor gráfico",
                        "Bloom / cielo",
                        "Cerrar",
                        "Restablecer",
                        "Restablecer gráficos",
                        "Restablece solo los valores gráficos de esta ventana.",
                        "Predeterminado",
                        "Activado",
                        "Desactivado",
                        "Mostrar botón de gráficos",
                        "Muestra el editor gráfico independiente. La ciudad permanece visible, los cambios se aplican de inmediato y se guarda la posición de la ventana.",
                        "Mostrar botón de texturas",
                    };
                    break;
                case "fr":
                    text = new[]
                    {
                        "Graphismes",
                        "Enhanced Graphics and Textures – ouvrir l’éditeur graphique",
                        "Bloom / ciel",
                        "Fermer",
                        "Réinitialiser",
                        "Réinitialiser les graphismes",
                        "Restaure uniquement les valeurs graphiques de cette fenêtre.",
                        "Réglage du jeu",
                        "Activé",
                        "Désactivé",
                        "Afficher le bouton graphique",
                        "Affiche l’éditeur graphique séparé. La ville reste visible, les changements sont immédiats et la position de la fenêtre est enregistrée.",
                        "Afficher le bouton des textures",
                    };
                    break;
                case "it":
                    text = new[]
                    {
                        "Grafica",
                        "Enhanced Graphics and Textures – apri editor grafico",
                        "Bloom / cielo",
                        "Chiudi",
                        "Ripristina",
                        "Ripristina grafica",
                        "Ripristina solo i valori grafici di questa finestra.",
                        "Predefinito",
                        "Attivo",
                        "Disattivo",
                        "Mostra pulsante grafica",
                        "Mostra l’editor grafico separato. La città resta visibile, le modifiche sono immediate e la posizione della finestra viene salvata.",
                        "Mostra pulsante texture",
                    };
                    break;
                default:
                    text = new[]
                    {
                        "Graphics",
                        "Enhanced Graphics and Textures – open graphics editor",
                        "Bloom / sky",
                        "Close",
                        "Reset",
                        "Reset graphics",
                        "Restores only the graphics values in this window.",
                        "Game default",
                        "On",
                        "Off",
                        "Show graphics app button",
                        "Shows the separate in-game graphics editor. The city remains visible, changes apply immediately, and the window position is saved.",
                        "Show texture app button",
                    };
                    break;
            }
            const string prefix = "AdvancedGraphics.UI.Graphics.";
            var keys = new[] { "Title", "Open", "Lighting", "Close", "Reset", "ResetAll",
                "TipResetAll", "GameDefault", "On", "Off" };
            for (int i = 0; i < keys.Length; i++) entries[prefix + keys[i]] = text[i];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShowGraphicsAppButton))] = text[10];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ShowGraphicsAppButton))] = text[11];
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShowAppButton))] = text[12];
            entries[prefix + "Details"] = entries[m_Setting.GetOptionGroupLocaleID(Setting.DetailGroup)];
            entries[prefix + "Terrain"] = entries[m_Setting.GetOptionGroupLocaleID(Setting.TerrainGroup)];
            entries[prefix + "Shadows"] = entries[m_Setting.GetOptionGroupLocaleID(Setting.ShadowsGroup)];
            var controls = new[] { nameof(Setting.TreeLodDistance), nameof(Setting.GlobalDetailScalePercent),
                nameof(Setting.TerrainTessellationLevel), nameof(Setting.TerrainPatchSizeUnits),
                nameof(Setting.ShadowResolution), nameof(Setting.ShadowDistanceMeters),
                nameof(Setting.SmallShadowCasterThreshold), nameof(Setting.TaaSharpenStrength), nameof(Setting.TaaStabilityEnabled), nameof(Setting.TaaStabilityStrength),
                nameof(Setting.BloomMode), nameof(Setting.BloomIntensity),
                nameof(Setting.SunBloomStrength), nameof(Setting.SkyExposure) };
            foreach (var name in controls)
            {
                entries[prefix + name] = entries[m_Setting.GetOptionLabelLocaleID(name)];
                entries[prefix + "Tip" + name] = entries[m_Setting.GetOptionDescLocaleID(name)];
            }
        }
    }
}








