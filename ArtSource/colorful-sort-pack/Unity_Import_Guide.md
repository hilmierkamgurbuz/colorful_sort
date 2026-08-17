# Unity Import Guide

Bu paket 1440 x 2560 portre arayuz ve 512 px'lik gameplay hucre olcegi icin hazirlanmistir. PNG dosyalari Unity'de kullanilacak nihai ciktilardir; `Sources` klasoru duzenlenebilir kaynaklari ve uretim betiklerini icerir.

## Backgrounds

- `Texture Type`: Sprite (2D and UI)
- `Sprite Mode`: Single
- `sRGB`: On
- `Mip Maps`: Off
- `Wrap Mode`: Clamp
- `Filter Mode`: Bilinear
- `Max Size`: 4096
- Canvas Scaler icin referans cozumurluk: 1080 x 1920, `Match Width Or Height`: 0.5
- Arka planlari ekrani kaplayacak bicimde `AspectRatioFitter / Envelope Parent` ile kullanin. Ana UI ogelerini telefonun `Screen.safeArea` sinirlari icinde tutun.

## UI Sprites

- `Texture Type`: Sprite (2D and UI)
- `Sprite Mode`: Single
- `Alpha Is Transparency`: On
- `Mesh Type`: Full Rect
- `Mip Maps`: Off
- `Wrap Mode`: Clamp
- `Filter Mode`: Bilinear
- `Compression`: None veya High Quality
- 9-slice sinirlari `AssetManifest.json` icindeki `border` alanindan Sprite Editor'e girilmelidir.
- Buton dosyalarindaki `_normal`, `_pressed` ve `_disabled` durumlarini Unity `Selectable / Transition: Sprite Swap` alanlarina baglayin.

Buton, HUD ve panel gorsellerinde yazi yoktur. Level, coin, can ve panel metinlerini TextMeshPro ile ekleyin. Referansa yakin yazi stili icin kirik beyaz dolgu (`#FFF6D6`), koyu mor/kahverengi outline ve hafif alt golge kullanin.

## Gameplay Sprites

- `Texture Type`: Sprite (2D and UI)
- `Sprite Mode`: Single
- `Pixels Per Unit`: 512
- `Alpha Is Transparency`: On
- `Mesh Type`: Full Rect
- `Mip Maps`: Off
- `Wrap Mode`: Clamp (`gameplay_block_pattern_512.png` icin Repeat)
- `Filter Mode`: Bilinear

Bir mantiksal hucre 512 px = 1 Unity unit olacak sekilde tasarlanmistir. 3D kupunuz 1 x 1 x 1 unit degilse butun 2D gameplay parcalarini ayni parent altinda uniform olceklendirin.

### Onerilen Tam Iki-Hucreli Slotlar

- Normal iki-hucreli kolon icin `slot_complete_2cell.png` (640 x 1664), buzlu kolon icin `slot_ice_complete_2cell.png` (640 x 1984) kullanin.
- Her dosya ayri bir tam, sifir-tuval built-in ImageGen rerenderidir. Final pipeline yalniz ayni ham renderdan alfa temizligi, tek subject crop, butun nesneye ayni X/Y oranli tek affine scale ve seffaf canvas padding uygular; artwork compositing, moduler sprite girdisi, nonuniform veya bolgesel warp kullanmaz.
- Iki dosya da tek `SpriteRenderer` ile kullanilir; `Pixels Per Unit: 512`, pivot `Bottom Center (0.5, 0.0)` olmalidir.
- Tam sprite kendi crown, iki groove bandi, 2x2 stud oturma tabani ve gerekli alt bitisi icerir. Buzlu surumde frost shelf ve uc sarkit da ayni sprite'a birlesiktir.
- Sabit iki hucrede `Simple` kullanin; `Sliced` ile serbestce esnetmeyin veya moduler top/middle/bottom parcalariyla ust uste bindirmeyin. Hucre sayisini artirmak icin asagidaki tam 512 px adimli `Tiled` ayarini kullanin.
- Opsiyonel `slot_shadow`, tam sprite'in alt-merkez pivotunun arkasinda merkezlenebilir.
- Ileri seviye degisken-yukseklik alternatifi olarak `SpriteRenderer / Draw Mode: Tiled` yalniz tam 512 px hucre adimlarinda kullanilabilir. Manifestteki opsiyonel `(Left, Bottom, Right, Top)` border normal icin `(160, 832, 160, 320)`, buzlu icin `(160, 1152, 160, 320)` degeridir. Sabit iki hucrede `Simple` tercih edin.

### Legacy / Opsiyonel Moduler Kolonlar

