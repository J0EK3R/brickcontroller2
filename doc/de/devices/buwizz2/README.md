# BuWizz 2

BuWizz 2 ist ein Bluetooth-LE-Akku-Brick, der bis zu vier Motoren mit Strom versorgt und steuert.

![BuWizz 2](../../../../BrickController2/BrickController2/UI/Images/buwizz_image.png)

## Kanäle

Das Gerät stellt vier Kanäle bereit:

| Kanäle | Zweck | Ausgabe |
|---|---|---|
| 1–4 | Power-Functions-Motoren | -100 % bis +100 % |

## Erste Schritte

1. Schalte den BuWizz 2 ein.
2. Öffne die Geräteliste und tippe auf **Nach neuen Geräten suchen**. Gefundene Geräte werden zur Liste hinzugefügt.
3. Verwende das Gerät in einem Modell und weise seine Kanäle Controller-Aktionen zu.

## Ausgabestufe

Die Ausgabestufe begrenzt die Motorleistung. Es gibt vier Stufen: **Niedrig**, **Normal** (Standard), **Hoch** und **Ludicrous**. Ändere die Standardstufe über die Einstellung **Standard-Ausgabestufe**. Während der Steuerung kannst du die aktuelle Stufe mit dem Makro **Ausgabestufe festlegen** ändern.

## Hinweise

- Höhere Ausgabestufen verbrauchen mehr Strom und verkürzen die Laufzeit.
- Das Gerät meldet während der Verbindung sowohl die Akku- als auch die Motorspannung.
- Bei einigen BuWizz-2-Geräten sind die Anschlüsse vertauscht. Die App erkennt diese Geräte und vertauscht die Kanäle automatisch.

## Siehe auch

- [Geräte](../../pages/device-list/README.md)
- [Controller-Aktion](../../pages/controller-action/README.md)
