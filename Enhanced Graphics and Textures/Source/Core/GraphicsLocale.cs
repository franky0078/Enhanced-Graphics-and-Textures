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

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            Mod.Graphics?.Capture();
            var detail = Mod.Graphics?.BaseDetail ?? "?";
            var shadow = Mod.Graphics?.BaseShadowResolution ?? "?";
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
                        "Schattenauflösung", $"Standard: {shadow} Pixel. Höher: schärfere Schatten, mehr VRAM- und GPU-Bedarf, oft weniger FPS. Niedriger: weichere oder pixeligere Schatten, oft mehr FPS. Schatten im Spielmenü aktivieren.",
                        "Schattenreichweite (m)", "Standard: Spielwert (0 = kein Eingriff). Höher: Sonnenschatten bis in größere Entfernung, oft weniger FPS. Niedriger: kürzere Schattenreichweite, oft mehr FPS. Wirkt nur bei aktivierten Schatten.",
                        "Auf Standard zurücksetzen", "Alle Werte zurücksetzen",
                        "Baum-LOD-Distanz auf 100 % zurücksetzen.", "Globale Objektdetails auf 100 % zurücksetzen.",
                        "Geometrie-Cache auf 4096 MB zurücksetzen.", "Terrain-Tessellation auf 5 zurücksetzen.",
                        "Terrain-Patchgröße auf 64 zurücksetzen.", $"Schattenauflösung auf {shadow} Pixel (Startwert des Spiels) zurücksetzen.",
                        "Schattenreichweite auf den Spielwert zurücksetzen (0 = kein Eingriff).",
                        "Alle Optionen auf ihre Standardwerte zurücksetzen.",
                        "Schärfe", "Kleine Schattenwerfer", $"Standard: Spielwerte (Höhe / Volumen: {culling}; 0 = kein Eingriff). Höher: mehr kleine Schatten ausgeblendet, oft schneller, aber weniger feine Schatten. Niedriger: mehr kleine Schatten, meist langsamer. Wirkt nur bei aktivierten Schatten.",
                        "TAA-Schärfe", "Standard: Kamerawert (0 = kein Eingriff). Schritte von 0,05; 0,50 entspricht HDRP-Stärke 0,50. Höher: schärferes TAA-Bild, aber mögliche dunkle Kanten. Niedriger: weicher. Wirkt nur mit TAA, nicht mit DLSS/FSR2.",
                        "Schwellenwert für kleine Schattenwerfer auf die Spielwerte zurücksetzen.", "TAA-Schärfe auf den ursprünglichen Kamerawert zurücksetzen." };
                    break;
                case "es":
                    t = new[] { "Enhanced Graphics & Textures", "General", "Árboles", "Detalles", "Caché", "Terreno", "Sombras", "Restablecer",
                        "Distancia LOD de árboles", "Predeterminado: 100 %. Más alto: árboles detallados más lejos, normalmente menos FPS. Más bajo: se simplifican antes, normalmente más FPS. No mejora los modelos simplificados del propio recurso.",
                        "Detalle global de objetos", $"Predeterminado: 100 % (juego {detail}). Más alto: más detalle en todos los objetos, incluidos árboles, normalmente menos FPS. Más bajo: menos detalle, normalmente más FPS. Por encima del juego es experimental.",
                        "Caché de geometría (MB)", "Predeterminado: 4096 MB. Más alto: más geometría en memoria y mayor uso de VRAM; no garantiza mejor imagen ni más FPS. Más bajo: menos VRAM, pero un caché insuficiente puede provocar tirones. Juego hasta 4096 MB; mod hasta 16384 MB.",
                        "Teselación del terreno", "Predeterminado: 5. Más alto: terreno potencialmente más detallado, normalmente más lento. Más bajo: terreno más simple, normalmente más rápido. Juego hasta 5; por encima es experimental y puede limitarse.",
                        "Tamaño de parche del terreno", "Predeterminado: 64. Más pequeño: subdivisión más fina y posibles mejores transiciones, pero más trabajo de dibujo y quizá menos FPS. Más grande: menos llamadas de dibujo y normalmente más FPS. Juego hasta 64.",
                        "Resolución de sombras", $"Predeterminado: {shadow} píxeles. Más alto: sombras más nítidas, más uso de VRAM y GPU, normalmente menos FPS. Más bajo: sombras más borrosas o pixeladas, normalmente más FPS. Activa las sombras en el juego.",
                        "Distancia de sombras (m)", "Predeterminado: valor del juego (0 = sin modificar). Más alto: sombras solares visibles más lejos, normalmente menos FPS. Más bajo: menor alcance, normalmente más FPS. Requiere sombras activas.",
                        "Restablecer valor", "Restablecer todo",
                        "Restablecer la distancia LOD de árboles al 100 %.", "Restablecer el detalle global al 100 %.",
                        "Restablecer la caché de geometría a 4096 MB.", "Restablecer la teselación del terreno a 5.",
                        "Restablecer el tamaño de parche a 64.", $"Restablecer la resolución de sombras a {shadow} píxeles (valor inicial del juego).",
                        "Restablecer la distancia de sombras al juego (0 = sin modificar).", "Restablecer todas las opciones a sus valores predeterminados.",
                        "Nitidez", "Objetos pequeños que proyectan sombras", $"Predeterminado: juego (altura / volumen: {culling}; 0 = sin modificar). Más alto: oculta más sombras pequeñas, suele mejorar FPS, pero pierde sombras finas. Más bajo: más sombras pequeñas, suele reducir FPS. Requiere sombras activas.",
                        "Nitidez TAA", "Predeterminado: valor de la cámara (0 = sin modificar). Pasos de 0,05; 0,50 corresponde a intensidad HDRP 0,50. Más alto: imagen TAA más nítida, pero posibles bordes oscuros. Más bajo: más suave. Solo con TAA, no DLSS/FSR2.",
                        "Restablecer los objetos pequeños que proyectan sombras al juego.", "Restablecer la nitidez TAA al valor original de la cámara." };
                    break;
                case "fr":
                    t = new[] { "Enhanced Graphics & Textures", "Général", "Arbres", "Détails", "Cache", "Terrain", "Ombres", "Réinitialiser",
                        "Distance LOD des arbres", "Défaut : 100 %. Plus élevé : arbres détaillés plus loin, généralement moins de FPS. Plus bas : simplification plus tôt, généralement plus de FPS. Ne modifie pas les modèles simplifiés des ressources.",
                        "Détails globaux des objets", $"Défaut : 100 % (jeu {detail}). Plus élevé : plus de détails sur tous les objets, arbres compris, généralement moins de FPS. Plus bas : moins de détails, généralement plus de FPS. Au-delà du jeu : expérimental.",
                        "Cache de géométrie (Mo)", "Défaut : 4096 Mo. Plus élevé : plus de géométrie en mémoire et plus de VRAM ; ni meilleure image ni plus de FPS garantis. Plus bas : moins de VRAM, mais un cache insuffisant peut causer des saccades. Jeu jusqu'à 4096 Mo ; mod jusqu'à 16384 Mo.",
                        "Tessellation du terrain", "Défaut : 5. Plus élevé : terrain potentiellement plus détaillé, généralement plus lent. Plus bas : terrain plus simple, généralement plus rapide. Jeu jusqu'à 5 ; au-delà, expérimental et parfois limité.",
                        "Taille des parcelles", "Défaut : 64. Plus petit : terrain subdivisé plus finement et transitions parfois meilleures, mais plus de rendu et parfois moins de FPS. Plus grand : moins d'appels de rendu, souvent plus de FPS. Jeu jusqu'à 64.",
                        "Résolution des ombres", $"Défaut : {shadow} pixels. Plus élevé : ombres plus nettes, plus de VRAM et de GPU, souvent moins de FPS. Plus bas : ombres plus floues ou pixelisées, souvent plus de FPS. Activez les ombres dans le jeu.",
                        "Portée des ombres (m)", "Défaut : valeur du jeu (0 = aucun changement). Plus élevé : ombres du soleil plus loin, souvent moins de FPS. Plus bas : portée réduite, souvent plus de FPS. Nécessite les ombres activées.",
                        "Rétablir la valeur", "Tout réinitialiser",
                        "Rétablir la distance LOD des arbres à 100 %.", "Rétablir les détails globaux à 100 %.",
                        "Rétablir le cache de géométrie à 4096 Mo.", "Rétablir la tessellation du terrain à 5.",
                        "Rétablir la taille des parcelles à 64.", $"Rétablir la résolution des ombres à {shadow} pixels (valeur initiale du jeu).",
                        "Rétablir la portée des ombres au jeu (0 = aucun changement).", "Rétablir toutes les options à leurs valeurs par défaut.",
                        "Netteté", "Petits objets projetant des ombres", $"Défaut : jeu (hauteur / volume : {culling} ; 0 = aucun changement). Plus élevé : masque plus de petites ombres, souvent plus rapide, mais moins de détails. Plus bas : plus de petites ombres, souvent plus lent. Nécessite les ombres activées.",
                        "Netteté TAA", "Défaut : valeur de la caméra (0 = aucun changement). Pas de 0,05 ; 0,50 correspond à une force HDRP de 0,50. Plus élevé : image TAA plus nette, mais contours sombres possibles. Plus bas : plus douce. TAA uniquement, sans DLSS/FSR2.",
                        "Rétablir le seuil des petits objets projetant des ombres au jeu.", "Rétablir la netteté TAA à la valeur initiale de la caméra." };
                    break;
                case "it":
                    t = new[] { "Enhanced Graphics & Textures", "Generale", "Alberi", "Dettagli", "Cache", "Terreno", "Ombre", "Ripristina",
                        "Distanza LOD degli alberi", "Predefinito: 100%. Più alto: alberi dettagliati più lontano, di solito meno FPS. Più basso: semplificazione anticipata, di solito più FPS. Non migliora i modelli semplificati degli asset.",
                        "Dettagli globali degli oggetti", $"Predefinito: 100% (gioco {detail}). Più alto: più dettaglio per tutti gli oggetti, alberi inclusi, di solito meno FPS. Più basso: meno dettaglio, di solito più FPS. Oltre il limite del gioco è sperimentale.",
                        "Cache geometria (MB)", "Predefinito: 4096 MB. Più alto: più geometria in memoria e più VRAM; qualità e FPS non migliorano automaticamente. Più basso: meno VRAM, ma una cache insufficiente può causare scatti. Gioco fino a 4096 MB; mod fino a 16384 MB.",
                        "Tessellazione terreno", "Predefinito: 5. Più alto: geometria del terreno potenzialmente più fine, di solito più lento. Più basso: terreno più semplice, di solito più veloce. Gioco fino a 5; oltre è sperimentale e può essere limitato.",
                        "Dimensione patch terreno", "Predefinito: 64. Più piccolo: suddivisione più fine e possibili transizioni migliori, ma più lavoro di disegno e forse meno FPS. Più grande: meno chiamate di disegno, spesso più FPS. Gioco fino a 64.",
                        "Risoluzione ombre", $"Predefinito: {shadow} pixel. Più alto: ombre più nitide, più VRAM e GPU, spesso meno FPS. Più basso: ombre più sfocate o pixelate, spesso più FPS. Attiva le ombre nel gioco.",
                        "Distanza ombre (m)", "Predefinito: valore del gioco (0 = nessuna modifica). Più alto: ombre solari più lontane, spesso meno FPS. Più basso: distanza minore, spesso più FPS. Richiede le ombre attive.",
                        "Ripristina valore", "Ripristina tutto",
                        "Ripristina la distanza LOD degli alberi al 100%.", "Ripristina i dettagli globali al 100%.",
                        "Ripristina la cache geometria a 4096 MB.", "Ripristina la tessellazione terreno a 5.",
                        "Ripristina la dimensione patch a 64.", $"Ripristina la risoluzione ombre a {shadow} pixel (valore iniziale del gioco).",
                        "Ripristina la distanza ombre al gioco (0 = nessuna modifica).", "Ripristina tutte le opzioni ai valori predefiniti.",
                        "Nitidezza", "Piccoli oggetti che proiettano ombre", $"Predefinito: gioco (altezza / volume: {culling}; 0 = nessuna modifica). Più alto: esclude più ombre piccole, spesso più FPS, ma meno dettagli. Più basso: più ombre piccole, spesso meno FPS. Richiede le ombre attive.",
                        "Nitidezza TAA", "Predefinito: valore della camera (0 = nessuna modifica). Passi di 0,05; 0,50 equivale a intensità HDRP 0,50. Più alto: immagine TAA più nitida, ma possibili contorni scuri. Più basso: più morbida. Solo con TAA, non DLSS/FSR2.",
                        "Ripristina la soglia per le ombre piccole al valore del gioco.", "Ripristina la nitidezza TAA al valore iniziale della camera." };
                    break;
                default:
                    t = new[] { "Enhanced Graphics & Textures", "General", "Trees", "Details", "Cache", "Terrain", "Shadows", "Reset",
                        "Tree LOD distance", "Default: 100%. Higher: detailed trees farther away, usually fewer FPS. Lower: trees simplify sooner, usually more FPS. Cannot improve an asset's simplified model.",
                        "Global object detail", $"Default: 100% (game value {detail}). Higher: more detail on all objects including trees, usually fewer FPS. Lower: less detail, usually more FPS. Beyond the game range is experimental.",
                        "Geometry cache (MB)", "Default: 4096 MB. Higher: more geometry kept in memory and more VRAM used; neither better image quality nor more FPS is guaranteed. Lower: less VRAM, but too little cache may cause stutter. Game menu up to 4096 MB; mod up to 16384 MB.",
                        "Terrain tessellation", "Default: 5. Higher: potentially finer terrain geometry, usually slower. Lower: simpler terrain, usually faster. Game menu up to 5; higher values are experimental and may be clamped.",
                        "Terrain patch size", "Default: 64. Smaller: finer terrain subdivision and possibly better transitions, but more draw work and possibly fewer FPS. Larger: fewer draw calls, often more FPS. Game menu up to 64.",
                        "Shadow resolution", $"Default: {shadow} pixels. Higher: sharper shadows, more VRAM and GPU work, often fewer FPS. Lower: softer or more pixelated shadows, often more FPS. Enable shadows in game options.",
                        "Shadow distance (m)", "Default: game value (0 = no override). Higher: sun shadows visible farther away, often fewer FPS. Lower: shorter reach, often more FPS. Requires shadows enabled in game options.",
                        "Restore default", "Reset all values",
                        "Restore tree LOD distance to 100%.", "Restore global object detail to 100%.",
                        "Restore geometry cache to 4096 MB.", "Restore terrain tessellation to 5.",
                        "Restore terrain patch size to 64.", $"Restore shadow resolution to {shadow} pixels (game value at startup).",
                        "Restore shadow distance to the game value (0 = no override).", "Restore every option to its default value.",
                        "Sharpness", "Small shadow casters", $"Default: game values (height / volume: {culling}; 0 = no override). Higher: culls more small shadows, often faster but loses fine shadows. Lower: keeps more small shadows, usually slower. Requires shadows enabled.",
                        "TAA sharpness", "Default: camera value (0 = no override). Steps of 0.05; 0.50 equals HDRP strength 0.50. Higher: sharper TAA image but possible dark outlines. Lower: softer. Only affects TAA, not DLSS/FSR2.",
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
                        "UV-Größe", "100 % = ursprüngliche Mustergröße; bis 1000 %. Höher vergrößert die Asphaltkörnung. Die Rissgröße wird separat eingestellt.",
                        "Texturkachelung", "0,1 bis 8,0 in 0,1-Schritten. Unter 1,0 weniger zusätzliche Risse, über 1,0 mehr. Die Asphaltkörnung bleibt bei allen Stufen gleich groß. Das Worldspace-Material wiederholt dieselbe Kachel weiterhin entlang der Straße.",
                        "UV-Versatz X", "Verschiebt das gekachelte Asphaltmuster horizontal. 100 % entspricht wieder 0 %.",
                        "UV-Versatz Y", "Verschiebt das gekachelte Asphaltmuster vertikal. 100 % entspricht wieder 0 %.",
                        "UV-Drehung", "Dreht das Asphaltmuster und seine Tiefenwirkung von 0 bis 360 Grad in Schritten von 1 Grad.",
                        "Asphalt zurücksetzen", "Stellt die originalen Straßentexturen und alle Asphaltregler wieder her." };
                    break;
                case "es":
                    asphalt = new[] { "Detalle del asfalto", "0 = textura original; 1 = asfalto generado; 2 = Road_BaseColor.png propia. Si los ajustes están activados, UV, relieve y grietas funcionan en todos los niveles.",
                        "Tamaño UV", "100 % = tamaño original; hasta 1000 %. Aumenta el grano del asfalto; el tamaño de las grietas es independiente.",
                        "Repetición de textura", "De 0,1 a 8,0 en pasos de 0,1. Menos de 1,0 reduce las grietas añadidas; más de 1,0 las aumenta. El grano del asfalto no cambia. El material repite el mismo mosaico en la carretera.",
                        "Desplazamiento UV X", "Desplaza el patrón horizontalmente; 100 % equivale a 0 %.",
                        "Desplazamiento UV Y", "Desplaza el patrón verticalmente; 100 % equivale a 0 %.",
                        "Rotación UV", "Gira el patrón y su relieve de 0 a 360 grados en pasos de 1 grado.",
                        "Restablecer asfalto", "Restaura las texturas originales y todos los controles del asfalto." };
                    break;
                case "fr":
                    asphalt = new[] { "Détails de l’asphalte", "0 = texture d’origine ; 1 = asphalte généré ; 2 = votre Road_BaseColor.png. Quand les réglages sont actifs, UV, relief et fissures fonctionnent à tous les niveaux.",
                        "Taille UV", "100 % = taille d’origine ; jusqu’à 1000 %. Agrandit le grain ; la taille des fissures se règle séparément.",
                        "Répétition de texture", "De 0,1 à 8,0 par pas de 0,1. Sous 1,0 diminue les fissures ajoutées ; au-dessus de 1,0 les augmente. Le grain reste inchangé. Le matériau répète la même tuile sur la route.",
                        "Décalage UV X", "Décale le motif horizontalement ; 100 % revient à 0 %.",
                        "Décalage UV Y", "Décale le motif verticalement ; 100 % revient à 0 %.",
                        "Rotation UV", "Fait pivoter le motif et son relief de 0 à 360 degrés par pas de 1 degré.",
                        "Rétablir l’asphalte", "Restaure les textures d’origine et tous les réglages d’asphalte." };
                    break;
                case "it":
                    asphalt = new[] { "Dettaglio asfalto", "0 = texture originale; 1 = asfalto generato; 2 = Road_BaseColor.png personale. Con le regolazioni attive, UV, rilievo e crepe funzionano a tutti i livelli.",
                        "Dimensione UV", "100% = dimensione originale; fino a 1000%. Ingrandisce la grana; la dimensione delle crepe è separata.",
                        "Ripetizione texture", "Da 0,1 a 8,0 con passi di 0,1. Sotto 1,0 riduce le crepe aggiunte; sopra 1,0 le aumenta. La grana non cambia. Il materiale ripete la stessa tessera sulla strada.",
                        "Spostamento UV X", "Sposta il motivo in orizzontale; 100% equivale a 0%.",
                        "Spostamento UV Y", "Sposta il motivo in verticale; 100% equivale a 0%.",
                        "Rotazione UV", "Ruota il motivo e il rilievo da 0 a 360 gradi in passi di 1 grado.",
                        "Ripristina asfalto", "Ripristina le texture originali e tutte le regolazioni dell’asfalto." };
                    break;
                default:
                    asphalt = new[] { "Asphalt detail", "0 = original texture; 1 = uniform generated asphalt grain; 2 = your Road_BaseColor.png. With asphalt adjustments enabled, UV, depth, and cracks work at every level.",
                        "UV size", "100% = original pattern size; up to 1000%. Enlarges asphalt grain; crack size is controlled separately.",
                        "Texture tiling", "0.1 to 8.0 in 0.1 steps. Below 1.0 reduces extra cracks, above 1.0 increases them. Asphalt grain size is unaffected. The world-space material still repeats the same tile along the road.",
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
                case "de": source = new[] { "Straßentextur", "Generiert oder eigene Road_BaseColor.png. Aus der eigenen Textur wird eine passende Normalmap für die Tiefenwirkung abgeleitet. Asphaltstruktur muss mindestens 1 sein.", "Generiert", "Eigene PNG" }; break;
                case "es": source = new[] { "Textura de carretera", "Generada o Road_BaseColor.png propia. Se deriva un mapa normal de la textura propia para dar relieve. El detalle debe ser 1 o superior.", "Generada", "PNG propia" }; break;
                case "fr": source = new[] { "Texture de route", "Générée ou votre Road_BaseColor.png. Une carte normale est créée à partir de votre texture pour donner du relief. Le niveau de détail doit être au moins 1.", "Générée", "PNG personnelle" }; break;
                case "it": source = new[] { "Texture stradale", "Generata o Road_BaseColor.png personalizzata. Dalla texture personalizzata viene creata una normal map per dare rilievo. Il dettaglio deve essere almeno 1.", "Generata", "PNG personale" }; break;
                default: source = new[] { "Road texture", "Generated or custom Road_BaseColor.png. A matching normal map is derived from the custom texture for surface relief. Asphalt detail must be at least 1.", "Generated", "Custom PNG" }; break;
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
                case "it": textureFiles = new[] { "Apri cartella texture", "Apre la cartella locale per Road_BaseColor.png. Il PNG personale non viene distribuito con la mod.", "Ricarica texture personale", "Rilegge Road_BaseColor.png. Seleziona il livello 2.", "Livello 2: copia Road_BaseColor.png nella cartella aperta dalle opzioni e ricaricala." }; break;
                default: textureFiles = new[] { "Open texture folder", "Opens the local folder for Road_BaseColor.png. Your PNG is not distributed with the mod.", "Reload custom texture", "Reads Road_BaseColor.png again after copying or replacing the file. Select asphalt detail level 2.", "Level 2: put Road_BaseColor.png in the folder opened from mod options, then reload it." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenRoadTextureFolder)), textureFiles[0]);
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
                case "de": depth = new[] { "Tiefenwirkung", "0 = originale Straßen-Normalmap. 100 bis 1000 verstärkt Asphalt- und Rissrelief passend zur UV-Größe bei allen Texturstufen. Keine echte Geometrie oder Parallax-Verdeckung." }; break;
                case "es": depth = new[] { "Relieve", "0 = normal original. De 100 a 1000 refuerza el relieve y las grietas con todas las texturas. No añade geometría ni paralaje real." }; break;
                case "fr": depth = new[] { "Relief", "0 = normale d’origine. De 100 à 1000 accentue le relief et les fissures avec toutes les textures. Sans géométrie ni parallaxe réelle." }; break;
                case "it": depth = new[] { "Rilievo", "0 = normale originale. Da 100 a 1000 accentua grana e crepe su tutte le texture. Nessuna geometria o parallasse reale." }; break;
                default: depth = new[] { "Surface depth", "0 = original road normal. 100 to 1000 strengthens asphalt and crack relief at every texture level. No geometry or true parallax occlusion." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.RoadDepthStrengthPercent)), depth[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.RoadDepthStrengthPercent)), depth[1]);
            string[] cracks;
            switch (m_Language)
            {
                case "de": cracks = new[] { "Rissanzahl", "0 = keine zusätzlichen Risse. Bis zu 400 Risse bei allen Asphaltstufen. Kachelung erhöht die Verteilung auf maximal 800 Risse; mehr Risse verlängern die Neuberechnung.",
                    "Rissgröße", "Länge zusätzlicher Risse: 25 % = fein, 100 % = normal, bis 600 % = sehr groß. Unabhängig von der UV-Größe; bei allen Stufen mit Rissanzahl über 0.",
                    "Risszufall", "0 = gleichmäßig verteilte, ruhigere Risse; 100 = unregelmäßigere Positionen und Verläufe. Die Anordnung bleibt bei gleichen Einstellungen reproduzierbar." }; break;
                case "es": cracks = new[] { "Cantidad de grietas", "0 = ninguna. Hasta 400 en todos los niveles; la repetición aumenta la distribución hasta un máximo de 800. Más grietas requieren más tiempo.",
                    "Tamaño de grietas", "Longitud: 25 % = finas, 100 % = normal, hasta 600 % = muy grandes. Independiente del tamaño UV.",
                    "Aleatoriedad de grietas", "0 = distribución más regular; 100 = posiciones y formas más irregulares. Los mismos ajustes producen el mismo patrón." }; break;
                case "fr": cracks = new[] { "Nombre de fissures", "0 = aucune. Jusqu’à 400 à tous les niveaux ; la répétition augmente la répartition jusqu’à 800. Plus de fissures augmentent le calcul.",
                    "Taille des fissures", "Longueur : 25 % = fines, 100 % = normal, jusqu’à 600 % = très grandes. Indépendante de la taille UV.",
                    "Aléa des fissures", "0 = répartition plus régulière ; 100 = positions et tracés plus irréguliers. Les mêmes réglages reproduisent le même motif." }; break;
                case "it": cracks = new[] { "Numero di crepe", "0 = nessuna. Fino a 400 su tutti i livelli; la ripetizione aumenta la distribuzione fino a 800. Più crepe richiedono più calcolo.",
                    "Dimensione crepe", "Lunghezza: 25% = sottili, 100% = normale, fino a 600% = molto grandi. Indipendente dalla dimensione UV.",
                    "Casualità crepe", "0 = distribuzione più regolare; 100 = posizioni e tracciati più irregolari. Le stesse impostazioni riproducono lo stesso motivo." }; break;
                default: cracks = new[] { "Crack count", "0 = no extra cracks. Up to 400 on every asphalt level. Tiling increases distribution up to 800 cracks; more cracks take longer to rebuild.",
                    "Crack size", "Length: 25% = fine, 100% = normal, up to 600% = very large. Independent of UV size; works on all levels when count is above 0.",
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
                case "de": width = new[] { "Rissbreite", "25 % = schmal, 100 % = bisherige Breite, bis 400 % = breiter. Wirkt auf zusätzliche Risse bei allen Stufen; größere Breite kann die Neuberechnung verlangsamen." }; break;
                case "es": width = new[] { "Ancho de grietas", "25 % = estrecho, 100 % = ancho anterior, hasta 400 % = más ancho. Afecta a las grietas añadidas en todos los niveles." }; break;
                case "fr": width = new[] { "Largeur des fissures", "25 % = fin, 100 % = largeur précédente, jusqu’à 400 % = plus large. Agit sur les fissures ajoutées à tous les niveaux." }; break;
                case "it": width = new[] { "Larghezza crepe", "25% = strette, 100% = larghezza precedente, fino a 400% = più larghe. Agisce sulle crepe aggiunte a tutti i livelli." }; break;
                default: width = new[] { "Crack width", "25% = narrow, 100% = previous width, up to 400% = wider. Affects cracks added to any asphalt level; wider cracks can take longer to rebuild." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.RoadCrackWidthPercent)), width[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.RoadCrackWidthPercent)), width[1]);
            string[] opacity;
            switch (m_Language)
            {
                case "de": opacity = new[] { "Rissdeckkraft", "Standard: 100 %. 0 % blendet zusätzliche Risse aus; 200 % macht sie dunkler und verstärkt ihr Relief. Wirkt bei allen Stufen mit Rissanzahl über 0. Höhere Deckkraft kann unnatürlich aussehen." }; break;
                case "es": opacity = new[] { "Opacidad de grietas", "Predeterminado: 100 %. 0 % oculta las grietas añadidas; 200 % las oscurece y refuerza su relieve. Requiere más de 0 grietas en cualquier nivel." }; break;
                case "fr": opacity = new[] { "Opacité des fissures", "Par défaut : 100 %. 0 % masque les fissures ajoutées ; 200 % les assombrit et renforce leur relief. Nécessite des fissures actives à tous les niveaux." }; break;
                case "it": opacity = new[] { "Opacità crepe", "Predefinito: 100%. 0% nasconde le crepe aggiunte; 200% le scurisce e ne aumenta il rilievo. Richiede crepe attive a qualsiasi livello." }; break;
                default: opacity = new[] { "Crack opacity", "Default: 100%. 0% hides extra cracks; 200% darkens them and strengthens their relief. Requires a crack count above 0 on any level. High opacity can look unnatural." }; break;
            }
            entries.Add(m_Setting.GetOptionLabelLocaleID(nameof(Setting.RoadCrackOpacityPercent)), opacity[0]);
            entries.Add(m_Setting.GetOptionDescLocaleID(nameof(Setting.RoadCrackOpacityPercent)), opacity[1]);
            string[] panel;
            switch (m_Language)
            {
                case "de": panel = new[] { "Asphalt-Fenster", "Asphalt-Button im Spiel anzeigen", "Zeigt einen Button während des Spiels. Er öffnet ein verschiebbares Fenster zur direkten Einstellung des Asphalts ohne die unscharfe Optionsansicht.", "Asphalt", "Asphalt", "Schließen", "Textur", "Generiert", "Eigene PNG", "Asphaltstruktur", "UV-Größe", "Tiefenwirkung", "Kachelung", "UV-Versatz X", "UV-Versatz Y", "Drehung", "Rissanzahl", "Rissgröße", "Risszufall", "Zurücksetzen", "Rissbreite", "Rissdeckkraft" }; break;
                case "es": panel = new[] { "Ventana de asfalto", "Mostrar botón de asfalto", "Muestra un botón durante el juego para ajustar el asfalto directamente en una ventana movible.", "Asfalto", "Asfalto", "Cerrar", "Textura", "Generada", "PNG propia", "Detalle", "Tamaño UV", "Relieve", "Repetición", "Desplazamiento X", "Desplazamiento Y", "Rotación", "Grietas", "Tamaño de grietas", "Variación", "Restablecer", "Ancho de grietas", "Opacidad de grietas" }; break;
                case "fr": panel = new[] { "Fenêtre asphalte", "Afficher le bouton asphalte", "Affiche un bouton en jeu ouvrant une fenêtre déplaçable pour régler l’asphalte directement.", "Asphalte", "Asphalte", "Fermer", "Texture", "Générée", "PNG personnelle", "Détails", "Taille UV", "Relief", "Répétition", "Décalage X", "Décalage Y", "Rotation", "Fissures", "Taille fissures", "Variation", "Réinitialiser", "Largeur des fissures", "Opacité des fissures" }; break;
                case "it": panel = new[] { "Finestra asfalto", "Mostra pulsante asfalto", "Mostra un pulsante nel gioco che apre una finestra mobile per modificare direttamente l’asfalto.", "Asfalto", "Asfalto", "Chiudi", "Texture", "Generata", "PNG personale", "Dettaglio", "Dimensione UV", "Rilievo", "Ripetizione", "Offset X", "Offset Y", "Rotazione", "Crepe", "Dimensione crepe", "Casualità", "Ripristina", "Larghezza crepe", "Opacità crepe" }; break;
                default: panel = new[] { "Road panel", "Show asphalt button in game", "Shows a button during gameplay to open a movable asphalt settings window without the blurred options view.", "Asphalt", "Asphalt", "Close", "Texture", "Generated", "Custom PNG", "Detail", "UV size", "Surface depth", "Tiling", "UV offset X", "UV offset Y", "Rotation", "Crack count", "Crack size", "Randomness", "Reset", "Crack width", "Crack opacity" }; break;
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
                case "de": gravel = new[] { "Schotter: UV und Tiefe", "Gleisschotter anpassen", "Ändert ausschließlich die TrainGravelWorldspace-Textur der Gleise; Schienen und Schwellen bleiben unverändert.", "Schotterstruktur", "0 = Original; 1 = hellerer, feinerer Schotter mit passender Normalmap; 2 = eigene RailGravel_BaseColor.png.", "Schotter-UV-Größe", "100 = ursprüngliche Größe; höher vergrößert die Kiesstruktur, bis 1000.", "Schotter-Tiefenwirkung", "0 = Original-Normalmap bei Original, flach bei Generiert; höhere Werte verstärken die passende Schotter-Normalmap oder die berechnete Tiefe der eigenen Textur. Keine echte Geometrie.", "Schotter-Versatz X", "Verschiebt die Schottertextur horizontal.", "Schotter-Versatz Y", "Verschiebt die Schottertextur vertikal.", "Schotter-Drehung", "Dreht Schotter und Tiefenwirkung frei von 0 bis 360 Grad.", "Schotter zurücksetzen", "Stellt Originaltexturen und Schotterwerte wieder her.", "Schotterordner öffnen", "Öffnet RailTextures für RailGravel_BaseColor.png.", "Eigene Schottertextur neu laden", "Lädt RailGravel_BaseColor.png nach dem Kopieren erneut.", "RailGravel_BaseColor.png in RailTextures kopieren und in den Optionen neu laden." }; break;
                case "es": gravel = new[] { "Balasto: UV y relieve", "Ajustar balasto", "Solo cambia TrainGravelWorldspace; rieles y traviesas permanecen intactos.", "Textura del balasto", "0 = original; 1 = balasto más claro y fino con su normal correspondiente; 2 = RailGravel_BaseColor.png propia.", "Tamaño UV del balasto", "100 = tamaño original; valores mayores amplían el grano, hasta 1000.", "Relieve del balasto", "0 = normal original en modo Original, plana en Generado; valores mayores realzan la normal del balasto o el relieve de la textura propia. Sin geometría real.", "Desplazamiento X", "Desplaza el balasto horizontalmente.", "Desplazamiento Y", "Desplaza el balasto verticalmente.", "Rotación del balasto", "Gira el patrón de 0 a 360 grados.", "Restablecer balasto", "Restaura texturas y valores originales.", "Abrir carpeta del balasto", "Abre RailTextures para RailGravel_BaseColor.png.", "Recargar balasto personal", "Vuelve a cargar RailGravel_BaseColor.png.", "Coloca RailGravel_BaseColor.png en RailTextures y vuelve a cargarla en opciones." }; break;
                case "fr": gravel = new[] { "Ballast : UV et relief", "Modifier le ballast", "Modifie uniquement TrainGravelWorldspace ; rails et traverses restent intacts.", "Texture du ballast", "0 = origine ; 1 = ballast plus clair et plus fin avec sa normale associée ; 2 = RailGravel_BaseColor.png personnelle.", "Taille UV du ballast", "100 = taille d’origine ; plus grand agrandit les pierres, jusqu’à 1000.", "Relief du ballast", "0 = normale originale en mode Origine, surface plate en mode Généré ; les valeurs supérieures renforcent la normale du ballast ou le relief calculé de la texture personnelle. Sans géométrie réelle.", "Décalage X", "Décale le ballast horizontalement.", "Décalage Y", "Décale le ballast verticalement.", "Rotation du ballast", "Tourne la texture de 0 à 360 degrés.", "Réinitialiser le ballast", "Restaure textures et valeurs initiales.", "Ouvrir le dossier ballast", "Ouvre RailTextures pour RailGravel_BaseColor.png.", "Recharger le ballast personnel", "Recharge RailGravel_BaseColor.png.", "Placez RailGravel_BaseColor.png dans RailTextures, puis rechargez dans les options." }; break;
                case "it": gravel = new[] { "Pietrisco: UV e rilievo", "Modifica pietrisco", "Cambia solo TrainGravelWorldspace; rotaie e traversine restano intatte.", "Struttura pietrisco", "0 = originale; 1 = pietrisco più chiaro e fine con relativa normale; 2 = RailGravel_BaseColor.png personale.", "Dimensione UV pietrisco", "100 = originale; valori maggiori ingrandiscono la grana fino a 1000.", "Rilievo pietrisco", "0 = normale originale in modalità Originale, piatta in Generata; valori maggiori rafforzano la normale del pietrisco o il rilievo calcolato della texture personale. Nessuna geometria reale.", "Spostamento X", "Sposta il pietrisco in orizzontale.", "Spostamento Y", "Sposta il pietrisco in verticale.", "Rotazione pietrisco", "Ruota la texture da 0 a 360 gradi.", "Ripristina pietrisco", "Ripristina texture e valori originali.", "Apri cartella pietrisco", "Apre RailTextures per RailGravel_BaseColor.png.", "Ricarica pietrisco personale", "Ricarica RailGravel_BaseColor.png.", "Copia RailGravel_BaseColor.png in RailTextures e ricarica nelle opzioni." }; break;
                default: gravel = new[] { "Track gravel UV and depth", "Enable track gravel adjustments", "Changes only the track's TrainGravelWorldspace texture; steel rails and sleepers remain unchanged.", "Gravel texture", "0 = original; 1 = lighter, finer ballast with its matching normal map; 2 = custom RailGravel_BaseColor.png.", "Gravel UV size", "100 = original size; higher enlarges stones up to 1000.", "Gravel depth", "0 = original normal at level 0, flat at level 1; higher values strengthen the matching ballast normal, or color-derived relief at level 2. No geometry.", "Gravel offset X", "Shifts the gravel pattern horizontally.", "Gravel offset Y", "Shifts the gravel pattern vertically.", "Gravel rotation", "Rotates gravel and surface depth freely from 0 to 360 degrees.", "Reset gravel", "Restores the original track gravel maps and controls.", "Open gravel texture folder", "Opens RailTextures for RailGravel_BaseColor.png.", "Reload custom gravel texture", "Reloads RailGravel_BaseColor.png after copying it.", "Put RailGravel_BaseColor.png in RailTextures and reload it in options." }; break;
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
            AddColorFilterEntries(entries);
            return entries;
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
                    description = "Asphalt scale on parking spaces: 0.1 to 8.0 in 0.1 steps. 1.0 = initial mod scale; higher = finer, smaller texture, lower = coarser texture. Affects color and surface depth. Saved separately for Original, Generated and Custom PNG.";
                    break;
            }
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ParkingTextureScaleFactor))] = label;
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ParkingTextureScaleFactor))] = description;
            entries["AdvancedGraphics.UI.ParkingScale"] = label;
            entries["AdvancedGraphics.UI.TipParkingScale"] = description;
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
    }
}


