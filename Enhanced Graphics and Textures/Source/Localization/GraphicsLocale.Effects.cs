using System.Collections.Generic;

namespace Advanced_Graphics
{
    public sealed partial class GraphicsLocale
    {
        // Graphics window: contact shadows, ambient occlusion, cascades, exposure, fog and shadow range (all languages).
        private void AddEffectEntries(Dictionary<string, string> entries)
        {
            string[] t;
            switch (m_Language)
            {
                case "de":
                    t = new[]
                    {
                        "Effekte",
                        "Spielwerte",
                        "Nebel",
                        "Kontaktschatten",
                        "Feine Schatten auf kurze Entfernung, z. B. an Bordsteinen, Zäunen, Pollern und Baumfüßen. Aus = Spielwert. Schaltet die Kontaktschatten der Sonne und der Kamera ein; alle anderen Kameraeinstellungen bleiben unverändert. Leichte GPU-Kosten.",
                        "Kontaktschatten Länge (m)",
                        "Wie weit ein Kontaktschatten reicht: 0,05–1,00 m. Größere Werte zeigen auch höhere Kanten, können aber Schattenstreifen erzeugen. Ausgangswert 0,30.",
                        "Kontaktschatten Deckkraft (%)",
                        "Stärke der Kontaktschatten: 0–100 %. Ausgangswert 100 %.",
                        "Kontaktschatten Reichweite (m)",
                        "Bis zu dieser Kameraentfernung werden Kontaktschatten berechnet, danach blenden sie aus: 10–2000 m. Ausgangswert 300 m. Größere Reichweite kostet etwas mehr Leistung.",
                        "Umgebungsverdeckung Stärke (%)",
                        "Skaliert die Umgebungsverdeckung (SSAO) des Spiels: 100 % = Spielwert, 0 % = keine, bis 400 %. Ist die Umgebungsverdeckung in den Spieloptionen aus, bleibt sie aus.",
                        "Umgebungsverdeckung Radius (%)",
                        "Skaliert den Radius der Umgebungsverdeckung: 100 % = Spielwert, 25–400 %. Größer = weichere, breitere Verdunkelung an Gebäudefüßen und Ecken.",
                        "Nebeldichte (%)",
                        "Dichte des Nebels relativ zum Spiel: 100 % = Spielwert, 200 % halbiert die Sichtweite, 0 % schaltet den Nebel aus. Folgt Tageszeit und Wetter des Spiels.",
                        "Schattenkaskaden Nahbereich",
                        "Verschiebt die Schattenkaskaden näher zur Kamera: 0 = Spielwert, 100 = stärkste Verschiebung. Nahe Schatten werden schärfer, entfernte Schatten weicher.",
                        "Belichtungskorrektur (EV)",
                        "Helligkeit des ganzen Bildes in Blendenstufen: -3 bis +3, 0 = Spielwert. Positive Werte machen das Bild heller. Unabhängig von der Himmelsbelichtung.",
                        "Belichtungsanpassung Tempo (%)",
                        "Wie schnell sich die automatische Belichtung an hell und dunkel anpasst: 100 % = Spielwert, 10–500 %. Niedrig = weniger „Pumpen“ beim Zoomen, hoch = schnellere Anpassung.",
                    };
                    break;
                case "es":
                    t = new[]
                    {
                        "Efectos",
                        "Valores del juego",
                        "Niebla",
                        "Sombras de contacto",
                        "Sombras finas a corta distancia, p. ej. en bordillos, vallas, bolardos y troncos. Desactivado = valor del juego. Activa las sombras de contacto del sol y de la cámara; el resto de ajustes de la cámara no cambia. Coste de GPU bajo.",
                        "Longitud de sombras de contacto (m)",
                        "Alcance de la sombra de contacto: 0,05–1,00 m. Valores altos muestran bordes más altos, pero pueden crear franjas. Valor inicial 0,30.",
                        "Opacidad de sombras de contacto (%)",
                        "Intensidad de las sombras de contacto: 0–100 %. Valor inicial 100 %.",
                        "Distancia de sombras de contacto (m)",
                        "Hasta esta distancia de la cámara se calculan las sombras de contacto; después se desvanecen: 10–2000 m. Valor inicial 300 m.",
                        "Intensidad de oclusión ambiental (%)",
                        "Escala la oclusión ambiental (SSAO) del juego: 100 % = valor del juego, 0 % = ninguna, hasta 400 %. Si está desactivada en las opciones del juego, sigue desactivada.",
                        "Radio de oclusión ambiental (%)",
                        "Escala el radio de la oclusión ambiental: 100 % = valor del juego, 25–400 %.",
                        "Densidad de niebla (%)",
                        "Densidad de la niebla respecto al juego: 100 % = valor del juego, 200 % reduce la visibilidad a la mitad, 0 % elimina la niebla. Sigue la hora y el clima del juego.",
                        "Cascadas de sombra cercanas",
                        "Acerca las cascadas de sombra a la cámara: 0 = valor del juego, 100 = máximo. Las sombras cercanas son más nítidas y las lejanas más suaves.",
                        "Compensación de exposición (EV)",
                        "Brillo de toda la imagen: -3 a +3, 0 = valor del juego. Valores positivos aclaran la imagen. Independiente de la exposición del cielo.",
                        "Velocidad de adaptación de exposición (%)",
                        "Rapidez con la que la exposición automática se adapta: 100 % = valor del juego, 10–500 %. Bajo = menos cambios bruscos al hacer zoom.",
                    };
                    break;
                case "fr":
                    t = new[]
                    {
                        "Effets",
                        "Valeurs du jeu",
                        "Brouillard",
                        "Ombres de contact",
                        "Ombres fines à courte distance, p. ex. bordures, clôtures, bornes et pieds d’arbres. Désactivé = valeur du jeu. Active les ombres de contact du soleil et de la caméra ; les autres réglages de la caméra restent inchangés. Faible coût GPU.",
                        "Longueur des ombres de contact (m)",
                        "Portée d’une ombre de contact : 0,05–1,00 m. Des valeurs élevées montrent des arêtes plus hautes mais peuvent créer des stries. Valeur initiale 0,30.",
                        "Opacité des ombres de contact (%)",
                        "Intensité des ombres de contact : 0–100 %. Valeur initiale 100 %.",
                        "Distance des ombres de contact (m)",
                        "Les ombres de contact sont calculées jusqu’à cette distance de la caméra, puis s’estompent : 10–2000 m. Valeur initiale 300 m.",
                        "Intensité de l’occlusion ambiante (%)",
                        "Ajuste l’occlusion ambiante (SSAO) du jeu : 100 % = valeur du jeu, 0 % = aucune, jusqu’à 400 %. Si elle est désactivée dans les options du jeu, elle le reste.",
                        "Rayon de l’occlusion ambiante (%)",
                        "Ajuste le rayon de l’occlusion ambiante : 100 % = valeur du jeu, 25–400 %.",
                        "Densité du brouillard (%)",
                        "Densité du brouillard par rapport au jeu : 100 % = valeur du jeu, 200 % divise la visibilité par deux, 0 % supprime le brouillard. Suit l’heure et la météo du jeu.",
                        "Cascades d’ombres proches",
                        "Rapproche les cascades d’ombres de la caméra : 0 = valeur du jeu, 100 = maximum. Les ombres proches deviennent plus nettes, les lointaines plus douces.",
                        "Correction d’exposition (EV)",
                        "Luminosité de toute l’image : -3 à +3, 0 = valeur du jeu. Les valeurs positives éclaircissent l’image. Indépendant de l’exposition du ciel.",
                        "Vitesse d’adaptation de l’exposition (%)",
                        "Rapidité d’adaptation de l’exposition automatique : 100 % = valeur du jeu, 10–500 %. Bas = moins de variations lors du zoom.",
                    };
                    break;
                case "it":
                    t = new[]
                    {
                        "Effetti",
                        "Valori del gioco",
                        "Nebbia",
                        "Ombre di contatto",
                        "Ombre sottili a breve distanza, ad es. su cordoli, recinzioni, paletti e alberi. Disattivo = valore del gioco. Attiva le ombre di contatto del sole e della camera; le altre impostazioni della camera restano invariate. Costo GPU ridotto.",
                        "Lunghezza ombre di contatto (m)",
                        "Portata dell’ombra di contatto: 0,05–1,00 m. Valori alti mostrano bordi più alti ma possono creare striature. Valore iniziale 0,30.",
                        "Opacità ombre di contatto (%)",
                        "Intensità delle ombre di contatto: 0–100 %. Valore iniziale 100 %.",
                        "Distanza ombre di contatto (m)",
                        "Fino a questa distanza dalla camera vengono calcolate le ombre di contatto, poi sfumano: 10–2000 m. Valore iniziale 300 m.",
                        "Intensità occlusione ambientale (%)",
                        "Scala l’occlusione ambientale (SSAO) del gioco: 100 % = valore del gioco, 0 % = nessuna, fino a 400 %. Se è disattivata nelle opzioni del gioco, resta disattivata.",
                        "Raggio occlusione ambientale (%)",
                        "Scala il raggio dell’occlusione ambientale: 100 % = valore del gioco, 25–400 %.",
                        "Densità nebbia (%)",
                        "Densità della nebbia rispetto al gioco: 100 % = valore del gioco, 200 % dimezza la visibilità, 0 % rimuove la nebbia. Segue ora del giorno e meteo del gioco.",
                        "Cascate d’ombra vicine",
                        "Avvicina le cascate d’ombra alla camera: 0 = valore del gioco, 100 = massimo. Le ombre vicine diventano più nitide, quelle lontane più morbide.",
                        "Compensazione esposizione (EV)",
                        "Luminosità dell’intera immagine: da -3 a +3, 0 = valore del gioco. Valori positivi schiariscono l’immagine. Indipendente dall’esposizione del cielo.",
                        "Velocità adattamento esposizione (%)",
                        "Rapidità con cui l’esposizione automatica si adatta: 100 % = valore del gioco, 10–500 %. Basso = meno sbalzi durante lo zoom.",
                    };
                    break;
                case "ru":
                    t = new[]
                    {
                        "Эффекты",
                        "Значения игры",
                        "Туман",
                        "Контактные тени",
                        "Мелкие тени на близком расстоянии, например у бордюров, заборов, столбиков и деревьев. Выкл. = значение игры. Включает контактные тени для солнца и камеры; остальные настройки камеры не меняются. Небольшая нагрузка на GPU.",
                        "Длина контактных теней (м)",
                        "Насколько далеко тянется контактная тень: 0,05–1,00 м. Большие значения показывают и более высокие края, но могут давать полосы. Начальное значение 0,30.",
                        "Непрозрачность контактных теней (%)",
                        "Сила контактных теней: 0–100 %. Начальное значение 100 %.",
                        "Дальность контактных теней (м)",
                        "До этого расстояния от камеры рассчитываются контактные тени, дальше они исчезают: 10–2000 м. Начальное значение 300 м.",
                        "Сила затенения окружения (%)",
                        "Масштабирует затенение окружения (SSAO) игры: 100 % = значение игры, 0 % = нет, до 400 %. Если в настройках игры SSAO выключено, оно остаётся выключенным.",
                        "Радиус затенения окружения (%)",
                        "Масштабирует радиус затенения окружения: 100 % = значение игры, 25–400 %. Больше = мягче и шире затемнение у оснований зданий и в углах.",
                        "Плотность тумана (%)",
                        "Плотность тумана относительно игры: 100 % = значение игры, 200 % вдвое сокращает видимость, 0 % убирает туман. Следует времени суток и погоде.",
                        "Каскады теней вблизи",
                        "Сдвигает каскады теней ближе к камере: 0 = значение игры, 100 = максимум. Ближние тени становятся чётче, дальние мягче.",
                        "Коррекция экспозиции (EV)",
                        "Яркость всего изображения: от -3 до +3, 0 = значение игры. Положительные значения делают изображение светлее. Не зависит от экспозиции неба.",
                        "Скорость адаптации экспозиции (%)",
                        "Как быстро автоматическая экспозиция подстраивается под светлые и тёмные сцены: 100 % = значение игры, 10–500 %. Меньше = меньше «пульсации» при зуме.",
                    };
                    break;
                case "pl":
                    t = new[]
                    {
                        "Efekty",
                        "Wartości gry",
                        "Mgła",
                        "Cienie kontaktowe",
                        "Drobne cienie z bliska, np. przy krawężnikach, płotach, słupkach i drzewach. Wył. = wartość gry. Włącza cienie kontaktowe słońca i kamery; pozostałe ustawienia kamery bez zmian. Niewielkie obciążenie GPU.",
                        "Długość cieni kontaktowych (m)",
                        "Zasięg cienia kontaktowego: 0,05–1,00 m. Większe wartości pokazują też wyższe krawędzie, ale mogą tworzyć smugi. Wartość początkowa 0,30.",
                        "Krycie cieni kontaktowych (%)",
                        "Siła cieni kontaktowych: 0–100 %. Wartość początkowa 100 %.",
                        "Zasięg cieni kontaktowych (m)",
                        "Do tej odległości od kamery liczone są cienie kontaktowe, dalej zanikają: 10–2000 m. Wartość początkowa 300 m.",
                        "Siła okluzji otoczenia (%)",
                        "Skaluje okluzję otoczenia (SSAO) gry: 100 % = wartość gry, 0 % = brak, do 400 %. Jeśli SSAO jest wyłączone w opcjach gry, pozostaje wyłączone.",
                        "Promień okluzji otoczenia (%)",
                        "Skaluje promień okluzji otoczenia: 100 % = wartość gry, 25–400 %. Więcej = miękkie, szersze zaciemnienie u podstaw budynków i w narożnikach.",
                        "Gęstość mgły (%)",
                        "Gęstość mgły względem gry: 100 % = wartość gry, 200 % skraca widoczność o połowę, 0 % usuwa mgłę. Podąża za porą dnia i pogodą.",
                        "Kaskady cieni z bliska",
                        "Przesuwa kaskady cieni bliżej kamery: 0 = wartość gry, 100 = maksimum. Bliskie cienie są ostrzejsze, dalekie bardziej miękkie.",
                        "Korekta ekspozycji (EV)",
                        "Jasność całego obrazu: od -3 do +3, 0 = wartość gry. Wartości dodatnie rozjaśniają obraz. Niezależna od ekspozycji nieba.",
                        "Szybkość adaptacji ekspozycji (%)",
                        "Jak szybko automatyczna ekspozycja dopasowuje się do jasnych i ciemnych scen: 100 % = wartość gry, 10–500 %. Mniej = mniej „pulsowania” przy zoomie.",
                    };
                    break;
                case "pt-BR":
                    t = new[]
                    {
                        "Efeitos",
                        "Valores do jogo",
                        "Neblina",
                        "Sombras de contato",
                        "Sombras finas a curta distância, p. ex. em meios-fios, cercas, balizadores e árvores. Desligado = valor do jogo. Ativa as sombras de contato do sol e da câmera; as demais configurações da câmera não mudam. Baixo custo de GPU.",
                        "Comprimento das sombras de contato (m)",
                        "Alcance da sombra de contato: 0,05–1,00 m. Valores maiores mostram bordas mais altas, mas podem criar faixas. Valor inicial 0,30.",
                        "Opacidade das sombras de contato (%)",
                        "Intensidade das sombras de contato: 0–100 %. Valor inicial 100 %.",
                        "Distância das sombras de contato (m)",
                        "Até esta distância da câmera as sombras de contato são calculadas; depois desaparecem: 10–2000 m. Valor inicial 300 m.",
                        "Intensidade da oclusão de ambiente (%)",
                        "Escala a oclusão de ambiente (SSAO) do jogo: 100 % = valor do jogo, 0 % = nenhuma, até 400 %. Se estiver desligada nas opções do jogo, continua desligada.",
                        "Raio da oclusão de ambiente (%)",
                        "Escala o raio da oclusão de ambiente: 100 % = valor do jogo, 25–400 %. Maior = escurecimento mais suave e largo na base dos prédios e nos cantos.",
                        "Densidade da neblina (%)",
                        "Densidade da neblina em relação ao jogo: 100 % = valor do jogo, 200 % reduz a visibilidade pela metade, 0 % remove a neblina. Segue a hora do dia e o clima.",
                        "Cascatas de sombra próximas",
                        "Aproxima as cascatas de sombra da câmera: 0 = valor do jogo, 100 = máximo. Sombras próximas ficam mais nítidas, as distantes mais suaves.",
                        "Compensação de exposição (EV)",
                        "Brilho da imagem inteira: de -3 a +3, 0 = valor do jogo. Valores positivos clareiam a imagem. Independente da exposição do céu.",
                        "Velocidade de adaptação da exposição (%)",
                        "Rapidez com que a exposição automática se adapta a cenas claras e escuras: 100 % = valor do jogo, 10–500 %. Menos = menos oscilação ao dar zoom.",
                    };
                    break;
                case "ja":
                    t = new[]
                    {
                        "エフェクト",
                        "ゲームの値",
                        "霧",
                        "コンタクトシャドウ",
                        "縁石、フェンス、ボラード、木の根元などの近距離の細かい影。オフ = ゲームの値。太陽とカメラのコンタクトシャドウを有効にし、その他のカメラ設定は変更しません。GPU負荷は小さめです。",
                        "コンタクトシャドウの長さ (m)",
                        "コンタクトシャドウの届く距離: 0.05–1.00 m。大きい値では高い縁にも影が付きますが、筋が出ることがあります。初期値 0.30。",
                        "コンタクトシャドウの不透明度 (%)",
                        "コンタクトシャドウの強さ: 0–100 %。初期値 100 %。",
                        "コンタクトシャドウの範囲 (m)",
                        "カメラからこの距離までコンタクトシャドウを計算し、その先はフェードアウトします: 10–2000 m。初期値 300 m。",
                        "アンビエントオクルージョンの強さ (%)",
                        "ゲームのアンビエントオクルージョン (SSAO) を拡大縮小します: 100 % = ゲームの値、0 % = なし、最大 400 %。ゲームのオプションでオフの場合はオフのままです。",
                        "アンビエントオクルージョンの半径 (%)",
                        "アンビエントオクルージョンの半径を拡大縮小します: 100 % = ゲームの値、25–400 %。大きいほど建物の足元や角の暗がりが柔らかく広くなります。",
                        "霧の濃さ (%)",
                        "ゲームに対する霧の濃さ: 100 % = ゲームの値、200 % で視程が半分、0 % で霧なし。時間帯と天候に追従します。",
                        "シャドウカスケード近距離",
                        "シャドウカスケードをカメラに近づけます: 0 = ゲームの値、100 = 最大。近くの影はシャープに、遠くの影は柔らかくなります。",
                        "露出補正 (EV)",
                        "画面全体の明るさ: -3〜+3、0 = ゲームの値。正の値で明るくなります。空の露出とは別です。",
                        "露出の順応速度 (%)",
                        "自動露出が明暗に順応する速さ: 100 % = ゲームの値、10–500 %。低いほどズーム時の明るさの揺れが減ります。",
                    };
                    break;
                case "ko":
                    t = new[]
                    {
                        "효과",
                        "게임 값",
                        "안개",
                        "접촉 그림자",
                        "연석, 울타리, 볼라드, 나무 밑동 등 가까운 거리의 섬세한 그림자. 끔 = 게임 값. 태양과 카메라의 접촉 그림자를 켜며 다른 카메라 설정은 바꾸지 않습니다. GPU 부하가 적습니다.",
                        "접촉 그림자 길이 (m)",
                        "접촉 그림자가 닿는 거리: 0.05–1.00 m. 값이 크면 높은 모서리에도 그림자가 생기지만 줄무늬가 생길 수 있습니다. 초기값 0.30.",
                        "접촉 그림자 불투명도 (%)",
                        "접촉 그림자의 강도: 0–100 %. 초기값 100 %.",
                        "접촉 그림자 범위 (m)",
                        "카메라에서 이 거리까지 접촉 그림자를 계산하고 그 너머에서는 사라집니다: 10–2000 m. 초기값 300 m.",
                        "앰비언트 오클루전 강도 (%)",
                        "게임의 앰비언트 오클루전(SSAO)을 조절합니다: 100 % = 게임 값, 0 % = 없음, 최대 400 %. 게임 옵션에서 꺼져 있으면 꺼진 상태로 유지됩니다.",
                        "앰비언트 오클루전 반경 (%)",
                        "앰비언트 오클루전 반경을 조절합니다: 100 % = 게임 값, 25–400 %. 클수록 건물 아래와 모서리의 어두움이 부드럽고 넓어집니다.",
                        "안개 밀도 (%)",
                        "게임 대비 안개 밀도: 100 % = 게임 값, 200 %는 가시거리를 절반으로, 0 %는 안개를 없앱니다. 시간대와 날씨를 따릅니다.",
                        "그림자 캐스케이드 근거리",
                        "그림자 캐스케이드를 카메라 쪽으로 당깁니다: 0 = 게임 값, 100 = 최대. 가까운 그림자는 선명해지고 먼 그림자는 부드러워집니다.",
                        "노출 보정 (EV)",
                        "화면 전체의 밝기: -3 ~ +3, 0 = 게임 값. 양수 값은 화면을 밝게 합니다. 하늘 노출과는 별개입니다.",
                        "노출 적응 속도 (%)",
                        "자동 노출이 밝고 어두운 장면에 적응하는 속도: 100 % = 게임 값, 10–500 %. 낮을수록 줌할 때 밝기 변화가 줄어듭니다.",
                    };
                    break;
                case "zh-HANS":
                    t = new[]
                    {
                        "效果",
                        "游戏数值",
                        "雾",
                        "接触阴影",
                        "近距离的细小阴影,例如路缘、围栏、护柱和树根处。关 = 游戏数值。为太阳和摄像机启用接触阴影;其他摄像机设置保持不变。GPU 开销较小。",
                        "接触阴影长度 (米)",
                        "接触阴影的延伸距离:0.05–1.00 米。较大的值也能显示较高的边缘,但可能产生条纹。初始值 0.30。",
                        "接触阴影不透明度 (%)",
                        "接触阴影的强度:0–100 %。初始值 100 %。",
                        "接触阴影范围 (米)",
                        "在距摄像机此距离内计算接触阴影,超出后逐渐消失:10–2000 米。初始值 300 米。",
                        "环境光遮蔽强度 (%)",
                        "缩放游戏的环境光遮蔽 (SSAO):100 % = 游戏数值,0 % = 无,最高 400 %。若在游戏选项中关闭,则保持关闭。",
                        "环境光遮蔽半径 (%)",
                        "缩放环境光遮蔽半径:100 % = 游戏数值,25–400 %。越大,建筑底部和角落的暗部越柔和、越宽。",
                        "雾浓度 (%)",
                        "相对于游戏的雾浓度:100 % = 游戏数值,200 % 使能见度减半,0 % 去除雾。跟随时间和天气变化。",
                        "阴影级联近距离",
                        "将阴影级联移近摄像机:0 = 游戏数值,100 = 最大。近处阴影更清晰,远处阴影更柔和。",
                        "曝光补偿 (EV)",
                        "整个画面的亮度:-3 至 +3,0 = 游戏数值。正值使画面变亮。与天空曝光无关。",
                        "曝光适应速度 (%)",
                        "自动曝光适应明暗场景的速度:100 % = 游戏数值,10–500 %。越低,缩放时的亮度波动越小。",
                    };
                    break;
                case "zh-HANT":
                    t = new[]
                    {
                        "效果",
                        "遊戲數值",
                        "霧",
                        "接觸陰影",
                        "近距離的細小陰影,例如路緣、圍欄、護柱和樹根處。關 = 遊戲數值。為太陽和攝影機啟用接觸陰影;其他攝影機設定保持不變。GPU 負擔較小。",
                        "接觸陰影長度 (公尺)",
                        "接觸陰影的延伸距離:0.05–1.00 公尺。較大的值也能顯示較高的邊緣,但可能產生條紋。初始值 0.30。",
                        "接觸陰影不透明度 (%)",
                        "接觸陰影的強度:0–100 %。初始值 100 %。",
                        "接觸陰影範圍 (公尺)",
                        "在距攝影機此距離內計算接觸陰影,超出後逐漸消失:10–2000 公尺。初始值 300 公尺。",
                        "環境光遮蔽強度 (%)",
                        "縮放遊戲的環境光遮蔽 (SSAO):100 % = 遊戲數值,0 % = 無,最高 400 %。若在遊戲選項中關閉,則保持關閉。",
                        "環境光遮蔽半徑 (%)",
                        "縮放環境光遮蔽半徑:100 % = 遊戲數值,25–400 %。越大,建築底部和角落的暗部越柔和、越寬。",
                        "霧濃度 (%)",
                        "相對於遊戲的霧濃度:100 % = 遊戲數值,200 % 使能見度減半,0 % 去除霧。跟隨時間和天氣變化。",
                        "陰影級聯近距離",
                        "將陰影級聯移近攝影機:0 = 遊戲數值,100 = 最大。近處陰影更清晰,遠處陰影更柔和。",
                        "曝光補償 (EV)",
                        "整個畫面的亮度:-3 至 +3,0 = 遊戲數值。正值使畫面變亮。與天空曝光無關。",
                        "曝光適應速度 (%)",
                        "自動曝光適應明暗場景的速度:100 % = 遊戲數值,10–500 %。越低,縮放時的亮度波動越小。",
                    };
                    break;
                default:
                    t = new[]
                    {
                        "Effects",
                        "Game values",
                        "Fog",
                        "Contact shadows",
                        "Fine short-range shadows, e.g. on curbs, fences, bollards and tree bases. Off = game value. Enables contact shadows for the sun and the camera; all other camera settings stay unchanged. Low GPU cost.",
                        "Contact shadow length (m)",
                        "How far a contact shadow reaches: 0.05–1.00 m. Larger values also show taller edges but can cause shadow streaks. Initial value 0.30.",
                        "Contact shadow opacity (%)",
                        "Strength of contact shadows: 0–100 %. Initial value 100 %.",
                        "Contact shadow range (m)",
                        "Contact shadows are computed up to this camera distance and fade out beyond it: 10–2000 m. Initial value 300 m. A longer range costs a little more performance.",
                        "Ambient occlusion strength (%)",
                        "Scales the game's ambient occlusion (SSAO): 100 % = game value, 0 % = none, up to 400 %. If ambient occlusion is off in the game options, it stays off.",
                        "Ambient occlusion radius (%)",
                        "Scales the ambient occlusion radius: 100 % = game value, 25–400 %. Larger = softer, wider darkening at building bases and corners.",
                        "Fog density (%)",
                        "Fog density relative to the game: 100 % = game value, 200 % halves the visibility distance, 0 % removes the fog. Follows the game's time of day and weather.",
                        "Shadow cascades near focus",
                        "Moves the shadow cascades closer to the camera: 0 = game value, 100 = strongest. Near shadows become sharper, distant shadows softer.",
                        "Exposure compensation (EV)",
                        "Brightness of the whole image in stops: -3 to +3, 0 = game value. Positive values brighten the image. Independent of sky exposure.",
                        "Exposure adaptation speed (%)",
                        "How fast the automatic exposure adapts to bright and dark scenes: 100 % = game value, 10–500 %. Low = less “pumping” while zooming, high = faster adaptation.",
                    };
                    break;
            }
            const string prefix = "AdvancedGraphics.UI.Graphics.";
            entries[prefix + "Effects"] = t[0];
            entries[prefix + "EffectsGameValues"] = t[1];
            entries[prefix + "FogDistance"] = t[2];
            var keys = new[] { "ContactShadowsEnabled", "ContactShadowLength", "ContactShadowOpacityPercent",
                "ContactShadowDistance", "AmbientOcclusionStrengthPercent", "AmbientOcclusionRadiusPercent",
                "FogDensityPercent", "ShadowCascadeFocus", "ExposureCompensation", "ExposureAdaptationPercent" };
            for (int i = 0; i < keys.Length; i++)
            {
                entries[prefix + keys[i]] = t[3 + i * 2];
                entries[prefix + "Tip" + keys[i]] = t[4 + i * 2];
            }
            AddShadowRangeEntries(entries);
        }

