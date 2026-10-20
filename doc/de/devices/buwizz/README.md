# BuWizz

BuWizz (1.0) ist ein Bluetooth-LE-Akku-Brick, der bis zu vier Motoren mit Strom versorgt und steuert.

![BuWizz](../../../../BrickController2/BrickController2/UI/Images/buwizz_image.png)

## Kanäle

Das Gerät stellt vier Kanäle bereit:

| Kanäle | Zweck | Ausgabe |
|---|---|---|
| 1–4 | Power-Functions-Motoren | -100 % bis +100 % |

## Erste Schritte

1. Schalte den BuWizz ein.
2. Öffne die Geräteliste und tippe auf **Nach neuen Geräten suchen**. Gefundene Geräte werden zur Liste hinzugefügt.
3. Verwende das Gerät in einem Modell und weise seine Kanäle Controller-Aktionen zu.

## Ausgabestufe

Die Ausgabestufe begrenzt die Motorleistung. Es gibt drei Stufen: **Niedrig**, **Normal** (Standard) und **Hoch**. Ändere die Standardstufe über die Einstellung **Standard-Ausgabestufe**.

## Hinweise

- Höhere Ausgabestufen verbrauchen mehr Strom und verkürzen die Laufzeit.
- Die Akkuspannung wird auf der Geräteseite nicht angezeigt.

## Siehe auch

- [Geräte](../../pages/device-list/README.md)
- [Controller-Aktion](../../pages/controller-action/README.md)