Asagidaki parcalar yalniz iki disinda degisken hucre sayisi gereken level-editor kolonlari icin korunur:

1. Normal kolon icin `slot_top` en uste yerlestirilir.
2. Her hucre icin bir `slot_cell_repeat` tekrar edilir.
3. `slot_bottom` en alta, `slot_shadow` gerekirse kolonun arkasina yerlestirilir. Bloklarin oturacagi 2x2 stud tabani `slot_bottom` sprite'ina birlesiktir; ayri bir taban eklemeyin.
4. Buzlu kolon icin `slot_ice_top`, tekrar edilen `slot_ice_middle` ve `slot_ice_bottom` kullanilir. Ayni 2x2 stud tabani, frost bandi ve uc buz sarkiti bottom sprite'ina birlesiktir; ayri taban veya buz eklemek gerekmez.
5. Kapali kolonda `cover_bottom_cap` son hucre olarak kullanilir. Ustte kalan hucreler icin `cover_cell_repeat`, hucre sinirlarina `cover_separator` ve en uste `cover_top_cap` eklenir.
6. Eski ayri buz bandi/kristalleri yalniz ozel varyasyonlar icin opsiyonel olarak korunmustur.
7. Mystery davranisi icin koyu yuz overlay'i ve `question_mark_decal` birbirinden bagimsiz kullanilabilir.

Legacy moduler kolonlar icin kesin piksel montaj koordinatlari (sol-ust referans):

- Normal slot: `slot_top (0,0)`, hucre `i` icin `slot_cell_repeat (0, 320 + i*512)`, `slot_bottom (0, 320 + hucreSayisi*512)`.
- Buz-entegre slot: `slot_ice_top (0,0)`, hucre `i` icin `slot_ice_middle (0, 320 + i*512)`, `slot_ice_bottom (0, 320 + hucreSayisi*512)`.
- Normal ve buzlu slot pivotlari: top `(0.5,0)`, middle `(0.5,0.5)`, bottom `(0.5,1)`.
- Unity yukari-Y duzeninde bottom attach `(0,0)`, middle merkezleri `(0, 0.5 + k)`, top attach `(0, hucreSayisi)` kullanilir.
- Top, middle ve bottom sprite'larini ayni parent altinda ayni transform olcegiyle kullanin; parcalari birbirinden bagimsiz yatay esnetmeyin. Uc parcalarin birlesime yakin genis bantlari ilgili middle varyantiyla piksel uyumlu hazirlanmistir.
- Aileleri capraz karistirmayin: `slot_top + slot_cell_repeat + slot_bottom` normal ucludur; `slot_ice_top + slot_ice_middle + slot_ice_bottom` buzlu ucludur.
- Opsiyonel `slot_shadow`, tamamlanmis kolonun altinda merkezlenir.
- Cover: `cover_top_cap (0,0)`, repeat hucre `i` icin `(32, 320 + i*512)` (`i=0..hucreSayisi-2`), son hucre `cover_bottom_cap (32, 320 + (hucreSayisi-1)*512)`.
- Cover separator `i` icin `(32, 240 + i*512)` (`i=1..hucreSayisi-1`).
- Cover pivotlari: top `(0.5,0)`, repeat/bottom `(0.5,0.5)`.
- Unity yukari-Y duzeninde bottom center `(0,0.5)`, repeat merkezleri `(0,1.5+k)`, top bottom-pivot `(0,hucreSayisi)` kullanilir.
- Cover sprite'larinda yan renk seritleri veya sembol bulunmaz; bunlari ayri layer/decal olarak ekleyin.
- Opsiyonel eski ayri ice katmanlama (band sol-ust referansi): sol kristal `(18,210)`, orta `(224,190)`, sag `(432,210)`, frost band `(0,0)`.
- Mystery yuz ve soru isareti ayni 640 x 640 canvas/pivot kullanir; 1:1 bindirin.

Gameplay parcalari kameraya bakan world-space SpriteRenderer veya Quad uzerinde kullanilmalidir. Onerilen siralama: arka plan 0, kolon 10, 3D kupler 20, cover/ice/mystery overlay 30, ekran UI 100+.

## Safe Area ve QA

- Ana menu arka planinin ust bolgesi HUD, alt-orta bolgesi level butonu icin bos birakilmistir.
- 19.5:9 ve 20:9 ekranlarda kenarlardan kirpma yapin; UI'yi arka plan resminin icine sabitlemeyin.
- Referans ekran goruntulerindeki reklamlar, etkinlik butonlari, alt navigasyon ve degirmen sahnesi pakete dahil degildir.