        private void AddShadowRangeEntries(Dictionary<string, string> entries)
        {
            string[] t;
            switch (m_Language)
            {
                case "de":
                    t = new[]
                    {
                        "Schattenreichweite (%)",
                        "Reichweite der Sonnenschatten in Prozent der aktuellen Spielreichweite: 100 % = Spiel, 25–200 %. Das Spiel passt die Reichweite selbst an das an, was die Kamera sieht; der Regler folgt dem. Weniger: Schatten enden früher, mehr FPS. Mehr: ferne Schatten bei flachem Blick zum Horizont, kostet Leistung. Die Kaskaden-Abstände in Metern bleiben gleich.",
                        "Setzt die Schattenreichweite auf 100 % (Spielwert) zurück.",
                        "Schattenreichweite Spiel aktuell",
                    };
                    break;
                case "es":
                    t = new[]
                    {
                        "Alcance de sombras (%)",
                        "Alcance de las sombras del sol en porcentaje del alcance actual del juego: 100 % = juego, 25–200 %. El juego ajusta el alcance a lo que ve la cámara y el control lo sigue. Menos: las sombras terminan antes, más FPS. Más: sombras lejanas con vista rasante, cuesta rendimiento. Las distancias de las cascadas en metros no cambian.",
                        "Restablece el alcance de sombras al 100 % (valor del juego).",
                        "Alcance actual del juego",
                    };
                    break;
                case "fr":
                    t = new[]
                    {
                        "Portée des ombres (%)",
                        "Portée des ombres du soleil en pourcentage de la portée actuelle du jeu : 100 % = jeu, 25–200 %. Le jeu adapte la portée à ce que voit la caméra et le curseur la suit. Moins : les ombres s’arrêtent plus tôt, plus de FPS. Plus : ombres lointaines en vue rasante, coûte des performances. Les distances des cascades en mètres restent identiques.",
                        "Rétablit la portée des ombres à 100 % (valeur du jeu).",
                        "Portée actuelle du jeu",
                    };
                    break;
                case "it":
                    t = new[]
                    {
                        "Portata ombre (%)",
                        "Portata delle ombre del sole in percentuale della portata attuale del gioco: 100 % = gioco, 25–200 %. Il gioco adatta la portata a ciò che vede la camera e il cursore la segue. Meno: le ombre finiscono prima, più FPS. Di più: ombre lontane con vista radente, costa prestazioni. Le distanze delle cascate in metri restano uguali.",
                        "Ripristina la portata delle ombre al 100 % (valore del gioco).",
                        "Portata attuale del gioco",
                    };
                    break;
                case "ru":
                    t = new[]
                    {
                        "Дальность теней (%)",
                        "Дальность солнечных теней в процентах от текущей дальности игры: 100 % = игра, 25–200 %. Игра сама подстраивает дальность под то, что видит камера, ползунок следует за ней. Меньше: тени заканчиваются раньше, больше FPS. Больше: дальние тени при взгляде к горизонту, требует производительности. Расстояния каскадов в метрах не меняются.",
                        "Сбрасывает дальность теней на 100 % (значение игры).",
                        "Текущая дальность теней игры",
                    };
                    break;
                case "pl":
                    t = new[]
                    {
                        "Zasięg cieni (%)",
                        "Zasięg cieni słońca w procentach aktualnego zasięgu gry: 100 % = gra, 25–200 %. Gra sama dopasowuje zasięg do tego, co widzi kamera, a suwak za nim podąża. Mniej: cienie kończą się wcześniej, więcej FPS. Więcej: dalekie cienie przy patrzeniu w stronę horyzontu, kosztuje wydajność. Odległości kaskad w metrach się nie zmieniają.",
                        "Przywraca zasięg cieni do 100 % (wartość gry).",
                        "Aktualny zasięg cieni gry",
                    };
                    break;
                case "pt-BR":
                    t = new[]
                    {
                        "Alcance das sombras (%)",
                        "Alcance das sombras do sol em porcentagem do alcance atual do jogo: 100 % = jogo, 25–200 %. O jogo adapta o alcance ao que a câmera vê e o controle o acompanha. Menos: as sombras terminam antes, mais FPS. Mais: sombras distantes ao olhar para o horizonte, custa desempenho. As distâncias das cascatas em metros não mudam.",
                        "Redefine o alcance das sombras para 100 % (valor do jogo).",
                        "Alcance atual do jogo",
                    };
                    break;
                case "ja":
                    t = new[]
                    {
                        "影の範囲 (%)",
                        "太陽の影の範囲を、ゲームの現在の範囲に対するパーセントで指定します: 100 % = ゲーム、25–200 %。ゲームはカメラに見える範囲に合わせて範囲を調整し、スライダーはそれに追従します。小さい: 影が早く途切れ、FPSが上がります。大きい: 地平線方向の遠くの影、負荷が増えます。カスケードのメートル単位の距離は変わりません。",
                        "影の範囲を 100 %(ゲームの値)に戻します。",
                        "ゲームの現在の影の範囲",
                    };
                    break;
                case "ko":
                    t = new[]
                    {
                        "그림자 거리 (%)",
                        "태양 그림자 거리를 게임의 현재 거리에 대한 백분율로 지정합니다: 100 % = 게임, 25–200 %. 게임은 카메라에 보이는 범위에 맞춰 거리를 조정하고 슬라이더는 이를 따릅니다. 낮게: 그림자가 더 일찍 끝나고 FPS가 오릅니다. 높게: 지평선 쪽의 먼 그림자, 성능을 소모합니다. 캐스케이드의 미터 거리는 그대로입니다.",
                        "그림자 거리를 100 %(게임 값)로 되돌립니다.",
                        "게임의 현재 그림자 거리",
                    };
                    break;
                case "zh-HANS":
                    t = new[]
                    {
                        "阴影距离 (%)",
                        "太阳阴影距离,以游戏当前距离的百分比表示:100 % = 游戏,25–200 %。游戏会根据摄像机所见范围自动调整距离,滑块随之变化。较低:阴影更早结束,FPS 更高。较高:朝地平线方向的远处阴影,消耗性能。级联的米制距离保持不变。",
                        "将阴影距离重置为 100 %(游戏数值)。",
                        "游戏当前阴影距离",
                    };
                    break;
                case "zh-HANT":
                    t = new[]
                    {
                        "陰影距離 (%)",
                        "太陽陰影距離,以遊戲目前距離的百分比表示:100 % = 遊戲,25–200 %。遊戲會依攝影機所見範圍自動調整距離,滑桿隨之變化。較低:陰影更早結束,FPS 更高。較高:朝地平線方向的遠處陰影,耗費效能。級聯的公尺距離保持不變。",
                        "將陰影距離重設為 100 %(遊戲數值)。",
                        "遊戲目前陰影距離",
                    };
                    break;
                default:
                    t = new[]
                    {
                        "Shadow range (%)",
                        "Sun shadow range as a percentage of the game's current range: 100 % = game, 25–200 %. The game adapts the range to what the camera sees and the slider follows it. Lower: shadows end earlier, more FPS. Higher: distant shadows when looking towards the horizon, costs performance. Cascade distances in metres stay the same.",
                        "Resets the shadow range to 100 % (game value).",
                        "Game shadow range now",
                    };
                    break;
            }
            const string prefix = "AdvancedGraphics.UI.Graphics.";
            entries[m_Setting.GetOptionLabelLocaleID(nameof(Setting.ShadowDistancePercent))] = entries[prefix + "ShadowDistancePercent"] = t[0];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ShadowDistancePercent))] = entries[prefix + "TipShadowDistancePercent"] = t[1];
            entries[m_Setting.GetOptionDescLocaleID(nameof(Setting.ResetShadowDistance))] = t[2];
            entries[prefix + "ShadowRangeNow"] = t[3];
        }
    }
}
