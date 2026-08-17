# Colorful Sort - 2D Asset Pack

Unity icin hazirlanan bu paket toplam 57 nihai sprite icerir:

- 4 background asseti (duz ve izometrik kup-blok grid'li gameplay alternatifleri dahil)
- 34 UI asseti
- 19 gameplay / level-editor asseti

## Klasorler

- `Backgrounds`: 1440 x 2560 menu ve gameplay arka planlari ile 512 x 512 seamless pattern.
- `UI`: level butonu, HUD, popup, buton durumlari ve ayri ikonlar.
- `Gameplay`: onerilen tek-parca iki hucreli normal/buzlu slotlar; legacy/opsiyonel tekrar edilebilir slot parcalari; ayri buz, top/repeat/bottom cover sistemi ve mystery parcalari.
- `Sources/ImageGen`: UI, ikon, gameplay ve izometrik kup-blok grid'li background icin ham built-in ImageGen ciktilari, tam promptlar ve yalnizca kirpma/alfa/olcekleme yapan raster islemciler.
- `Legacy_Vector_2026-08-17`: onceki SVG tabanli surumun geri alinabilir yedegi; aktif paket tarafindan kullanilmaz.

Unity import degerleri, pivotlar, PPU ve 9-slice sinirlari `AssetManifest.json` icindedir. Kurulum ve kolon montaji icin `Unity_Import_Guide.md` dosyasini kullanin.

`preview_contact_sheet.png` tum nihai assetleri tek sayfada gosterir. `qa_report.json`, 57/57 envanter, alfa, seamless pattern, tam iki-hucreli slot silueti/groove kontrolleri, legacy normal ve buz-entegre slot birlesimleri, cover montaji ve mystery overlay kontrollerinin sonucunu icerir.

## Gameplay Slot Tercihi

- Iki hucreli standart kolonlarda `Gameplay/slot_complete_2cell.png` veya buzlu `Gameplay/slot_ice_complete_2cell.png` kullanin. Bunlar tek SpriteRenderer ile kullanilan, alt-merkez pivotlu ve birlesim cizgisi olmayan onerilen dosyalardir.
- Bu iki onerilen sprite, her varyant icin sifir tuvalden uretilmis tek bir tam built-in ImageGen renderindan gelir. Nihai islem yalniz ayni ham renderdan alfa temizligi, tek kirpma, tum nesneye tek uniform/isotropic olcekleme ve seffaf padding icerir; moduler compositing veya nonuniform/bolgesel warp yoktur. Kanit kayitlari `Sources/ImageGen/Gameplay/SlotComplete2Cell_FullRerender_Prompts.md` ve `SlotComplete2Cell_FullRerender_QA.json` dosyalarindadir.
- Sabit iki hucre icin `Simple` kullanin. Hucre sayisini artirmak icin `Sliced` ile esnetmek yerine `SpriteRenderer / Tiled` kullanin; her 512 px yukseklik tam bir yeni hucre ekler. Border degerleri manifest ve Unity rehberindedir; genisligi yatay esnetmeyin.
- `slot_top + slot_cell_repeat + slot_bottom` ve buzlu karsiliklari yalniz degisken hucre sayisi gereken seviyeler icin legacy/opsiyonel olarak korunur. Normal ve buzlu bottom sprite'larinda 2x2 stud tabani birlesiktir; buzlu bottom'da buz bunun altinda devam eder.

Aktif `UI` ve `Gameplay` PNG'lerinin gorunur sanati OpenAI built-in ImageGen ile uretilmistir; SVG kullanilmaz. Normal/pressed/disabled durumlari ImageGen raster masterlarindan alfa, renk ve konum islemleriyle turetilmistir.

## Dinamik UI

Assetlerin icine level, coin, can veya panel metni basilmaz. Yazilari TextMeshPro ile ekleyin. Butonlarda `_normal`, `_pressed` ve `_disabled` dosyalarini Unity Sprite Swap durumlarina baglayin.

## Kapsam Disi

3D renkli kupler, reklamlar, etkinlik/teklif UI'lari, alt navigasyon ve referanstaki degirmen sahnesi bilerek dahil edilmemistir.
